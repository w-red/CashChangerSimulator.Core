using System.Collections.Concurrent;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>入金(Deposit)処理の非同期動作における信頼性と整合性を検証するテストクラス。</summary>
public class DepositReliabilityTests
{
    private class DepositReliabilityChanger : InternalSimulatorCashChanger
    {
        public ConcurrentBag<EventArgs> EventHistory { get; } = [];
        public int DataEventCount => EventHistory.Count(e => e is DataEventArgs);

        public DepositReliabilityChanger()
            : base()
        {
            // [STABILITY] Disable POS.NET internal event queueing to prevent duplicate NotifyEvent calls in headless environments.
            DisableUposEventQueuing = true;
        }

        protected override void NotifyEvent(EventArgs e)
        {
            EventHistory.Add(e);

            // base.NotifyEvent is NOT called if we want to completely isolate the event fired from coordinator
            // base.NotifyEvent(e);
        }
    }

    /// <summary>バックグラウンドでの入金挿入と、メインスレッドでの確定操作が競合した場合に DataEvent が正しく1回だけ発行されることを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    /// <summary>データの追跡と確定が並行して行われる際の DataEvent の整合性を検証する。</summary>
    [Fact]
    public async Task DataEventConsistencyUnderConcurrentTrackAndFix()
    {
        // Arrange
        var changer = new DepositReliabilityChanger
        {
            SkipStateVerification = true
        };
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;
        changer.RealTimeDataEnabled = false;

        changer.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act: Background insertions
        var cts = new CancellationTokenSource();
        var ct = TestContext.Current.CancellationToken;

        var insertionTask = Task.Run(
            async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                if (cts.Token.IsCancellationRequested || ct.IsCancellationRequested)
                {
                    break;
                }

                changer.DepositController.TrackDeposit(key);
                await Task.Yield();
            }
        }, ct);

        // Wait slightly and FixDeposit concurrently, ensuring at least one insertion has registered
        while (changer.DepositAmount == 0 && !ct.IsCancellationRequested && !insertionTask.IsCompleted)
        {
            await Task.Delay(1, ct).ConfigureAwait(false);
        }
        changer.FixDeposit();
        cts.Cancel();

        // Ensure task is finished to prevent assembly lock
        await insertionTask;

        // Assert: Wait for UPOS event delivery thread to fire DataEvent
        var dataEvents = new List<DataEventArgs>();
        const int timeoutMs = 2000;
        int elapsedMs = 0;
        while (elapsedMs < timeoutMs)
        {
            dataEvents = changer.EventHistory.OfType<DataEventArgs>().ToList();
            if (dataEvents.Count == 1)
                break;
            await Task.Delay(10).ConfigureAwait(false);
            elapsedMs += 10;
        }

        dataEvents.Count.ShouldBe(1, $"Expected exactly 1 DataEvent on Fix, but found {dataEvents.Count}. Events: {string.Join(", ", changer.EventHistory.Select(e => e.GetType().Name))}");
        changer.DepositAmount.ShouldBeGreaterThan(0);

        changer.Close();
    }

    /// <summary>入金セッションのライフサイクル(Begin->Track->Fix->End)を高頻度で繰り返し、状態の破損や整合性エラーが発生しないことを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    /// <summary>入金セクションのライフサイクルを高頻度で繰り返した際の信頼性を検証する。</summary>
    [Fact]
    public async Task RapidSessionLifecycleReliability()
    {
        // Arrange
        var changer = new DepositReliabilityChanger
        {
            SkipStateVerification = true
        };
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;
        changer.RealTimeDataEnabled = false;

        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var ct = TestContext.Current.CancellationToken;

        // Act: 100 iterations of full deposit lifecycle
        for (int i = 0; i < 100; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            changer.EventHistory.Clear();
            changer.BeginDeposit();
            changer.DepositController.TrackDeposit(key);
            changer.FixDeposit();
            changer.EndDeposit(CashDepositAction.Change);

            // Assert
            var dataEvents = changer.EventHistory.OfType<DataEventArgs>().ToList();
            dataEvents.Count.ShouldBe(1, $"Iteration {i}: Expected 1 DataEvent, got {dataEvents.Count}.");
            ((int)changer.DepositStatus).ShouldBe((int)CashDepositStatus.End);
            changer.DepositController.IsFixed.ShouldBeFalse();
        }

        changer.Close();
    }

    /// <summary>一時停止(Pause)の切り替えと入金挿入が並行して発生した場合に、停止中の挿入が無視され、状態が整合していることを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    /// <summary>一時停止と再開が競合する条件下での動作の整合性を検証する。</summary>
    [Fact]
    public async Task PauseResumeRaceCondition()
    {
        // Arrange
        var changer = new DepositReliabilityChanger
        {
            SkipStateVerification = true
        };
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.BeginDeposit();

        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var ct = TestContext.Current.CancellationToken;

        // Act: Concurrent pause/resume and insertions
        var cts = new CancellationTokenSource();
        var task = Task.Run(
            async () =>
        {
            while (!cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                changer.DepositController.TrackDeposit(key);
                await Task.Yield();
            }
        }, ct);

        for (int i = 0; i < 10; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            changer.PauseDeposit(CashDepositPause.Pause);
            await Task.Delay(5, ct).ConfigureAwait(false);
            var pausedAmount = changer.DepositAmount;

            await Task.Delay(10, ct).ConfigureAwait(false);
            changer.DepositAmount.ShouldBe(pausedAmount, $"Amount should not increase while paused (Iteration {i}).");

            changer.PauseDeposit(CashDepositPause.Restart);
            await Task.Delay(5, ct).ConfigureAwait(false);
        }

        cts.Cancel();
        await task; // Ensure background insertion loop has exited

        changer.FixDeposit();
        changer.Close();
    }
}
