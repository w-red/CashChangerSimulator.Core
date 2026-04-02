using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using System.Collections.Concurrent;
using System.Threading;

namespace CashChangerSimulator.Tests.Device;

/// <summary>入金（Deposit）処理の非同期動作における信頼性と整合性を検証するテストクラス。</summary>
public class DepositReliabilityTests
{
    private class DepositReliabilityChanger : InternalSimulatorCashChanger
    {
        public ConcurrentBag<EventArgs> EventHistory { get; } = [];
        public int DataEventCount => EventHistory.Count(e => e is DataEventArgs);

        public DepositReliabilityChanger() : base() 
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
    [Fact]
    public async Task DataEvent_Consistency_Under_Concurrent_Track_And_Fix()
    {
        // Arrange
        var changer = new DepositReliabilityChanger();
        changer.SkipStateVerification = true;
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
        
        var insertionTask = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                if (cts.Token.IsCancellationRequested || ct.IsCancellationRequested) break;
                changer.DepositController.TrackDeposit(key);
                await Task.Delay(5, ct);
            }
        }, ct);

        // Wait slightly and FixDeposit concurrently
        await Task.Delay(10, ct);
        changer.FixDeposit();
        cts.Cancel();
        
        try { await Task.WhenAny(insertionTask, Task.Delay(500, ct)); } catch (OperationCanceledException) { }

        // Assert
        var events = changer.EventHistory.ToList();
        var dataEvents = events.OfType<DataEventArgs>().ToList();
        
        dataEvents.Count.ShouldBe(1, $"Expected exactly 1 DataEvent on Fix, but found {dataEvents.Count}. Events: {string.Join(", ", events.Select(e => e.GetType().Name))}");
        changer.DepositAmount.ShouldBeGreaterThan(0);
        
        changer.Close();
    }

    /// <summary>入金セッションのライフサイクル（Begin->Track->Fix->End）を高頻度で繰り返し、状態の破損や整合性エラーが発生しないことを検証します。</summary>
    [Fact]
    public async Task Rapid_Session_Lifecycle_Reliability()
    {
        // Arrange
        var changer = new DepositReliabilityChanger();
        changer.SkipStateVerification = true;
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
            if (ct.IsCancellationRequested) break;
            
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

    /// <summary>一時停止（Pause）の切り替えと入金挿入が並行して発生した場合に、停止中の挿入が無視され、状態が整合していることを検証します。</summary>
    [Fact]
    public async Task Pause_Resume_Race_Condition()
    {
        // Arrange
        var changer = new DepositReliabilityChanger();
        changer.SkipStateVerification = true;
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.BeginDeposit();

        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var ct = TestContext.Current.CancellationToken;

        // Act: Concurrent pause/resume and insertions
        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                changer.DepositController.TrackDeposit(key);
                await Task.Delay(1, ct);
            }
        }, ct);

        for (int i = 0; i < 10; i++)
        {
            if (ct.IsCancellationRequested) break;
            
            changer.PauseDeposit(CashDepositPause.Pause);
            await Task.Delay(5, ct);
            var pausedAmount = changer.DepositAmount;
            
            await Task.Delay(10, ct);
            changer.DepositAmount.ShouldBe(pausedAmount, $"Amount should not increase while paused (Iteration {i}).");

            changer.PauseDeposit(CashDepositPause.Restart);
            await Task.Delay(5, ct);
        }

        cts.Cancel();
        try { await Task.WhenAny(task, Task.Delay(500, ct)); } catch (OperationCanceledException) { }
        
        changer.FixDeposit();
        changer.Close();
    }
}
