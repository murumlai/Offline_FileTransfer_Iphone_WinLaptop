using System.Globalization;
using System.Text.RegularExpressions;

namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// Parses the localized size strings the Windows shell returns for portable-device items,
/// e.g. "1.997 KB", "3.24 MB", "12 bytes".
/// </summary>
internal static partial class ShellSize
{
    [GeneratedRegex(@"([\d.,]+)\s*(bytes|KB|MB|GB|TB)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizeRegex();

    public static long? Parse(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var match = SizeRegex().Match(detail.Trim());
        if (!match.Success)
        {
            return null;
        }

        var numberText = match.Groups[1].Value.Replace(",", string.Empty);
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.CurrentCulture, out number))
        {
            return null;
        }

        var unit = match.Groups[2].Success ? match.Groups[2].Value.ToUpperInvariant() : "BYTES";
        var multiplier = unit switch
        {
            "KB" => 1024d,
            "MB" => 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            "TB" => 1024d * 1024d * 1024d * 1024d,
            _ => 1d,
        };

        return (long)Math.Round(number * multiplier);
    }
}
