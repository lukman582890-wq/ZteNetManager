using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace ZteNetManager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DeviceRow> _devices = new();

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
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            GatewayText.Text = gateway?.ToString() ?? "Tidak ditemukan";
            StatusText.Text = "Gateway dibaca dari konfigurasi interface Windows. Router API belum diuji.";
            await ScanLocalAsync();
        }
        catch (Exception ex) { StatusText.Text = $"Gagal membaca jaringan: {ex.Message}"; }
    }

    private async Task ScanLocalAsync()
    {
        StatusText.Text = "Membaca interface lokal Windows...";
        var rows = await Task.Run(() => NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => {
                try {
                    var props = n.GetIPProperties();
                    var mac = FormatMac(n.GetPhysicalAddress().GetAddressBytes());
                    return props.UnicastAddresses
                        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => new DeviceRow(n.Name, a.Address.ToString(), mac, "Local interface", "PC ini"));
                } catch { return Enumerable.Empty<DeviceRow>(); }
            }).ToList());

        _devices.Clear();
        foreach (var row in rows.DistinctBy(r => (r.IPv4, r.Mac))) _devices.Add(row);
        DeviceCount.Text = _devices.Count.ToString();
        StatusText.Text = "Hanya interface PC yang ditampilkan. Klien router belum dipindai; data ini bukan daftar seluruh perangkat.";
    }

    private static string FormatMac(byte[] bytes) => bytes.Length == 0 ? "—" : string.Join(":", bytes.Select(b => b.ToString("X2")));
    private void Dashboard_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Dashboard"; Subtitle.Text = "Monitor perangkat pada jaringan lokal Anda"; }
    private async void Scan_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Devices"; await ScanLocalAsync(); }
    private void Router_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Router diagnostics"; Subtitle.Text = "ZTE F6201B — kemampuan firmware otomatis belum diverifikasi."; StatusText.Text = "Verifikasi metode autentikasi dan akses lokal sebelum mengaktifkan integrasi."; }
    private void Access_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Access control"; Subtitle.Text = "WLAN blacklist tersedia pada halaman admin yang ditunjukkan."; MessageBox.Show("Otomatisasi belum diaktifkan karena endpoint dan autentikasi firmware belum diverifikasi. Jangan anggap perangkat terblokir sebelum aturan router dibaca ulang.", "Access control belum tersedia", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void Settings_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Settings"; Subtitle.Text = "Pengaturan router dan kredensial aman akan ditambahkan."; StatusText.Text = "Jangan masukkan kredensial router ke source code atau repository."; }
}

public sealed record DeviceRow(string DisplayName, string IPv4, string Mac, string Source, string Status);
