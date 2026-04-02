# OPOS Compliance Mapping

本シミュレーターにおける OPOS (OLE for Retail POS) 標準との対応関係およびエラーマッピングについて記述します。

## ResultCode Mapping

本シミュレーターがスローする `PosControlException` は、以下の `ErrorCode` にマッピングされます。

| ErrorCode             | OPOS 定数 (OPOS_E_*) | 発生条件                                                |
| :-------------------- | :------------------- | :------------------------------------------------------ |
| **Success** (0)       | SUCCESS              | 正常終了。                                              |
| **Closed** (101)      | CLOSED               | デバイスが閉じている（Openされていない）。              |
| **NotClaimed** (102)  | NOTCLAIMED           | デバイスが他から占有されている。                        |
| **NotOpen** (103)     | NOTOPEN              | デバイスがオープンされていない。                        |
| **Disabled** (104)    | DISABLED             | デバイスが有効化されていない。                          |
| **Illegal** (106)     | ILLEGAL              | 不正なパラメータ、順序、またはサポートされない引数。    |
| **NoHardware** (107)  | NOHARDWARE           | 物理的なハードウェアが見つからない、または未接続。      |
| **Offline** (108)     | OFFLINE              | デバイスがオフライン状態。                              |
| **NoService** (109)   | NOSERVICE            | 制御サービスが利用できない。                            |
| **Failure** (111)     | FAILURE              | 致命的な失敗（メカニカルトラブル等）。                  |
| **Timeout** (112)     | TIMEOUT              | デバイスからの応答タイムアウト。                        |
| **Busy** (113)        | BUSY                 | 処理の実行中（ビジー状態）。                            |
| **Extended** (114)    | EXTENDED             | デバイス固有の拡張エラー。                              |
| **NoInventory** (118) | (Custom)             | 在庫不足（シミュレータ固有）。                          |
| **Unimplemented** (119)| (Custom)            | 未実装の機能へのアクセス。                              |
| **Jammed** (300)      | (Custom)             | ハードウェア・ジャムが発生。                            |
| **Overlapped** (301)  | (Custom)             | 処理が重複している。                                    |

## ResultCodeExtended Mapping

`ErrorCode.Extended` が発生した際の拡張エラー定義です。

| Extended Code | Member Name  | 意味                                                     |
| :------------ | :----------: | :------------------------------------------------------- |
| **201**       | OverDispense | 現金不足のため、指定された金額または金種を払い出せない。 |

> [!NOTE]
> 実装コード上では `UposCashChangerErrorCodeExtended` 列挙型を使用して定数定義されています。

## イベント通知
OPOS コントロールへのイベント通知は `QueueEvent` メソッドを介して行われ、標準的な `DataEvent`, `StatusUpdateEvent` として配送されます。
シミュレーターの UI 上では、これらのイベント発生履歴が「Activity Feed」に記録されます。

---
*英語版については、[OposComplianceMapping.md](OposComplianceMapping.md) を参照してください。*
