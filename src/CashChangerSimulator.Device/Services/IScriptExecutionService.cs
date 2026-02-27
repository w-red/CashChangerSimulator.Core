namespace CashChangerSimulator.Device.Services;

/// <summary>
/// Defines the contract for executing automated scripts in the simulator.
/// </summary>
public interface IScriptExecutionService
{
    Task ExecuteScriptAsync(string json);
}
