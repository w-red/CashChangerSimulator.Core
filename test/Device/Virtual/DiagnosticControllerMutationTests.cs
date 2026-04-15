using System.Reflection;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DiagnosticController のミューテーションテストを補強するテストクラス。</summary>
public class DiagnosticControllerMutationTests : DeviceTestBase
{
    private readonly DiagnosticController _controller;

    /// <summary>テストの初期設定を行います。</summary>
    public DiagnosticControllerMutationTests()
    {
        _controller = new DiagnosticController(Inventory, StatusManager);
        // テストのためにデバイスを接続状態にする
        StatusManager.Input.IsConnected.Value = true;
    }

    /// <summary>Inventory に null を渡した場合に ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void ConstructorWhenInventoryIsNullThrowsException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => new DiagnosticController(null!, StatusManager));
        ex.ParamName.ShouldBe("inventory");
    }

    /// <summary>コンストラクタの引数バリデーション（nullチェック）を検証します。</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ConstructorWhenArgumentIsNullThrowsArgumentNullException(bool nullInventory, bool nullStatus)
    {
        Should.Throw<ArgumentNullException>(() => new DiagnosticController(
            nullInventory ? null! : Inventory,
            nullStatus ? null! : StatusManager));
    }

    /// <summary>正常時に適切なヘルスレポートが生成されることを検証します（各レベルの網羅）。</summary>
    [Theory]
    [InlineData(DeviceHealthCheckLevel.Internal, "Internal", new[] { "Inventory: OK", "Total Denominations: 0", "Status: OK" })]
    [InlineData(DeviceHealthCheckLevel.External, "External", new[] { "Hardware: Connected", "Jam Status: Normal" })]
    [InlineData(DeviceHealthCheckLevel.Interactive, "Interactive", new[] { "Interactive check initiated", "verify LED patterns" })]
    public void GetHealthReportReturnsValidReportForEachLevel(DeviceHealthCheckLevel level, string expectedTitle, string[] expectedKeywords)
    {
        // Act
        var report = _controller.GetHealthReport(level);
 
        // Assert
        report.ShouldContain($"--- {expectedTitle} Health Check Report ---");
        foreach (var keyword in expectedKeywords)
        {
            report.ShouldContain(keyword);
        }
    }

    /// <summary>ハードウェアの状態が変化した際に、レポートに適切なキーワードが含まれることを検証します（400, 404, 405 撃破）。</summary>
    [Theory]
    [InlineData(false, false, "Disconnected")]
    [InlineData(true, true, "Jammed")]
    [InlineData(true, false, "Normal")]
    public void GetHealthReportWhenStatusChangesContainsCorrectKeywords(bool isConnected, bool isJammed, string expectedKeyword)
    {
        // Arrange
        StatusManager.Input.IsConnected.Value = isConnected;
        StatusManager.Input.IsJammed.Value = isJammed;

        // Act
        var report = _controller.GetHealthReport(DeviceHealthCheckLevel.External);

        // Assert
        report.ShouldContain(expectedKeyword);
        report.ShouldNotContain("---  Health Check Report ---"); // タイトルが空でないことの確認
    }

    /// <summary>統計情報の取得において、ワイルドカードと個別指定の論理条件 (||) を網羅します（Logical mutation 撃破）。</summary>
    [Theory]
    [InlineData(new[] { "*" }, true, true)]
    [InlineData(new[] { "SuccessfulDepletionCount" }, true, false)]
    [InlineData(new[] { "FailedDepletionCount" }, false, true)]
    [InlineData(new[] { "SuccessfulDepletionCount", "FailedDepletionCount" }, true, true)]
    [InlineData(new[] { "*", "SuccessfulDepletionCount" }, true, true)]
    [InlineData(new[] { "NonExistent" }, false, false)]
    public void RetrieveStatisticsFiltersCorrectly(string[] filter, bool expectSuccess, bool expectFailed)
    {
        // Arrange
        _controller.IncrementSuccessfulDepletion();
        _controller.IncrementFailedDepletion();

        // Act
        var stats = _controller.RetrieveStatistics(filter);

        // Assert
        var normalizedStats = stats.Replace("\r\n", "\n");
        normalizedStats.ShouldStartWith("<CommonStatistics>\n");
        
        if (expectSuccess) normalizedStats.ShouldContain("<SuccessfulDepletionCount>1</SuccessfulDepletionCount>");
        if (expectFailed) normalizedStats.ShouldContain("<FailedDepletionCount>1</FailedDepletionCount>");
        normalizedStats.ShouldEndWith("</CommonStatistics>\n");

        if (filter.Length == 1 && filter[0] == "*")
        {
            normalizedStats.ShouldBe("<CommonStatistics>\n  <SuccessfulDepletionCount>1</SuccessfulDepletionCount>\n  <FailedDepletionCount>1</FailedDepletionCount>\n</CommonStatistics>\n");
        }
    }

    /// <summary>RetrieveStatistics に null を渡した場合に例外が発生することを検証します（411 撃破）。</summary>
    [Fact]
    public void RetrieveStatisticsWhenArgumentIsNullThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => _controller.RetrieveStatistics(null!));
        ex.ParamName.ShouldBe("statistics");
    }

    /// <summary>インクリメントメソッドが正確に動作することを検証します。</summary>
    [Fact]
    public void IncrementMethodsWorkCorrectly()
    {
        // Act
        _controller.IncrementSuccessfulDepletion();
        _controller.IncrementFailedDepletion();

        // Assert
        _controller.SuccessfulDepletionCount.ShouldBe(1);
        _controller.FailedDepletionCount.ShouldBe(1);
    }

    /// <summary>Dispose 済み状態で公開メソッドが ObjectDisposedException を投げることを検証します。</summary>
    [Theory]
    [InlineData(nameof(DiagnosticController.GetHealthReport))]
    [InlineData(nameof(DiagnosticController.RetrieveStatistics))]
    public void AllPublicMethodsThrowObjectDisposedExceptionAfterDispose(string methodName)
    {
        // Arrange
        _controller.Dispose();

        // Act & Assert
        if (methodName == nameof(DiagnosticController.GetHealthReport))
            Should.Throw<ObjectDisposedException>(() => _controller.GetHealthReport(DeviceHealthCheckLevel.Internal));
        else
            Should.Throw<ObjectDisposedException>(() => _controller.RetrieveStatistics(new[] { "*" }));
    }

    /// <summary>Dispose の内部挙動と副作用を厳密に検証します（L105 撃破等）。</summary>
    [Fact]
    public void DisposeStrictVerification()
    {
        // Arrange
        var testable = new TestableDiagnosticController(Inventory, StatusManager);
 
        // Act
        testable.Dispose();
 
        // Assert
        testable.OnDisposingCalled.ShouldBeTrue("OnDisposing must be called");
        testable.OnDisposingCallCount.ShouldBe(1, "OnDisposing should be called exactly once despite redundant Dispose calls");
 
        // ObjectDisposedException の ObjectName 検証
        var ex = Should.Throw<ObjectDisposedException>(() => testable.GetHealthReport(DeviceHealthCheckLevel.Internal));
        ex.ObjectName.ShouldContain(nameof(DiagnosticController));
 
        // 冪等性の検証 (2回呼んでも内部処理が走らないことを確認)
        testable.Dispose();
        testable.OnDisposingCallCount.ShouldBe(1, "OnDisposing should NOT be called again if already disposed");
    }

    /// <summary>リフレクションを用いて private な disposed フラグを直接検証します。</summary>
    [Fact]
    public void DisposeSetsPrivateDisposedFlag()
    {
        // Act
        _controller.Dispose();

        // Assert
        var field = typeof(DiagnosticController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        ((bool)field.GetValue(_controller)!).ShouldBeTrue();
    }

    /// <summary>内部レポートのフォーマットを厳密に検証します。</summary>
    [Fact]
    public void GetHealthReportInternalFormattingIsStrict()
    {
        // Arrange
        _controller.IncrementSuccessfulDepletion();

        // Act
        var report = _controller.GetHealthReport(DeviceHealthCheckLevel.Internal);

        // Assert
        report.ShouldContain("--- Internal Health Check Report ---");
        report.ShouldContain("Inventory: OK");
        report.ShouldMatch(@"Total Denominations: \d+");
    }

    /// <summary>
    /// DiagnosticController の内部保護メンバーを検証するためのテスト用クラス。
    /// </summary>
    private class TestableDiagnosticController : DiagnosticController
    {
        public TestableDiagnosticController(Inventory inventory, HardwareStatusManager hardwareStatusManager)
            : base(inventory, hardwareStatusManager)
        {
        }

        public bool OnDisposingCalled { get; private set; }
        public int OnDisposingCallCount { get; private set; }

        protected override void OnDisposing(bool disposing)
        {
            OnDisposingCalled = true;
            OnDisposingCallCount++;
            base.OnDisposing(disposing);
        }
    }
}
