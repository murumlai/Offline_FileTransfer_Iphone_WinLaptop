# Implementation Plan: Offline iPhone USB Transfer

Build a Windows desktop tool for USB-only iPhone media transfer with no iPhone-side app install. The recommended approach is feasibility-first: prove exactly which iPhone storage surfaces are reachable from an iPhone 16 on the target Windows laptop, especially the Files app `Downloads` folder, then implement the product around the reachable sources.

Camera-roll photos and videos are supported by Apple's Windows USB flow. App file-sharing folders may be reachable through Apple native services. Arbitrary Files app folders, including `Downloads`, are not guaranteed and may be impossible without an iOS app, jailbreak, backup parsing, or cloud sync.

## Steps

1. Phase 1 - Bootstrap the greenfield project.
   - Create a .NET 8 Windows desktop solution under `c:\Users\lloganat\source\repos\offline_fileTransfer`.
   - Recommended projects:
     - `src\OfflineFileTransfer.App` - WPF desktop UI.
     - `src\OfflineFileTransfer.Core` - device, file, filter, and transfer abstractions.
     - `src\OfflineFileTransfer.WindowsDevices` - Windows Portable Devices/PTP provider for camera-roll media.
     - `src\OfflineFileTransfer.IosNative` - optional C/C++ bridge for libimobiledevice-style native iOS services.
     - `tests\OfflineFileTransfer.Core.Tests` - unit tests for filters, path handling, transfer planning, and copy rules.
   - Add `docs\ios-usb-access.md` documenting the iOS access boundaries and prerequisites.

2. Phase 2 - Build a diagnostic feasibility spike. Depends on step 1.
   - Add a small diagnostic runner or hidden diagnostics screen that reports:
     - Whether an iPhone is connected over USB.
     - Whether the phone is locked, untrusted, or trusted.
     - Whether Apple Devices / Apple Mobile Device support is installed on Windows.
     - Whether WPD/PTP camera-roll media is enumerable.
     - Whether AFC/media-domain access is available.
     - Whether app file-sharing containers are enumerable.
     - Whether any path corresponding to `Downloads` is visible and readable.
   - Test likely reachable surfaces in order:
     - WPD/PTP `Internal Storage/DCIM` photos and videos.
     - AFC media domain, if available.
     - HouseArrest/app document containers for apps that already expose file sharing.
     - Any visible `Downloads` directory returned by those services.
   - Outcome gate: if `Downloads` is not visible, keep it out of the core promise and show it as unsupported in the UI with a clear reason.

3. Phase 3 - Define the core file model and provider abstraction. Depends on step 2 diagnostics shape.
   - Define `DeviceInfo`, `RemoteFileItem`, `RemoteFolderItem`, `FileSourceKind`, `FileFilter`, `TransferRequest`, `TransferResult`, and provider interfaces such as `IPhoneFileProvider` and `ITransferService`.
   - Normalize all providers into one browse tree so the UI can treat camera roll, app file-sharing folders, and any reachable Downloads folder consistently.
   - Store metadata needed for filters: name, extension, MIME/type category when known, byte size, modified date when available, source provider, and remote path/id.

4. Phase 4 - Implement supported USB providers. Depends on steps 2 and 3.
   - Implement `WpdMediaProvider` using Windows Portable Devices COM APIs to enumerate and download camera-roll media exposed by iPhone over USB.
   - If diagnostics prove value, implement `IosFileSharingProvider` through a native bridge around libimobiledevice-compatible services for app document containers and any reachable AFC media folders.
   - Keep provider failures isolated so lack of AFC/app-file-sharing access does not break camera-roll import.

5. Phase 5 - Implement filtering and browsing. Depends on step 3; parallel with parts of step 4 after provider contracts are stable.
   - Add file-type filters for images, videos, audio, documents, archives, and custom extensions.
   - Add size filters with minimum and maximum size in KB/MB/GB.
   - Support search by filename and source selection, including a dedicated `Downloads` source only when diagnostics prove it is reachable.
   - Make enumeration cancellable and incremental so large photo libraries do not freeze the UI.

