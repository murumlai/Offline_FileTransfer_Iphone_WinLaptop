using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OfflineFileTransfer.Core.Diagnostics;

namespace OfflineFileTransfer.App;

/// <summary>
/// Maps a <see cref="DiagnosticStatus"/> to an indicator brush for the diagnostics list.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Pass = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush Fail = new(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush Neutral = new(Color.FromRgb(0x9C, 0xA3, 0xAF));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiagnosticStatus status
            ? status switch
            {
                DiagnosticStatus.Pass => Pass,
                DiagnosticStatus.Warning => Warning,
                DiagnosticStatus.Fail => Fail,
                _ => Neutral,
            }
            : Neutral;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
