namespace OfflineFileTransfer.App.Mvvm;

/// <summary>
/// Human-readable formatting helpers for the UI.
/// </summary>
public static class DisplayFormat
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Bytes(long? bytes)
    {
        if (bytes is not { } value)
        {
            return "—";
        }

        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value} {Units[unit]}" : $"{size:0.#} {Units[unit]}";
    }

    public static string Date(DateTimeOffset? value) =>
        value?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—";
}
