using System.IO;
using System.Xml.Linq;

namespace ZteNetManager;

/// <summary>
/// Parses read-only XML structures observed on the ZTE F6201B web UI.
/// This class does not perform network requests or modify router settings.
/// </summary>
public static class RouterXmlParser
{
    public static IReadOnlyList<RouterAccessPoint> ParseAccessPoints(string xml)
    {
        var root = ParseRoot(xml);
        return ReadInstances(root, "OBJ_WLANAP_ID")
            .Select(values => new RouterAccessPoint(
                Get(values, "Alias"),
                Get(values, "_InstID"),
                Get(values, "WLANViewName"),
                Get(values, "ACLPolicy")))
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId) ||
                           !string.IsNullOrWhiteSpace(item.Ssid))
            .ToArray();
    }

    public static IReadOnlyList<RouterMacRule> ParseMacRules(string xml)
    {
        var root = ParseRoot(xml);
        return ReadInstances(root, "OBJ_ACLCFG_ID")
            .Select(values => new RouterMacRule(
                Get(values, "_InstID"),
                Get(values, "Name"),
                NormalizeMac(Get(values, "MACAddress")),
                Get(values, "Interface")))
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId) ||
                           !string.IsNullOrWhiteSpace(item.MacAddress))
            .ToArray();
    }

    /// <summary>
    /// Parses the observed OBJ_WLAN_AD_ID associated-client table and resolves
    /// the AP reference to the configured SSID name when present in the same XML.
    /// </summary>
    public static IReadOnlyList<RouterWifiClient> ParseAssociatedClients(string xml)
    {
        var root = ParseRoot(xml);
        var ssids = ReadInstances(root, "OBJ_WLANAP_ID")
            .Select(values => (Alias: Get(values, "_InstID"), Name: Get(values, "ESSID")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Alias))
            .GroupBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Name, StringComparer.OrdinalIgnoreCase);

        return ReadInstances(root, "OBJ_WLAN_AD_ID")
            .Select(values =>
            {
                var ap = Get(values, "AliasName");
                var ssid = Get(values, "SSIDName");
                if (ssids.TryGetValue(ap, out var configuredName) &&
                    !string.IsNullOrWhiteSpace(configuredName) &&
                    (string.IsNullOrWhiteSpace(ssid) || ssid.Equals("Kosong", StringComparison.OrdinalIgnoreCase)))
                    ssid = configuredName;
                return new RouterWifiClient(
                    Get(values, "_InstID"),
                    Get(values, "HostName"),
                    Get(values, "IPAddress"),
                    NormalizeMac(Get(values, "MACAddress")),
                    ap,
                    ssid,
                    Get(values, "RSSI"),
                    Get(values, "CurrentMode"),
                    Get(values, "LinkTime"));
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.MacAddress) ||
                           !string.IsNullOrWhiteSpace(item.IPAddress) ||
                           !string.IsNullOrWhiteSpace(item.HostName))
            .ToArray();
    }

    /// <summary>Returns configured SSID aliases and names from OBJ_WLANAP_ID.</summary>
    public static IReadOnlyList<RouterSsid> ParseSsidNames(string xml)
    {
        var root = ParseRoot(xml);
        return ReadInstances(root, "OBJ_WLANAP_ID")
            .Select(values => new RouterSsid(Get(values, "_InstID"), Get(values, "Alias"), Get(values, "ESSID")))
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId))
            .ToArray();
    }

    private static XElement ParseRoot(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML response is empty.", nameof(xml));

        // XML parser prohibits DTD processing and external entity resolution.
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml),
            new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null
            });
        return XElement.Load(reader);
    }

    private static IEnumerable<Dictionary<string, string>> ReadInstances(XElement root, string objectName)
    {
        var container = root.Descendants().FirstOrDefault(e => e.Name.LocalName == objectName);
        if (container is null)
            return Array.Empty<Dictionary<string, string>>();

        return container.Elements().Where(e => e.Name.LocalName == "Instance").Select(instance =>
        {
            var pairs = instance.Elements().Where(e => e.Name.LocalName == "ParaName")
                .Zip(instance.Elements().Where(e => e.Name.LocalName == "ParaValue"),
                    (name, value) => (Name: name.Value.Trim(), Value: value.Value.Trim()));
            return pairs.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
        }).ToArray();
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

    private static string NormalizeMac(string mac)
    {
        var compact = new string((mac ?? string.Empty).Where(Uri.IsHexDigit).ToArray());
        if (compact.Length != 12) return mac?.Trim() ?? string.Empty;
        return string.Join(":", Enumerable.Range(0, 6)
            .Select(i => compact.Substring(i * 2, 2).ToUpperInvariant()));
    }
}

public sealed record RouterAccessPoint(string Ssid, string InstanceId, string RadioReference, string Policy)
{
    public string PolicyLabel => Policy switch
    {
        "Ban" => "Black List",
        "White" => "White List",
        "Disabled" => "No Filter",
        "" => "Unknown",
        _ => Policy
    };
}

public sealed record RouterMacRule(string InstanceId, string Name, string MacAddress, string InterfaceReference);
