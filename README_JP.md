# CashChanger Simulator - Core

このリポジトリは、CashChanger Simulator のコアロジックおよびデバイスエミュレーションを提供します。各種アプリケーション（WPF、CLI、Web API 等）を支える共通基盤として設計されています。

## 📦 本プロジェクトの構成

リポジトリは以下のモジュールに分割されています。

- **[CashChangerSimulator.Core](https://github.com/w-red/CashChangerSimulator.Core)** (本リポジトリ): ビジネスロジック、UPOS/OPOS エミュレータ、Service Object。
- **[CashChangerSimulator.UI.Wpf](https://github.com/w-red/CashChangerSimulator.UI.Wpf)**: WPF 版デスクトップアプリケーション。
- **[CashChangerSimulator.UI.Cli](https://github.com/w-red/CashChangerSimulator.UI.Cli)**: 自動化テストや軽量監視用のコマンドラインインタフェース。

---

## 🚀 Live Demo / Simulator API

Virtual Cash Changer API は Google Cloud Run 上で動作しており、ローカル環境のセットアップなしに API 連携のテストが可能です。

- **Base URL**: [https://cash-changer-api-904915502524.asia-northeast1.run.app](https://cash-changer-api-904915502524.asia-northeast1.run.app)
- **Interactive Documentation (Scalar)**: [View API Reference](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## 主な機能 (コアロジック)

- **UPOS 準拠の動作**: `DispenseChange`, `DispenseCash` や 入金サイクル (`BeginDeposit` から `EndDeposit`) 全体をエミュレートします。
- **マルチ通貨対応**: JPY, USD など、デノミネーションの定義を柔軟に変更可能です。
- **Service Object 実装**: POS for .NET 互換のサービスオブジェクトを含みます。
- **入出金ロジック**: 在庫状態や入金追跡など、堅牢な状態管理を提供します。

## セットアップ

### 前提条件

- .NET 10.0 SDK

### ビルド

```powershell
# Core ライブラリとデバイスシミュレータのビルド
dotnet build
```

### ローカル NuGet パッケージの発行

UI プロジェクト（WPF, CLI）で利用するために、ローカルな NuGet ソースへパッケージを発行します。

```powershell
./scripts/publish_local.ps1
```

## ドキュメント

詳細は `docs/` ディレクトリ内のドキュメントをご参照ください。

- [Architecture Overview](docs/Architecture_JP.md): システム設計概要。
- [UPOS Compliance Mapping](docs/UposComplianceMapping_JP.md): UPOS インタフェースの実装状況。
- [OPOS Compliance Mapping](docs/OposComplianceMapping_JP.md): OPOS 動作とエラーコードのマッピング。

---
*英語版は [README.md](README.md) を参照してください。*
