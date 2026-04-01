# CashChanger Simulator - アーキテクチャ概要

このドキュメントでは、CashChanger Simulator アプリケーションのアーキテクチャ構成について説明します。本シミュレーターは、信頼性が高く拡張性に優れた、WPFベースの釣銭機デバイス（UPOS 準拠の自動預け払い機など）のシミュレーション環境を提供することを目的としています。

## ハイレベル・アーキテクチャ

シミュレーターは、ユーザーインターフェース、デバイスシミュレーション、およびコアビジネスロジックを分離した、複数のモジュール層で構成されています。

```mermaid
graph TD
    %% Define Styles
    classDef uiLayer fill:#eef2ff,stroke:#6366f1,stroke-width:2px;
    classDef coreLayer fill:#f0fdf4,stroke:#22c55e,stroke-width:2px;
    classDef deviceLayer fill:#fffbeb,stroke:#f59e0b,stroke-width:2px;
    classDef infraLayer fill:#f8fafc,stroke:#94a3b8,stroke-width:2px;
    
    %% UI Layer
    subgraph UI ["プレゼンテーション層 (WPF)"]
        MainWindow["MainWindow"]
        DepositView["Deposit View / ViewModel"]
        DispenseView["Dispense View / ViewModel"]
        PosView["POS Transaction ViewModel"]
        AdvancedSim["高度なシミュレーション (Script, Events)"]
        ActivityFeed["アクティビティフィード (リアルタイムイベント)"]
    end
    class MainWindow,DepositView,DispenseView,PosView,AdvancedSim,ActivityFeed uiLayer

    %% Device/Service Layer
    subgraph Device ["デバイス層"]
        DepositController["DepositController"]
        DispenseController["DispenseController"]
        SimCashChanger["SimulatorCashChanger (ハードウェア互換)"]
        CashCountParser["CashCountParser (UPOS 文字列パーサ)"]
        ScriptService["ScriptExecutionService"]
    end
    class DepositController,DispenseController,SimCashChanger,CashCountParser,ScriptService deviceLayer

    %% Core Layer
    subgraph Core ["コアビジネスロジック"]
        Manager["CashChangerManager"]
        Inventory["在庫管理"]
        History["取引履歴"]
        Calc["釣銭計算アルゴリズム"]
        OposCode["UPOS/OPOS エラー定義"]
    end
    class Manager,Inventory,History,Calc,OposCode coreLayer

    %% Infrastructure
    subgraph Infrastructure ["基盤 / インフラ"]
        Logger["ZLogger (高スループット構造化ログ)"]
        Config["シミュレーション設定 (TOML)"]
    end
    class Logger,Config infraLayer

    %% Relationships
    MainWindow --> DepositView
    MainWindow --> DispenseView
    MainWindow --> PosView
    MainWindow --> AdvancedSim
    MainWindow --> ActivityFeed

    DepositView --> DepositController
    DispenseView --> DispenseController
    AdvancedSim --> ScriptService
    AdvancedSim --> SimCashChanger

    DepositController --> Manager
    DepositController --> SimCashChanger
    
    DispenseController --> Manager
    DispenseController --> SimCashChanger
    
    ScriptService --> SimCashChanger
    
    SimCashChanger --> CashCountParser
    SimCashChanger --> OposCode
    
    Manager --> Inventory
    Manager --> Calc
    Manager --> History
    
    SimCashChanger --> Logger
    Manager --> Logger
    ActivityFeed --> History
```

## 主要コンポーネント

1. **プレゼンテーション層 (`CashChangerSimulator.UI.Wpf`)**
    - **WPF (Windows Presentation Foundation)** と **MaterialDesignThemes** を使用して構築されています。
    - **R3** (Reactive Extensions) を活用し、応答性の高い View-ViewModel バインディングを実現しています。
    - **アクティビティフィード**: `TransactionHistory` と直接連携し、`RealTimeDataEnabled` が有効な場合の `DataEvent` や `StatusUpdateEvent` をリアルタイムに表示します。
    - `AdvancedSimulationWindow` などのコンポーネントにより、JSON スクリプトによる高度なストレス・シナリオテストが可能です。

2. **デバイス層 (`CashChangerSimulator.Device`)**
    - ビジネスロジックと仮想ハードウェア間の調整を行います。
    - **`SimulatorCashChanger`**: UPOS サービスオブジェクトの本体です。`DirectIO` による独自拡張（一括在庫調整、不一致シミュレーション等）や、非同期処理の状態管理を担当します。
    - **`CashCountParser`**: UPOS 標準のセミコロン区切り形式（例：`Coins;Bills`）を解析する専用パーサです。通貨係数によるスケーリングや、小数の省略形式（例：`.5`）に対応しています。
    - `DepositController` および `DispenseController` は、物理的なモータ遅延やカセットの状態をシミュレートした上で、コアロジックを呼び出します。

3. **コア層 (`CashChangerSimulator.Core`)**
    - UI やインフラストラクチャに依存しない独立した層です。
    - `CashChangerManager` が在庫の総計やログ履歴などの不変条件を保持します。
    - **エラー処理**: `UposCashChangerErrorCodeExtended` を通じてエラー定義を標準化し、OPOS/UPOS の拡張ResultCode（`OverDispense` 等）への準拠を保証しています。
    - `ChangeCalculator` アルゴリズムが、現在の在庫構成に基づいた最適な金種組み合わせを計算します。

4. **インフラストラクチャ層**
    - **ZLogger** を使用し、UI スレッドをブロックすることなく大量のイベントログを高速に出力します。
    - 設定管理には **TOML** ファイルを採用しており、通貨や金種の定義を容易にカスタマイズ可能です。

---
*英語版については、[Architecture.md](Architecture.md) を参照してください。*
