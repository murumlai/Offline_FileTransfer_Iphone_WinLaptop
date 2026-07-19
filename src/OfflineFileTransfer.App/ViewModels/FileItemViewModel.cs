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

    /// <summary>Numeric size used for sorting; unknown sizes sort as -1.</summary>
    public long SizeSortKey => Model.SizeBytes ?? -1;

    public string ModifiedDisplay => DisplayFormat.Date(Model.ModifiedUtc);

    /// <summary>Sortable modified timestamp; unknown dates sort first.</summary>
    public DateTimeOffset ModifiedSortKey => Model.ModifiedUtc ?? DateTimeOffset.MinValue;

    public string SourceDisplay => Model.Source.ToString();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
