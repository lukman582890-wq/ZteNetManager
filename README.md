# ZTE IndiHome Network Manager

Windows desktop app for monitoring devices on your own ZTE F6201B IndiHome network and managing Wi-Fi MAC access control where the router firmware supports it.

## Current status

This repository is being bootstrapped. The initial implementation focuses on a safe Windows build pipeline and a WPF application foundation.

- Router identified by user: ZTE F6201B.
- Screenshots show WLAN access control modes per SSID (No Filter / Black List / White List) and MAC-address rules.
- The UI controls do not establish that a third-party app can change them automatically. Router integration must be verified against actual firmware.
- LAN filtering is separate from WLAN MAC filtering and must not be advertised as supported unless verified.
- Never commit router credentials.

## Build on Windows

The GitHub Actions workflow **Windows Build** restores dependencies, builds Release, runs tests when present, publishes a self-contained Windows x64 app, and uploads the artifacts. See the Actions tab.

Local commands:

```powershell
dotnet restore ZteNetManager.sln
dotnet build ZteNetManager.sln -c Release --no-restore
dotnet test ZteNetManager.sln -c Release --no-build
```

See [docs/ROUTER-INTEGRATION.md](docs/ROUTER-INTEGRATION.md).

## Security

Use only on a network you own or administer. Confirm the target MAC before blocking; private/randomized MAC addresses may change. A request being sent is not proof of blocking; distinguish router rule state from verified access restriction.
