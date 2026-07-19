using System.Collections.ObjectModel;
using System.IO;
using OfflineFileTransfer.App.Mvvm;
using OfflineFileTransfer.Core.Diagnostics;
using OfflineFileTransfer.Core.Filtering;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;
using OfflineFileTransfer.Core.Transfers;

namespace OfflineFileTransfer.App.ViewModels;

/// <summary>
/// Coordinates device detection, source availability, browsing, filtering, and transfers.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IDeviceManager _deviceManager;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly Func<DeviceInfo, IReadOnlyList<IPhoneFileProvider>> _providerFactory;
    private readonly ITransferService _transferService;
    private readonly Func<string?> _pickFolder;
    private readonly Action<IReadOnlyList<IPhoneFileProvider>> _onProvidersReady;

    private readonly List<RemoteFileItem> _currentFolderFiles = new();
    private readonly Stack<string> _pathStack = new();

    private DeviceInfo? _device;
    private CancellationTokenSource? _cts;

    public MainViewModel(
        IDeviceManager deviceManager,
        IDiagnosticsService diagnosticsService,
        Func<DeviceInfo, IReadOnlyList<IPhoneFileProvider>> providerFactory,
        ITransferService transferService,
        Func<string?> pickFolder,
        Action<IReadOnlyList<IPhoneFileProvider>> onProvidersReady)
    {
        _deviceManager = deviceManager;
        _diagnosticsService = diagnosticsService;
        _providerFactory = providerFactory;
        _transferService = transferService;
        _pickFolder = pickFolder;
        _onProvidersReady = onProvidersReady;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync, () => !IsBusy);
        OpenSourceCommand = new AsyncRelayCommand(OpenSelectedSourceAsync, () => SelectedSource is { IsAvailable: true } && !IsBusy);
        OpenFolderCommand = new AsyncRelayCommand(OpenSelectedFolderAsync, () => SelectedFolder is not null && !IsBusy);
        GoUpCommand = new AsyncRelayCommand(GoUpAsync, () => _pathStack.Count > 0 && !IsBusy);
        ApplyFilterCommand = new RelayCommand(ApplyFilter, () => !IsBusy);
        ClearFilterCommand = new RelayCommand(ClearFilter, () => !IsBusy);
        SelectAllCommand = new RelayCommand(() => SetSelection(true));
        SelectNoneCommand = new RelayCommand(() => SetSelection(false));
        PickDestinationCommand = new RelayCommand(PickDestination);
        DownloadSelectedCommand = new AsyncRelayCommand(DownloadSelectedAsync, CanDownload);
        DownloadFilteredCommand = new AsyncRelayCommand(DownloadFilteredAsync, CanDownloadFiltered);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<SourceViewModel> Sources { get; } = new();
    public ObservableCollection<RemoteFolderItem> Folders { get; } = new();
    public ObservableCollection<FileItemViewModel> Files { get; } = new();
    public ObservableCollection<DiagnosticCheck> Diagnostics { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RunDiagnosticsCommand { get; }
    public AsyncRelayCommand OpenSourceCommand { get; }
    public AsyncRelayCommand OpenFolderCommand { get; }
    public AsyncRelayCommand GoUpCommand { get; }
    public RelayCommand ApplyFilterCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand PickDestinationCommand { get; }
    public AsyncRelayCommand DownloadSelectedCommand { get; }
    public AsyncRelayCommand DownloadFilteredCommand { get; }
    public RelayCommand CancelCommand { get; }

    #region Bindable state

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private string _deviceStatus = "No device detected. Connect an iPhone over USB.";
    public string DeviceStatus
    {
        get => _deviceStatus;
        private set => SetProperty(ref _deviceStatus, value);
    }

    private string _statusMessage = "Ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private string _currentPathDisplay = "";
    public string CurrentPathDisplay
    {
        get => _currentPathDisplay;
        private set => SetProperty(ref _currentPathDisplay, value);
    }

    private SourceViewModel? _selectedSource;
    public SourceViewModel? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private RemoteFolderItem? _selectedFolder;
    public RemoteFolderItem? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private string? _destinationFolder;
    public string? DestinationFolder
    {
        get => _destinationFolder;
        set
        {
            if (SetProperty(ref _destinationFolder, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private bool _preserveStructure;
    public bool PreserveStructure
    {
        get => _preserveStructure;
        set => SetProperty(ref _preserveStructure, value);
    }

    private DuplicatePolicy _duplicatePolicy = DuplicatePolicy.AutoRename;
    public DuplicatePolicy DuplicatePolicy
    {
        get => _duplicatePolicy;
        set => SetProperty(ref _duplicatePolicy, value);
    }

    public IReadOnlyList<DuplicatePolicy> DuplicatePolicies { get; } =
        Enum.GetValues<DuplicatePolicy>();

    // Filters
    public bool FilterImages { get; set; }
    public bool FilterVideos { get; set; }
    public bool FilterAudio { get; set; }
    public bool FilterDocuments { get; set; }
    public bool FilterArchives { get; set; }
    public string? CustomExtensions { get; set; }
    public string? SearchText { get; set; }
    public double? MinSize { get; set; }
    public double? MaxSize { get; set; }

    public IReadOnlyList<SizeUnit> SizeUnits { get; } = Enum.GetValues<SizeUnit>();
    public SizeUnit SelectedSizeUnit { get; set; } = SizeUnit.MB;

    private double _transferProgress;
    public double TransferProgress
    {
        get => _transferProgress;
        private set => SetProperty(ref _transferProgress, value);
    }

    #endregion

    public async Task RefreshAsync()
    {
        await RunGuarded(async ct =>
        {
            StatusMessage = "Detecting device…";
            var devices = await _deviceManager.GetConnectedDevicesAsync(ct).ConfigureAwait(true);
            _device = devices.FirstOrDefault();

            Sources.Clear();
            Folders.Clear();
            Files.Clear();
            _currentFolderFiles.Clear();
            _pathStack.Clear();
            CurrentPathDisplay = string.Empty;

            if (_device is null)
            {
                DeviceStatus = "No iPhone detected. Connect it via USB, unlock, and tap Trust.";
                StatusMessage = "No device.";
                return;
            }

            DeviceStatus = BuildDeviceStatus(_device);

            var providers = _providerFactory(_device);
            _onProvidersReady(providers);
            foreach (var provider in providers)
            {
                var vm = new SourceViewModel(provider);
                var availability = await provider.CheckAvailabilityAsync(_device, ct).ConfigureAwait(true);
                vm.IsAvailable = availability.IsAvailable;
                vm.Reason = availability.Reason;
                Sources.Add(vm);
            }

            SelectedSource = Sources.FirstOrDefault(s => s.IsAvailable) ?? Sources.FirstOrDefault();
            StatusMessage = $"Found {_device.Name}.";
        }).ConfigureAwait(true);
    }

    public async Task RunDiagnosticsAsync()
    {
        await RunGuarded(async ct =>
        {
            StatusMessage = "Running diagnostics…";
            var report = await _diagnosticsService.RunAsync(ct).ConfigureAwait(true);
            Diagnostics.Clear();
            foreach (var check in report.Checks)
            {
                Diagnostics.Add(check);
            }

            StatusMessage = report.IsCameraRollSupported
                ? "Diagnostics complete. Camera Roll is supported."
                : "Diagnostics complete. Camera Roll not confirmed.";
        }).ConfigureAwait(true);
    }

    private async Task OpenSelectedSourceAsync()
    {
        if (SelectedSource is null)
        {
            return;
        }

        _pathStack.Clear();
        await LoadFolderAsync(SelectedSource.Provider, string.Empty).ConfigureAwait(true);
    }

    private async Task OpenSelectedFolderAsync()
    {
        if (SelectedSource is null || SelectedFolder is null)
        {
            return;
        }

        _pathStack.Push(CurrentPathDisplay);
        await LoadFolderAsync(SelectedSource.Provider, SelectedFolder.RemotePath).ConfigureAwait(true);
    }

    private async Task GoUpAsync()
    {
        if (SelectedSource is null || _pathStack.Count == 0)
        {
            return;
        }

        var parent = _pathStack.Pop();
        await LoadFolderAsync(SelectedSource.Provider, parent).ConfigureAwait(true);
    }

    private async Task LoadFolderAsync(IPhoneFileProvider provider, string path)
    {
        await RunGuarded(async ct =>
        {
            StatusMessage = "Loading…";
            Folders.Clear();
            Files.Clear();
            _currentFolderFiles.Clear();
            CurrentPathDisplay = path;

            await foreach (var item in provider.EnumerateAsync(path, ct).ConfigureAwait(true))
            {
                switch (item)
                {
                    case RemoteFolderItem folder:
                        Folders.Add(folder);
                        break;
                    case RemoteFileItem file:
                        _currentFolderFiles.Add(file);
                        break;
                }
            }

            ApplyFilter();
            StatusMessage = $"{Folders.Count} folders, {_currentFolderFiles.Count} files.";
        }).ConfigureAwait(true);
    }

    public void ApplyFilter()
    {
        var filter = BuildFilter();
        Files.Clear();
        foreach (var file in filter.Apply(_currentFolderFiles))
        {
            Files.Add(new FileItemViewModel(file));
        }

        RaiseCommandStates();
    }

    private void ClearFilter()
    {
        FilterImages = FilterVideos = FilterAudio = FilterDocuments = FilterArchives = false;
        CustomExtensions = null;
        SearchText = null;
        MinSize = null;
        MaxSize = null;
        OnPropertyChanged(nameof(FilterImages));
        OnPropertyChanged(nameof(FilterVideos));
        OnPropertyChanged(nameof(FilterAudio));
        OnPropertyChanged(nameof(FilterDocuments));
        OnPropertyChanged(nameof(FilterArchives));
        OnPropertyChanged(nameof(CustomExtensions));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(MinSize));
        OnPropertyChanged(nameof(MaxSize));
        ApplyFilter();
    }

    private FileFilter BuildFilter()
    {
        var filter = new FileFilter();
        if (FilterImages) filter.Categories.Add(FileTypeCategory.Image);
        if (FilterVideos) filter.Categories.Add(FileTypeCategory.Video);
        if (FilterAudio) filter.Categories.Add(FileTypeCategory.Audio);
        if (FilterDocuments) filter.Categories.Add(FileTypeCategory.Document);
        if (FilterArchives) filter.Categories.Add(FileTypeCategory.Archive);

        if (!string.IsNullOrWhiteSpace(CustomExtensions))
        {
            foreach (var ext in CustomExtensions.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                filter.AddExtension(ext);
            }
        }

        filter.SearchText = SearchText;
        if (MinSize is { } min) filter.MinSizeBytes = SelectedSizeUnit.ToBytes(min);
        if (MaxSize is { } max) filter.MaxSizeBytes = SelectedSizeUnit.ToBytes(max);
        return filter;
    }

    private void SetSelection(bool selected)
    {
        foreach (var file in Files)
        {
            file.IsSelected = selected;
        }

        RaiseCommandStates();
    }

    private void PickDestination()
    {
        var picked = _pickFolder();
        if (!string.IsNullOrWhiteSpace(picked))
        {
            DestinationFolder = picked;
        }
    }

    private bool CanDownload() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(DestinationFolder) &&
        Files.Any(f => f.IsSelected);

    private bool CanDownloadFiltered() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(DestinationFolder) &&
        Files.Count > 0;

    private Task DownloadSelectedAsync() =>
        DownloadAsync(Files.Where(f => f.IsSelected).Select(f => f.Model).ToList());

    private Task DownloadFilteredAsync() =>
        DownloadAsync(Files.Select(f => f.Model).ToList());

    private async Task DownloadAsync(IReadOnlyList<RemoteFileItem> items)
    {
        if (items.Count == 0 || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return;
        }

        await RunGuarded(async ct =>
        {
            var request = new TransferRequest(items, DestinationFolder!, PreserveStructure, DuplicatePolicy);
            var progress = new Progress<TransferProgress>(p =>
            {
                TransferProgress = p.FileFraction * 100;
                StatusMessage = $"Copying {p.CurrentFileName} ({p.FilesCompleted}/{p.FilesTotal})…";
            });

            var result = await _transferService.TransferAsync(request, progress, ct).ConfigureAwait(true);
            TransferProgress = 0;

            StatusMessage = result.WasCanceled
                ? $"Canceled. {result.CopiedCount} copied, {result.FailedCount} failed."
                : $"Done. {result.CopiedCount} copied, {result.SkippedCount} skipped, {result.FailedCount} failed.";
        }).ConfigureAwait(true);
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "Canceling…";
    }

    private async Task RunGuarded(Func<CancellationToken, Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            await action(_cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private static string BuildDeviceStatus(DeviceInfo device)
    {
        var trust = device.TrustState switch
        {
            DeviceTrustState.Trusted => "trusted",
            DeviceTrustState.Locked => "locked — unlock the phone",
            DeviceTrustState.Untrusted => "not trusted — tap Trust/Allow",
            _ => "state unknown",
        };
        var apple = device.AppleSupportInstalled ? "Apple support installed" : "Apple support missing";
        return $"{device.Name} — {trust}. {apple}.";
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        RunDiagnosticsCommand.RaiseCanExecuteChanged();
        OpenSourceCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        GoUpCommand.RaiseCanExecuteChanged();
        ApplyFilterCommand.RaiseCanExecuteChanged();
        ClearFilterCommand.RaiseCanExecuteChanged();
        DownloadSelectedCommand.RaiseCanExecuteChanged();
        DownloadFilteredCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
