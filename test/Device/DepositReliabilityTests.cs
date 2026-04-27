using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet;
using Microsoft.PointOfService;
using Shouldly;
using System.Collections.Concurrent;

namespace CashChangerSimulator.Tests.Device;

/// <summary>入金(Deposit)処理の非同期動作における信頼性と整合性を検証するテストクラス。</summary>
public class DepositReliabilityTests
{
    private class DepositReliabilityChanger : InternalSimulatorCashChanger
    {
        public ConcurrentBag<EventArgs> EventHistory { get; } = [];

        public DepositReliabilityChanger()
        {
            // [STABILITY] Disable POS.NET internal event queueing to prevent duplicate NotifyEvent calls in headless environments.
            DisableUposEventQueuing = true;
        }

        protected override void NotifyEvent(EventArgs e)
        {
            EventHistory.Add(e);
        }
    }

    /// <summary>バックグラウンドでの入金挿入と、メインスレッドでの確定操作が競合した場合に DataEvent が正しく1回だけ発行されることを検証します。</summary>
    [Fact]
    public async Task DataEventConsistencyUnderConcurrentTrackAndFix()
    {
        // Arrange
        var changer = new DepositReliabilityChanger
        {
            SkipStateVerification = true
        };
        var ct = TestContext.Current.CancellationToken;

        await changer.OpenAsync();
        await changer.ClaimAsync(1000);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;
        changer.RealTimeDataEnabled = false;

        await changer.BeginDepositAsync();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act: Background insertions
        using var cts = new CancellationTokenSource();

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
            await Task.Delay(1, ct);
        }
        await changer.FixDepositAsync();
        await cts.CancelAsync();

        // Ensure task is finished to prevent assembly lock
        await insertionTask;

        // Assert: Wait for UPOS event delivery thread to fire DataEvent
        var dataEvents = new List<DataEventArgs>();
        const int timeoutMs = 2000;
        int elapsedMs = 0;
        while (elapsedMs < timeoutMs)
        {
            dataEvents = [.. changer.EventHistory.OfType<DataEventArgs>()];
            if (dataEvents.Count == 1)
                break;
            await Task.Delay(10, ct);
            elapsedMs += 10;
        }

        dataEvents.Count.ShouldBe(1, $"Expected exactly 1 DataEvent on Fix, but found {dataEvents.Count}. Events: {string.Join(", ", changer.EventHistory.Select(e => e.GetType().Name))}");
        changer.DepositAmount.ShouldBeGreaterThan(0);

        await changer.CloseAsync();
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
        var ct = TestContext.Current.CancellationToken;

        await changer.OpenAsync();
        await changer.ClaimAsync(1000);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;
        changer.RealTimeDataEnabled = false;

        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act: 100 iterations of full deposit lifecycle
        for (int i = 0; i < 100; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            changer.EventHistory.Clear();
            await changer.BeginDepositAsync();
            changer.DepositController.TrackDeposit(key);
            await changer.FixDepositAsync();
            await changer.EndDepositAsync(DepositAction.Change);

            // Assert
            List<DataEventArgs> dataEvents = [.. changer.EventHistory.OfType<DataEventArgs>()];
            dataEvents.Count.ShouldBe(1, $"Iteration {i}: Expected 1 DataEvent, got {dataEvents.Count}.");
            ((int)changer.DepositStatus).ShouldBe((int)CashDepositStatus.End);
            changer.DepositController.IsFixed.ShouldBeFalse();
        }

        await changer.CloseAsync();
    }

    /// <summary>一時停止(Pause)の切り替えと入金挿入が並行して発生した場合に、停止中の挿入が無視され、状態が整合していることを検証します。</summary>
    [Fact]
    public async Task PauseResumeRaceCondition()
    {
        // Arrange
        var changer = new DepositReliabilityChanger
        {
            SkipStateVerification = true
        };
        var ct = TestContext.Current.CancellationToken;

        await changer.OpenAsync();
        await changer.ClaimAsync(1000);
        changer.DeviceEnabled = true;
        await changer.BeginDepositAsync();

        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act: Concurrent pause/resume and insertions
        using var cts = new CancellationTokenSource();
        var task = Task.Run(
            async () =>
        {
            while (!cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                changer.DepositController.TrackDeposit(key);
                await Task.Delay(200, ct);
            }
        }, ct);

        for (int i = 0; i < 3; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await changer.PauseDepositAsync(DeviceDepositPause.Pause);

            await Task.Delay(1000, ct);
            var pausedAmount = changer.DepositAmount;

            await Task.Delay(1000, ct);
            
            var currentAmount = changer.DepositAmount;

            currentAmount.ShouldBe(pausedAmount, $"Amount should not increase while paused (Iteration {i}).");

            await changer.PauseDepositAsync(DeviceDepositPause.Resume);

            await Task.Delay(1000, ct);
        }

        await cts.CancelAsync();
        await task; // Ensure background insertion loop has exited

        await changer.FixDepositAsync();
        await changer.CloseAsync();
    }
}
