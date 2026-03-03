using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.UI.Cli.Services;
using Microsoft.PointOfService;
using Shouldly;
using System;
using Xunit;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCashServiceTests : CliTestBase
{
    [Fact]
    public void EndDepositShouldUpdateResultCodeAndPrintErrorMessageWhenNotInDepositMode()
    {
        var realChanger = new SimulatorCashChanger(null, null, null, null, null, null, null, null)
        {
            SkipStateVerification = true
        };
        realChanger.Open();
        realChanger.DeviceEnabled = true;
        // 注意: BeginDeposit() を呼んでいないため、EndDeposit() は Invalid State (Illegal) となるはず。

        var cashService = new CliCashService(realChanger, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        // CliCashService 内の EndDeposit() は、Changer の EndDeposit を呼び、例外をキャッチして UI フォーマットする。
        // ※内部の呼び出しは、実装側で固定（action: Change）で呼ばれることもあるため、テストとしてはその結果生じるエラーステータスを確認。
        cashService.EndDeposit();

        // Assert
        // 1. SimulatorCashChanger 内で ResultCode が期待したエラー値に更新されていること (UPOS標準への準拠)
        realChanger.ResultCode.ShouldBe((int)ErrorCode.Illegal);

        // 2. CLI UI サービスが PosControlException を補足し、エラー番号付きで画面出力していること
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Illegal}");
    }

    [Fact]
    public void DispenseShouldHandlePosControlExceptionAndCheckExtendedErrorCodeWhenInventoryIsInsufficient()
    {
        // Arrange
        var realChanger = new SimulatorCashChanger(null, null, null, null, null, null, null, null)
        {
            SkipStateVerification = true
        };
        realChanger.Open();
        realChanger.DeviceEnabled = true;

        var cashService = new CliCashService(realChanger, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        // 1000万など、初期在庫（通常は数万円程度）を明らかに超える金額を出金しようと試みる
        cashService.Dispense(10000000); 

        // Assert
        // ResultCode は Extended なエラー（例: Extended, Deposit/Dispense系エラー）になるはず
        realChanger.ResultCode.ShouldBe((int)ErrorCode.Extended);

        // UI に正しくエラーコードが反映されているか
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Extended}");
    }
}
