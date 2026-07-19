# iOS USB Access Boundaries

This document records exactly which iPhone storage surfaces this tool can and cannot
reach over USB on Windows, and why. It exists so the product promises only what iOS
actually permits without installing an app on the phone.

## Hard constraints

- **No iPhone app install.** Nothing is deployed to the phone. This rules out any
  approach that depends on a companion iOS app or app extension.
- **USB only.** Wi-Fi sync, iCloud, AirDrop, and cloud APIs are out of scope.
- **iOS sandboxing is not bypassed.** There is no jailbreak and no backup parsing.

## Access matrix

| Surface | Mechanism | Reachable over USB without an iPhone app? | Status in this tool |
|---|---|---|---|
| Camera Roll photos/videos (`DCIM`) | Windows Portable Devices (WPD/PTP) via the Shell namespace | Yes, when unlocked + trusted | **Supported (MVP)** |
| App File Sharing document containers | AFC / HouseArrest (native `libimobiledevice`-style bridge) | Sometimes, only for apps that expose File Sharing | **Optional**, disabled until the native bridge ships |
| AFC media domain | AFC (native bridge) | Sometimes | **Optional**, disabled until the native bridge ships |
| Files app `Downloads` folder | None permitted over USB | **No**, not without an iOS app / iCloud / backup | **Unsupported**, shown with a clear reason |
| Arbitrary Files app folders | None permitted over USB | No | Not attempted |

## Why `Downloads` is not promised

The Files app `Downloads` folder lives inside app/container sandboxes that iOS does not
expose to Windows over USB. Reaching it reliably would require one of:

1. An iOS app or extension (violates the no-install rule).
2. iCloud / Windows iCloud sync (out of scope, not USB).
3. Parsing an encrypted device backup (out of scope).

The diagnostics spike probes for any visible `Downloads` directory. If it is not visible,
the UI marks the `Downloads` source unsupported with the reason, instead of failing silently.

## Prerequisites on Windows

- Windows laptop with a USB cable.
- iPhone unlocked, with the **Trust / Allow** prompt accepted.
- **Apple Devices** app (or Apple Mobile Device Support / iTunes) installed so Windows
  can enumerate the iPhone as a portable device.

## Diagnostics checks

The feasibility spike (`WpdDiagnosticsService`) reports:

- iPhone connected over USB.
- Phone unlocked and trusted.
- Apple Devices / Mobile Device support installed.
- Camera Roll (`DCIM`) enumerable.
- AFC media-domain access (requires the native bridge; reported as unknown until enabled).
- App File Sharing containers enumerable (requires the native bridge).
- Files app `Downloads` visible/readable (expected to fail; treated as unsupported).
