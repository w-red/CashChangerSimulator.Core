# UPOS Compliance Mapping (CashChanger)

このドキュメントは、UnifiedPOS (UPOS) v1.15.1 仕様に対する本シミュレーターの実装状況を記述します。

## 凡例
- ✅: 実装済み (Implemented)
- ⚠️: 部分実装 / シミュレーション専用 (Partially implemented / Simulation only)
- ❌: 未実装 (Not Implemented)
- N/A: 本シミュレーターでは対象外 (Not Applicable)

## Methods

| Method                  | Status | Notes                                                                           |
| :---------------------- | :----: | :------------------------------------------------------------------------------ |
| **Open** / **Close**    |   ✅    | 基本的なライフサイクルをシミュレート。                                          |
| **Claim** / **Release** |   ✅    | 排他制御のシミュレーション。                                                    |
| **CheckHealth**         |   ✅    | 基本的なステータスチェック。                                                    |
| **DirectIO**            |   ✅    | 独自拡張コマンド（SET_OVERLAP, SET_JAM, ADJUST_CASH_COUNTS_STR 等）をサポート。 |
| **BeginDeposit**        |   ✅    | 入金セッションの開始。                                                          |
| **EndDeposit**          |   ✅    | 入金セッションの終了。引数（CashDepositAction）に応じた払出。                   |
| **FixDeposit**          |   ✅    | 投入金額の確定シミュレーション。                                                |
| **PauseDeposit**        |   ✅    | 入金の中断・再開。                                                              |
| **ReadCashCounts**      |   ✅    | 現在の在庫一覧の取得。                                                          |
| **DispenseChange**      |   ✅    | 指定金額の払出。標準お釣り計算アルゴリズム搭載。                                |
| **DispenseCash**        |   ✅    | 指定金種・枚数の払出。                                                          |
| **AdjustCashCounts**    |   ✅    | 在庫の論理的な増減。                                                            |

## Properties

| Property                | Status | Notes                                  |
| :---------------------- | :----: | :------------------------------------- |
| **DeviceEnabled**       |   ✅    |                                        |
| **DataEventEnabled**    |   ✅    |                                        |
| **AsyncMode**           |   ✅    | 払出処理の非同期実行をサポート。       |
| **CurrencyCode**        |   ✅    | アクティブな通貨の指定。               |
| **CurrencyCodeList**    |   ✅    | 設定ファイル(TOML)に基づき動的に生成。 |
| **DepositAmount**       |   ✅    | 現在の入金セッションでの合計額。       |
| **DepositCashList**     |   ✅    | 現在の入金セッションでの詳細枚数。     |
| **DeviceStatus**        |   ✅    | OK, EMPTY, NEAR_EMPTY 等。             |
| **FullStatus**          |   ✅    | OK, FULL, NEAR_FULL 等。               |
| **RealTimeDataEnabled** |   ✅    | 有効時、入金ごとに DataEvent を通知。  |

## Events

| Event                   | Status | Notes                                                              |
| :---------------------- | :----: | :----------------------------------------------------------------- |
| **DataEvent**           |   ✅    | 入金完了またはリアルタイム通知通知（RealTimeDataEnabled=True時）。 |
| **StatusUpdateEvent**   |   ✅    | 状態変化（OK, Jam, Removed, AsyncFinished等）の通知。              |
| **ErrorEvent**          |   ✅    | 非同期操作等の失敗時に通知。                                       |
| **OutputCompleteEvent** |   ✅    | 非同期操作完了時に通知。                                           |

---
*英語版については、[UposComplianceMapping.md](UposComplianceMapping.md) を参照してください。*
