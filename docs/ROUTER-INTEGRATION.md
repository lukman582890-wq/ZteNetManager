# ZTE F6201B router integration

Screenshots and read-only responses supplied by the user confirm the authenticated F6201B web UI exposes WLAN MAC-filter configuration over HTTP XHR/XML.

## Confirmed read-only response shapes

### Per-SSID policy

The request tag observed in DevTools is `wlan_macfilteraclpolicy`. Its XML response contains `OBJ_WLANSETTING_ID` radio instances and `OBJ_WLANAP_ID` access-point instances. The AP instances expose:

- `_InstID`: e.g. `DEV.WIFI.AP1`
- `Alias`: e.g. `SSID1`
- `WLANViewName`: radio reference, e.g. `DEV.WIFI.RD1`
- `ACLPolicy`: observed values include `Ban` (UI Black List) and `Disabled` (No Filter)

The supplied snapshot reported SSID1 as `Ban`; SSID2–SSID8 as `Disabled`. The radio objects mapped RD1 to 2.4 GHz and RD2 to 5 GHz. These are observations from one router snapshot, not defaults to hard-code.

### MAC rule inventory

The request tag observed in DevTools is `wlan_macfilterrule`. Its XML response contains `OBJ_ACLCFG_ID` instances with:

- `_InstID`: e.g. `DEV.ACL1`
- `MACAddress`: a colon-separated MAC address
- `Name`: the rule label
- `Interface`: an AP reference, e.g. `DEV.WIFI.AP1`

The supplied snapshot showed one rule associated with `DEV.WIFI.AP1`. A rule's presence alone does not prove that the device is currently blocked; the AP policy must also be interpreted and the router's behavior verified.

### Transport and session caveats

The observed requests are HTTP GETs returning XML with success markers such as `IF_ERRORSTR=SUCC`. They were made from an authenticated browser session. The exact URL parameters, session requirements, firmware behavior, and expiry handling must be verified before the desktop app can safely make these requests. Do not copy browser cookies or session identifiers into the source code, logs, repository, or support messages.

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

Local gateway and ARP-cache discovery are implemented. The XML response structures for WLAN policy and MAC rule inventory have been identified from the user's authenticated browser session. A reusable, DTD-disabled XML parser has been added for the observed access-point policy and MAC-rule response shapes. End-to-end authenticated reads from the desktop app, complete router client inventory, and automatic MAC filtering are not yet implemented. The UI must not claim a device is blocked until that behavior can be verified.
