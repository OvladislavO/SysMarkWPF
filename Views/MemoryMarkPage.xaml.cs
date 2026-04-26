using Hardware.Info;
using SysMarkWPF.Models;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace SysMarkWPF.Views
{
    public partial class MemoryMarkPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;

        public MemoryMarkPage()
        {
            InitializeComponent();
            Loaded += MemoryMarkPage_Loaded;
        }

        private void MemoryMarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRamInfo();
            LoadPreviousResults();
        }

        private void LoadPreviousResults()
        {
            if (!BenchmarkResults.MemoryCompleted) return;
            ReadScore.Text = BenchmarkResults.MemoryReadScore.ToString();
            ReadSpeed.Text = $"{BenchmarkResults.MemoryReadSpeed:F1} MB/s";
            ReadBlock.Text = "512 MB";
            ReadTime.Text = "— ms";
            WriteScore.Text = BenchmarkResults.MemoryWriteScore.ToString();
            WriteSpeed.Text = $"{BenchmarkResults.MemoryWriteSpeed:F1} MB/s";
            WriteBlock.Text = "512 MB";
            WriteTime.Text = "— ms";
            LatencyScore.Text = BenchmarkResults.MemoryLatencyScore.ToString();
            LatencyNs.Text = $"{BenchmarkResults.MemoryLatencyNs:F2} ns";
            LatencyAccesses.Text = "10 000 000";
            LatencyTime.Text = "— ms";
            TotalScore.Text = BenchmarkResults.MemoryTotalScore.ToString();
            UpdateProgress(100);
            StatusText.Text = "Last test results loaded.";
        }

        private void LoadRamInfo()
        {
            try
            {
                var hw = new HardwareInfo();
                hw.RefreshMemoryList();
                var mem = hw.MemoryList.FirstOrDefault();
                long totalRam = hw.MemoryList.Sum(m => (long)m.Capacity);
                long availableRam = (long)hw.MemoryStatus.AvailablePhysical;

                RamNameText.Text = $"{totalRam / (1024 * 1024 * 1024)} GB DDR4";
                RamDetailsText.Text = mem != null
                    ? $"{mem.Speed} MHz  |  Available: {availableRam / (1024 * 1024 * 1024)} GB"
                    : "—";
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
                // Тест 1 — Sequential Read
                StatusText.Text = "Test 1/3: Sequential Read...";
                UpdateProgress(5);
                var readResult = await Task.Run(() => RunReadTest(_cts.Token));
                UpdateProgress(33);
                Dispatcher.Invoke(() =>
                {
                    ReadScore.Text = readResult.Score.ToString();
                    ReadSpeed.Text = $"{readResult.SpeedMBps:F1} MB/s";
                    ReadBlock.Text = $"{readResult.BlockSizeMb} MB";
                    ReadTime.Text = $"{readResult.TimeMs} ms";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 2 — Sequential Write
                StatusText.Text = "Test 2/3: Sequential Write...";
                var writeResult = await Task.Run(() => RunWriteTest(_cts.Token));
                UpdateProgress(66);
                Dispatcher.Invoke(() =>
                {
                    WriteScore.Text = writeResult.Score.ToString();
                    WriteSpeed.Text = $"{writeResult.SpeedMBps:F1} MB/s";
                    WriteBlock.Text = $"{writeResult.BlockSizeMb} MB";
                    WriteTime.Text = $"{writeResult.TimeMs} ms";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 3 — Latency
                StatusText.Text = "Test 3/3: Latency...";
                var latencyResult = await Task.Run(() => RunLatencyTest(_cts.Token));
                UpdateProgress(100);
                Dispatcher.Invoke(() =>
                {
                    LatencyScore.Text = latencyResult.Score.ToString();
                    LatencyNs.Text = $"{latencyResult.LatencyNs:F2} ns";
                    LatencyAccesses.Text = $"{latencyResult.Accesses:N0}";
                    LatencyTime.Text = $"{latencyResult.TimeMs} ms";
                });

                int total = (int)(readResult.Score * 0.4 +
                                  writeResult.Score * 0.4 +
                                  latencyResult.Score * 0.2);
                Dispatcher.Invoke(() =>
                {
                    TotalScore.Text = total.ToString();
                    StatusText.Text = "Test completed!";
                });

                BenchmarkResults.MemoryTotalScore = total;
                BenchmarkResults.MemoryReadScore = readResult.Score;
                BenchmarkResults.MemoryWriteScore = writeResult.Score;
                BenchmarkResults.MemoryLatencyScore = latencyResult.Score;
                BenchmarkResults.MemoryReadSpeed = readResult.SpeedMBps;
                BenchmarkResults.MemoryWriteSpeed = writeResult.SpeedMBps;
                BenchmarkResults.MemoryLatencyNs = latencyResult.LatencyNs;
                BenchmarkResults.MemoryCompleted = true;

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

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.NavigateTo(new MainPage());
        }

        private void SaveTxt_Click(object sender, RoutedEventArgs e) =>
    SysMarkWPF.Services.ExportService.SaveTxt();

        private void SavePng_Click(object sender, RoutedEventArgs e) =>
            SysMarkWPF.Services.ExportService.SavePng(this);

        private void SaveDocx_Click(object sender, RoutedEventArgs e) =>
            SysMarkWPF.Services.ExportService.SaveDocx();

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
            ReadScore.Text = "—"; ReadSpeed.Text = "— MB/s";
            ReadBlock.Text = "— MB"; ReadTime.Text = "— ms";
            WriteScore.Text = "—"; WriteSpeed.Text = "— MB/s";
            WriteBlock.Text = "— MB"; WriteTime.Text = "— ms";
            LatencyScore.Text = "—"; LatencyNs.Text = "— ns";
            LatencyAccesses.Text = "—"; LatencyTime.Text = "— ms";
            TotalScore.Text = "—";
            UpdateProgress(0);
        }

        // --- Тесты ---

        private static ReadResult RunReadTest(CancellationToken token)
        {
            int blockSizeMb = 512;
            int iterations = 10;
            var data = new byte[blockSizeMb * 1024 * 1024];
            new Random(42).NextBytes(data);
            long totalMs = 0;
            long totalBytes = 0;

            for (int i = 0; i < iterations; i++)
            {
                if (token.IsCancellationRequested) break;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                long sum = 0;
                for (int j = 0; j < data.Length; j += 64)
                    sum += data[j];
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
                totalBytes += data.Length;
                GC.KeepAlive(sum);
            }

            double speedMBps = totalBytes / (1024.0 * 1024.0) / (totalMs / 1000.0);
            int score = (int)Math.Min(speedMBps / 20000.0 * 5000, 9999);

            return new ReadResult
            {
                Score = score,
                SpeedMBps = speedMBps,
                BlockSizeMb = blockSizeMb,
                TimeMs = totalMs
            };
        }

        private static WriteResult RunWriteTest(CancellationToken token)
        {
            int blockSizeMb = 512;
            int iterations = 10;
            var data = new byte[blockSizeMb * 1024 * 1024];
            long totalMs = 0;
            long totalBytes = 0;

            for (int i = 0; i < iterations; i++)
            {
                if (token.IsCancellationRequested) break;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Array.Fill(data, (byte)(i % 256));
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
                totalBytes += data.Length;
            }

            double speedMBps = totalBytes / (1024.0 * 1024.0) / (totalMs / 1000.0);
            int score = (int)Math.Min(speedMBps / 20000.0 * 5000, 9999);

            return new WriteResult
            {
                Score = score,
                SpeedMBps = speedMBps,
                BlockSizeMb = blockSizeMb,
                TimeMs = totalMs
            };
        }

        private static LatencyResult RunLatencyTest(CancellationToken token)
        {
            int size = 64 * 1024 * 1024;
            int accesses = 10_000_000;
            var data = new int[size / sizeof(int)];
            var rng = new Random(42);

            for (int i = 0; i < data.Length; i++)
                data[i] = rng.Next(data.Length);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int idx = 0;
            for (int i = 0; i < accesses && !token.IsCancellationRequested; i++)
                idx = data[idx % data.Length];
            sw.Stop();
            GC.KeepAlive(idx);

            double latencyNs = sw.Elapsed.TotalNanoseconds / accesses;
            int score = (int)Math.Min(10.0 / Math.Max(latencyNs, 0.1) * 5000, 9999);

            return new LatencyResult
            {
                Score = score,
                LatencyNs = latencyNs,
                Accesses = accesses,
                TimeMs = sw.ElapsedMilliseconds
            };
        }

        // --- Records ---

        private record ReadResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
            public int BlockSizeMb { get; init; }
            public long TimeMs { get; init; }
        }

        private record WriteResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
            public int BlockSizeMb { get; init; }
            public long TimeMs { get; init; }
        }

        private record LatencyResult
        {
            public int Score { get; init; }
            public double LatencyNs { get; init; }
            public int Accesses { get; init; }
            public long TimeMs { get; init; }
        }
    }
}