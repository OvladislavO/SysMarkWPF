using SysMarkWPF.Models;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardware.Info;

namespace SysMarkWPF.Views
{
    public partial class PassMarkPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _isPaused = false;
        private SemaphoreSlim _pauseSemaphore = new SemaphoreSlim(1, 1);
        private readonly string _testFilePath = Path.Combine(
            Path.GetTempPath(), "sysmark_passmark.tmp");

        public PassMarkPage()
        {
            InitializeComponent();
            Loaded += PassMarkPage_Loaded;
        }

        private void PassMarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadExistingResults();
        }

        private void LoadExistingResults()
        {
            if (BenchmarkResults.CpuCompleted) UpdateCpuUI();
            if (BenchmarkResults.MemoryCompleted) UpdateMemoryUI();
            if (BenchmarkResults.DiskCompleted) UpdateDiskUI();
            if (BenchmarkResults.NetworkCompleted) UpdateNetworkUI();
            UpdateTotalScore();
            UpdateOverallProgress(0, "Press Run All to begin");
        }

        private async void RunAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            _isRunning = true;
            _isPaused = false;
            _cts = new CancellationTokenSource();
            BenchmarkResults.Reset();
            ResetUI();

            try
            {
                await RunCpuTests();
                if (_cts.Token.IsCancellationRequested) return;

                await RunMemoryTests();
                if (_cts.Token.IsCancellationRequested) return;

                await RunDiskTests();
                if (_cts.Token.IsCancellationRequested) return;

                await RunNetworkTests();
                if (_cts.Token.IsCancellationRequested) return;

                UpdateTotalScore();
                UpdateStatus("All tests completed!");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Tests stopped.");
            }
            finally
            {
                _isRunning = false;
                _isPaused = false;
                PauseButton.Content = "Pause";
                CleanupTestFile();

                UpdatePartialScore();
            }
        }

        private void UpdatePartialScore()
        {
            int completedCount = 0;
            double total = 0;

            if (BenchmarkResults.CpuCompleted)
            {
                total += BenchmarkResults.CpuTotalScore * 0.35;
                completedCount++;
            }
            if (BenchmarkResults.MemoryCompleted)
            {
                total += BenchmarkResults.MemoryTotalScore * 0.25;
                completedCount++;
            }
            if (BenchmarkResults.DiskCompleted)
            {
                total += BenchmarkResults.DiskTotalScore * 0.25;
                completedCount++;
            }
            if (BenchmarkResults.NetworkCompleted)
            {
                total += BenchmarkResults.NetworkTotalScore * 0.15;
                completedCount++;
            }

            if (completedCount == 0) return;

            double totalWeight = 0;
            if (BenchmarkResults.CpuCompleted) totalWeight += 0.35;
            if (BenchmarkResults.MemoryCompleted) totalWeight += 0.25;
            if (BenchmarkResults.DiskCompleted) totalWeight += 0.25;
            if (BenchmarkResults.NetworkCompleted) totalWeight += 0.15;

            int score = (int)(total / totalWeight);
            Dispatcher.Invoke(() =>
            {
                TotalScore.Text = completedCount == 4
                    ? score.ToString()
                    : $"{score} *"; 
            });
        }
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            switch (_isPaused)
            {
                case false when _isRunning:
                    _isPaused = true;
                    _pauseSemaphore.Wait(0);
                    PauseButton.Content = "Resume";
                    UpdateStatus("Paused...");
                    break;
                case true:
                    _isPaused = false;
                    _pauseSemaphore.Release();
                    PauseButton.Content = "Pause";
                    UpdateStatus("Resuming...");
                    break;
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            CleanupTestFile();
            MainWindow.Instance?.NavigateTo(new MainPage());
        }

        // ── UI helpers ─────────────────────────────────────────────

        private void UpdateStatus(string text) =>
            Dispatcher.Invoke(() => StatusText.Text = text);

        private void UpdateOverallProgress(int value, string? status = null)
        {
            Dispatcher.Invoke(() =>
            {
                OverallProgressBar.Value = value;
                OverallProgressText.Text = $"{value}%";
                if (status != null) StatusText.Text = status;
            });
        }

        private void UpdateMiniBar(string test, int value, string label)
        {
            Dispatcher.Invoke(() =>
            {
                switch (test)
                {
                    case "cpu":
                        CpuMiniBar.Value = value;
                        CpuMiniStatus.Text = value == 100 ? "✓" : label;
                        break;
                    case "mem":
                        MemMiniBar.Value = value;
                        MemMiniStatus.Text = value == 100 ? "✓" : label;
                        break;
                    case "disk":
                        DiskMiniBar.Value = value;
                        DiskMiniStatus.Text = value == 100 ? "✓" : label;
                        break;
                    case "net":
                        NetMiniBar.Value = value;
                        NetMiniStatus.Text = value == 100 ? "✓" : label;
                        break;
                }
            });
        }

        private void ResetUI()
        {
            Dispatcher.Invoke(() =>
            {
                TotalScore.Text = "—";

                CpuTotalText.Text = "— pts";
                CpuMathScoreText.Text = "— pts"; CpuMathSpeedText.Text = "— Mop/s";
                CpuSortScoreText.Text = "— pts"; CpuSortTimeText.Text = "— ms";
                CpuAesScoreText.Text = "— pts"; CpuAesSpeedText.Text = "— MB/s";

                MemTotalText.Text = "— pts";
                MemReadScoreText.Text = "— pts"; MemReadSpeedText.Text = "— MB/s";
                MemWriteScoreText.Text = "— pts"; MemWriteSpeedText.Text = "— MB/s";
                MemLatencyScoreText.Text = "— pts"; MemLatencyNsText.Text = "— ns";

                DiskTotalText.Text = "— pts";
                DiskSeqReadScoreText.Text = "— pts"; DiskSeqReadSpeedText.Text = "— MB/s";
                DiskSeqWriteScoreText.Text = "— pts"; DiskSeqWriteSpeedText.Text = "— MB/s";
                DiskRandScoreText.Text = "— pts"; DiskRandSpeedText.Text = "— MB/s";

                NetTotalText.Text = "— pts";
                NetPingScoreText.Text = "— pts"; NetPingAvgText.Text = "— ms";
                NetDnsScoreText.Text = "— pts"; NetDnsAvgText.Text = "— ms";
                NetAdapterScoreText.Text = "— pts"; NetAdapterSpeedText.Text = "— Mbps";

                CpuMiniBar.Value = 0; CpuMiniStatus.Text = "0%";
                MemMiniBar.Value = 0; MemMiniStatus.Text = "0%";
                DiskMiniBar.Value = 0; DiskMiniStatus.Text = "0%";
                NetMiniBar.Value = 0; NetMiniStatus.Text = "0%";
                OverallProgressBar.Value = 0; OverallProgressText.Text = "0%";
            });
        }

        private void UpdateCpuUI()
        {
            Dispatcher.Invoke(() =>
            {
                CpuTotalText.Text = $"{BenchmarkResults.CpuTotalScore} pts";
                CpuMathScoreText.Text = $"{BenchmarkResults.CpuMathScore} pts";
                CpuMathSpeedText.Text = $"{BenchmarkResults.CpuMathSpeed:F1} Mop/s";
                CpuSortScoreText.Text = $"{BenchmarkResults.CpuSortScore} pts";
                CpuSortTimeText.Text = $"{BenchmarkResults.CpuSortTime} ms";
                CpuAesScoreText.Text = $"{BenchmarkResults.CpuAesScore} pts";
                CpuAesSpeedText.Text = $"{BenchmarkResults.CpuAesSpeed:F1} MB/s";
            });
        }

        private void UpdateMemoryUI()
        {
            Dispatcher.Invoke(() =>
            {
                MemTotalText.Text = $"{BenchmarkResults.MemoryTotalScore} pts";
                MemReadScoreText.Text = $"{BenchmarkResults.MemoryReadScore} pts";
                MemReadSpeedText.Text = $"{BenchmarkResults.MemoryReadSpeed:F1} MB/s";
                MemWriteScoreText.Text = $"{BenchmarkResults.MemoryWriteScore} pts";
                MemWriteSpeedText.Text = $"{BenchmarkResults.MemoryWriteSpeed:F1} MB/s";
                MemLatencyScoreText.Text = $"{BenchmarkResults.MemoryLatencyScore} pts";
                MemLatencyNsText.Text = $"{BenchmarkResults.MemoryLatencyNs:F2} ns";
            });
        }

        private void UpdateDiskUI()
        {
            Dispatcher.Invoke(() =>
            {
                DiskTotalText.Text = $"{BenchmarkResults.DiskTotalScore} pts";
                DiskSeqReadScoreText.Text = $"{BenchmarkResults.DiskSeqReadScore} pts";
                DiskSeqReadSpeedText.Text = $"{BenchmarkResults.DiskSeqReadSpeed:F1} MB/s";
                DiskSeqWriteScoreText.Text = $"{BenchmarkResults.DiskSeqWriteScore} pts";
                DiskSeqWriteSpeedText.Text = $"{BenchmarkResults.DiskSeqWriteSpeed:F1} MB/s";
                DiskRandScoreText.Text = $"{BenchmarkResults.DiskRandScore} pts";
                DiskRandSpeedText.Text =
                    $"{(BenchmarkResults.DiskRandReadSpeed + BenchmarkResults.DiskRandWriteSpeed) / 2:F1} MB/s";
            });
        }

        private void UpdateNetworkUI()
        {
            Dispatcher.Invoke(() =>
            {
                NetTotalText.Text = $"{BenchmarkResults.NetworkTotalScore} pts";
                NetPingScoreText.Text = $"{BenchmarkResults.NetworkPingScore} pts";
                NetPingAvgText.Text = $"{BenchmarkResults.NetworkAvgPing:F1} ms";
                NetDnsScoreText.Text = $"{BenchmarkResults.NetworkDnsScore} pts";
                NetDnsAvgText.Text = $"{BenchmarkResults.NetworkAvgDns:F1} ms";
                NetAdapterScoreText.Text = $"{BenchmarkResults.NetworkAdapterScore} pts";
                NetAdapterSpeedText.Text = $"{BenchmarkResults.NetworkLinkSpeed} Mbps";
            });
        }

        private void UpdateTotalScore()
        {
            if (!BenchmarkResults.CpuCompleted ||
                !BenchmarkResults.MemoryCompleted ||
                !BenchmarkResults.DiskCompleted ||
                !BenchmarkResults.NetworkCompleted) return;

            int total = (int)(BenchmarkResults.CpuTotalScore * 0.35 +
                              BenchmarkResults.MemoryTotalScore * 0.25 +
                              BenchmarkResults.DiskTotalScore * 0.25 +
                              BenchmarkResults.NetworkTotalScore * 0.15);
            Dispatcher.Invoke(() => TotalScore.Text = total.ToString());
        }

        private void CleanupTestFile()
        {
            try
            {
                if (File.Exists(_testFilePath))
                    File.Delete(_testFilePath);
            }
            catch { }
        }

        private async Task CheckPause()
        {
            if (_isPaused)
                await _pauseSemaphore.WaitAsync();
            if (_isPaused)
                _pauseSemaphore.Release();
        }

        // ── CPU тесты ──────────────────────────────────────────────

        private async Task RunCpuTests()
        {
            UpdateStatus("CPU Mark: Math operations...");
            UpdateMiniBar("cpu", 5, "5%");

            var mathResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunMathTest(_cts!.Token);
            });
            BenchmarkResults.CpuMathScore = mathResult.Score;
            BenchmarkResults.CpuMathSpeed = mathResult.SpeedMops;
            UpdateMiniBar("cpu", 33, "33%");
            UpdateOverallProgress(8);
            UpdateCpuUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("CPU Mark: Sorting...");
            var sortResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunSortTest(_cts!.Token,
                    (cur, tot) =>
                    {
                        int p = 33 + (int)(cur / (double)tot * 33);
                        UpdateMiniBar("cpu", p, $"{p}%");
                        UpdateOverallProgress(8 + (int)(cur / (double)tot * 8));
                        UpdateStatus($"CPU Mark: Sorting {cur}/{tot}...");
                    });
            });
            BenchmarkResults.CpuSortScore = sortResult.Score;
            BenchmarkResults.CpuSortTime = sortResult.TimeMs;
            UpdateMiniBar("cpu", 66, "66%");
            UpdateOverallProgress(16);
            UpdateCpuUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("CPU Mark: Encryption...");
            var aesResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunAesTest(_cts!.Token);
            });
            BenchmarkResults.CpuAesScore = aesResult.Score;
            BenchmarkResults.CpuAesSpeed = aesResult.SpeedMBps;
            BenchmarkResults.CpuTotalScore = (int)(
                mathResult.Score * 0.4 +
                sortResult.Score * 0.3 +
                aesResult.Score * 0.3);
            BenchmarkResults.CpuCompleted = true;
            UpdateMiniBar("cpu", 100, "✓");
            UpdateOverallProgress(25, "CPU Mark completed!");
            UpdateCpuUI();
        }

        // ── Memory тесты ───────────────────────────────────────────

        private async Task RunMemoryTests()
        {
            UpdateStatus("Memory Mark: Sequential Read...");
            UpdateMiniBar("mem", 5, "5%");

            var readResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunMemReadTest(_cts!.Token);
            });
            BenchmarkResults.MemoryReadScore = readResult.Score;
            BenchmarkResults.MemoryReadSpeed = readResult.SpeedMBps;
            UpdateMiniBar("mem", 33, "33%");
            UpdateOverallProgress(33);
            UpdateMemoryUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Memory Mark: Sequential Write...");
            var writeResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunMemWriteTest(_cts!.Token);
            });
            BenchmarkResults.MemoryWriteScore = writeResult.Score;
            BenchmarkResults.MemoryWriteSpeed = writeResult.SpeedMBps;
            UpdateMiniBar("mem", 66, "66%");
            UpdateOverallProgress(41);
            UpdateMemoryUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Memory Mark: Latency...");
            var latResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunMemLatencyTest(_cts!.Token);
            });
            BenchmarkResults.MemoryLatencyScore = latResult.Score;
            BenchmarkResults.MemoryLatencyNs = latResult.LatencyNs;
            BenchmarkResults.MemoryTotalScore = (int)(
                readResult.Score * 0.4 +
                writeResult.Score * 0.4 +
                latResult.Score * 0.2);
            BenchmarkResults.MemoryCompleted = true;
            UpdateMiniBar("mem", 100, "✓");
            UpdateOverallProgress(50, "Memory Mark completed!");
            UpdateMemoryUI();
        }

        // ── Disk тесты ─────────────────────────────────────────────

        private async Task RunDiskTests()
        {
            UpdateStatus("Disk Mark: Sequential Read...");
            UpdateMiniBar("disk", 5, "5%");

            var seqReadResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunDiskSeqRead(_testFilePath, _cts!.Token,
                    (s, p) =>
                    {
                        int mp = (int)(p / 100.0 * 33);
                        UpdateMiniBar("disk", mp, $"{mp}%");
                        UpdateOverallProgress(50 + (int)(mp / 100.0 * 8));
                        UpdateStatus(s);
                    });
            });
            BenchmarkResults.DiskSeqReadScore = seqReadResult.Score;
            BenchmarkResults.DiskSeqReadSpeed = seqReadResult.SpeedMBps;
            UpdateMiniBar("disk", 33, "33%");
            UpdateOverallProgress(58);
            UpdateDiskUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Disk Mark: Sequential Write...");
            var seqWriteResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunDiskSeqWrite(_testFilePath, _cts!.Token,
                    (s, p) =>
                    {
                        int mp = 33 + (int)(p / 100.0 * 33);
                        UpdateMiniBar("disk", mp, $"{mp}%");
                        UpdateOverallProgress(58 + (int)(p / 100.0 * 8));
                        UpdateStatus(s);
                    });
            });
            BenchmarkResults.DiskSeqWriteScore = seqWriteResult.Score;
            BenchmarkResults.DiskSeqWriteSpeed = seqWriteResult.SpeedMBps;
            UpdateMiniBar("disk", 66, "66%");
            UpdateOverallProgress(66);
            UpdateDiskUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Disk Mark: Random Read/Write...");
            var randResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunDiskRandom(_testFilePath, _cts!.Token);
            });
            BenchmarkResults.DiskRandScore = randResult.Score;
            BenchmarkResults.DiskRandReadSpeed = randResult.ReadSpeedMBps;
            BenchmarkResults.DiskRandWriteSpeed = randResult.WriteSpeedMBps;
            BenchmarkResults.DiskTotalScore = (int)(
                seqReadResult.Score * 0.4 +
                seqWriteResult.Score * 0.4 +
                randResult.Score * 0.2);
            BenchmarkResults.DiskCompleted = true;
            UpdateMiniBar("disk", 100, "✓");
            UpdateOverallProgress(75, "Disk Mark completed!");
            UpdateDiskUI();
        }

        // ── Network тесты ──────────────────────────────────────────

        private async Task RunNetworkTests()
        {
            UpdateStatus("Network Mark: Ping test...");
            UpdateMiniBar("net", 5, "5%");
            UpdateOverallProgress(75);

            var pingResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunNetPingTest(_cts!.Token);
            });
            BenchmarkResults.NetworkPingScore = pingResult.Score;
            BenchmarkResults.NetworkAvgPing = pingResult.AvgMs;
            UpdateMiniBar("net", 33, "33%");
            UpdateOverallProgress(83);
            UpdateNetworkUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Network Mark: DNS Speed test...");
            var dnsResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunNetDnsTest(_cts!.Token);
            });
            BenchmarkResults.NetworkDnsScore = dnsResult.Score;
            BenchmarkResults.NetworkAvgDns = dnsResult.AvgMs;
            UpdateMiniBar("net", 66, "66%");
            UpdateOverallProgress(91);
            UpdateNetworkUI();

            await CheckPause();
            if (_cts!.Token.IsCancellationRequested) return;

            UpdateStatus("Network Mark: Adapter test...");
            var adapterResult = await Task.Run(async () =>
            {
                await CheckPause();
                return RunNetAdapterTest(_cts!.Token);
            });
            BenchmarkResults.NetworkAdapterScore = adapterResult.Score;
            BenchmarkResults.NetworkLinkSpeed = adapterResult.SpeedMbps;
            BenchmarkResults.NetworkTotalScore = (int)(
                pingResult.Score * 0.4 +
                dnsResult.Score * 0.4 +
                adapterResult.Score * 0.2);
            BenchmarkResults.NetworkCompleted = true;
            UpdateMiniBar("net", 100, "✓");
            UpdateOverallProgress(100, "All tests completed!");
            UpdateNetworkUI();
        }

        // ── Реализации CPU тестов ──────────────────────────────────

        private static MathResult RunMathTest(CancellationToken token)
        {
            long operations = 0;
            double result = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000 && !token.IsCancellationRequested)
            {
                for (int i = 1; i < 10000; i++)
                {
                    result += Math.Sqrt(i) * Math.Sin(i) * Math.Cos(i) / (Math.Tan(i + 1) + 1);
                    result += Math.Log(Math.Abs(result) + 1) * Math.Pow(i % 10 + 1, 1.5);
                    result += Math.Exp(result % 1) * Math.Atan(i);
                }
                operations += 10000;
            }
            sw.Stop();
            double speedMops = operations / (sw.ElapsedMilliseconds / 1000.0) / 1_000_000;
            return new MathResult
            {
                Score = (int)Math.Min(speedMops / 5.0 * 5000, 9999),
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
                    .Select(_ => rng.Next()).ToArray();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Array.Sort(arr);
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
                onProgress?.Invoke(iter + 1, iterations);
            }
            double avgMs = totalMs / (double)iterations;
            return new SortResult
            {
                Score = (int)Math.Min(400.0 / Math.Max(avgMs, 1) * 5000, 9999),
                TimeMs = totalMs
            };
        }

        private static AesResult RunAesTest(CancellationToken token)
        {
            int blockSizeMb = 64;
            int totalBlocks = 16;
            var data = new byte[blockSizeMb * 1024 * 1024];
            new Random(42).NextBytes(data);
            long totalMs = 0;
            using var aes = Aes.Create();
            aes.GenerateKey(); aes.GenerateIV();
            for (int i = 0; i < totalBlocks; i++)
            {
                if (token.IsCancellationRequested) break;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, aes.CreateEncryptor(),
                    CryptoStreamMode.Write);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
            }
            double speed = blockSizeMb * totalBlocks / (totalMs / 1000.0);
            return new AesResult
            {
                Score = (int)Math.Min(speed / 1000.0 * 5000, 9999),
                SpeedMBps = speed
            };
        }

        // ── Реализации Memory тестов ───────────────────────────────

        private static MemResult RunMemReadTest(CancellationToken token)
        {
            int blockSizeMb = 512;
            var data = new byte[blockSizeMb * 1024 * 1024];
            new Random(42).NextBytes(data);
            long totalMs = 0;
            for (int i = 0; i < 10 && !token.IsCancellationRequested; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                long sum = 0;
                for (int j = 0; j < data.Length; j += 64) sum += data[j];
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
                GC.KeepAlive(sum);
            }
            double speed = blockSizeMb * 10 / (totalMs / 1000.0);
            return new MemResult
            {
                Score = (int)Math.Min(speed / 20000.0 * 5000, 9999),
                SpeedMBps = speed
            };
        }

        private static MemResult RunMemWriteTest(CancellationToken token)
        {
            int blockSizeMb = 512;
            var data = new byte[blockSizeMb * 1024 * 1024];
            long totalMs = 0;
            for (int i = 0; i < 10 && !token.IsCancellationRequested; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Array.Fill(data, (byte)(i % 256));
                sw.Stop();
                totalMs += sw.ElapsedMilliseconds;
            }
            double speed = blockSizeMb * 10 / (totalMs / 1000.0);
            return new MemResult
            {
                Score = (int)Math.Min(speed / 20000.0 * 5000, 9999),
                SpeedMBps = speed
            };
        }

        private static LatResult RunMemLatencyTest(CancellationToken token)
        {
            int size = 64 * 1024 * 1024;
            int accesses = 10_000_000;
            var data = new int[size / sizeof(int)];
            var rng = new Random(42);
            for (int i = 0; i < data.Length; i++) data[i] = rng.Next(data.Length);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int idx = 0;
            for (int i = 0; i < accesses && !token.IsCancellationRequested; i++)
                idx = data[idx % data.Length];
            sw.Stop();
            GC.KeepAlive(idx);
            double latNs = sw.Elapsed.TotalNanoseconds / accesses;
            return new LatResult
            {
                Score = (int)Math.Min(10.0 / Math.Max(latNs, 0.1) * 5000, 9999),
                LatencyNs = latNs
            };
        }

        // ── Реализации Disk тестов ─────────────────────────────────

        private static DiskSeqResult RunDiskSeqRead(string filePath,
            CancellationToken token, Action<string, int>? onProgress = null)
        {
            int fileSizeMb = 1024;
            int bufMb = 16;
            var buf = new byte[bufMb * 1024 * 1024];
            var speeds = new List<double>();
            int iterations = 5;
            var files = Enumerable.Range(0, iterations)
                .Select(i => filePath + $".r{i}.tmp").ToArray();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int p = (int)((i + 1) / (double)iterations * 50);
                    onProgress?.Invoke($"Disk Read: Writing {i + 1}/{iterations}...", p);
                    using var fw = new FileStream(files[i], FileMode.Create,
                        FileAccess.Write, FileShare.None, buf.Length,
                        FileOptions.WriteThrough);
                    new Random().NextBytes(buf);
                    for (int c = 0; c < fileSizeMb / bufMb; c++)
                        fw.Write(buf, 0, buf.Length);
                    fw.Flush();
                }
                for (int i = 0; i < files.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int p = 50 + (int)((i + 1) / (double)iterations * 50);
                    onProgress?.Invoke($"Disk Read: Reading {i + 1}/{iterations}...", p);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using var fs = new FileStream(files[i], FileMode.Open,
                        FileAccess.Read, FileShare.None, buf.Length,
                        FileOptions.SequentialScan | FileOptions.WriteThrough);
                    while (fs.Read(buf, 0, buf.Length) > 0
                           && !token.IsCancellationRequested) { }
                    sw.Stop();
                    speeds.Add(fileSizeMb / (sw.ElapsedMilliseconds / 1000.0));
                }
            }
            finally
            {
                foreach (var f in files) try { File.Delete(f); } catch { }
            }
            speeds.Sort();
            double med = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0;
            return new DiskSeqResult
            {
                Score = (int)Math.Min(med / 5000.0 * 5000, 9999),
                SpeedMBps = med
            };
        }

        private static DiskSeqResult RunDiskSeqWrite(string filePath,
            CancellationToken token, Action<string, int>? onProgress = null)
        {
            int fileSizeMb = 512;
            int bufMb = 8;
            var buf = new byte[bufMb * 1024 * 1024];
            new Random(42).NextBytes(buf);
            int chunks = fileSizeMb / bufMb;
            var speeds = new List<double>();
            int iterations = 3;
            for (int iter = 0; iter < iterations && !token.IsCancellationRequested; iter++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var fs = new FileStream(filePath, FileMode.Create,
                    FileAccess.Write, FileShare.None, buf.Length,
                    FileOptions.WriteThrough);
                for (int i = 0; i < chunks && !token.IsCancellationRequested; i++)
                {
                    fs.Write(buf, 0, buf.Length);
                    int p = (int)((iter * chunks + i + 1) /
                                  (double)(iterations * chunks) * 100);
                    onProgress?.Invoke(
                        $"Disk Write: Iteration {iter + 1}/{iterations}...", p);
                }
                fs.Flush();
                sw.Stop();
                speeds.Add(fileSizeMb / (sw.ElapsedMilliseconds / 1000.0));
            }
            speeds.Sort();
            double med = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0;
            return new DiskSeqResult
            {
                Score = (int)Math.Min(med / 150.0 * 5000, 9999),
                SpeedMBps = med
            };
        }

        private static DiskRandResult RunDiskRandom(string filePath,
            CancellationToken token)
        {
            int fileSizeMb = 512;
            int blockSize = 64 * 1024;
            int operations = 1000;
            long fileSize = (long)fileSizeMb * 1024 * 1024;
            var rSpeeds = new List<double>();
            var wSpeeds = new List<double>();
            var rng = new Random(42);
            var buf = new byte[blockSize];
            rng.NextBytes(buf);
            for (int iter = 0; iter < 3 && !token.IsCancellationRequested; iter++)
            {
                var swW = System.Diagnostics.Stopwatch.StartNew();
                using (var fs = new FileStream(filePath, FileMode.Create,
                    FileAccess.Write, FileShare.None, blockSize,
                    FileOptions.WriteThrough))
                {
                    fs.SetLength(fileSize);
                    for (int i = 0; i < operations && !token.IsCancellationRequested; i++)
                    {
                        long pos = (long)(rng.NextDouble() * (fileSize - blockSize));
                        pos -= pos % blockSize;
                        fs.Seek(pos, SeekOrigin.Begin);
                        fs.Write(buf, 0, buf.Length);
                    }
                    fs.Flush();
                }
                swW.Stop();
                var swR = System.Diagnostics.Stopwatch.StartNew();
                using (var fs = new FileStream(filePath, FileMode.Open,
                    FileAccess.Read, FileShare.None, blockSize,
                    FileOptions.RandomAccess))
                {
                    var rbuf = new byte[blockSize];
                    for (int i = 0; i < operations && !token.IsCancellationRequested; i++)
                    {
                        long pos = (long)(rng.NextDouble() * (fileSize - blockSize));
                        pos -= pos % blockSize;
                        fs.Seek(pos, SeekOrigin.Begin);
                        fs.Read(rbuf, 0, rbuf.Length);
                    }
                }
                swR.Stop();
                double mb = operations * blockSize / (1024.0 * 1024.0);
                rSpeeds.Add(mb / (swR.ElapsedMilliseconds / 1000.0));
                wSpeeds.Add(mb / (swW.ElapsedMilliseconds / 1000.0));
            }
            rSpeeds.Sort(); wSpeeds.Sort();
            double mr = rSpeeds.Count > 0 ? rSpeeds[rSpeeds.Count / 2] : 0;
            double mw = wSpeeds.Count > 0 ? wSpeeds[wSpeeds.Count / 2] : 0;
            return new DiskRandResult
            {
                Score = (int)Math.Min((mr + mw) / 2.0 / 85.0 * 5000, 9999),
                ReadSpeedMBps = mr,
                WriteSpeedMBps = mw
            };
        }

        // ── Реализации Network тестов ──────────────────────────────

        private static NetPingResult RunNetPingTest(CancellationToken token)
        {
            var hosts = new[] { "8.8.8.8", "1.1.1.1", "google.com" };
            var times = new List<long>();
            int sent = 0, lost = 0;
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
                        else lost++;
                    }
                    catch { lost++; }
                }
            }
            if (times.Count == 0)
                return new NetPingResult { Score = 0, AvgMs = 999 };
            double avg = times.Average();
            double loss = sent > 0 ? lost / (double)sent * 100 : 100;
            int score = (int)Math.Min(
                50.0 / Math.Max(avg, 1) * 5000 * (1 - loss / 200), 9999);
            return new NetPingResult { Score = score, AvgMs = avg };
        }

        private static NetDnsResult RunNetDnsTest(CancellationToken token)
        {
            var random = new Random();
            var tlds = new[] { "com", "net", "org", "io", "co" };
            var times = new List<double>();
            for (int i = 0; i < 20 && !token.IsCancellationRequested; i++)
            {
                string domain = $"{Guid.NewGuid():N}.{tlds[random.Next(tlds.Length)]}";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try { Dns.GetHostAddresses(domain); } catch { }
                sw.Stop();
                times.Add(Math.Min(sw.Elapsed.TotalMilliseconds, 2000));
                Thread.Sleep(50);
            }
            double avg = times.Count > 0 ? times.Average() : 999;
            int score = (int)Math.Min(100.0 / Math.Max(avg, 1) * 5000, 9999);
            return new NetDnsResult { Score = score, AvgMs = avg };
        }

        private static NetAdapterResult RunNetAdapterTest(CancellationToken token)
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    !n.Description.Contains("Virtual",
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
                return new NetAdapterResult { Score = 0, SpeedMbps = 0 };

            long speedMbps = adapter.Speed / 1_000_000;
            bool isVirtual =
                adapter.Description.Contains("Virtual",
                    StringComparison.OrdinalIgnoreCase) ||
                adapter.Name.Contains("vEthernet",
                    StringComparison.OrdinalIgnoreCase);

            int score = isVirtual
                ? (int)Math.Min(speedMbps / 1000.0 * 2500, 4999)
                : (int)Math.Min(speedMbps / 1000.0 * 5000, 9999);

            return new NetAdapterResult { Score = score, SpeedMbps = speedMbps };
        }

        // ── Records ────────────────────────────────────────────────

        private record MathResult
        {
            public int Score { get; init; }
            public double SpeedMops { get; init; }
        }
        private record SortResult
        {
            public int Score { get; init; }
            public long TimeMs { get; init; }
        }
        private record AesResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
        }
        private record MemResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
        }
        private record LatResult
        {
            public int Score { get; init; }
            public double LatencyNs { get; init; }
        }
        private record DiskSeqResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
        }
        private record DiskRandResult
        {
            public int Score { get; init; }
            public double ReadSpeedMBps { get; init; }
            public double WriteSpeedMBps { get; init; }
        }
        private record NetPingResult
        {
            public int Score { get; init; }
            public double AvgMs { get; init; }
        }
        private record NetDnsResult
        {
            public int Score { get; init; }
            public double AvgMs { get; init; }
        }
        private record NetAdapterResult
        {
            public int Score { get; init; }
            public long SpeedMbps { get; init; }
        }
    }
}