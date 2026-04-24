using Hardware.Info;
using SysMarkWPF.Models;
using SysMarkWPF.Services;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;

namespace SysMarkWPF.Views
{
    public partial class MainPage : Page
    {
        private readonly HardwareInfo _hardwareInfo;

        public MainPage()
        {
            InitializeComponent();
            _hardwareInfo = new HardwareInfo();
            Loaded += MainPage_Loaded;
            SizeChanged += Page_SizeChanged;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLastResults();

            if (SystemInfoCache.IsLoaded)
            {
                ApplySystemInfoFromCache();
                UpdateButtonsHeight();
                return;
            }

            await Task.Run(() => _hardwareInfo.RefreshAll());
            FetchAndCacheSystemInfo();
            UpdateButtonsHeight();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateButtonsHeight();
        }

        private void UpdateButtonsHeight()
        {
            if (!IsLoaded) return;

            Dispatcher.InvokeAsync(() =>
            {
                double margins = 16 * 2 + 12 * 5;
                double fixedHeight =
                    HeaderBorder.ActualHeight +
                    SysInfoBorder.ActualHeight +
                    RunAllButton.ActualHeight +
                    NavButtonsGrid.ActualHeight +
                    SaveBorder.ActualHeight +
                    margins;

                double available = ActualHeight - fixedHeight;
                LastResultsGrid.Height = Math.Max(80, available);

            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void LoadLastResults()
        {
            LastCpuScore.Text = BenchmarkResults.CpuCompleted
                ? BenchmarkResults.CpuTotalScore.ToString() : "—";
            LastMemoryScore.Text = BenchmarkResults.MemoryCompleted
                ? BenchmarkResults.MemoryTotalScore.ToString() : "—";
            LastDiskScore.Text = BenchmarkResults.DiskCompleted
                ? BenchmarkResults.DiskTotalScore.ToString() : "—";
            LastNetworkScore.Text = BenchmarkResults.NetworkCompleted
                ? BenchmarkResults.NetworkTotalScore.ToString() : "—";
        }

        private void ApplySystemInfoFromCache()
        {
            CpuName.Text = SystemInfoCache.CpuName;
            CpuCores.Text = SystemInfoCache.CpuCores;
            CpuSpeed.Text = SystemInfoCache.CpuSpeed;
            RamTotal.Text = SystemInfoCache.RamTotal;
            RamSpeed.Text = SystemInfoCache.RamSpeed;
            RamAvailable.Text = SystemInfoCache.RamAvailable;
            GpuName.Text = SystemInfoCache.GpuName;
            GpuVram.Text = SystemInfoCache.GpuVram;
            GpuDriver.Text = SystemInfoCache.GpuDriver;
            DiskName.Text = SystemInfoCache.DiskName;
            DiskType.Text = SystemInfoCache.DiskType;
            DiskFree.Text = SystemInfoCache.DiskFree;
            OsName.Text = SystemInfoCache.OsName;
            OsBuild.Text = SystemInfoCache.OsBuild;
            ComputerName.Text = SystemInfoCache.ComputerName;
            UserName.Text = SystemInfoCache.UserName;
            NetworkAdapter.Text = SystemInfoCache.NetworkAdapter;
            NetworkIp.Text = SystemInfoCache.NetworkIp;
        }

        private void FetchAndCacheSystemInfo()
        {
            try
            {
                var cpu = _hardwareInfo.CpuList.FirstOrDefault();
                if (cpu != null)
                {
                    SystemInfoCache.CpuName = cpu.Name.Trim();
                    SystemInfoCache.CpuCores =
                        $"{cpu.NumberOfCores} cores / {cpu.NumberOfLogicalProcessors} threads";
                    SystemInfoCache.CpuSpeed = $"{cpu.CurrentClockSpeed} MHz";
                }

                var totalRam = _hardwareInfo.MemoryList.Sum(m => (long)m.Capacity);
                var availableRam = _hardwareInfo.MemoryStatus.AvailablePhysical;
                SystemInfoCache.RamTotal = $"{totalRam / (1024 * 1024 * 1024)} GB DDR4";
                SystemInfoCache.RamAvailable =
                    $"Available: {availableRam / (1024 * 1024 * 1024)} GB";
                var mem = _hardwareInfo.MemoryList.FirstOrDefault();
                SystemInfoCache.RamSpeed = mem != null ? $"{mem.Speed} MHz" : "—";

                var gpu = _hardwareInfo.VideoControllerList.FirstOrDefault();
                if (gpu != null)
                {
                    SystemInfoCache.GpuName = gpu.Name.Trim();
                    SystemInfoCache.GpuVram =
                        $"{gpu.AdapterRAM / (1024 * 1024 * 1024)} GB VRAM";
                    SystemInfoCache.GpuDriver = $"Driver: {gpu.DriverVersion}";
                }

                var disk = _hardwareInfo.DriveList.FirstOrDefault();
                if (disk != null)
                {
                    SystemInfoCache.DiskName = disk.Model.Trim();
                    SystemInfoCache.DiskType =
                        $"{disk.Size / (1024 * 1024 * 1024)} GB";
                    var free = System.IO.DriveInfo.GetDrives()
                        .FirstOrDefault(d => d.IsReady &&
                            d.DriveType == System.IO.DriveType.Fixed);
                    SystemInfoCache.DiskFree = free != null
                        ? $"Free: {free.AvailableFreeSpace / (1024 * 1024 * 1024)} GB"
                        : "—";
                }

                SystemInfoCache.OsName = System.Runtime.InteropServices
                    .RuntimeInformation.OSDescription;
                SystemInfoCache.OsBuild =
                    $"Build: {Environment.OSVersion.Version}";
                SystemInfoCache.ComputerName = Environment.MachineName;
                SystemInfoCache.UserName = $"User: {Environment.UserName}";

                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (adapter != null)
                {
                    SystemInfoCache.NetworkAdapter = adapter.Name;
                    var ip = adapter.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a =>
                            a.Address.AddressFamily == AddressFamily.InterNetwork);
                    SystemInfoCache.NetworkIp = ip?.Address.ToString() ?? "—";
                }

                SystemInfoCache.IsLoaded = true;
                Dispatcher.Invoke(ApplySystemInfoFromCache);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading system info: {ex.Message}");
            }
        }

        private void CpuMark_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new CpuMarkPage());

        private void MemoryMark_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new MemoryMarkPage());

        private void DiskMark_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new DiskMarkPage());

        private void NetworkMark_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new NetworkMarkPage());

        private void RunAllTests_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance?.NavigateTo(new PassMarkPage());

        private void SaveTxt_Click(object sender, RoutedEventArgs e) =>
            ExportService.SaveTxt();

        private void SavePng_Click(object sender, RoutedEventArgs e) =>
            ExportService.SavePng(this);

        private void SaveDocx_Click(object sender, RoutedEventArgs e) =>
            ExportService.SaveDocx();
    }
}