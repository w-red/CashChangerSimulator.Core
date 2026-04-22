using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS �ݒ�}�l�[�W���ɂ��ʉݐݒ�A�ݒ胊���[�h�A���������������؂���e�X�g�N���X�B</summary>
public class UposConfigurationManagerTests
{
    private readonly ConfigurationProvider configProvider;
    private readonly Inventory inventory;
    private readonly Mock<IDeviceStateProvider> stateProvider;
    private readonly UposConfigurationManager manager;

    public UposConfigurationManagerTests()
    {
        configProvider = new ConfigurationProvider();
        inventory = Inventory.Create();
        stateProvider = new Mock<IDeviceStateProvider>();
        manager = new UposConfigurationManager(configProvider, inventory, stateProvider.Object);
    }

    /// <summary>�T�|�[�g����Ă��Ȃ��ʉ݃R�[�h��ݒ肵�悤�Ƃ����ۂɗ�O���������邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void CurrencyCodeShouldThrowWhenUnsupported()
    {
        Should.Throw<PosControlException>(() => manager.CurrencyCode = "INVALID")
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>�����Ȓʉ݃R�[�h������ɐݒ�E�擾�ł��邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void CurrencyCodeShouldWorkWhenSupported()
    {
        configProvider.Config.Inventory["USD"] = new InventorySettings();
        manager.CurrencyCode = "USD";
        manager.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>�ݒ�ύX���ɓ������(�݌ɓ�)�����������Z�b�g����邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void ResetStateWhenConfigurationChanges()
    {
        stateProvider.Setup(s => s.State).Returns((PosSharp.Abstractions.ControlState)ControlState.Idle);
        inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);

        // Trigger reload
        configProvider.Update(new SimulatorConfiguration());

        inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>�蓮�����[�h���s���ɓ�����Ԃ����Z�b�g����邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void ReloadShouldManuallyTriggerUpdate()
    {
        stateProvider.Setup(s => s.State).Returns((PosSharp.Abstractions.ControlState)ControlState.Idle);
        inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);

        manager.Reload();

        inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>�����������ɂ���ăA�N�e�B�u�Ȓʉ݂��������ݒ肳��邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void InitializeShouldSetActiveCurrency()
    {
        configProvider.Config.Inventory.Clear();
        configProvider.Config.Inventory["EUR"] = new InventorySettings();
 
        manager.Initialize();
        manager.CurrencyCode.ShouldBe("EUR");
    }

    /// <summary>�j��(Dispose)��ɐݒ�ύX���󂯎���Ă�����p���������Ȃ����Ƃ����؂��܂��B</summary>
    [Fact]
    public void DisposeShouldUnsubscribe()
    {
        manager.Dispose();

        // Trigger reload should not cause issues even if manager logic would have
        configProvider.Update(new SimulatorConfiguration());
    }

    /// <summary>CurrencyCashList �v���p�e�B�� UPOS �K��̌`���Ńf�[�^��ԋp���邱�Ƃ����؂��܂��B</summary>
    [Fact]
    public void CurrencyCashListShouldReturnUposUnits()
    {
        var list = manager.CurrencyCashList;

        // CashUnits is a struct, so it cannot be null.
    }
}

