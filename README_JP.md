# CashChanger Simulator - Core

[![NuGet Version](https://img.shields.io/nuget/v/CashChangerSimulator.Core)](https://www.nuget.org/packages/CashChangerSimulator.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

このリポジトリは、CashChanger Simulator のコアロジックおよびハードウェア・デバイスエミュレーションを提供します。各種アプリケーション（WPF、CLI、Web API 等）を支える共通基盤として設計されています。

## 📦 本リポジトリに含まれる NuGet パッケージ

本リポジトリでは、以下の 4 つのパッケージを管理しています：

- **CashChangerSimulator.Core**: プラットフォーム非依存のコアロジック、通貨計算、およびハードウェア・イベント抽象化。
- **CashChangerSimulator.Device.Virtual**: 純粋な C# による仮想ハードウェア。Web/Linux/Windows 上で動作、.NET 10 対応。
- **CashChangerSimulator.Device.PosForDotNet**: Windows 固有の UPOS (POS for .NET) アダプター。既存システムとの連携用。

> [!NOTE]
> ユーザーインターフェース・コンポーネントは、それぞれ専用のリポジトリで管理されています：
> [**Cli** (コマンドライン・インターフェース)](https://github.com/w-red/CashChangerSimulator.Cli)、[**Wpf** (Windows デスクトップ UI)](https://github.com/w-red/CashChangerSimulator.Wpf)

---

## 主な機能 (プラットフォーム非依存コア)

- **リアクティブ・メッセージング (R3)**: [**R3**](https://github.com/Cysharp/R3) を活用し、`IsBusy` や `State` の一貫した管理、および非同期イベント通知を実現。
- **ゼロ依存のイベント抽象化**: 上位コンポーネント (Core/Cli) は、Windows 固有ライブラリから完全に切り離された抽象化イベント定義にのみ依存。
- **UPOS 準拠の動作**: 仮想デバイス層を通じて `DispenseChange`, `DispenseCash` や入金サイクルを正確にエミュレート。
- **マルチプラットフォーム対応**: Core および Virtual Device プロジェクトは Windows 固有ライブラリへの依存がゼロ。
- **確実なリソース管理**: `CompositeDisposable` パターンを採用し、メモリリークの防止とリソースの確実な解放を保証。

## セットアップ

### NuGet を利用する場合 (推奨)

NuGet.org または GitHub Packages から公式パッケージをインストールできます。

```powershell
dotnet add package CashChangerSimulator.Core
dotnet add package CashChangerSimulator.Device.Virtual
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
- [OPOS Compliance Mapping](docs/OposComplianceMapping.md): OPOS 動作とエラーコードのマッピング。

---
*英語版は [README.md](README.md) を参照してください。*
