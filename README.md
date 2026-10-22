# ZTE IndiHome Network Manager

A Windows desktop utility for inspecting the local network connected to a ZTE F6201B router. Router write operations remain disabled until the exact firmware's authenticated interface is verified.

## Current features

- WPF dashboard for Windows.
- Detects the IPv4 gateway from an active local interface.
- Scans only a directly connected IPv4 subnet with a prefix from /24 to /30 (maximum 254 addresses).
- Reads the Windows ARP cache after the scan to display IP/MAC entries.
- Labels discovery data carefully: ARP cache entries are not a guaranteed live inventory, and devices that block ICMP or do not refresh ARP may be missed.
- Windows GitHub Actions workflow builds and publishes a self-contained x64 application artifact.

## Known limitations

- This is not yet a complete router-client inventory; the router's own authenticated client list is more authoritative.
- ZTE F6201B automatic MAC filtering is not implemented. The visible web UI options do not prove that a stable, safe API is available.
- LAN filtering and WLAN MAC filtering are different features. LAN filtering is unsupported until verified.
- No router password, cookie, or session token should be committed or logged.
- The app is a development build, not an installer.

## Build on Windows

Install the .NET 10 SDK and run:

```powershell
dotnet restore ZteNetManager.sln
dotnet build ZteNetManager.sln -c Release --no-restore
dotnet publish src/ZteNetManager.App/ZteNetManager.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish
```

Run the published ZteNetManager.exe from artifacts/publish. The GitHub Actions artifact is available from the latest successful run under the Actions tab.

## Safety

Use only on a network you own or are authorized to administer. Confirm a device's MAC before changing any access rule; private/randomized MAC addresses may vary. A rule being configured is not proof that access was actually denied.
