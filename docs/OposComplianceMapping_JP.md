# OPOS Compliance Mapping

本シミュレーターにおける OPOS (OLE for Retail POS) 標準との対応関係およびエラーマッピングについて記述します。

## ResultCode Mapping

本シミュレーターがスローする `PosControlException` は、以下の `ErrorCode` にマッピングされます。

| ErrorCode          | OPOS 定数 (OPOS_E_*) | 発生条件                                                |
| :----------------- | :------------------- | :------------------------------------------------------ |
| **Success** (0)    | SUCCESS              | 正常終了。                                              |
| **Illegal** (106)  | ILLEGAL              | 不正なパラメータ、順序、またはサポートされない引数.     |
| **Failure** (111)  | FAILURE              | メカニカルトラブル（Jam等）が発生している状態での操作。 |
| **Extended** (114) | EXTENDED             | デバイス固有の拡張エラー（在庫不足など）。              |
| **Busy** (113)     | BUSY                 | 非同期処理実行中の二重呼び出し。                        |

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
