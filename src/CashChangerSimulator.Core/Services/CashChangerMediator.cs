using CashChangerSimulator.Core.Models;
using PosSharp.Core;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>驥｣驫ｭ讖溷崋譛峨・迥ｶ諷九→繝励Ο繝代ユ繧｣繧堤ｮ｡逅・☆繧九Γ繝・ぅ繧ｨ繝ｼ繧ｿ繝ｼ繧ｯ繝ｩ繧ｹ縲・/summary></summary>
public class CashChangerMediator : UposMediator
{
    private readonly ReactiveProperty<int> depositAmount = new(0);
    private readonly ReactiveProperty<DeviceDepositStatus> depositStatus = new(DeviceDepositStatus.None);
    private readonly ReactiveProperty<CashChangerFullStatus> fullStatus = new(CashChangerFullStatus.OK);

    /// <summary>迴ｾ蝨ｨ謚募・縺輔ｌ縺ｦ縺・ｋ驥鷹｡阪ｒ蜿門ｾ励＠縺ｾ縺吶・/summary></summary>
    public virtual ReactiveProperty<int> DepositAmountProperty => depositAmount;

    /// <summary>迴ｾ蝨ｨ縺ｮ蜈･驥醍憾諷九ｒ蜿門ｾ励＠縺ｾ縺吶・/summary></summary>
    public virtual ReactiveProperty<DeviceDepositStatus> DepositStatusProperty => depositStatus;

    /// <summary>迴ｾ蝨ｨ縺ｮ貅譚ｯ/遨ｺ縺咲憾諷九ｒ蜿門ｾ励＠縺ｾ縺吶・/summary></summary>
    public virtual ReactiveProperty<CashChangerFullStatus> FullStatusProperty => fullStatus;

    /// <summary>迴ｾ蝨ｨ謚募・縺輔ｌ縺ｦ縺・ｋ驥鷹｡阪・/summary></summary>
    public int DepositAmount
    {
        get => depositAmount.Value;
        set => depositAmount.Value = value;
    }

    /// <summary>迴ｾ蝨ｨ縺ｮ蜈･驥醍憾諷九・/summary></summary>
    public DeviceDepositStatus DepositStatus
    {
        get => depositStatus.Value;
        set => depositStatus.Value = value;
    }

    /// <summary>迴ｾ蝨ｨ縺ｮ貅譚ｯ/遨ｺ縺咲憾諷九・/summary></summary>
    public CashChangerFullStatus FullStatus
    {
        get => fullStatus.Value;
        set => fullStatus.Value = value;
    }
}
