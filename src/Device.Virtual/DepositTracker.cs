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
    private readonly Subject<UposDataEventArgs> dataEventsSubject = new();
    private readonly Subject<UposErrorEventArgs> errorEventsSubject = new();
    private readonly CompositeDisposable disposables = [];
    private CancellationTokenSource? depositCts;
    private bool disposed;

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => changedSubject;

    /// <summary>データイベントの通知ストリーム。</summary>
    public Observable<UposDataEventArgs> DataEvents => dataEventsSubject;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public Observable<UposErrorEventArgs> ErrorEvents => errorEventsSubject;

    /// <summary>新しいセッション用の CancellationTokenSource を作成し、内部状態を更新します。</summary>
    /// <returns>新しい CancellationTokenSource。</returns>
    public CancellationTokenSource CreateNewCts()
    {
        depositCts?.Dispose();
        depositCts = new CancellationTokenSource();
        return depositCts;
    }

    /// <summary>新しい払い出しセッション用の CancellationToken を作成します。</summary>
    /// <returns>新しいトークン。</returns>
    public CancellationToken CreateNewToken()
    {
        return CreateNewCts().Token;
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
            dataEventsSubject.OnNext(new UposDataEventArgs(data));
        }
    }

    /// <summary>エラー発生を通知します。</summary>
    /// <param name="errorCode">エラーコード。</param>
    /// <param name="extended">拡張エラーコード。</param>
    public void NotifyError(DeviceErrorCode errorCode, int extended)
    {
        if (!disposed)
        {
            errorEventsSubject.OnNext(new UposErrorEventArgs((UposErrorCode)errorCode, extended, PosSharp.Abstractions.UposErrorLocus.Output, PosSharp.Abstractions.UposErrorResponse.Retry));
        }
    }

    /// <summary>投入された現金の追跡処理（集計、バリデーション、シリアル番号生成）を行います。</summary>
    /// <param name="key">投入された金種。</param>
    /// <param name="count">投入された枚数。</param>
    /// <param name="state">現在の預入状態。</param>
    /// <returns>更新後の預入状態。</returns>
    public DepositState ProcessDenominationTracking(DenominationKey key, int count, DepositState state)
    {
        var config = configProvider.Config;
        var denomConfig = config.GetDenominationSetting(key);
        var maxCount = denomConfig.Full;

        var currentInStorage = inventory.GetCount(key);
        var available = Math.Max(0, maxCount - currentInStorage);
        var overflow = Math.Max(0, count - available);

        // [UPOS] Record progress
        inventory.AddEscrow(key, count);

        var newCounts = state.Counts.SetItem(key, state.Counts.GetValueOrDefault(key) + count);
        var newAmount = state.DepositAmount + (key.Value * count);
        var newOverflow = state.OverflowAmount + (key.Value * overflow);

        var serialsBuilder = state.DepositedSerials.ToBuilder();
        for (var i = 0; i < count; i++)
        {
            serialsBuilder.Add($"SN-{key.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString()[..8]}");
        }

        return state with
        {
            Status = DeviceDepositStatus.Counting,
            Counts = newCounts,
            DepositAmount = newAmount,
            OverflowAmount = newOverflow,
            DepositedSerials = serialsBuilder.ToImmutable()
        };
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
