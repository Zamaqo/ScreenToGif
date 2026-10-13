using System;

namespace ScreenToGif.Cloud;

/// <summary>
/// A file or folder returned while browsing a cloud preset.
/// </summary>
public class CloudEntry
{
    public string Name { get; set; }

    /// <summary>
    /// Object key or disk path used to open or enter this entry.
    /// </summary>
    public string Key { get; set; }

    public bool IsFolder { get; set; }

    public long Size { get; set; }

    public string SizeLabel { get; set; }

    public string ModifiedLabel { get; set; }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";

        if (bytes < 1024 * 1024)
            return (bytes / 1024d).ToString("0.#") + " KB";

        return (bytes / 1024d / 1024d).ToString("0.#") + " MB";
    }

    public static string FormatModified(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        if (DateTimeOffset.TryParse(raw, out var moment))
            return moment.ToLocalTime().ToString("g");

        return raw;
    }
}
