using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Services;

/// <summary>シミュレータの設定と言語・通貨状態を管理するインターフェース。</summary>
public interface IUposConfigurationManager
{
    /// <summary>現在アクティブな通貨コードを取得または設定します。</summary>
    string CurrencyCode { get; set; }

    /// <summary>利用可能な通貨コードのリストを取得します。</summary>
    string[] CurrencyCodeList { get; }

    /// <summary>入金可能な通貨コードのリストを取得します。</summary>
    string[] DepositCodeList { get; }

    /// <summary>利用可能なキャッシュのリストを取得します。</summary>
    CashUnits CurrencyCashList { get; }

    /// <summary>入金可能なキャッシュのリストを取得します。</summary>
    CashUnits DepositCashList { get; }

    /// <summary>設定を初期化し、リロードの購読を開始します。</summary>
    void Initialize();
}
