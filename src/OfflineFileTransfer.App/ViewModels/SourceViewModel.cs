using OfflineFileTransfer.App.Mvvm;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.App.ViewModels;

/// <summary>
/// A browsable source (Camera Roll, App File Sharing, Downloads) with its availability state.
/// </summary>
public sealed class SourceViewModel : ObservableObject
{
    private bool _isAvailable;
    private string? _reason;

    public SourceViewModel(IPhoneFileProvider provider)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IPhoneFileProvider Provider { get; }

    public FileSourceKind Kind => Provider.SourceKind;

    public string DisplayName => Provider.DisplayName;

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string? Reason
    {
        get => _reason;
        set
        {
            if (SetProperty(ref _reason, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsAvailable ? "Available" : Reason ?? "Unavailable";
}
