namespace CashChangerSimulator.Device.Testing;

/// <summary>SimulatorCashChanger のテスト用拡張を提供します。</summary>
/// <remarks>
/// テストプロジェクトにおいてデバイスの内部状態（イベント発火や非同期フラグ）への直感的なアクセスを実現します。
/// ※C# 14 Extension Types の仕様策定状況に合わせ、現在は静的拡張メソッドとして実装されています。
/// </remarks>
public static class SimulatorTestingExtensions
{
    extension(SimulatorCashChanger sim)
    {
        /// <summary>状態検証をスキップするかどうかを取得または設定します。</summary>
        /// <remarks>
        /// テストセットアップ用。
        /// true を設定すると、UPOS の状態遷移ルールを無視して、任意のタイミングでテストデータの投入が可能になります。
        /// </remarks>
        public bool SkipStateVerification
        {
            get => sim.Context.Mediator.SkipStateVerification;
            set => sim.Context.Mediator.SkipStateVerification = value;
        }

        /// <summary>UPOS イベントを強制的に発生させます。</summary>
        /// <remarks>特定のステータス変更イベントが発生した際の、アプリケーション側の応答を検証するために使用します。</remarks>
        public void FireEvent(EventArgs e)
            => sim.FireEventInternal(e);

        /// <summary>非同期処理の実行状態を強制的に設定します。</summary>
        /// <remarks>デバイスが通信中や動作中（ビジー状態）である場合の排他制御ロジックをテストするために使用します。</remarks>
        public void SetAsyncProcessing(bool isBusy)
            => sim.SetAsyncProcessingInternal(isBusy);
    }
}
