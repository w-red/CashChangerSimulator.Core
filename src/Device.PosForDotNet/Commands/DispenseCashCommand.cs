using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>金種指定出金操作をカプセル化するコマンド。</summary>
public class DispenseCashCommand : IUposCommand
{
    private readonly DispenseController _controller;
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DepositController _depositController;
    private readonly IReadOnlyDictionary<DenominationKey, int> _counts;
    private readonly bool _async;
    private IUposMediator? _mediator;

    /// <summary>金種指定出金コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">出金制御を司るコントローラー。</param>
    /// <param name="inventory">在庫情報を管理するインベントリ。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理するマネージャー。</param>
    /// <param name="depositController">入金状態を確認するためのコントローラー。</param>
    /// <param name="counts">出金する金種と枚数のセット。</param>
    /// <param name="async">非同期実行するかどうか。</param>
    public DispenseCashCommand(
        DispenseController controller,
        Inventory inventory,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        IReadOnlyDictionary<DenominationKey, int> counts,
        bool async)
    {
        _controller = controller;
        _inventory = inventory;
        _hardwareStatusManager = hardwareStatusManager;
        _depositController = depositController;
        _counts = counts;
        _async = async;
    }

    /// <summary>金種指定出金操作を実行します。</summary>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (_controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("DispenseCash failed", _controller.LastErrorCode, _controller.LastErrorCodeExtended);
        }
    }

    /// <summary>金種指定出金操作を非同期で実行します。</summary>
    public async Task ExecuteAsync()
    {
        if (_async && _mediator != null)
        {
            _mediator.IsBusy = true;
        }

        await _controller.DispenseCashAsync(_counts, _async);
    }

    /// <summary>コマンド実行前の状態および事前条件（在庫やハードウェア状態）を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        _mediator = mediator;
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
