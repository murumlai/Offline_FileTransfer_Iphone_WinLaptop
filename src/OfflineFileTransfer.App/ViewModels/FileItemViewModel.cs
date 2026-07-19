using OfflineFileTransfer.App.Mvvm;
using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.App.ViewModels;

/// <summary>
/// Selectable view over a <see cref="RemoteFileItem"/> for the file list.
/// </summary>
public sealed class FileItemViewModel : ObservableObject
{
    private bool _isSelected;

    public FileItemViewModel(RemoteFileItem model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public RemoteFileItem Model { get; }

    public string Name => Model.Name;

    public string Category => Model.Category.ToString();

    public string SizeDisplay => DisplayFormat.Bytes(Model.SizeBytes);

    public string ModifiedDisplay => DisplayFormat.Date(Model.ModifiedUtc);

    public string SourceDisplay => Model.Source.ToString();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
