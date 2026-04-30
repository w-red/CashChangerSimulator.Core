using System.Collections.Immutable;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金セッションの内部状態を保持する不変レコード。DepositController からの責務分離のために導入されました。</summary>
public sealed record DepositState(
    decimal DepositAmount = 0m,
    decimal OverflowAmount = 0m,
    decimal RejectAmount = 0m,
    decimal RequiredAmount = 0m,
    DeviceDepositStatus Status = DeviceDepositStatus.None,
    bool IsPaused = false,
    bool IsFixed = false,
    bool IsBusy = false,
    DeviceErrorCode LastErrorCode = DeviceErrorCode.Success,
    int LastErrorCodeExtended = 0,
    ImmutableDictionary<DenominationKey, int>? Counts = null,
    ImmutableList<string>? DepositedSerials = null,
    ImmutableList<string>? LastDepositedSerials = null)
{
    /// <summary>投入された金種ごとの枚数。</summary>
    public ImmutableDictionary<DenominationKey, int> Counts { get; init; } = Counts ?? [];

    /// <summary>投入された紙幣のシリアル番号リスト。</summary>
    public ImmutableList<string> DepositedSerials { get; init; } = DepositedSerials ?? [];

    /// <summary>確定時に同期される直前のシリアル番号リスト。</summary>
    public ImmutableList<string> LastDepositedSerials { get; init; } = LastDepositedSerials ?? [];

    /// <summary>初期状態を取得します。</summary>
    public static DepositState Empty { get; } = new();
}
