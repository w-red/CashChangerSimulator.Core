using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;
using System;
using System.Linq;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>現金を指定の文字列（UPOS形式）で調整する戦略。</summary>
public class AdjustCashCountsStrStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.AdjustCashCountsStr;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        if (obj is string strVal)
        {
            try
            {
                var activeKeys = device._inventory.AllCounts.Select(kv => kv.Key).ToList();
                var factor = device.GetCurrencyFactor(device.CurrencyCode);
                var counts = CashCountParser.Parse(strVal, activeKeys, factor);
                device.AdjustCashCounts(counts);
                return new DirectIOData(data, obj);
            }
            catch (Exception ex)
            {
                throw new PosControlException($"Failed to parse adjust string: {ex.Message}", ErrorCode.Illegal, ex);
            }
        }
        else
        {
            throw new PosControlException("ADJUST_CASH_COUNTS_STR requires a string object.", ErrorCode.Illegal);
        }
    }
}
