# ZTE F6201B router integration

Screenshots supplied by the user show the authenticated F6201B UI with WLAN Access Control modes per SSID (No Filter / Black List / White List) and a rule editor with Name, SSID and MAC Address fields.

This establishes that WLAN MAC rules exist in the web UI. It does not establish stable HTTP endpoints, request payloads, CSRF protections, or login/session behavior for automated changes.

## Current local discovery

The Windows app identifies an active IPv4 gateway, then performs a bounded scan only when the connected subnet is /24 through /30. It reads the Windows ARP cache and shows IP/MAC entries. This is best-effort local discovery, not an authoritative list of every connected router client. ICMP filtering, stale ARP entries, guest-network isolation, and multiple adapters can affect results.

The app intentionally skips active sweeps of broader subnets to avoid scanning a large address range.

## Requirements before router writes are enabled

1. Verify the exact router firmware and login flow on the user's unit.
2. Discover the gateway automatically, but allow explicit configuration later.
3. Store credentials using Windows Credential Manager, not source code or plaintext configuration.
4. Begin with read-only requests; never guess endpoints.
5. Any future mutation must protect session/CSRF tokens, handle expiry and timeouts, and read the changed rule back from the router.
6. Warn before changing a WLAN SSID to Black List, preserve unrelated SSID settings/rules, and confirm the target MAC. Private MACs may vary by network.
7. Distinguish configured-rule state from verified access restriction.
8. WLAN MAC filtering does not imply LAN filtering. LAN access control remains unsupported until independently verified.
9. Never log passwords, cookies, tokens, or session identifiers.

## Current status

Local gateway and ARP-cache discovery are implemented. Complete router client inventory and automatic MAC filtering are not yet implemented. The UI must not claim a device is blocked until that behavior can be verified.
