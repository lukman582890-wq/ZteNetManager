# ZTE F6201B router integration

Screenshots supplied by the user show the authenticated F6201B UI with WLAN Access Control mode per SSID (No Filter / Black List / White List) and a rule editor with Name, SSID and MAC Address fields.

This establishes that WLAN MAC rules exist in the web UI. It does **not** establish stable HTTP endpoints, request payloads, CSRF protections, or login/session behavior for automated changes.

## Requirements

1. Discover the gateway from the active Windows interface.
2. Let the user configure router URL and credentials; store secrets in Windows Credential Manager, never plaintext in source or ordinary database fields.
3. Verify the exact firmware/login behavior before enabling automatic changes.
4. Start read-only. Never guess endpoints.
5. Any future mutation must use authenticated session state, handle CSRF/session expiry, use timeouts, and read back the rule.
6. Warn before changing a WLAN SSID to Black List, do not alter other SSIDs/rules, and confirm the intended MAC. Private MACs can vary by network.
7. Distinguish configured rule state from proven access restriction.
8. WLAN MAC filtering does not imply LAN filtering. LAN filtering stays unsupported until independently verified.
9. Never log passwords, cookies, tokens, or session identifiers.

## Current status

Starter app only identifies local interfaces and likely gateway. It does not enumerate every router client and does not automatically change MAC filtering. Those limits are explicitly shown in the UI.
