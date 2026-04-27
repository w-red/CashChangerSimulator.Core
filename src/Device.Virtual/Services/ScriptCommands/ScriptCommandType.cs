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

    /// <summary>コマンド名（大文字）。</summary>
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
        var normalized = op.ToUpperInvariant().Replace("-", "", StringComparison.Ordinal);
        return normalized switch
        {
            "OPEN" => Open,
            "SET" => Set,
            "INJECTERROR" => InjectError,
            "ASSERT" => Assert,
            "BEGINDEPOSIT" => BeginDeposit,
            "TRACKDEPOSIT" => TrackDeposit,
            "FIXDEPOSIT" => FixDeposit,
            "ENDDEPOSIT" => EndDeposit,
            "DISPENSE" => Dispense,
            "DELAY" => Delay,
            "ENABLE" => Enable,
            "REPEAT" => Repeat,
            _ => new ScriptCommandType(normalized)
        };
    }

    /// <summary>文字列から CurrencyCashType を取得します。</summary>
    /// <param name="type">種別文字列。</param>
    /// <returns>対応する CurrencyCashType。</returns>
    public static Core.Models.CurrencyCashType ToCurrencyCashType(string? type)
    {
        return string.Equals(type, "coin", StringComparison.OrdinalIgnoreCase)
            ? Core.Models.CurrencyCashType.Coin
            : Core.Models.CurrencyCashType.Bill;
    }

    /// <summary>文字列から DepositAction を取得します。</summary>
    /// <param name="action">アクション文字列。</param>
    /// <returns>対応する DepositAction。</returns>
    public static Core.Models.DepositAction ToDepositAction(string? action)
    {
        return (action?.ToUpperInvariant()) switch
        {
            "REPAY" => Core.Models.DepositAction.Repay,
            "CHANGE" => Core.Models.DepositAction.Change,
            "NOCHANGE" => Core.Models.DepositAction.NoChange,
            "STORE" => Core.Models.DepositAction.NoChange,
            _ => Core.Models.DepositAction.NoChange
        };
    }
}
