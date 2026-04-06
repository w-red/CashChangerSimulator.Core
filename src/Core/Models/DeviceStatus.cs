namespace CashChangerSimulator.Core.Models;

/// <summary>
/// Status codes for the device status update events.
/// (Common UPOS status codes or internal mapping).
/// </summary>
public enum DeviceStatus
{
    /// <summary>None or unknown.</summary>
    None = 0,

    /// <summary>Power is on.</summary>
    PowerOn = 2001,

    /// <summary>Power is off or disconnected.</summary>
    PowerOff = 2002,

    /// <summary>Status is normal.</summary>
    JournalOk = 12,

    /// <summary>Status is jammed or error.</summary>
    JournalEmpty = 11,
}
