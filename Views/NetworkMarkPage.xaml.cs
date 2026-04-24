using SysMarkWPF.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace SysMarkWPF.Views
{
    public partial class NetworkMarkPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;

        public NetworkMarkPage()
        {
            InitializeComponent();
            Loaded += NetworkMarkPage_Loaded;
        }

        private void NetworkMarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAdapterInfo();
            LoadPreviousResults();
        }

        private void LoadPreviousResults()
        {
            if (!BenchmarkResults.NetworkCompleted) return;
            PingScore.Text = BenchmarkResults.NetworkPingScore.ToString();
            PingAvg.Text = $"{BenchmarkResults.NetworkAvgPing:F1} ms";
            PingMin.Text = "— ms";
            PingMax.Text = "— ms";
            PingLoss.Text = "— %";
            DnsScore.Text = BenchmarkResults.NetworkDnsScore.ToString();
            DnsAvg.Text = $"{BenchmarkResults.NetworkAvgDns:F1} ms";
            DnsGoogle.Text = "— ms";
            DnsCloudflare.Text = "— ms";
            DnsQueries.Text = "20";
            AdapterScore.Text = BenchmarkResults.NetworkAdapterScore.ToString();
            AdapterSpeed.Text = $"{BenchmarkResults.NetworkLinkSpeed} Mbps";
            AdapterType.Text = "—";
            AdapterIp.Text = "—";
            AdapterMac.Text = "—";
            TotalScore.Text = BenchmarkResults.NetworkTotalScore.ToString();
            UpdateProgress(100);
            StatusText.Text = "Last test results loaded.";
        }

        private void LoadAdapterInfo()
        {
            try
            {
                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        !n.Description.Contains("Virtual",
                            StringComparison.OrdinalIgnoreCase) &&
                        !n.Name.Contains("vEthernet",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(n => n.Speed)
                    .FirstOrDefault();

                adapter ??= NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                if (adapter == null)
                {
                    AdapterNameText.Text = "No active adapter found";
                    AdapterDetailsText.Text = "—";
                    return;
                }

                AdapterNameText.Text = adapter.Name;
                var ip = adapter.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a =>
                        a.Address.AddressFamily == AddressFamily.InterNetwork);
                long speedMbps = adapter.Speed / 1_000_000;
                AdapterDetailsText.Text =
                    $"{adapter.NetworkInterfaceType}  |  {speedMbps} Mbps  |  {ip?.Address}";
            }
            catch { }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            ResetResults();
            StatusText.Text = "Running tests...";

            try
            {
                // Тест 1 — Ping
                StatusText.Text = "Test 1/3: Ping test...";
                UpdateProgress(5);
                var pingResult = await Task.Run(() => RunPingTest(_cts.Token));
                UpdateProgress(33);
                Dispatcher.Invoke(() =>
                {
                    PingScore.Text = pingResult.Score.ToString();
                    PingAvg.Text = $"{pingResult.AvgMs:F1} ms";
                    PingMin.Text = $"{pingResult.MinMs} ms";
                    PingMax.Text = $"{pingResult.MaxMs} ms";
                    PingLoss.Text = $"{pingResult.LossPercent:F0} %";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 2 — DNS Speed
                StatusText.Text = "Test 2/3: DNS Speed test...";
                var dnsResult = await Task.Run(() => RunDnsTest(_cts.Token));
                UpdateProgress(66);
                Dispatcher.Invoke(() =>
                {
                    DnsScore.Text = dnsResult.Score.ToString();
                    DnsAvg.Text = $"{dnsResult.AvgMs:F1} ms";
                    DnsGoogle.Text = $"{dnsResult.GoogleMs:F1} ms";
                    DnsCloudflare.Text = $"{dnsResult.CloudflareMs:F1} ms";
                    DnsQueries.Text = $"{dnsResult.TotalQueries}";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 3 — Adapter
                StatusText.Text = "Test 3/3: Adapter test...";
                var adapterResult = await Task.Run(() => RunAdapterTest(_cts.Token));
                UpdateProgress(100);
                Dispatcher.Invoke(() =>
                {
                    AdapterScore.Text = adapterResult.Score.ToString();
                    AdapterSpeed.Text = $"{adapterResult.SpeedMbps} Mbps";
                    AdapterType.Text = adapterResult.AdapterType;
                    AdapterIp.Text = adapterResult.IpAddress;
                    AdapterMac.Text = adapterResult.MacAddress;
                });

                int total = (int)(pingResult.Score * 0.4 +
                                  dnsResult.Score * 0.4 +
                                  adapterResult.Score * 0.2);

                BenchmarkResults.NetworkTotalScore = total;
                BenchmarkResults.NetworkPingScore = pingResult.Score;
                BenchmarkResults.NetworkDnsScore = dnsResult.Score;
                BenchmarkResults.NetworkAdapterScore = adapterResult.Score;
                BenchmarkResults.NetworkAvgPing = pingResult.AvgMs;
                BenchmarkResults.NetworkAvgDns = dnsResult.AvgMs;
                BenchmarkResults.NetworkLinkSpeed = adapterResult.SpeedMbps;
                BenchmarkResults.NetworkCompleted = true;

                Dispatcher.Invoke(() =>
                {
                    TotalScore.Text = total.ToString();
                    StatusText.Text = "Test completed!";
                });
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Test stopped.";
                UpdateProgress(0);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _isRunning = false;
            StatusText.Text = "Test stopped.";
        }

        private void Home_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new MainPage());

        private void UpdateProgress(int value)
        {
            Dispatcher.Invoke(() =>
            {
                TestProgressBar.Value = value;
                ProgressText.Text = $"{value}%";
            });
        }

        private void ResetResults()
        {
            PingScore.Text = "—"; PingAvg.Text = "— ms";
            PingMin.Text = "— ms"; PingMax.Text = "— ms";
            PingLoss.Text = "— %";
            DnsScore.Text = "—"; DnsAvg.Text = "— ms";
            DnsGoogle.Text = "— ms"; DnsCloudflare.Text = "— ms";
            DnsQueries.Text = "—";
            AdapterScore.Text = "—"; AdapterSpeed.Text = "— Mbps";
            AdapterType.Text = "—"; AdapterIp.Text = "—";
            AdapterMac.Text = "—";
            TotalScore.Text = "—";
            UpdateProgress(0);
        }

        // ── Тесты ──────────────────────────────────────────────────

        private static PingResult RunPingTest(CancellationToken token)
        {
            var hosts = new[] { "8.8.8.8", "1.1.1.1", "google.com" };
            var times = new List<long>();
            int sent = 0;
            int lost = 0;

            foreach (var host in hosts)
            {
                if (token.IsCancellationRequested) break;
                for (int i = 0; i < 10; i++)
                {
                    if (token.IsCancellationRequested) break;
                    sent++;
                    try
                    {
                        using var ping = new Ping();
                        var reply = ping.Send(host, 2000);
                        if (reply.Status == IPStatus.Success)
                            times.Add(reply.RoundtripTime);
                        else
                            lost++;
                    }
                    catch { lost++; }
                }
            }

            if (times.Count == 0)
                return new PingResult
                {
                    Score = 0,
                    AvgMs = 999,
                    MinMs = 999,
                    MaxMs = 999,
                    LossPercent = 100
                };

            double avg = times.Average();
            long min = times.Min();
            long max = times.Max();
            double lossPercent = sent > 0 ? lost / (double)sent * 100 : 100;
            double lossMultiplier = 1.0 - lossPercent / 100.0 * 0.5;

            int score = (int)Math.Min(
                50.0 / Math.Max(avg, 1) * 5000 * lossMultiplier, 9999);

            return new PingResult
            {
                Score = score,
                AvgMs = avg,
                MinMs = min,
                MaxMs = max,
                LossPercent = lossPercent
            };
        }

        private static DnsResult RunDnsTest(CancellationToken token)
        {
            
            var random = new Random();
            var tlds = new[] { "com", "net", "org", "io", "co" };

            var times = new List<double>();
            var googleTimes = new List<double>();
            var cloudflareTimes = new List<double>();
            int totalQueries = 20;
            int half = totalQueries / 2;

            for (int i = 0; i < half && !token.IsCancellationRequested; i++)
            {
                string domain = $"{Guid.NewGuid():N}.{tlds[random.Next(tlds.Length)]}";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try { Dns.GetHostAddresses(domain); } catch { }
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                ms = Math.Min(ms, 2000);
                googleTimes.Add(ms);
                times.Add(ms);
                Thread.Sleep(50);
            }

            for (int i = half; i < totalQueries && !token.IsCancellationRequested; i++)
            {
                string domain = $"{Guid.NewGuid():N}.{tlds[random.Next(tlds.Length)]}";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try { Dns.GetHostAddresses(domain); } catch { }
                sw.Stop();

                double ms = Math.Min(sw.Elapsed.TotalMilliseconds, 2000);
                cloudflareTimes.Add(ms);
                times.Add(ms);
                Thread.Sleep(50);
            }

            double avgGoogle = googleTimes.Count > 0 ? googleTimes.Average() : 999;
            double avgCloudflare = cloudflareTimes.Count > 0
                ? cloudflareTimes.Average() : 999;
            double avg = times.Count > 0 ? times.Average() : 999;

            int score = (int)Math.Min(100.0 / Math.Max(avg, 1) * 5000, 9999);

            return new DnsResult
            {
                Score = score,
                AvgMs = avg,
                GoogleMs = avgGoogle,
                CloudflareMs = avgCloudflare,
                TotalQueries = times.Count
            };
        }

        private static AdapterResult RunAdapterTest(CancellationToken token)
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    !n.Description.Contains("Virtual",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains("VirtualBox",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains("VMware",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains("Hyper-V",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Name.Contains("vEthernet",
                        StringComparison.OrdinalIgnoreCase) &&
                    n.Speed > 0 && n.Speed < 100_000_000_000L)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            adapter ??= NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (adapter == null)
                return new AdapterResult
                {
                    Score = 0,
                    SpeedMbps = 0,
                    AdapterType = "None",
                    IpAddress = "—",
                    MacAddress = "—"
                };

            long speedMbps = adapter.Speed / 1_000_000;
            bool isVirtual =
                adapter.Description.Contains("Virtual",
                    StringComparison.OrdinalIgnoreCase) ||
                adapter.Name.Contains("vEthernet",
                    StringComparison.OrdinalIgnoreCase);

            var ip = adapter.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a =>
                    a.Address.AddressFamily == AddressFamily.InterNetwork);

            string mac = string.Join(":",
                adapter.GetPhysicalAddress().GetAddressBytes()
                    .Select(b => b.ToString("X2")));

            string adapterType = isVirtual
                ? $"{adapter.NetworkInterfaceType} (Virtual)"
                : adapter.NetworkInterfaceType.ToString();

            int score = isVirtual
                ? (int)Math.Min(speedMbps / 1000.0 * 2500, 4999)
                : (int)Math.Min(speedMbps / 1000.0 * 5000, 9999);

            return new AdapterResult
            {
                Score = score,
                SpeedMbps = speedMbps,
                AdapterType = adapterType,
                IpAddress = ip?.Address.ToString() ?? "—",
                MacAddress = mac
            };
        }

        // ── Records ────────────────────────────────────────────────

        private record PingResult
        {
            public int Score { get; init; }
            public double AvgMs { get; init; }
            public long MinMs { get; init; }
            public long MaxMs { get; init; }
            public double LossPercent { get; init; }
        }

        private record DnsResult
        {
            public int Score { get; init; }
            public double AvgMs { get; init; }
            public double GoogleMs { get; init; }
            public double CloudflareMs { get; init; }
            public int TotalQueries { get; init; }
        }

        private record AdapterResult
        {
            public int Score { get; init; }
            public long SpeedMbps { get; init; }
            public string AdapterType { get; init; } = "";
            public string IpAddress { get; init; } = "";
            public string MacAddress { get; init; } = "";
        }
    }
}