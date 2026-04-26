using Hardware.Info;
using SysMarkWPF.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SysMarkWPF.Views
{
    public partial class DiskMarkPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly string _testFilePath = Path.Combine(
            Path.GetTempPath(), "sysmark_disktest.tmp");

        public DiskMarkPage()
        {
            InitializeComponent();
            Loaded += DiskMarkPage_Loaded;
        }

        private void DiskMarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDiskInfo();
            LoadPreviousResults();
        }

        private void LoadPreviousResults()
        {
            if (!BenchmarkResults.DiskCompleted) return;
            SeqReadScore.Text = BenchmarkResults.DiskSeqReadScore.ToString();
            SeqReadSpeed.Text = $"{BenchmarkResults.DiskSeqReadSpeed:F1} MB/s";
            SeqReadFile.Text = "1024 MB";
            SeqReadTime.Text = "— ms";
            SeqWriteScore.Text = BenchmarkResults.DiskSeqWriteScore.ToString();
            SeqWriteSpeed.Text = $"{BenchmarkResults.DiskSeqWriteSpeed:F1} MB/s";
            SeqWriteFile.Text = "512 MB";
            SeqWriteTime.Text = "— ms";
            RandScore.Text = BenchmarkResults.DiskRandScore.ToString();
            RandReadSpeed.Text = $"{BenchmarkResults.DiskRandReadSpeed:F1} MB/s";
            RandWriteSpeed.Text = $"{BenchmarkResults.DiskRandWriteSpeed:F1} MB/s";
            RandTime.Text = "— ms";
            TotalScore.Text = BenchmarkResults.DiskTotalScore.ToString();
            UpdateProgress(100);
            StatusText.Text = "Last test results loaded.";
        }

        private void LoadDiskInfo()
        {
            try
            {
                var hw = new HardwareInfo();
                hw.RefreshDriveList();
                var disk = hw.DriveList.FirstOrDefault();
                var drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

                if (disk != null)
                {
                    DiskNameText.Text = disk.Model.Trim();
                    string free = drive != null
                        ? $"{drive.AvailableFreeSpace / (1024 * 1024 * 1024)} GB free"
                        : "—";
                    DiskDetailsText.Text =
                        $"{disk.Size / (1024 * 1024 * 1024)} GB  |  {free}";
                }
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
                StatusText.Text = "Test 1/3: Preparing Sequential Read...";
                UpdateProgress(5);
                var seqReadResult = await Task.Run(() =>
                    RunSeqReadTest(_testFilePath, _cts.Token,
                        (status, progress) => Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = status;
                            TestProgressBar.Value = progress;
                            ProgressText.Text = $"{progress}%";
                        })));
                UpdateProgress(33);
                Dispatcher.Invoke(() =>
                {
                    SeqReadScore.Text = seqReadResult.Score.ToString();
                    SeqReadSpeed.Text = $"{seqReadResult.SpeedMBps:F1} MB/s";
                    SeqReadFile.Text = $"{seqReadResult.FileSizeMb} MB";
                    SeqReadTime.Text = $"{seqReadResult.TimeMs} ms";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 2 — Sequential Write
                StatusText.Text = "Test 2/3: Sequential Write...";
                UpdateProgress(33);
                var seqWriteResult = await Task.Run(() =>
                    RunSeqWriteTest(_testFilePath, _cts.Token,
                        (status, progress) => Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = status;
                            TestProgressBar.Value = progress;
                            ProgressText.Text = $"{progress}%";
                        })));
                UpdateProgress(66);
                Dispatcher.Invoke(() =>
                {
                    SeqWriteScore.Text = seqWriteResult.Score.ToString();
                    SeqWriteSpeed.Text = $"{seqWriteResult.SpeedMBps:F1} MB/s";
                    SeqWriteFile.Text = $"{seqWriteResult.FileSizeMb} MB";
                    SeqWriteTime.Text = $"{seqWriteResult.TimeMs} ms";
                });

                if (_cts.Token.IsCancellationRequested) return;

                // Тест 3 — Random Read/Write
                StatusText.Text = "Test 3/3: Random Read/Write...";
                var randResult = await Task.Run(() =>
                    RunRandomTest(_testFilePath, _cts.Token));
                UpdateProgress(100);
                Dispatcher.Invoke(() =>
                {
                    RandScore.Text = randResult.Score.ToString();
                    RandReadSpeed.Text = $"{randResult.ReadSpeedMBps:F1} MB/s";
                    RandWriteSpeed.Text = $"{randResult.WriteSpeedMBps:F1} MB/s";
                    RandTime.Text = $"{randResult.TimeMs} ms";
                });

                int total = (int)(seqReadResult.Score * 0.4 +
                                  seqWriteResult.Score * 0.4 +
                                  randResult.Score * 0.2);
                Dispatcher.Invoke(() =>
                {
                    TotalScore.Text = total.ToString();
                    StatusText.Text = "Test completed!";
                });

                BenchmarkResults.DiskTotalScore = total;
                BenchmarkResults.DiskSeqReadScore = seqReadResult.Score;
                BenchmarkResults.DiskSeqWriteScore = seqWriteResult.Score;
                BenchmarkResults.DiskRandScore = randResult.Score;
                BenchmarkResults.DiskSeqReadSpeed = seqReadResult.SpeedMBps;
                BenchmarkResults.DiskSeqWriteSpeed = seqWriteResult.SpeedMBps;
                BenchmarkResults.DiskRandReadSpeed = randResult.ReadSpeedMBps;
                BenchmarkResults.DiskRandWriteSpeed = randResult.WriteSpeedMBps;
                BenchmarkResults.DiskCompleted = true;
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Test stopped.";
                UpdateProgress(0);
            }
            finally
            {
                _isRunning = false;
                CleanupTestFile();
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
            CleanupTestFile();
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
            SeqReadScore.Text = "—"; SeqReadSpeed.Text = "— MB/s";
            SeqReadFile.Text = "— MB"; SeqReadTime.Text = "— ms";
            SeqWriteScore.Text = "—"; SeqWriteSpeed.Text = "— MB/s";
            SeqWriteFile.Text = "— MB"; SeqWriteTime.Text = "— ms";
            RandScore.Text = "—"; RandReadSpeed.Text = "— MB/s";
            RandWriteSpeed.Text = "— MB/s"; RandTime.Text = "— ms";
            TotalScore.Text = "—";
            UpdateProgress(0);
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

        // --- Тесты ---

        private static SeqReadResult RunSeqReadTest(
            string filePath, CancellationToken token,
            Action<string, int>? onProgress = null)
        {
            int fileSizeMb = 1024;
            int bufferSizeMb = 16;
            var buffer = new byte[bufferSizeMb * 1024 * 1024];
            var speeds = new List<double>();
            int iterations = 5;

            var testFiles = Enumerable.Range(0, iterations)
                .Select(i => filePath + $".read{i}.tmp")
                .ToArray();

            try
            {
                // Пишем файлы
                for (int i = 0; i < testFiles.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int prog = 5 + (int)((i + 1) / (double)iterations * 10);
                    onProgress?.Invoke(
                        $"Test 1/3: Writing file {i + 1}/{iterations}...", prog);
                    using var fw = new FileStream(
                        testFiles[i], FileMode.Create, FileAccess.Write,
                        FileShare.None, buffer.Length, FileOptions.WriteThrough);
                    new Random().NextBytes(buffer);
                    for (int c = 0; c < fileSizeMb / bufferSizeMb; c++)
                        fw.Write(buffer, 0, buffer.Length);
                    fw.Flush();
                }

                // Читаем и собираем скорости
                for (int i = 0; i < testFiles.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int prog = 15 + (int)((i + 1) / (double)iterations * 18);
                    onProgress?.Invoke(
                        $"Test 1/3: Reading file {i + 1}/{iterations}...", prog);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using var fs = new FileStream(
                        testFiles[i], FileMode.Open, FileAccess.Read,
                        FileShare.None, buffer.Length,
                        FileOptions.SequentialScan | FileOptions.WriteThrough);
                    while (fs.Read(buffer, 0, buffer.Length) > 0
                           && !token.IsCancellationRequested) { }
                    sw.Stop();
                    speeds.Add(fileSizeMb / (sw.ElapsedMilliseconds / 1000.0));
                }
            }
            finally
            {
                foreach (var f in testFiles)
                    try { File.Delete(f); } catch { }
            }

            speeds.Sort();
            double medianSpeed = speeds[speeds.Count / 2];
            long totalMs = (long)(fileSizeMb / medianSpeed * 1000 * iterations);
            int score = (int)Math.Min(medianSpeed / 5000.0 * 5000, 9999);

            return new SeqReadResult
            {
                Score = score,
                SpeedMBps = medianSpeed,
                FileSizeMb = fileSizeMb,
                TimeMs = totalMs
            };
        }

        private static SeqWriteResult RunSeqWriteTest(
            string filePath, CancellationToken token,
            Action<string, int>? onProgress = null)
        {
            int fileSizeMb = 512;
            int bufferSizeMb = 8;
            var buffer = new byte[bufferSizeMb * 1024 * 1024];
            new Random(42).NextBytes(buffer);
            int chunks = fileSizeMb / bufferSizeMb;
            var speeds = new List<double>();
            int iterations = 3;

            for (int iter = 0; iter < iterations && !token.IsCancellationRequested; iter++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (var fs = new FileStream(
                    filePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, buffer.Length, FileOptions.WriteThrough))
                {
                    for (int i = 0; i < chunks && !token.IsCancellationRequested; i++)
                    {
                        fs.Write(buffer, 0, buffer.Length);
                        int prog = 33 + (int)(
                            (iter * chunks + i + 1) /
                            (double)(iterations * chunks) * 33);
                        onProgress?.Invoke(
                            $"Test 2/3: Write iteration {iter + 1}/{iterations}...",
                            prog);
                    }
                    fs.Flush();
                }
                sw.Stop();
                speeds.Add(fileSizeMb / (sw.ElapsedMilliseconds / 1000.0));
            }

            speeds.Sort();
            double medianSpeed = speeds[speeds.Count / 2];
            long totalMs = (long)(fileSizeMb / medianSpeed * 1000 * iterations);
            int score = (int)Math.Min(medianSpeed / 150.0 * 5000, 9999);

            return new SeqWriteResult
            {
                Score = score,
                SpeedMBps = medianSpeed,
                FileSizeMb = fileSizeMb,
                TimeMs = totalMs
            };
        }

        private static RandomResult RunRandomTest(
            string filePath, CancellationToken token)
        {
            int fileSizeMb = 512;
            int blockSize = 64 * 1024;
            int operations = 1000;
            long fileSize = (long)fileSizeMb * 1024 * 1024;
            var readSpeeds = new List<double>();
            var writeSpeeds = new List<double>();
            int iterations = 3;
            var rng = new Random(42);
            var buffer = new byte[blockSize];
            rng.NextBytes(buffer);

            for (int iter = 0; iter < iterations && !token.IsCancellationRequested; iter++)
            {
                // Random Write
                var swWrite = System.Diagnostics.Stopwatch.StartNew();
                using (var fs = new FileStream(
                    filePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, blockSize, FileOptions.WriteThrough))
                {
                    fs.SetLength(fileSize);
                    for (int i = 0; i < operations && !token.IsCancellationRequested; i++)
                    {
                        long maxPos = fileSize - blockSize;
                        long pos = (long)(rng.NextDouble() * maxPos);
                        pos -= pos % blockSize;
                        fs.Seek(pos, SeekOrigin.Begin);
                        fs.Write(buffer, 0, buffer.Length);
                    }
                    fs.Flush();
                }
                swWrite.Stop();

                // Random Read
                var swRead = System.Diagnostics.Stopwatch.StartNew();
                using (var fs = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read,
                    FileShare.None, blockSize, FileOptions.RandomAccess))
                {
                    var readBuf = new byte[blockSize];
                    for (int i = 0; i < operations && !token.IsCancellationRequested; i++)
                    {
                        long maxPos = fileSize - blockSize;
                        long pos = (long)(rng.NextDouble() * maxPos);
                        pos -= pos % blockSize;
                        fs.Seek(pos, SeekOrigin.Begin);
                        fs.Read(readBuf, 0, readBuf.Length);
                    }
                }
                swRead.Stop();

                double totalDataMb = operations * blockSize / (1024.0 * 1024.0);
                readSpeeds.Add(totalDataMb / (swRead.ElapsedMilliseconds / 1000.0));
                writeSpeeds.Add(totalDataMb / (swWrite.ElapsedMilliseconds / 1000.0));
            }

            readSpeeds.Sort();
            writeSpeeds.Sort();
            double medianRead = readSpeeds[readSpeeds.Count / 2];
            double medianWrite = writeSpeeds[writeSpeeds.Count / 2];
            long totalMs = (long)((readSpeeds.Sum() + writeSpeeds.Sum()) * 1000);
            int score = (int)Math.Min(
                (medianRead + medianWrite) / 2.0 / 85.0 * 5000, 9999);

            return new RandomResult
            {
                Score = score,
                ReadSpeedMBps = medianRead,
                WriteSpeedMBps = medianWrite,
                TimeMs = totalMs
            };
        }

        // --- Records ---

        private record SeqReadResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
            public int FileSizeMb { get; init; }
            public long TimeMs { get; init; }
        }

        private record SeqWriteResult
        {
            public int Score { get; init; }
            public double SpeedMBps { get; init; }
            public int FileSizeMb { get; init; }
            public long TimeMs { get; init; }
        }

        private record RandomResult
        {
            public int Score { get; init; }
            public double ReadSpeedMBps { get; init; }
            public double WriteSpeedMBps { get; init; }
            public long TimeMs { get; init; }
        }
    }
}