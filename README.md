# Offline iPhone USB Transfer

A Windows desktop tool for USB-only iPhone media transfer with **no app installed on the
iPhone**. It imports camera-roll photos and videos over USB using Windows Portable Devices,
with filtering, browsing, and reliable downloads.

> See [docs/ios-usb-access.md](docs/ios-usb-access.md) for exactly which iPhone storage
> surfaces are reachable over USB and why. Camera Roll is the supported baseline; the Files
> app `Downloads` folder is **not** reachable over USB without an iOS app and is shown as
> unsupported.

## Features

- Detects a connected iPhone and reports connection / lock / trust state.
- Diagnostics feasibility screen for which surfaces are reachable.
- Browse Camera Roll (and optional app File Sharing when a native bridge is enabled).
- Filter by type (images, videos, audio, documents, archives), custom extensions,
  filename search, and min/max size.
- Download selected files or all filtered files to a Windows folder.
- Preserve folder structure, choose duplicate handling (skip / overwrite / auto-rename),
  progress, and cancellation.

## Projects

| Project | Purpose |
|---|---|
| `src/OfflineFileTransfer.Core` | Models, filters, transfer orchestration, provider contracts (no OS deps). |
| `src/OfflineFileTransfer.WindowsDevices` | WPD/PTP camera-roll provider, device manager, diagnostics. |
| `src/OfflineFileTransfer.IosNative` | Optional native iOS bridge provider (AFC / File Sharing), disabled by default. |
| `src/OfflineFileTransfer.App` | WPF desktop UI (MVVM). |
| `tests/OfflineFileTransfer.Core.Tests` | Unit tests for filters, path handling, and transfers. |

## Prerequisites

- Windows.
- .NET SDK capable of building `net8.0` / `net8.0-windows` targets.
- **Apple Devices** app (or Apple Mobile Device Support / iTunes) so Windows can see the iPhone.
- An iPhone connected by USB, unlocked, with **Trust / Allow** accepted.

## Build and test

```powershell
dotnet build OfflineFileTransfer.slnx
dotnet test tests/OfflineFileTransfer.Core.Tests/OfflineFileTransfer.Core.Tests.csproj
```

## Run

```powershell
dotnet run --project src/OfflineFileTransfer.App/OfflineFileTransfer.App.csproj
```

## Publish a self-contained app

```powershell
dotnet publish src/OfflineFileTransfer.App/OfflineFileTransfer.App.csproj -c Release -r win-x64
```

The App project produces a self-contained, single-file `win-x64` build when a runtime
identifier is supplied. Output lands in
`src/OfflineFileTransfer.App/bin/Release/net8.0-windows/win-x64/publish/`.

## Limitations

- No full iPhone filesystem browsing; iOS does not expose arbitrary user files over USB.
- No bypass of iOS sandboxing.
- iCloud-optimized originals that are not stored locally on the phone cannot be downloaded.
- No iPhone app is installed by this tool.
