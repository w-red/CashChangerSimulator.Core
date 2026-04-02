# CashChangerSimulator Stabilization Lessons

本文書では、`CashChangerSimulator` の信頼性向上作業（特に非同期デポジット処理の安定化）から得られた教訓を記録します。

## 1. イベント発火の集約管理 (Centralized Event Firing)

### 問題点 (The Problem)
各コマンド（`FixDepositCommand` など）が個別に `DataEvent` などの UPOS イベントを発火させていたため、内部の `StatusCoordinator` が行う状態監視による発火と重複し、同一の操作で複数のイベントが発行される不整合が発生した。

### 教訓 (The Lesson)
- **単一責任の原則**: UPOS イベントの発火ロジックは `StatusCoordinator` に集約し、各コマンドは純粋にドメインレベルのコントローラーを操作するだけに留める。
- **不整合の防止**: コマンド側で 「RealTimeData が無効ならイベントを発火する」 といった条件付きロジックを持つと、状態管理が分散し、今回のような重複バグの原因となる。

## 2. 高頻度処理における状態遷移のガード (State Transition Guarding)

### 問題点 (The Problem)
高速なライフサイクル（`Begin` -> `Track` -> `Fix` -> `End`）を繰り返すテストにおいて、`StatusCoordinator` が同一の状態変化を複数回検知したり、フラグのクリアが間に合わなかったりした。

### 教訓 (The Lesson)
- **`DistinctUntilChanged` の活用**: `depositController.Changed` などの監視対象に対し、`Select` で取得した状態データに `DistinctUntilChanged` を適用することで、実効的な状態変化があったときのみ後続の処理（イベント発火）を行うようにする。
- **ライフサイクル初期化の徹底**: デポジットの開始 (`Start`) や終了 (`End`) を明示的にトラッキングし、`_wasFixed` などの内部フラグを確実にリセットする。

## 3. ヘッドレス CI 環境でのイベント安定化 (Stability in Headless CI)

### 問題点 (The Problem)
POS for .NET の内部イベントスレッド（`QueueEvent`）は、ヘッドレス環境やユニットテスト実行環境において NRE (NullReferenceException) をスローしたり、非同期的な遅延によりアサーションが失敗したりする場合がある。

### 教訓 (The Lesson)
- **バイパスフラグの活用**: `InternalSimulatorCashChanger.DisableUposEventQueuing` を `true` に設定し、POS.NET の内部キューイングを介さず、シミュレータのフック（`NotifyEvent`）を直接呼び出すことで、同期的かつ安定したイベント検証を可能にする。
- **`Task.Run` vs `Parallel.For`**: 並行性の検証には `Task.Run` を使用し、ハードウェアスレッドとアプリケーションスレッドが別々に動作する現実のユースケースに近づける。

## 4. xUnit での CancellationToken の正しい扱い

### 教訓 (The Lesson)
- 非同期テストメソッドでは、`TestContext.Current.CancellationToken` を使用してタイムアウトやテストの中断を正しく処理する（`xUnit1051` 警告への対応）。
