using System.Windows;
using OfflineFileTransfer.App.Services;
using OfflineFileTransfer.App.ViewModels;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;
using OfflineFileTransfer.Core.Transfers;
using OfflineFileTransfer.IosNative;
using OfflineFileTransfer.WindowsDevices;

namespace OfflineFileTransfer.App;

/// <summary>
/// Interaction logic for MainWindow.xaml. Acts as the composition root.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var registry = new ProviderRegistry();
        var deviceManager = new WpdDeviceManager();
        var diagnostics = new WpdDiagnosticsService(deviceManager);
        var transferService = new FileTransferService(registry.Resolve);
        var hotspotUploadServer = new HotspotUploadServer();

        var viewModel = new MainViewModel(
            deviceManager,
            diagnostics,
            BuildProviders,
            transferService,
            FolderPicker.Pick,
            providers => registry.Set(providers),
            hotspotUploadServer);

        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.RefreshAsync();
        Closed += async (_, _) => await viewModel.DisposeAsync();
    }

    private static IReadOnlyList<IPhoneFileProvider> BuildProviders(DeviceInfo device)
    {
        var providers = new List<IPhoneFileProvider>
        {
            new WpdMediaProvider(device.Name),
        };

        if (IosFileSharingProvider.BridgeAvailable)
        {
            providers.Add(new IosFileSharingProvider());
        }

        return providers;
    }
}