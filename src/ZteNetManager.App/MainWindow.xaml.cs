using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows;

namespace ZteNetManager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DeviceRow> _devices = new();
    private bool _scanRunning;

    public MainWindow()
    {
        InitializeComponent();
        DevicesGrid.ItemsSource = _devices;
        Loaded += async (_, _) => await RefreshDiagnosticsAsync();
    }

    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            var selected = FindLanInterface();
            GatewayText.Text = selected?.Gateway?.ToString() ?? "Tidak ditemukan";
            StatusText.Text = "Gateway dibaca dari interface LAN aktif.";
            await ScanLocalAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Gagal membaca jaringan: {ex.Message}";
        }
    }

    private static LanInterface? FindLanInterface()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(n => n.OperationalStatus == OperationalStatus.Up)
                     .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                     .ThenByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
        {
            try
            {
                var props = nic.GetIPProperties();
                var gateway = props.GatewayAddresses
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                var address = props.UnicastAddresses.FirstOrDefault(a =>
                    a.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(a.Address) &&
                    !a.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal));
                if (gateway is not null && address is not null)
                    return new LanInterface(nic.Name, address.Address, address.IPv4Mask, gateway,
                        FormatMac(nic.GetPhysicalAddress().GetAddressBytes()));
            }
            catch { /* Skip interfaces that Windows cannot query. */ }
        }
        return null;
    }

    private async Task ScanLocalAsync()
    {
        if (_scanRunning) return;
        _scanRunning = true;
        try
        {
            StatusText.Text = "Memindai host pada subnet lokal dan membaca ARP cache...";
            var scan = await Task.Run(DiscoverDevices);
            var rows = scan.Devices;
            _devices.Clear();
            foreach (var row in rows.OrderBy(r => r.IPv4, IpAddressComparer.Instance))
                _devices.Add(row);
            DeviceCount.Text = _devices.Count.ToString();
            StatusText.Text = rows.Count == 0
                ? "Tidak ada perangkat yang terdeteksi. Pastikan PC terhubung ke LAN/Wi-Fi yang benar. " + scan.Note
                : $"Selesai: {rows.Count} entri ditemukan. {scan.Note}ARP cache bukan daftar pasti semua perangkat online; perangkat yang memblokir ICMP mungkin tidak terdeteksi.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Pemindaian gagal: {ex.Message}";
        }
        finally { _scanRunning = false; }
    }

    private static (List<DeviceRow> Devices, string Note) DiscoverDevices()
    {
        var results = new Dictionary<string, DeviceRow>(StringComparer.OrdinalIgnoreCase);
        var lan = FindLanInterface();
        if (lan is null)
            return (results.Values.ToList(), "Tidak ditemukan adapter IPv4 aktif dengan default gateway.");

        var note = "";

        results[lan.Address.ToString()] = new DeviceRow("PC ini", lan.Address.ToString(), lan.Mac, lan.Name, "Interface lokal");

        if (!TryGetSmallSubnet(lan.Address, lan.Mask, out var hosts, out var reason))
        {
            results[lan.Gateway.ToString()] = new DeviceRow("Gateway", lan.Gateway.ToString(), "—", "Gateway", "Terdeteksi dari konfigurasi Windows");
            note = $"{reason}. Pemindaian aktif hanya dijalankan untuk subnet /24 sampai /30; ";
            return (results.Values.ToList(), note);
        }

        // Bound the scan to at most 254 host addresses on the directly connected LAN.
        using var semaphore = new SemaphoreSlim(24);
        var probes = hosts.Where(ip => !ip.Equals(lan.Address) && !ip.Equals(lan.Gateway))
            .Select(async ip =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var ping = new Ping();
                    try { await ping.SendPingAsync(ip, 450); }
                    catch (PingException) { }
                    catch (SocketException) { }
                    catch (InvalidOperationException) { }
                    catch (TimeoutException) { }
                }
                finally { semaphore.Release(); }
            }).ToArray();
        Task.WhenAll(probes).GetAwaiter().GetResult();

        var arpEntries = ReadArpCache();
        foreach (var entry in arpEntries)
        {
            if (!hosts.Contains(entry.Key) || entry.Key.Equals(lan.Address)) continue;
            results[entry.Key.ToString()] = new DeviceRow(
                entry.Key.Equals(lan.Gateway) ? "Gateway modem/router" : "Perangkat jaringan",
                entry.Key.ToString(), entry.Value, "ARP cache", "Terlihat di cache; status online belum pasti");
        }

        if (!results.ContainsKey(lan.Gateway.ToString()))
            results[lan.Gateway.ToString()] = new DeviceRow("Gateway modem/router", lan.Gateway.ToString(), "—", "Gateway", "Dari konfigurasi Windows");

        return (results.Values.ToList(), note);
    }

    private static bool TryGetSmallSubnet(IPAddress address, IPAddress mask, out HashSet<IPAddress> hosts, out string reason)
    {
        hosts = new HashSet<IPAddress>();
        reason = "";
        var a = address.GetAddressBytes();
        var m = mask.GetAddressBytes();
        if (a.Length != 4 || m.Length != 4)
        {
            reason = "Bukan IPv4";
            return false;
        }

        var prefix = 0;
        var sawZero = false;
        foreach (var b in m)
        for (var bit = 7; bit >= 0; bit--)
        {
            var set = (b & (1 << bit)) != 0;
            if (set && sawZero) { reason = "Subnet mask tidak valid"; return false; }
            if (set) prefix++; else sawZero = true;
        }

        if (prefix < 24 || prefix > 30)
        {
            reason = "Subnet lebih luas dari /24 atau terlalu kecil; pemindaian aktif dilewati demi keamanan";
            return false;
        }

        uint ip = ToUInt32(a);
        uint maskValue = ToUInt32(m);
        uint network = ip & maskValue;
        uint broadcast = network | ~maskValue;
        for (uint current = network + 1; current < broadcast; current++)
            hosts.Add(FromUInt32(current));
        return hosts.Count <= 254;
    }

    private static Dictionary<IPAddress, string> ReadArpCache()
    {
        var entries = new Dictionary<IPAddress, string>();
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "arp.exe",
                    Arguments = "-a",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            foreach (Match match in Regex.Matches(output,
                         @"(?m)^\s*(\d{1,3}(?:\.\d{1,3}){3})\s+([0-9a-fA-F-]{17})\s+"))
            {
                if (!IPAddress.TryParse(match.Groups[1].Value, out var ip)) continue;
                var mac = match.Groups[2].Value.Replace('-', ':').ToUpperInvariant();
                if (mac != "FF:FF:FF:FF:FF:FF" && mac != "00:00:00:00:00:00")
                    entries[ip] = mac;
            }
        }
        catch { /* ARP cache may be unavailable; report whatever discovery produced. */ }
        return entries;
    }

    private static uint ToUInt32(byte[] bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    private static IPAddress FromUInt32(uint value) =>
        new(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });

    private static string FormatMac(byte[] bytes) =>
        bytes.Length == 0 ? "—" : string.Join(":", bytes.Select(b => b.ToString("X2")));

    private void Dashboard_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Dashboard";
        Subtitle.Text = "Monitor perangkat pada jaringan lokal Anda";
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Devices";
        await ScanLocalAsync();
    }

    private void OpenRouterAdmin_Click(object sender, RoutedEventArgs e)
    {
        var gatewayText = GatewayText.Text;
        if (!IPAddress.TryParse(gatewayText, out var gateway) ||
            gateway.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(gateway))
        {
            MessageBox.Show("Gateway IPv4 belum terdeteksi. Jalankan pemindaian jaringan terlebih dahulu.",
                "Admin router tidak tersedia", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var uri = new UriBuilder(Uri.UriSchemeHttp, gateway.ToString()).Uri;
            Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tidak dapat membuka halaman router: {ex.Message}",
                "Gagal membuka admin router", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Router_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Router diagnostics";
        Subtitle.Text = "ZTE F6201B — kemampuan firmware otomatis belum diverifikasi.";
        StatusText.Text = "Gateway dan ARP cache membantu diagnosis LAN, tetapi bukan daftar klien resmi router. Endpoint firmware belum diverifikasi.";
    }

    private void Access_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Access control";
        Subtitle.Text = "WLAN blacklist tersedia pada halaman admin router.";
        MessageBox.Show(
            "Otomatisasi belum diaktifkan karena endpoint dan autentikasi firmware belum diverifikasi. Jangan anggap perangkat terblokir sebelum aturan router dibaca ulang.",
            "Access control belum tersedia", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Settings";
        Subtitle.Text = "Pengaturan router dan kredensial aman akan ditambahkan.";
        StatusText.Text = "Jangan masukkan kredensial router ke source code atau repository.";
    }

    private sealed record LanInterface(string Name, IPAddress Address, IPAddress Mask, IPAddress Gateway, string Mac);

    private sealed class IpAddressComparer : IComparer<string>
    {
        public static readonly IpAddressComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            if (IPAddress.TryParse(x, out var a) && IPAddress.TryParse(y, out var b))
                return ToUInt32(a.GetAddressBytes()).CompareTo(ToUInt32(b.GetAddressBytes()));
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}

public sealed record DeviceRow(string DisplayName, string IPv4, string Mac, string Source, string Status);
