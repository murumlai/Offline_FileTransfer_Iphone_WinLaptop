using System.Collections.ObjectModel;
using System.IO;
using OfflineFileTransfer.App.Mvvm;
using OfflineFileTransfer.App.Services;
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
    private const string DefaultHotspotSsid = "muru_file";
    private const string DefaultHotspotPassphrase = "12345678";

    private readonly IDeviceManager _deviceManager;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly Func<DeviceInfo, IReadOnlyList<IPhoneFileProvider>> _providerFactory;
    private readonly ITransferService _transferService;
    private readonly Func<string?> _pickFolder;
    private readonly Action<IReadOnlyList<IPhoneFileProvider>> _onProvidersReady;
    private readonly HotspotUploadServer _hotspotUploadServer;
    private readonly MobileHotspotService _mobileHotspotService;
    private readonly SynchronizationContext? _syncContext;

    private readonly List<RemoteFileItem> _currentFolderFiles = new();
    private readonly Stack<string> _pathStack = new();

    private DeviceInfo? _device;
    private CancellationTokenSource? _cts;
    private HotspotUploadSession? _hotspotSession;

    public MainViewModel(
        IDeviceManager deviceManager,
        IDiagnosticsService diagnosticsService,
        Func<DeviceInfo, IReadOnlyList<IPhoneFileProvider>> providerFactory,
        ITransferService transferService,
        Func<string?> pickFolder,
        Action<IReadOnlyList<IPhoneFileProvider>> onProvidersReady,
        HotspotUploadServer hotspotUploadServer,
        MobileHotspotService mobileHotspotService)
    {
        _deviceManager = deviceManager;
        _diagnosticsService = diagnosticsService;
        _providerFactory = providerFactory;
        _transferService = transferService;
        _pickFolder = pickFolder;
        _onProvidersReady = onProvidersReady;
        _hotspotUploadServer = hotspotUploadServer;
        _mobileHotspotService = mobileHotspotService;
        _syncContext = SynchronizationContext.Current;
        _hotspotUploadServer.FileReceived += OnHotspotFileReceived;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync, () => !IsBusy);
        OpenSourceCommand = new AsyncRelayCommand(OpenSelectedSourceAsync, () => SelectedSource is { IsAvailable: true } && !IsBusy);
        OpenFolderCommand = new AsyncRelayCommand(OpenSelectedFolderAsync, () => SelectedFolder is not null && !IsBusy);
        GoUpCommand = new AsyncRelayCommand(GoUpAsync, () => _pathStack.Count > 0 && !IsBusy);
        ApplyFilterCommand = new AsyncRelayCommand(ApplyFilterAsync, () => !IsBusy);
        ClearFilterCommand = new RelayCommand(ClearFilter, () => !IsBusy);
        SelectAllCommand = new RelayCommand(() => SetSelection(true));
        SelectNoneCommand = new RelayCommand(() => SetSelection(false));
        PickDestinationCommand = new RelayCommand(PickDestination);
        DownloadSelectedCommand = new AsyncRelayCommand(DownloadSelectedAsync, CanDownload);
        DownloadFilteredCommand = new AsyncRelayCommand(DownloadFilteredAsync, CanDownloadFiltered);
        StartHotspotUploadCommand = new AsyncRelayCommand(StartHotspotUploadAsync, CanStartHotspotUpload);
        StopHotspotUploadCommand = new AsyncRelayCommand(StopHotspotUploadAsync, () => IsHotspotUploadRunning);
        LoadHotspotConfigCommand = new AsyncRelayCommand(LoadHotspotConfigAsync);
        ConfigureHotspotNetworkCommand = new AsyncRelayCommand(ConfigureHotspotNetworkAsync);
        StartHotspotNetworkCommand = new AsyncRelayCommand(StartHotspotNetworkAsync);
        StopHotspotNetworkCommand = new AsyncRelayCommand(StopHotspotNetworkAsync);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<SourceViewModel> Sources { get; } = new();
    public ObservableCollection<RemoteFolderItem> Folders { get; } = new();
    public ObservableCollection<FileItemViewModel> Files { get; } = new();
    public ObservableCollection<DiagnosticCheck> Diagnostics { get; } = new();
    public ObservableCollection<string> HotspotUploadUrls { get; } = new();
    public ObservableCollection<HotspotUploadReceivedFile> HotspotReceivedFiles { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RunDiagnosticsCommand { get; }
    public AsyncRelayCommand OpenSourceCommand { get; }
    public AsyncRelayCommand OpenFolderCommand { get; }
    public AsyncRelayCommand GoUpCommand { get; }
    public AsyncRelayCommand ApplyFilterCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand PickDestinationCommand { get; }
    public AsyncRelayCommand DownloadSelectedCommand { get; }
    public AsyncRelayCommand DownloadFilteredCommand { get; }
    public AsyncRelayCommand StartHotspotUploadCommand { get; }
    public AsyncRelayCommand StopHotspotUploadCommand { get; }
    public AsyncRelayCommand LoadHotspotConfigCommand { get; }
    public AsyncRelayCommand ConfigureHotspotNetworkCommand { get; }
    public AsyncRelayCommand StartHotspotNetworkCommand { get; }
    public AsyncRelayCommand StopHotspotNetworkCommand { get; }
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

    private string _deviceStatus = "No iPhone detected over USB. USB browsing needs a cable; hotspot upload does not.";
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

    private string _hotspotUploadStatus = "Choose a destination folder, start the hotspot upload server, then open the URL on the iPhone.";
    public string HotspotUploadStatus
    {
        get => _hotspotUploadStatus;
        private set => SetProperty(ref _hotspotUploadStatus, value);
    }

    private string _hotspotSsid = DefaultHotspotSsid;
    public string HotspotSsid
    {
        get => _hotspotSsid;
        set => SetProperty(ref _hotspotSsid, value);
    }

    private string _hotspotPassphrase = DefaultHotspotPassphrase;
    public string HotspotPassphrase
    {
        get => _hotspotPassphrase;
        set => SetProperty(ref _hotspotPassphrase, value);
    }

    private string _hotspotNetworkStatus = "Load config to see current hotspot settings, or enter new ones and apply.";
    public string HotspotNetworkStatus
    {
        get => _hotspotNetworkStatus;
        private set => SetProperty(ref _hotspotNetworkStatus, value);
    }

    private string _hotspotPrimaryUrl = string.Empty;
    public string HotspotPrimaryUrl
    {
        get => _hotspotPrimaryUrl;
        private set => SetProperty(ref _hotspotPrimaryUrl, value);
    }

    private bool _isHotspotUploadRunning;
    public bool IsHotspotUploadRunning
    {
        get => _isHotspotUploadRunning;
        private set
        {
            if (SetProperty(ref _isHotspotUploadRunning, value))
            {
                RaiseCommandStates();
            }
        }
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
                DeviceStatus = "No iPhone detected over USB. Use a cable for USB browsing, or use hotspot upload without USB.";
                StatusMessage = "No USB device.";
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

    public async ValueTask DisposeAsync()
    {
        _hotspotUploadServer.FileReceived -= OnHotspotFileReceived;
        await _hotspotUploadServer.DisposeAsync().ConfigureAwait(false);
    }

    public async Task ConfigureAndStartDefaultHotspotAsync()
    {
        HotspotSsid = DefaultHotspotSsid;
        HotspotPassphrase = DefaultHotspotPassphrase;
        HotspotNetworkStatus = "Applying default hotspot configuration...";

        var configureError = await _mobileHotspotService.ConfigureAsync(HotspotSsid, HotspotPassphrase)
            .ConfigureAwait(true);
        if (configureError is not null)
        {
            HotspotNetworkStatus = $"Failed to configure hotspot: {configureError}";
            return;
        }

        HotspotNetworkStatus = "Starting hotspot...";
        var startError = await _mobileHotspotService.StartAsync().ConfigureAwait(true);
        HotspotNetworkStatus = startError is null
            ? $"Hotspot is ON. Connect your iPhone to {HotspotSsid}."
            : $"Failed to start hotspot: {startError}";
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

    /// <summary>
    /// Apply the filter. With no filter set, shows the current folder. When any filter is
    /// active (e.g. a size range), recursively walks every folder in the selected source and
    /// lists all matching files.
    /// </summary>
    private async Task ApplyFilterAsync()
    {
        var filter = BuildFilter();
        if (filter.IsEmpty)
        {
            ApplyFilter();
            return;
        }

        var source = SelectedSource is { IsAvailable: true }
            ? SelectedSource
            : Sources.FirstOrDefault(s => s.IsAvailable);
        if (source is null)
        {
            ApplyFilter();
            return;
        }

        await RunGuarded(async ct =>
        {
            StatusMessage = "Searching all folders\u2026";
            Folders.Clear();
            Files.Clear();
            _currentFolderFiles.Clear();
            CurrentPathDisplay = "(search results across all folders)";

            var found = 0;
            var scanned = 0;
            var pending = new Stack<string>();
            pending.Push(string.Empty);

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var current = pending.Pop();
                scanned++;

                await foreach (var item in source.Provider.EnumerateAsync(current, ct).ConfigureAwait(true))
                {
                    switch (item)
                    {
                        case RemoteFolderItem folder:
                            pending.Push(folder.RemotePath);
                            break;
                        case RemoteFileItem file when filter.Matches(file):
                            Files.Add(new FileItemViewModel(file));
                            found++;
                            break;
                    }
                }

                StatusMessage = $"Searching\u2026 {found} match(es) in {scanned} folder(s) scanned.";
            }

            StatusMessage = $"Search complete. {found} match(es) across {scanned} folder(s).";
            RaiseCommandStates();
        }).ConfigureAwait(true);
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

    private bool CanStartHotspotUpload() =>
        !IsHotspotUploadRunning;

    private async Task StartHotspotUploadAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationFolder))
        {
            var picked = _pickFolder();
            if (string.IsNullOrWhiteSpace(picked))
            {
                HotspotUploadStatus = "Choose a Windows folder to receive iPhone uploads.";
                return;
            }

            DestinationFolder = picked;
        }

        try
        {
            HotspotUploadStatus = "Starting hotspot upload server...";
            _hotspotSession = await _hotspotUploadServer.StartAsync(DestinationFolder!)
                .ConfigureAwait(true);

            HotspotUploadUrls.Clear();
            foreach (var url in _hotspotSession.UploadUrls)
            {
                HotspotUploadUrls.Add(url);
            }

            HotspotPrimaryUrl = _hotspotSession.PrimaryUrl;
            IsHotspotUploadRunning = true;
            HotspotUploadStatus = "Server running. Open the URL on the iPhone while both devices are on the same hotspot.";
        }
        catch (Exception ex)
        {
            _hotspotSession = null;
            HotspotPrimaryUrl = string.Empty;
            HotspotUploadUrls.Clear();
            IsHotspotUploadRunning = false;
            HotspotUploadStatus = $"Could not start hotspot upload: {ex.Message}";
        }
    }

    private async Task StopHotspotUploadAsync()
    {
        await _hotspotUploadServer.StopAsync().ConfigureAwait(true);
        _hotspotSession = null;
        HotspotPrimaryUrl = string.Empty;
        HotspotUploadUrls.Clear();
        IsHotspotUploadRunning = false;
        HotspotUploadStatus = "Hotspot upload server stopped.";
    }

    private Task LoadHotspotConfigAsync()
    {
        var config = _mobileHotspotService.GetConfiguration();
        if (config is null)
        {
            HotspotNetworkStatus = "Could not read hotspot configuration. No network adapter found.";
            return Task.CompletedTask;
        }
        HotspotSsid = config.Ssid;
        HotspotPassphrase = config.Passphrase;
        var state = _mobileHotspotService.GetState();
        HotspotNetworkStatus = $"Hotspot is currently {state?.ToString()?.ToLowerInvariant() ?? "unknown"}.";
        return Task.CompletedTask;
    }

    private async Task ConfigureHotspotNetworkAsync()
    {
        if (string.IsNullOrWhiteSpace(HotspotSsid))
        {
            HotspotNetworkStatus = "Enter a network name (SSID).";
            return;
        }
        if (HotspotPassphrase.Length < 8)
        {
            HotspotNetworkStatus = "Password must be at least 8 characters.";
            return;
        }
        HotspotNetworkStatus = "Applying configuration...";
        var error = await _mobileHotspotService.ConfigureAsync(HotspotSsid, HotspotPassphrase).ConfigureAwait(true);
        HotspotNetworkStatus = error is null ? "Configuration applied successfully." : $"Failed: {error}";
    }

    private async Task StartHotspotNetworkAsync()
    {
        HotspotNetworkStatus = "Starting hotspot...";
        var error = await _mobileHotspotService.StartAsync().ConfigureAwait(true);
        HotspotNetworkStatus = error is null
            ? "Hotspot is ON. Connect your iPhone to this network, then start the upload server."
            : $"Failed to start hotspot: {error}";
    }

    private async Task StopHotspotNetworkAsync()
    {
        HotspotNetworkStatus = "Stopping hotspot...";
        var error = await _mobileHotspotService.StopAsync().ConfigureAwait(true);
        HotspotNetworkStatus = error is null ? "Hotspot stopped." : $"Failed to stop hotspot: {error}";
    }

    private void OnHotspotFileReceived(object? sender, HotspotUploadReceivedEventArgs e)
    {
        void ApplyReceivedFile()
        {
            HotspotReceivedFiles.Insert(0, e.File);
            HotspotUploadStatus = $"Received {e.File.FileName}.";
        }

        if (_syncContext is null)
        {
            ApplyReceivedFile();
            return;
        }

        _syncContext.Post(_ => ApplyReceivedFile(), null);
    }

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
