using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using R3;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>投入された現金の追跡と通知、非同期リソースのライフサイクルを担当するクラス。</summary>
internal sealed class DepositTracker(Inventory inventory, ConfigurationProvider configProvider) : IDisposable
{
    private readonly Inventory inventory = inventory;
    private readonly ConfigurationProvider configProvider = configProvider;
    private readonly Subject<Unit> changedSubject = new();
    private readonly Subject<PosSharp.Abstractions.UposDataEventArgs> dataEventsSubject = new();
    private readonly Subject<PosSharp.Abstractions.UposErrorEventArgs> errorEventsSubject = new();
    private readonly CompositeDisposable disposables = [];
    private CancellationTokenSource? depositCts;
    private bool disposed;

    /// <summary>初期化します。</summary>
    public DepositTracker()
        : this(null!, null!)
    {
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => changedSubject;

    /// <summary>データイベントの通知ストリーム。</summary>
    public Observable<PosSharp.Abstractions.UposDataEventArgs> DataEvents => dataEventsSubject;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public Observable<PosSharp.Abstractions.UposErrorEventArgs> ErrorEvents => errorEventsSubject;

    /// <summary>新しい払い出しセッション用の CancellationToken を作成します。</summary>
    /// <returns>新しいトークン。</returns>
    public CancellationToken CreateNewToken()
    {
        depositCts?.Dispose();
        depositCts = new CancellationTokenSource();
        return depositCts.Token;
    }

    /// <summary>現在の操作を非同期でキャンセルします。</summary>
    /// <returns>完了を示すタスク。</returns>
    public async Task CancelCurrentAsync()
    {
        if (depositCts != null && !depositCts.IsCancellationRequested)
        {
            await depositCts.CancelAsync().ConfigureAwait(false);
            depositCts.Dispose();
            depositCts = null;
        }
    }

    /// <summary>現在の操作をキャンセルします。</summary>
    /// <returns>キャンセルが実行された場合は true。</returns>
    public bool CancelCurrent()
    {
        if (depositCts == null || depositCts.IsCancellationRequested)
        {
            return false;
        }

        depositCts.Cancel();
        return true;
    }

    /// <summary>トークンをリセット(破棄)します。</summary>
    public void ResetToken()
    {
        depositCts?.Dispose();
        depositCts = null;
    }

    /// <summary>状態変更を通知します。</summary>
    public void NotifyChanged()
    {
        if (!disposed)
        {
            changedSubject.OnNext(Unit.Default);
        }
    }

    /// <summary>データ受信を通知します。</summary>
    /// <param name="data">データ値。</param>
    public void NotifyData(int data)
    {
        if (!disposed)
        {
            dataEventsSubject.OnNext(new PosSharp.Abstractions.UposDataEventArgs(data));
        }
    }

    /// <summary>エラー発生を通知します。</summary>
    /// <param name="errorCode">エラーコード。</param>
    /// <param name="extended">拡張エラーコード。</param>
    public void NotifyError(DeviceErrorCode errorCode, int extended)
    {
        if (!disposed)
        {
            errorEventsSubject.OnNext(new PosSharp.Abstractions.UposErrorEventArgs((PosSharp.Abstractions.UposErrorCode)errorCode, extended, PosSharp.Abstractions.UposErrorLocus.Output, PosSharp.Abstractions.UposErrorResponse.Retry));
        }
    }

    /// <summary>投入された現金の追跡処理（集計、バリデーション、シリアル番号生成）を行います。</summary>
    /// <param name="key">投入された金種。</param>
    /// <param name="count">投入された枚数。</param>
    /// <param name="state">更新対象の預入状態。</param>
    public void ProcessDenominationTracking(DenominationKey key, int count, DepositState state)
    {
        // [UPOS] Simulate identification/validation
        state.Status = DeviceDepositStatus.Validation;

        var config = configProvider.Config;
        var denomConfig = config.GetDenominationSetting(key);
        var maxCount = denomConfig.Full;

        var currentInStorage = inventory.GetCount(key);
        var available = Math.Max(0, maxCount - currentInStorage);
        var overflow = Math.Max(0, count - available);

        // [LIFECYCLE] Record progress
        if (state.Counts.TryGetValue(key, out var current))
        {
            state.Counts[key] = current + count;
        }
        else
        {
            state.Counts[key] = count;
        }

        inventory.AddEscrow(key, count);
        state.DepositAmount += key.Value * count;
        state.OverflowAmount += key.Value * overflow;

        for (var i = 0; i < count; i++)
        {
            state.DepositedSerials.Add($"SN-{key.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString()[..8]}");
        }

        // [LIFECYCLE] Finished identification
        state.Status = DeviceDepositStatus.Counting;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        depositCts?.Dispose();
        changedSubject.OnCompleted();
        changedSubject.Dispose();
        dataEventsSubject.OnCompleted();
        dataEventsSubject.Dispose();
        errorEventsSubject.OnCompleted();
        errorEventsSubject.Dispose();
        disposables.Dispose();
    }
}
