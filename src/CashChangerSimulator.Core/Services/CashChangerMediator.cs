using CashChangerSimulator.Core.Models;
using PosSharp.Core;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>釣銭機固有の状態とプロパティを管理するメディエータークラス。</summary>
public class CashChangerMediator : UposMediator
{
    private readonly ReactiveProperty<int> depositAmount = new(0);
    private readonly ReactiveProperty<DeviceDepositStatus> depositStatus = new(DeviceDepositStatus.None);
    private readonly ReactiveProperty<CashChangerFullStatus> fullStatus = new(CashChangerFullStatus.OK);

    /// <summary>現在投入されている金額を取得します。</summary>
    public virtual ReactiveProperty<int> DepositAmountProperty => depositAmount;

    /// <summary>現在の入金状態を取得します。</summary>
    public virtual ReactiveProperty<DeviceDepositStatus> DepositStatusProperty => depositStatus;

    /// <summary>現在の満杯/空き状態を取得します。</summary>
    public virtual ReactiveProperty<CashChangerFullStatus> FullStatusProperty => fullStatus;

    /// <summary>現在投入されている金額。</summary>
    public int DepositAmount
    {
        get => depositAmount.Value;
        set => depositAmount.Value = value;
    }

    /// <summary>現在の入金状態。</summary>
    public DeviceDepositStatus DepositStatus
    {
        get => depositStatus.Value;
        set => depositStatus.Value = value;
    }

    /// <summary>現在の満杯/空き状態。</summary>
    public CashChangerFullStatus FullStatus
    {
        get => fullStatus.Value;
        set => fullStatus.Value = value;
    }
}