6. Phase 6 - Implement downloads/transfers. Depends on steps 3 and 4.
   - Let the user choose a Windows destination folder.
   - Support downloading selected files, all visible filtered files, or an entire reachable folder.
   - Preserve source folder structure when requested.
   - Handle duplicate names with a predictable policy: skip, overwrite, or auto-rename.
   - Include progress, cancellation, retry, disconnect handling, and a transfer summary.

7. Phase 7 - Build the WPF UI. Depends on steps 3, 5, and a minimal provider implementation.
   - Main layout:
     - Device status/header with connection, trust, and prerequisite state.
     - Left source tree for Camera Roll, App File Sharing, and Downloads when available.
     - Filter panel for type, extension, size range, and search.
     - File list with name, type, size, modified date, source, and selection checkboxes.
     - Transfer footer with destination picker, progress, and download actions.
   - Show actionable states for locked phone, trust prompt needed, Apple Devices missing, unsupported Downloads, and transfer errors.

8. Phase 8 - Package and document. Depends on working app behavior.
   - Add Windows packaging/publishing for a self-contained .NET app.
   - Document prerequisites: Windows laptop, USB cable, unlocked iPhone, Trust/Allow prompt, Apple Devices app or Apple Mobile Device support on Windows.
   - Document limitations: no full iPhone filesystem browsing, no bypass of iOS sandboxing, no iCloud-only original downloads unless present locally on the phone, no iPhone app install.

## Relevant Files

- `c:\Users\lloganat\source\repos\offline_fileTransfer\OfflineFileTransfer.sln` - solution root.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\src\OfflineFileTransfer.App\` - WPF UI and app startup.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\src\OfflineFileTransfer.Core\` - shared models, filters, provider contracts, and transfer orchestration.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\src\OfflineFileTransfer.WindowsDevices\` - WPD/PTP camera-roll implementation.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\src\OfflineFileTransfer.IosNative\` - optional native iOS bridge for AFC/app-file-sharing services.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\tests\OfflineFileTransfer.Core.Tests\` - automated tests.
- `c:\Users\lloganat\source\repos\offline_fileTransfer\docs\ios-usb-access.md` - supported access matrix and limitations.

## Verification

1. Run `dotnet build` for the full solution.
2. Run `dotnet test` for core filtering, size filtering, path normalization, duplicate handling, and transfer planning.
3. On the Windows laptop, install Apple Devices if needed, connect iPhone 16 by USB, unlock it, and tap Trust/Allow.
4. Run the diagnostics and record:
   - iPhone detected.
   - trust state detected.
   - camera-roll enumeration succeeds.
   - app file-sharing enumeration succeeds or fails gracefully.
   - `Downloads` is visible/readable or explicitly reported unsupported.
5. Manual transfer tests:
   - Download one photo and one video from camera roll.
   - Download a filtered batch by type.
   - Download a filtered batch by min/max size.
   - If reachable, download media from `Downloads`.
   - If app file-sharing folders are reachable, download from one existing app container.
6. Resilience tests:
   - Phone locked.
   - Phone not trusted.
   - Cable unplugged mid-transfer.
   - Destination file already exists.
   - iCloud-optimized photo/video original not local on phone.

## Decisions

- No iPhone app install remains a hard requirement.
- USB cable is the primary connection path; Wi-Fi sync, iCloud, AirDrop, and cloud APIs are excluded.
- Full iPhone filesystem browsing is excluded because iOS does not expose arbitrary user files over USB.
- `Downloads` is treated as a priority target but not promised until the feasibility spike proves it is visible through permitted USB services.
- Camera-roll photos/videos are the baseline supported MVP.
- App file-sharing folders are an optional extension for apps already installed on the iPhone that expose document sharing.
- Installing a Windows desktop app and Windows-side Apple device support is acceptable because the no-install requirement applies to the iPhone.

## Further Considerations

1. If `Downloads` is not reachable over USB, the practical alternatives are: ask the user to move files into an app that exposes File Sharing, use iCloud/Windows iCloud, or build/install a companion iOS app. The current plan should keep those alternatives documented but out of the no-iPhone-app MVP.
2. If WPF feels too dated for the desired UI, WinUI 3 can replace the shell while keeping the same Core and provider projects. WPF is recommended first because it is simpler to bootstrap and package for a Windows-only utility.
3. If libimobiledevice Windows packaging is unstable, keep it behind an optional provider flag and ship the WPD camera-roll path first.