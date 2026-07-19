using Microsoft.Win32;

namespace OfflineFileTransfer.App.Services;

/// <summary>
/// Wraps the WPF folder picker so it can be injected and swapped in tests.
/// </summary>
public static class FolderPicker
{
    public static string? Pick()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a destination folder",
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
