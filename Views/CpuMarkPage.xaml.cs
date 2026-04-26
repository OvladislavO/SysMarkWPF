using Hardware.Info;
using SysMarkWPF.Models;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace SysMarkWPF.Views
{
    public partial class CpuMarkPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;

        public CpuMarkPage()
        {
            InitializeComponent();
            Loaded += CpuMarkPage_Loaded;
        }

        private void CpuMarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            try { LoadCpuInfo(); } catch { }
            try { LoadPreviousResults(); } catch { }
        }

        private void LoadPreviousResults()
        {
            if (!BenchmarkResults.CpuCompleted) return;

            MathScore.Text = BenchmarkResults.CpuMathScore.ToString();
            MathTime.Text = "— ms";
            MathOps.Text = "—";
            MathSpeed.Text = $"{BenchmarkResults.CpuMathSpeed:F1} Mop/s";
            SortScore.Text = BenchmarkResults.CpuSortScore.ToString();
            SortTime.Text = $"{BenchmarkResults.CpuSortTime} ms";
            SortSize.Text = "500 000 000";
            AesScore.Text = BenchmarkResults.CpuAesScore.ToString();
            AesTime.Text = "— ms";
            AesDataSize.Text = "1024 MB";
            AesSpeed.Text = $"{BenchmarkResults.CpuAesSpeed:F1} MB/s";
            TotalScore.Text = BenchmarkResults.CpuTotalScore.ToString();
            UpdateProgress(100);
            StatusText.Text = "Last test results loaded.";
        }

        private void LoadCpuInfo()
        {
            try
            {
                var hw = new HardwareInfo();
                hw.RefreshCPUList();
                var cpu = hw.CpuList.FirstOrDefault();
                if (cpu != null)
                {
                    CpuNameText.Text = cpu.Name.Trim();
                    CpuDetailsText.Text =
                        $"{cpu.NumberOfCores} cores / {cpu.NumberOfLogicalProcessors} threads  |  {cpu.CurrentClockSpeed} MHz";
                }
                else
                {
                    CpuNameText.Text = SystemInfoCache.CpuName;
                    CpuDetailsText.Text =
                        $"{SystemInfoCache.CpuCores}  |  {SystemInfoCache.CpuSpeed}";
                }
            }
            catch
            {
                CpuNameText.Text = SystemInfoCache.CpuName;
                CpuDetailsText.Text =
                    $"{SystemInfoCache.CpuCores}  |  {SystemInfoCache.CpuSpeed}";
            }
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
                // Тест 1 — Math
                StatusText.Text = "Test 1/3: Math operations...";
                UpdateProgress(5);
                var mathResult = await Task.Run(() => RunMathTest(_cts.Token));
                UpdateProgress(33);
                Dispatcher.Invoke(() =>
                {
                    MathScore.Text = mathResult.Score.ToString();
                    MathTime.Text = $"{mathResult.TimeMs} ms";
                    MathOps.Text = $"{mathResult.Operations:N0}";
                    MathSpeed.Text = $"{mathResult.SpeedMops:F1} Mop/s";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 2 — Sorting
                StatusText.Text = "Test 2/3: Sorting (100 iterations × 5M elements)...";
                var sortResult = await Task.Run(() => RunSortTest(_cts.Token,
                    (current, total) =>
                    {
                        int progress = 33 + (int)(current / (double)total * 33);
                        UpdateProgress(progress);
                        Dispatcher.Invoke(() =>
                            StatusText.Text = $"Test 2/3: Sorting... {current}/{total}");
                    }));
                UpdateProgress(66);
                Dispatcher.Invoke(() =>
                {
                    SortScore.Text = sortResult.Score.ToString();
                    SortTime.Text = $"{sortResult.TimeMs} ms";
                    SortSize.Text = $"{sortResult.ArraySize:N0}";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 3 — AES
                StatusText.Text = "Test 3/3: Encryption 1 GB...";
                var aesResult = await Task.Run(() => RunAesTest(_cts.Token));
                UpdateProgress(100);
                Dispatcher.Invoke(() =>
                {
                    AesScore.Text = aesResult.Score.ToString();
                    AesTime.Text = $"{aesResult.TimeMs} ms";
                    AesDataSize.Text = $"{aesResult.DataSizeMb} MB";
                    AesSpeed.Text = $"{aesResult.SpeedMBps:F1} MB/s";
                });

                int total = (int)(mathResult.Score * 0.4 +
                                  sortResult.Score * 0.3 +
                                  aesResult.Score * 0.3);
                Dispatcher.Invoke(() =>
                {
                    TotalScore.Text = total.ToString();
                    StatusText.Text = "Test completed!";
                });

                BenchmarkResults.CpuTotalScore = total;
                BenchmarkResults.CpuMathScore = mathResult.Score;
                BenchmarkResults.CpuSortScore = sortResult.Score;
                BenchmarkResults.CpuAesScore = aesResult.Score;
                BenchmarkResults.CpuMathSpeed = mathResult.SpeedMops;
                BenchmarkResults.CpuSortTime = sortResult.TimeMs;
                BenchmarkResults.CpuAesSpeed = aesResult.SpeedMBps;
                BenchmarkResults.CpuCompleted = true;
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
            MathScore.Text = "—"; MathTime.Text = "— ms";
            MathOps.Text = "—"; MathSpeed.Text = "— Mop/s";
            SortScore.Text = "—"; SortTime.Text = "— ms";
            SortSize.Text = "—";
            AesScore.Text = "—"; AesTime.Text = "— ms";
            AesDataSize.Text = "— MB"; AesSpeed.Text = "— MB/s";
            TotalScore.Text = "—";
            UpdateProgress(0);
        }

        // --- Тесты ---

        private static MathResult RunMathTest(CancellationToken token)
        {
            long operations = 0;
            double result = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 3000 && !token.IsCancellationRequested)
            {
                for (int i = 1; i < 10000; i++)
                {
                    result += Math.Sqrt(i) * Math.Sin(i) * Math.Cos(i) /
                              (Math.Tan(i + 1) + 1);
                    result += Math.Log(Math.Abs(result) + 1) *
                              Math.Pow(i % 10 + 1, 1.5);
                    result += Math.Exp(result % 1) * Math.Atan(i);
                }
                operations += 10000;
            }

            sw.Stop();
            double speedMops = operations / (sw.ElapsedMilliseconds / 1000.0) / 1_000_000;
            int score = (int)Math.Min(speedMops / 5.0 * 5000, 9999);

            return new MathResult
            {
                Score = score,
                TimeMs = sw.ElapsedMilliseconds,
                Operations = operations,
                SpeedMops = speedMops
            };
        }

        private static SortResult RunSortTest(CancellationToken token,
            Action<int, int>? onProgress = null)
        {
            int arraySize = 5_000_000;
            int iterations = 100;
            long totalMs = 0;

            for (int iter = 0; iter < iterations; iter++)
            {
                if (token.IsCancellationRequested) break;

                var rng = new Random(iter);
                var arr = Enumerable.Range(0, arraySize)
                                    .Select(_ => rng.Next())
                                    .ToArray();

                var sw = System.Diagnostics.Stopwatch.StartNew();
                Array.Sort(arr);
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;

                onProgress?.Invoke(iter + 1, iterations);
            }

            double avgMs = totalMs / (double)iterations;
            int score = (int)Math.Min(400.0 / Math.Max(avgMs, 1) * 5000, 9999);

            return new SortResult
            {
                Score = score,
                TimeMs = totalMs,
                ArraySize = arraySize * iterations
            };
        }

        private static AesResult RunAesTest(CancellationToken token)
        {
            int blockSizeMb = 64;
            int totalBlocks = 16;
            int dataSizeMb = blockSizeMb * totalBlocks;
            var data = new byte[blockSizeMb * 1024 * 1024];
            new Random(42).NextBytes(data);
            long totalMs = 0;

            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            for (int i = 0; i < totalBlocks; i++)
            {
                if (token.IsCancellationRequested) break;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var ms = new System.IO.MemoryStream();
                using var cs = new CryptoStream(ms, aes.CreateEncryptor(),
                    CryptoStreamMode.Write);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
            }

            double speedMBps = dataSizeMb / (totalMs / 1000.0);
            int score = (int)Math.Min(speedMBps / 1000.0 * 5000, 9999);

            return new AesResult
            {
                Score = score,
                TimeMs = totalMs,
                DataSizeMb = dataSizeMb,
                SpeedMBps = speedMBps
            };
        }

        // --- Records ---

        private record MathResult
        {
            public int Score { get; init; }
            public long TimeMs { get; init; }
            public long Operations { get; init; }
            public double SpeedMops { get; init; }
        }

        private record SortResult
        {
            public int Score { get; init; }
            public long TimeMs { get; init; }
            public long ArraySize { get; init; }
        }

        private record AesResult
        {
            public int Score { get; init; }
            public long TimeMs { get; init; }
            public int DataSizeMb { get; init; }
            public double SpeedMBps { get; init; }
        }
    }
}