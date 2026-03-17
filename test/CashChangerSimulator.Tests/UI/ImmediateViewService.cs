using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.Tests.Mocks;

/// <summary>
/// A mock implementation of IViewService that does nothing but satisfies dependencies.
/// Unit tests shouldn't care about UI window opening unless explicitly testing it.
/// </summary>
public class ImmediateViewService : IViewService
{
    public void ShowSettingsWindow() { }

    public void ShowDepositWindow(DepositViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations) { }

    public void ShowDispenseWindow(DispenseViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations) { }

    public void ShowAdvancedSimulationWindow(AdvancedSimulationViewModel dataContext) { }

    public Task ShowDialogAsync(object content, string identifier = "RootDialog") => Task.CompletedTask;
}
