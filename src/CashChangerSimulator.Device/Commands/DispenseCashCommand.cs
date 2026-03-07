using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CashChangerSimulator.Device.Commands;

/// <summary>金種指定出金操作をカプセル化するコマンド。</summary>
public class DispenseCashCommand : IUposCommand
{
    private readonly DispenseController _controller;
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DepositController _depositController;
    private readonly IReadOnlyDictionary<DenominationKey, int> _counts;
    private readonly bool _async;
    private readonly Action<ErrorCode, int> _onComplete;

    /// <summary>金種指定出金コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">出金制御を司るコントローラー。</param>
    /// <param name="inventory">在庫情報を管理するインベントリ。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理するマネージャー。</param>
    /// <param name="depositController">入金状態を確認するためのコントローラー。</param>
    /// <param name="counts">出金する金種と枚数のセット。</param>
    /// <param name="async">非同期実行するかどうか。</param>
    /// <param name="onComplete">完了時に実行されるコールバック。</param>
    public DispenseCashCommand(
        DispenseController controller, 
        Inventory inventory, 
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        IReadOnlyDictionary<DenominationKey, int> counts, 
        bool async, 
        Action<ErrorCode, int> onComplete)
    {
        _controller = controller;
        _inventory = inventory;
        _hardwareStatusManager = hardwareStatusManager;
        _depositController = depositController;
        _counts = counts;
        _async = async;
        _onComplete = onComplete;
    }

    /// <summary>金種指定出金操作を実行します。</summary>
    public void Execute()
    {
        var task = _controller.DispenseCashAsync(_counts, _async, _onComplete);
        if (!_async)
        {
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>コマンド実行前の状態および事前条件（在庫やハードウェア状態）を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);

        // Pre-condition checks previously in Facade
        if (_hardwareStatusManager.IsJammed.Value)
            throw new PosControlException(
                "Device is jammed. Cannot dispense.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);

        if (_depositController.IsDepositInProgress)
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);

        // Inventory check previously in Facade
        foreach (var (key, count) in _counts)
        {
            if (!_inventory.AllCounts.Any(kv => kv.Key == key))
                throw new PosControlException(
                    $"Denomination {key} is not registered.",
                    ErrorCode.Illegal);

            if (_inventory.GetCount(key) < count)
                throw new PosControlException(
                    $"Insufficient inventory for {key}. Required: {count}, Available: {_inventory.GetCount(key)}",
                    ErrorCode.Extended,
                    (int)UposCashChangerErrorCodeExtended.OverDispense);
        }
    }
}
