# CashChanger Simulator - Core

[![NuGet Version](https://img.shields.io/nuget/v/CashChangerSimulator.Core)](https://www.nuget.org/packages/CashChangerSimulator.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

このリポジトリは、CashChanger Simulator のコアロジックおよびハードウェア・デバイスエミュレーションを提供します。各種アプリケーション（WPF、CLI、Web API 等）を支える共通基盤として設計されています。

## 📦 本プロジェクトの構成

このリポジトリは、コアロジックとシミュレーション層に焦点を当てています。UI コンポーネントは別のリポジトリで管理されています。

| リポジトリ / パッケージ | 説明 |
| --- | --- |
| **[CashChangerSimulator.Core](https://github.com/w-red/CashChangerSimulator.Core)** | プラットフォーム非依存のコアロジック、通貨計算、およびマネージャー。 |
| **[CashChangerSimulator.Device](https://github.com/w-red/CashChangerSimulator.Core)** | 抽象化されたデバイスインターフェースと共通のシミュレーション基盤。 |
| **[CashChangerSimulator.Device.Virtual](https://github.com/w-red/CashChangerSimulator.Core)** | 純粋な C# による仮想ハードウェア。Web/Linux/Windows 上で動作、.NET 10 対応。 |
| **[CashChangerSimulator.Device.PosForDotNet](https://github.com/w-red/CashChangerSimulator.Core)** | Windows 固有の UPOS (POS for .NET) アダプター。既存システムとの連携用。 |

> [!NOTE]
> **[Cli](https://github.com/w-red/CashChangerSimulator.Cli)** (コマンドライン・インターフェース) および **[Wpf](https://github.com/w-red/CashChangerSimulator.Wpf)** (Windows デスクトップ UI) は、それぞれ専用のリポジトリで管理されています。

---

## 主な機能 (プラットフォーム非依存コア)

- **デカップリング・アーキテクチャ**: ビジネスロジック (`Core`)、シミュレーション論理 (`Device.Virtual`)、ハードウェア・アダプター (`Device.PosForDotNet`) を厳格に分離。
- **UPOS 準拠の動作**: 仮想デバイス層を通じて `DispenseChange`, `DispenseCash` や入金サイクルを正確にエミュレート。
- **マルチプラットフォーム対応**: Core および Virtual Device プロジェクトは Windows 固有ライブラリへの依存がゼロであり、Web API、CLI、Linux 環境での利用が可能です。
- **堅牢な状態管理**: 入出金追跡や在庫管理にプラットフォーム非依存の型定義を使用。

## セットアップ

### NuGet を利用する場合 (推奨)

NuGet.org または GitHub Packages から公式パッケージをインストールできます。

```powershell
dotnet add package CashChangerSimulator.Core
dotnet add package CashChangerSimulator.Device
```

### ローカルビルド

ソースコードからビルドする場合：

```powershell
# Core ライブラリとデバイスシミュレータのビルド
dotnet build
```

---

## 🚀 Live Demo / Simulator API

Virtual Cash Changer API は Google Cloud Run 上で動作しており、ローカル環境のセットアップなしに API 連携のテストが可能です。

- **対話型ドキュメント (Scalar)**: [**API リファレンスを表示**](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## ドキュメント

詳細は `docs/` ディレクトリ内のドキュメントをご参照ください。

- [Architecture Overview](docs/Architecture_JP.md): システム設計概要。
- [UPOS Compliance Mapping](docs/UposComplianceMapping_JP.md): UPOS インタフェースの実装状況。
- [OPOS Compliance Mapping](docs/OposComplianceMapping_JP.md): OPOS 動作とエラーコードのマッピング。

---
*英語版は [README.md](README.md) を参照してください。*
