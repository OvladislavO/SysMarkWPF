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
            await Task.Run(() => _hardwareInfo.RefreshAll());
            LoadSystemInfo();
            LoadLastResults();
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

        private void LoadSystemInfo()
        {
            try
            {
                var cpu = _hardwareInfo.CpuList.FirstOrDefault();
                if (cpu != null)
                {
                    CpuName.Text = cpu.Name.Trim();
                    CpuCores.Text = $"{cpu.NumberOfCores} cores / {cpu.NumberOfLogicalProcessors} threads";
                    CpuSpeed.Text = $"{cpu.CurrentClockSpeed} MHz";
                }

                var totalRam = _hardwareInfo.MemoryList.Sum(m => (long)m.Capacity);
                var availableRam = _hardwareInfo.MemoryStatus.AvailablePhysical;
                RamTotal.Text = $"{totalRam / (1024 * 1024 * 1024)} GB DDR4";
                RamAvailable.Text = $"Available: {availableRam / (1024 * 1024 * 1024)} GB";
                var mem = _hardwareInfo.MemoryList.FirstOrDefault();
                RamSpeed.Text = mem != null ? $"{mem.Speed} MHz" : "—";

                var gpu = _hardwareInfo.VideoControllerList.FirstOrDefault();
                if (gpu != null)
                {
                    GpuName.Text = gpu.Name.Trim();
                    GpuVram.Text = $"{gpu.AdapterRAM / (1024 * 1024 * 1024)} GB VRAM";
                    GpuDriver.Text = $"Driver: {gpu.DriverVersion}";
                }

                var disk = _hardwareInfo.DriveList.FirstOrDefault();
                if (disk != null)
                {
                    DiskName.Text = disk.Model.Trim();
                    DiskType.Text = $"{disk.Size / (1024 * 1024 * 1024)} GB";
                    var free = System.IO.DriveInfo.GetDrives()
                        .FirstOrDefault(d => d.IsReady &&
                            d.DriveType == System.IO.DriveType.Fixed);
                    DiskFree.Text = free != null
                        ? $"Free: {free.AvailableFreeSpace / (1024 * 1024 * 1024)} GB"
                        : "—";
                }

                OsName.Text = System.Runtime.InteropServices
                    .RuntimeInformation.OSDescription;
                OsBuild.Text = $"Build: {Environment.OSVersion.Version}";
                ComputerName.Text = Environment.MachineName;
                UserName.Text = $"User: {Environment.UserName}";

                var adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (adapter != null)
                {
                    NetworkAdapter.Text = adapter.Name;
                    var ip = adapter.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a =>
                            a.Address.AddressFamily == AddressFamily.InterNetwork);
                    NetworkIp.Text = ip?.Address.ToString() ?? "—";
                }
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