# CashChanger Simulator

WPFベースの釣銭機シミュレーターです。UnifiedPOS (UPOS) 規格に準拠した動作をエミュレートし、POSアプリケーションのテストやデバッグを支援します。

## 主な機能

- **UPOS 準拠の挙動**: `DispenseChange`, `DispenseCash`, 入金サイクル（`BeginDeposit`〜`EndDeposit`）のシミュレーション。
- **マルチ通貨サポート**: JPY、USD 等、通貨ごとの金種設定が可能。
- **リアルタイムフィード**: 入金・払出・エラー状態の変更を即座に表示。
- **不一致シミュレーション**: 在庫の不一致状態（Discrepancy）を意図的に発生させ、例外処理のテストが可能。
- **スクリプト実行**: JSON形式のスクリプトによる自動シナリオテスト。

## セットアップ

### 前提条件

- .NET 10.0 SDK
- Windows OS (WPF アプリケーションのため)

### ビルドと実行

1. リポジトリをクローンまたはダウンロードします。
2. ターミナルでルートディレクトリに移動し、以下のコマンドを実行します。

```powershell
# ビルド
dotnet build

# アプリケーションの実行
dotnet run --project src/CashChangerSimulator.UI.Wpf/CashChangerSimulator.UI.Wpf.csproj
```

### テストの実行

```powershell
# すべてのテスト（単体・結合・UI）を実行
dotnet test
```

## ドキュメント

詳細な情報は `docs/` ディレクトリ配下のドキュメントを参照してください。

- [Architecture Overview](docs/Architecture_JP.md): アーキテクチャの概要
- [UPOS Compliance Mapping](docs/UposComplianceMapping_JP.md): UPOS インターフェースの対応状況
- [OPOS Compliance Mapping](docs/OposComplianceMapping_JP.md): OPOS エラーコードとの対応関係
- [標準モード操作説明書](docs/ApplicationOperatingInstructions_JP.md): アプリケーションの基本的な操作方法
- [POSモード操作説明書](docs/PosModeApplicationOperatingInstructions_JP.md): POS 連携シミュレーションのガイド

---
*英語版については、[README.md](README.md) を参照してください。*
