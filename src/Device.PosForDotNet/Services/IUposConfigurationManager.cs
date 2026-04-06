using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>シミュレータの設定と言語・通貨状態を管理するインターフェース。</summary>
public interface IUposConfigurationManager
{
    /// <summary>Gets or sets 現在アクティブな通貨コードを取得または設定します。</summary>
    string CurrencyCode { get; set; }

    /// <summary>Gets 利用可能な通貨コードのリストを取得します。</summary>
    string[] CurrencyCodeList { get; }

    /// <summary>Gets 入金可能な通貨コードのリストを取得します。</summary>
    string[] DepositCodeList { get; }

    /// <summary>Gets 利用可能なキャッシュのリストを取得します。</summary>
    CashUnits CurrencyCashList { get; }

    /// <summary>Gets 入金可能なキャッシュのリストを取得します。</summary>
    CashUnits DepositCashList { get; }

    /// <summary>設定を初期化し、リロードの購読を開始します。</summary>
    void Initialize();
}
