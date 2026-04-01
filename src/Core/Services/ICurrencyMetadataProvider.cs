using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>通貨のメタデータを提供するサービスのインターフェース。</summary>
public interface ICurrencyMetadataProvider
{
    /// <summary>通貨コード（例: "JPY"）。</summary>
    string CurrencyCode { get; }

    /// <summary>通貨記号（プレフィックス優先）。</summary>
    string Symbol { get; }

    /// <summary>通貨記号のプレフィックス（例: "¥", "$"）。通常、金額の前に表示されます。</summary>
    ReadOnlyReactiveProperty<string> SymbolPrefix { get; }

    /// <summary>通貨記号のサフィックス（例: "円"）。通常、金額の後ろに表示されます。</summary>
    ReadOnlyReactiveProperty<string> SymbolSuffix { get; }

    /// <summary>この通貨でサポートされている全金種のリスト（額面の降順）。</summary>
    IReadOnlyList<DenominationKey> SupportedDenominations { get; }

    /// <summary>メタデータが変更されたときに通知されるストリーム。</summary>
    Observable<Unit> Changed { get; }

    /// <summary>指定された金種の表示名を取得する。</summary>
    string GetDenominationName(DenominationKey key);

    /// <summary>指定された金種とカルチャの表示名を取得する。</summary>
    string GetDenominationName(DenominationKey key, string cultureCode);
}
