# CashChanger Simulator - Core

このリポジトリは、CashChanger Simulator のコアロジックおよびデバイスエミュレーションを提供します。各種アプリケーション（WPF、CLI、Web API 等）を支える共通基盤として設計されています。

## 📦 本プロジェクトの構成

リポジトリは以下のモジュールに分割されています。

- **[CashChangerSimulator.Core](https://github.com/w-red/CashChangerSimulator.Core)**: プラットフォーム非依存のビジネスロジック、マネージャー、共有モデル。
- **[CashChangerSimulator.Device.Virtual](https://github.com/w-red/CashChangerSimulator.Core/tree/main/src/Device.Virtual)**: プラットフォーム非依存のデバイスシミュレーション論理およびコントローラー。
- **[CashChangerSimulator.Device.PosForDotNet](https://github.com/w-red/CashChangerSimulator.Core/tree/main/src/Device.PosForDotNet)**: Windows 固有の POS for .NET (UPOS) アダプター層。

---

## 🚀 Live Demo / Simulator API

Virtual Cash Changer API は Google Cloud Run 上で動作しており、ローカル環境のセットアップなしに API 連携のテストが可能です。

- **Base URL**: [https://cash-changer-api-904915502524.asia-northeast1.run.app](https://cash-changer-api-904915502524.asia-northeast1.run.app)
- **Interactive Documentation (Scalar)**: [View API Reference](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## 主な機能 (プラットフォーム非依存コア)

- **デカップリング・アーキテクチャ**: ビジネスロジック (`Core`)、シミュレーション論理 (`Device.Virtual`)、ハードウェア・アダプター (`Device.PosForDotNet`) を厳格に分離。
- **UPOS 準拠の動作**: 仮想デバイス層を通じて `DispenseChange`, `DispenseCash` や入金サイクルを正確にエミュレート。
- **マルチプラットフォーム対応**: Core および Virtual Device プロジェクトは Windows 固有ライブラリへの依存がゼロであり、Web API、CLI、Linux 環境での利用が可能です。
- **堅牢な状態管理**: 入出金追跡や在庫管理にプラットフォーム非依存の型定義 (`DeviceErrorCode` 等) を使用。

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
