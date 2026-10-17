# Third-party notices

## windows-ble-hid

- Source: https://github.com/abhishek-raj/windows-ble-hid
- Reviewed commit: `b33a0043b3865ea8251a242eae27aa2f72816512`
- Copyright (c) 2026 Abhishek Raj
- License: MIT. Full text: `licenses/windows-ble-hid-MIT.txt`.
- Adapted files: `src/UniversalControl.Core/HidDescriptors.cs` and `VirtualKeyMap.cs`.
- Changes: namespace, JIS physical keys, OEM scan-code mapping, descriptor key range, remote-wake flag, horizontal AC Pan in the mouse descriptor.
- Other transport, safety, input routing and GUI code in this project is independently implemented.

## OpenSpan

- Source: https://github.com/Divisionless/OpenSpan
- Reviewed commit: `f60a7124be4d3d3c1ee3726154eaf6fd48d73514`
- Copyright (c) 2026 OpenSpan contributors. MIT.
- Used as research for encrypted HOGP reports and screen-edge behavior. No source code copied.
- License retained for reference: `licenses/OpenSpan-MIT.txt`.

## Microsoft runtimes and Windows projections

The self-contained Windows build includes .NET 10.0.13, WPF, Windows Forms,
and Windows SDK .NET projection assemblies (Microsoft.Windows.SDK.NET.Ref 10.0.26100.87).
The .NET distribution notices are retained in `licenses/dotnet-LICENSE.txt` and
`licenses/dotnet-ThirdPartyNotices.txt`.

- .NET: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- Windows SDK package license: https://aka.ms/WinSDKLicenseURL
- C#/WinRT: https://github.com/microsoft/CsWinRT/blob/master/LICENSE

Test-only dependencies (not linked into the application) are Microsoft.NET.Test.Sdk 17.11.1,
xUnit 2.9.2, and xunit.runner.visualstudio 2.8.2. NuGet restores their original package metadata.
