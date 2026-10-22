using System;
using System.Linq;
using System.Xml;
using ZteNetManager;

var passed = 0;
Run("access point policy parsing", () =>
{
    const string xml = """
        <ajax_response_xml_root>
          <OBJ_WLANAP_ID>
            <Instance>
              <ParaName>_InstID</ParaName><ParaValue>DEV.WIFI.AP1</ParaValue>
              <ParaName>Alias</ParaName><ParaValue>SSID1</ParaValue>
              <ParaName>WLANViewName</ParaName><ParaValue>DEV.WIFI.RD1</ParaValue>
              <ParaName>ACLPolicy</ParaName><ParaValue>Ban</ParaValue>
            </Instance>
            <Instance>
              <ParaName>_InstID</ParaName><ParaValue>DEV.WIFI.AP2</ParaValue>
              <ParaName>Alias</ParaName><ParaValue>SSID2</ParaValue>
              <ParaName>WLANViewName</ParaName><ParaValue>DEV.WIFI.RD1</ParaValue>
              <ParaName>ACLPolicy</ParaName><ParaValue>Disabled</ParaValue>
            </Instance>
          </OBJ_WLANAP_ID>
        </ajax_response_xml_root>
        """;
    var aps = RouterXmlParser.ParseAccessPoints(xml);
    Equal(2, aps.Count);
    Equal("SSID1", aps[0].Ssid);
    Equal("DEV.WIFI.AP1", aps[0].InstanceId);
    Equal("DEV.WIFI.RD1", aps[0].RadioReference);
    Equal("Ban", aps[0].Policy);
    Equal("Black List", aps[0].PolicyLabel);
    Equal("No Filter", aps[1].PolicyLabel);
});

Run("MAC rule parsing and normalization", () =>
{
    const string xml = """
        <ajax_response_xml_root>
          <OBJ_ACLCFG_ID>
            <Instance>
              <ParaName>_InstID</ParaName><ParaValue>DEV.ACL1</ParaValue>
              <ParaName>MACAddress</ParaName><ParaValue>2c:d8:de:70:01:09</ParaValue>
              <ParaName>Name</ParaName><ParaValue>test-device</ParaValue>
              <ParaName>Interface</ParaName><ParaValue>DEV.WIFI.AP1</ParaValue>
            </Instance>
          </OBJ_ACLCFG_ID>
        </ajax_response_xml_root>
        """;
    var rules = RouterXmlParser.ParseMacRules(xml);
    Equal(1, rules.Count);
    Equal("DEV.ACL1", rules[0].InstanceId);
    Equal("2C:D8:DE:70:01:09", rules[0].MacAddress);
    Equal("test-device", rules[0].Name);
    Equal("DEV.WIFI.AP1", rules[0].InterfaceReference);
});

Run("missing object returns empty collection", () =>
{
    Equal(0, RouterXmlParser.ParseMacRules("<root />").Count);
});

Run("empty XML is rejected", () =>
{
    Throws<ArgumentException>(() => RouterXmlParser.ParseAccessPoints(" "));
});

Run("malformed XML is rejected", () =>
{
    Throws<XmlException>(() => RouterXmlParser.ParseMacRules("<root>"));
});

Run("DTD is prohibited", () =>
{
    const string xml = "<!DOCTYPE root [<!ENTITY x 'boom'>]><root><OBJ_ACLCFG_ID>&x;</OBJ_ACLCFG_ID></root>";
    Throws<XmlException>(() => RouterXmlParser.ParseMacRules(xml));
});


Run("associated Wi-Fi client and SSID mapping", () =>
{
    const string xml = """
        <ajax_response_xml_root>
          <OBJ_WLAN_AD_ID>
            <Instance>
              <ParaName>_InstID</ParaName><ParaValue>DEV.WIFI.AP6.AD1</ParaValue>
              <ParaName>AliasName</ParaName><ParaValue>DEV.WIFI.AP6</ParaValue>
              <ParaName>SSIDName</ParaName><ParaValue>Kosong</ParaValue>
              <ParaName>HostName</ParaName><ParaValue>Test-iPad</ParaValue>
              <ParaName>IPAddress</ParaName><ParaValue>192.168.1.15</ParaValue>
              <ParaName>MACAddress</ParaName><ParaValue>5c:91:75:ef:fc:49</ParaValue>
              <ParaName>RSSI</ParaName><ParaValue>-57</ParaValue>
              <ParaName>CurrentMode</ParaName><ParaValue>11ax</ParaValue>
              <ParaName>LinkTime</ParaName><ParaValue>9202</ParaValue>
            </Instance>
          </OBJ_WLAN_AD_ID>
          <OBJ_WLANAP_ID>
            <Instance>
              <ParaName>_InstID</ParaName><ParaValue>DEV.WIFI.AP6</ParaValue>
              <ParaName>Alias</ParaName><ParaValue>SSID6</ParaValue>
              <ParaName>ESSID</ParaName><ParaValue>Bullsrecords</ParaValue>
            </Instance>
          </OBJ_WLANAP_ID>
        </ajax_response_xml_root>
        """;
    var clients = RouterXmlParser.ParseAssociatedClients(xml);
    Equal(1, clients.Count);
    Equal("Test-iPad", clients[0].HostName);
    Equal("192.168.1.15", clients[0].IPAddress);
    Equal("5C:91:75:EF:FC:49", clients[0].MacAddress);
    Equal("Bullsrecords", clients[0].SsidName);
    Equal("-57", clients[0].Rssi);
    Equal("11ax", clients[0].WifiMode);
    var ssids = RouterXmlParser.ParseSsidNames(xml);
    Equal(1, ssids.Count);
    Equal("SSID6", ssids[0].Alias);
    Equal("Bullsrecords", ssids[0].Essid);
});

Console.WriteLine($"All {passed} parser tests passed.");

void Run(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

void Equal<T>(T expected, T actual)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

void Throws<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}
