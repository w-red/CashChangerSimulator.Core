# API 仕様書 - CashChanger Simulator

CashChanger Simulator は、シミュレートされたハードウェアと対話するための 2 つの主要な方法を提供します：**POS for .NET (UPOS) 標準インターフェース**、または **ネイティブ C# リアクティブインターフェース**です。

## 1. 初期化と構成

シミュレータは `SimulatorDependencies` レコードを使用して初期化されます。これにより、カスタムプロバイダーを注入したり、デフォルトの実装を使用したりできます。

### SimulatorDependencies (レコード)
| プロパティ | 型 | 説明 |
| :--- | :--- | :--- |
| `ConfigProvider` | `ConfigurationProvider?` | 設定ロジック（TOML からのロードなど）を提供します。 |
| `Inventory` | `Inventory?` | デバイス内の現金の初期在庫。 |
| `Manager` | `CashChangerManager?` | 現金操作のコアコーディネーションロジック。 |
| `DepositController` | `DepositController?` | 入金サイクルのロジック。 |
| `DispenseController` | `DispenseController?` | 出金のロジック。 |
| `Mediator` | `IUposMediator?` | UPOS の状態とイベントをオーケストレートします。 |

---

## 2. POS for .NET インターフェース (`SimulatorCashChanger`)

`SimulatorCashChanger` クラスは、`Microsoft.PointOfService.CashChanger` 標準を実装しています。

### 基本的なライフサイクル
- `Open()`: デバイスをオープンします。
- `Close()`: デバイスをクローズします。
- `Claim(int timeout)`: デバイスを独占的に使用するために「ロック」します。
- `Release()`: 独占ロックを解放します。
- `DeviceEnabled = true/false`: デバイスをアクティブ化または非アクティブ化します。

### 入金操作 (Deposit)
- `BeginDeposit()`: 入金受付サイクルを開始します。
- `EndDeposit(CashDepositAction action)`: サイクルを終了し、現金を収納（確定）または返却します。
- `FixDeposit()`: 現在投入されている現金を確定させ、サイクル中に返却されないようにします。
- `PauseDeposit(CashDepositPause control)`: 入金処理を一時停止または再開します。

### 出金操作 (Dispense)
- `DispenseChange(int amount)`: 最適な組み合わせで指定された金額を払い出します。
- `DispenseCash(CashCount[] counts)`: 特定の金種と枚数を指定して払い出します。

### 在庫とステータス
- `ReadCashCounts()`: 現在の在庫枚数を取得します。
- `AdjustCashCounts(CashCount[] counts)`: 手動で在庫に現金を追加または削除します。
- `PurgeCash()`: すべての現金を回収庫（Collection Box）へ移動します。

### 拡張非同期メソッド
モダンな .NET アプリケーション向けに、`Task` ベースの非同期ラッパーを提供しています：
- `OpenAsync()`, `CloseAsync()`
- `ClaimAsync(timeout)`, `ReleaseAsync()`
- `BeginDepositAsync()`, `EndDepositAsync(action)`, `FixDepositAsync()`
- `DispenseChangeAsync(amount)`, `DispenseCashAsync(counts)`

---

## 3. ネイティブ C# リアクティブインターフェース (`ICashChangerDevice`)

POS for .NET を使用しないクロスプラットフォームアプリケーション（Web, Linux など）では、`ICashChangerDevice` インターフェース（`VirtualCashChangerDevice` が提供）を直接使用します。

### リアクティブプロパティ (R3 を使用)
- `IsBusy`: `ReadOnlyReactiveProperty<bool>` - 操作が進行中かどうかを示します。
- `State`: `ReadOnlyReactiveProperty<DeviceControlState>` - 現在の状態（Idle, Busy, Error など）。

### オブザーバブル (イベント通知)
- `DataEvents`: 現金が挿入または処理されたときに通知されます。
- `ErrorEvents`: ハードウェア層でエラーが発生したときに通知されます。
- `StatusUpdateEvents`: デバイスの健康状態（Near Full, Empty など）が変更されたときに通知されます。

---

## 4. 共通データ構造

### CashCount
`DispenseCash` や `AdjustCashCounts` で使用されます。
- `Denomination`: 通貨単位の価値。
- `Count`: 枚数。

### DeviceControlState (列挙型)
- `Closed` (クローズ), `Idle` (待機中), `Busy` (処理中), `Error` (エラー)。
