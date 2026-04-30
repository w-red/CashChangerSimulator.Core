using System.Collections.Frozen;
using System.Reflection;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>スクリプトコマンドの種類を表す列挙型クラス。</summary>
public sealed record ScriptCommandType
{
    /// <summary>OPEN コマンド。</summary>
    public static readonly ScriptCommandType Open = new("OPEN");

    /// <summary>SET コマンド。</summary>
    public static readonly ScriptCommandType Set = new("SET");

    /// <summary>INJECTERROR コマンド。</summary>
    public static readonly ScriptCommandType InjectError = new("INJECTERROR");

    /// <summary>ASSERT コマンド。</summary>
    public static readonly ScriptCommandType Assert = new("ASSERT");

    /// <summary>BEGINDEPOSIT コマンド。</summary>
    public static readonly ScriptCommandType BeginDeposit = new("BEGINDEPOSIT");

    /// <summary>TRACKDEPOSIT コマンド。</summary>
    public static readonly ScriptCommandType TrackDeposit = new("TRACKDEPOSIT");

    /// <summary>FIXDEPOSIT コマンド。</summary>
    public static readonly ScriptCommandType FixDeposit = new("FIXDEPOSIT");

    /// <summary>ENDDEPOSIT コマンド。</summary>
    public static readonly ScriptCommandType EndDeposit = new("ENDDEPOSIT");

    /// <summary>DISPENSE コマンド。</summary>
    public static readonly ScriptCommandType Dispense = new("DISPENSE");

    /// <summary>DELAY コマンド。</summary>
    public static readonly ScriptCommandType Delay = new("DELAY");

    /// <summary>ENABLE コマンド。</summary>
    public static readonly ScriptCommandType Enable = new("ENABLE");

    /// <summary>REPEAT コマンド（制御フロー用）。</summary>
    public static readonly ScriptCommandType Repeat = new("REPEAT");

    private static readonly FrozenDictionary<string, ScriptCommandType> AllTypes =
        typeof(ScriptCommandType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ScriptCommandType))
            .Select(f => (ScriptCommandType)f.GetValue(null)!)
            .ToFrozenDictionary(t => t.Name);

    private static readonly FrozenDictionary<string, CurrencyCashType> CashTypeMap =
        new Dictionary<string, CurrencyCashType>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(CurrencyCashType.Coin), CurrencyCashType.Coin },
            { nameof(CurrencyCashType.Bill), CurrencyCashType.Bill }
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, DepositAction> DepositActionMap =
        new Dictionary<string, DepositAction>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(DepositAction.Repay), DepositAction.Repay },
            { nameof(DepositAction.Change), DepositAction.Change },
            { nameof(DepositAction.NoChange), DepositAction.NoChange },
            { "STORE", DepositAction.NoChange } // STORE は NoChange のエイリアス
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>コマンド名(大文字)。</summary>
    public string Name { get; }

    private ScriptCommandType(string name) => Name = name;

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>文字列から ScriptCommandType を取得します。</summary>
    /// <param name="op">操作名の文字列。</param>
    /// <returns>対応する ScriptCommandType インスタンス。</returns>
    public static ScriptCommandType FromString(string op)
    {
        ArgumentNullException.ThrowIfNull(op);
        var normalized = op
            .ToUpperInvariant()
            .Replace("-", "", StringComparison.Ordinal);

        return AllTypes.TryGetValue(normalized, out var type)
            ? type
            : new ScriptCommandType(normalized);
    }

    /// <summary>文字列から CurrencyCashType を取得します。</summary>
    /// <param name="type">種別文字列。</param>
    /// <returns>対応する CurrencyCashType。</returns>
    public static CurrencyCashType ToCurrencyCashType(string? type)
    {
        if (type == null) return CurrencyCashType.Bill;
        return CashTypeMap.TryGetValue(type, out var cashType) ? cashType : CurrencyCashType.Bill;
    }

    /// <summary>文字列から DepositAction を取得します。</summary>
    /// <param name="action">アクション文字列。</param>
    /// <returns>対応する DepositAction。</returns>
    public static DepositAction ToDepositAction(string? action)
    {
        if (action == null) return DepositAction.NoChange;
        return DepositActionMap
            .TryGetValue(action, out var depositAction)
            ? depositAction : DepositAction.NoChange;
    }
}
