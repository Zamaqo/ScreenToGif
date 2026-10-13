namespace ScreenToGif.Domain.Enums;

/// <summary>
/// Where a save-as export writes the finished file.
/// </summary>
public enum OutputDestinations
{
    /// <summary>
    /// Save to a folder on this computer.
    /// </summary>
    Computer,

    /// <summary>
    /// Save to an enabled cloud preset. The local file is only a temporary copy.
    /// </summary>
    Cloud
}
