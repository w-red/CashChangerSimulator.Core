using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>直前の入金セッションで投入された紙幣のシリアル番号一覧を取得する戦略。</summary>
public class GetDepositedSerialsStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.GetDepositedSerials;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var serials = string.Join(",", device._depositController.LastDepositedSerials);
        return new DirectIOData(data, serials);
    }
}
