using System.Globalization;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using R3;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金(払出)処理の進行管理、イベント通知、およびキャンセル制御を担当するクラス。</summary>
internal sealed class DispenseTracker : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private readonly Subject<Unit> changedSubject = new();
    private readonly Subject<UposOutputCompleteEventArgs> outputCompleteEventsSubject = new();
    private readonly Subject<UposErrorEventArgs> errorEventsSubject = new();

    private CancellationTokenSource? dispenseCts;
    private bool disposed;

    /* Stryker disable all : Internal resource registration is structural and hard to verify individually without redundant tests */

    /// <summary>初期化します。</summary>
    public DispenseTracker()
    {
        disposables.Add(changedSubject);
        disposables.Add(outputCompleteEventsSubject);
        disposables.Add(errorEventsSubject);
    }

    /* Stryker restore all */

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => changedSubject;

    /// <summary>出力完了イベントを受け取るためのストリーム。</summary>
    public Observable<UposOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEventsSubject;

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<UposErrorEventArgs> ErrorEvents => errorEventsSubject;

    /// <summary>例外をエラーコードへマッピングします。</summary>
    /// <param name="ex">例外。</param>
    /// <param name="code">マッピングされたエラーコード。</param>
    /// <param name="codeEx">マッピングされた拡張エラーコード。</param>
    public static void HandleDispenseError(Exception ex, out DeviceErrorCode code, out int codeEx)
    {
        code = DeviceErrorCode.Failure;
        codeEx = 0;

        if (ex is DeviceException dex)
        {
            code = dex.ErrorCode;
            codeEx = dex.ErrorCodeExtended;
            return;
        }

        /* Stryker disable all : Reflection-based exception analysis for mocks/simulation is hard to verify via mutation */
        var type = ex.GetType();
        if (type.Name.Contains("PosControlException", StringComparison.Ordinal) || type.Name.Contains("MockPosControlException", StringComparison.Ordinal))
        {
            var errorCodeProp = type.GetProperty("ErrorCode");
            var errorCodeExtendedProp = type.GetProperty("ErrorCodeExtended");

            if (errorCodeProp != null)
            {
                var rawValue = errorCodeProp.GetValue(ex);
                if (rawValue is int i)
                {
                    code = (DeviceErrorCode)i;
                }
                else if (rawValue != null)
                {
                    code = (DeviceErrorCode)Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                }
            }

            if (errorCodeExtendedProp != null)
            {
                var rawValue = errorCodeExtendedProp.GetValue(ex);
                if (rawValue is int i)
                {
                    codeEx = i;
                }
                else if (rawValue != null)
                {
                    codeEx = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                }
            }
        }

        /* Stryker restore all */
    }

    /// <summary>新しいキャンセレーショントークンを取得します。以前のトークンは破棄されます。</summary>
    /// <returns>キャンセレーショントークン。</returns>
    public CancellationToken CreateNewToken()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispenseCts?.Dispose();
        dispenseCts = new CancellationTokenSource();
        return dispenseCts.Token;
    }

    /// <summary>現在のキャンセル処理を要求します。実行中だった場合は true を返します。</summary>
    /// <returns>キャンセル処理が発行された場合は true。</returns>
    public bool CancelCurrent()
    {
        if (dispenseCts != null && !dispenseCts.IsCancellationRequested)
        {
            dispenseCts.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>キャンセレーショントークンをリセット(破棄)します。</summary>
    public void ResetToken()
    {
        dispenseCts?.Dispose();
        dispenseCts = null;
    }

    /// <summary>状態変更イベントを発火します。</summary>
    public void NotifyChanged()
    {
        if (!disposed)
        {
            changedSubject.OnNext(Unit.Default);
        }
    }

    /// <summary>完了イベントを発火します。</summary>
    public void NotifyComplete()
    {
        if (!disposed)
        {
            outputCompleteEventsSubject.OnNext(new UposOutputCompleteEventArgs(0));
        }
    }

    /// <summary>エラーイベントを発火します。</summary>
    /// <param name="code">エラーコード。</param>
    /// <param name="codeEx">拡張エラーコード。</param>
    /// <param name="response">エラーレスポンス。</param>
    public void NotifyError(DeviceErrorCode code, int codeEx, UposErrorResponse response = PosSharp.Abstractions.UposErrorResponse.None)
    {
        if (!disposed)
        {
            errorEventsSubject.OnNext(new UposErrorEventArgs((UposErrorCode)code, codeEx, PosSharp.Abstractions.UposErrorLocus.Output, response));
        }
    }

    /// <summary>破棄します。</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        dispenseCts?.Cancel();
        dispenseCts?.Dispose();
        disposables.Dispose();
    }
}
