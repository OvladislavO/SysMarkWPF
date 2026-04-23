using SysMarkWPF.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SysMarkWPF.Services
{
    public static class ExportService
    {
        public static void SaveTxt()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "SysMarkWPF_Results",
                    DefaultExt = ".txt",
                    Filter = "Text files (.txt)|*.txt"
                };

                if (dialog.ShowDialog() != true) return;

                var lines = new List<string>
                {
                    "========================================",
                    "       SysMarkWPF — Benchmark Results  ",
                    "========================================",
                    $"Date: {DateTime.Now:dd.MM.yyyy HH:mm}",
                    "",
                    "--- CPU Mark ---",
                    BenchmarkResults.CpuCompleted
                        ? $"Total Score:     {BenchmarkResults.CpuTotalScore} pts"
                        : "Not completed",
                    BenchmarkResults.CpuCompleted
                        ? $"Math operations: {BenchmarkResults.CpuMathScore} pts  ({BenchmarkResults.CpuMathSpeed:F1} Mop/s)"
                        : "",
                    BenchmarkResults.CpuCompleted
                        ? $"Sorting:         {BenchmarkResults.CpuSortScore} pts  ({BenchmarkResults.CpuSortTime} ms)"
                        : "",
                    BenchmarkResults.CpuCompleted
                        ? $"Encryption AES:  {BenchmarkResults.CpuAesScore} pts  ({BenchmarkResults.CpuAesSpeed:F1} MB/s)"
                        : "",
                    "",
                    "--- Memory Mark ---",
                    BenchmarkResults.MemoryCompleted
                        ? $"Total Score:     {BenchmarkResults.MemoryTotalScore} pts"
                        : "Not completed",
                    BenchmarkResults.MemoryCompleted
                        ? $"Sequential Read: {BenchmarkResults.MemoryReadScore} pts  ({BenchmarkResults.MemoryReadSpeed:F1} MB/s)"
                        : "",
                    BenchmarkResults.MemoryCompleted
                        ? $"Sequential Write:{BenchmarkResults.MemoryWriteScore} pts  ({BenchmarkResults.MemoryWriteSpeed:F1} MB/s)"
                        : "",
                    BenchmarkResults.MemoryCompleted
                        ? $"Latency:         {BenchmarkResults.MemoryLatencyScore} pts  ({BenchmarkResults.MemoryLatencyNs:F2} ns)"
                        : "",
                    "",
                    "--- Disk Mark ---",
                    BenchmarkResults.DiskCompleted
                        ? $"Total Score:     {BenchmarkResults.DiskTotalScore} pts"
                        : "Not completed",
                    BenchmarkResults.DiskCompleted
                        ? $"Sequential Read: {BenchmarkResults.DiskSeqReadScore} pts  ({BenchmarkResults.DiskSeqReadSpeed:F1} MB/s)"
                        : "",
                    BenchmarkResults.DiskCompleted
                        ? $"Sequential Write:{BenchmarkResults.DiskSeqWriteScore} pts  ({BenchmarkResults.DiskSeqWriteSpeed:F1} MB/s)"
                        : "",
                    BenchmarkResults.DiskCompleted
                        ? $"Random R/W:      {BenchmarkResults.DiskRandScore} pts"
                        : "",
                    "",
                    "--- Network Mark ---",
                    BenchmarkResults.NetworkCompleted
                        ? $"Total Score:     {BenchmarkResults.NetworkTotalScore} pts"
                        : "Not completed",
                    BenchmarkResults.NetworkCompleted
                        ? $"Ping:            {BenchmarkResults.NetworkPingScore} pts  ({BenchmarkResults.NetworkAvgPing:F1} ms)"
                        : "",
                    BenchmarkResults.NetworkCompleted
                        ? $"DNS Speed:       {BenchmarkResults.NetworkDnsScore} pts  ({BenchmarkResults.NetworkAvgDns:F1} ms)"
                        : "",
                    "",
                    "========================================"
                };

                File.WriteAllLines(dialog.FileName, lines);
                MessageBox.Show("Results saved successfully!", "Save TXT",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving TXT: {ex.Message}");
            }
        }

        public static void SavePng(Page page)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "SysMarkWPF_Results",
                    DefaultExt = ".png",
                    Filter = "PNG Image (.png)|*.png"
                };

                if (dialog.ShowDialog() != true) return;

                var renderTarget = new RenderTargetBitmap(
                    (int)page.ActualWidth,
                    (int)page.ActualHeight,
                    96, 96,
                    PixelFormats.Pbgra32);

                renderTarget.Render(page);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);

                MessageBox.Show("Screenshot saved successfully!", "Save PNG",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving PNG: {ex.Message}");
            }
        }

        public static void SaveDocx()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "SysMarkWPF_Results",
                    DefaultExt = ".docx",
                    Filter = "Word Document (.docx)|*.docx"
                };

                if (dialog.ShowDialog() != true) return;

                using var doc = DocumentFormat.OpenXml.Packaging
                    .WordprocessingDocument.Create(dialog.FileName,
                    DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(
                    new DocumentFormat.OpenXml.Wordprocessing.Body());

                AddDocxParagraph(body, "SysMarkWPF — Benchmark Results",
                    fontSize: "32", bold: true);
                AddDocxParagraph(body, $"Date: {DateTime.Now:dd.MM.yyyy HH:mm}");
                AddDocxParagraph(body, "");

                AddDocxSection(body, "CPU Mark",
                    BenchmarkResults.CpuCompleted,
                    BenchmarkResults.CpuTotalScore,
                    new[]
                    {
                        ($"Math operations", $"{BenchmarkResults.CpuMathScore} pts  |  {BenchmarkResults.CpuMathSpeed:F1} Mop/s"),
                        ($"Sorting",         $"{BenchmarkResults.CpuSortScore} pts  |  {BenchmarkResults.CpuSortTime} ms"),
                        ($"Encryption AES",  $"{BenchmarkResults.CpuAesScore} pts  |  {BenchmarkResults.CpuAesSpeed:F1} MB/s"),
                    });

                AddDocxSection(body, "Memory Mark",
                    BenchmarkResults.MemoryCompleted,
                    BenchmarkResults.MemoryTotalScore,
                    new[]
                    {
                        ("Sequential Read",  $"{BenchmarkResults.MemoryReadScore} pts  |  {BenchmarkResults.MemoryReadSpeed:F1} MB/s"),
                        ("Sequential Write", $"{BenchmarkResults.MemoryWriteScore} pts  |  {BenchmarkResults.MemoryWriteSpeed:F1} MB/s"),
                        ("Latency",          $"{BenchmarkResults.MemoryLatencyScore} pts  |  {BenchmarkResults.MemoryLatencyNs:F2} ns"),
                    });

                AddDocxSection(body, "Disk Mark",
                    BenchmarkResults.DiskCompleted,
                    BenchmarkResults.DiskTotalScore,
                    new[]
                    {
                        ("Sequential Read",  $"{BenchmarkResults.DiskSeqReadScore} pts  |  {BenchmarkResults.DiskSeqReadSpeed:F1} MB/s"),
                        ("Sequential Write", $"{BenchmarkResults.DiskSeqWriteScore} pts  |  {BenchmarkResults.DiskSeqWriteSpeed:F1} MB/s"),
                        ("Random R/W",       $"{BenchmarkResults.DiskRandScore} pts"),
                    });

                AddDocxSection(body, "Network Mark",
                    BenchmarkResults.NetworkCompleted,
                    BenchmarkResults.NetworkTotalScore,
                    new[]
                    {
                        ("Ping",      $"{BenchmarkResults.NetworkPingScore} pts  |  {BenchmarkResults.NetworkAvgPing:F1} ms"),
                        ("DNS Speed", $"{BenchmarkResults.NetworkDnsScore} pts  |  {BenchmarkResults.NetworkAvgDns:F1} ms"),
                    });

                mainPart.Document.Save();

                MessageBox.Show("Results saved successfully!", "Save DOCX",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving DOCX: {ex.Message}");
            }
        }

        private static void AddDocxSection(
            DocumentFormat.OpenXml.Wordprocessing.Body body,
            string title, bool completed, int totalScore,
            (string label, string value)[] rows)
        {
            AddDocxParagraph(body, title, fontSize: "24", bold: true);
            AddDocxParagraph(body, completed
                ? $"Total Score: {totalScore} pts"
                : "Not completed");

            if (!completed) { AddDocxParagraph(body, ""); return; }

            foreach (var (label, value) in rows)
                AddDocxParagraph(body, $"  {label}: {value}");

            AddDocxParagraph(body, "");
        }

        private static void AddDocxParagraph(
            DocumentFormat.OpenXml.Wordprocessing.Body body,
            string text, string fontSize = "20", bool bold = false)
        {
            var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
            var run = new DocumentFormat.OpenXml.Wordprocessing.Run();
            var props = new DocumentFormat.OpenXml.Wordprocessing.RunProperties();

            props.AppendChild(
                new DocumentFormat.OpenXml.Wordprocessing.FontSize
                { Val = fontSize });

            if (bold)
                props.AppendChild(
                    new DocumentFormat.OpenXml.Wordprocessing.Bold());

            run.AppendChild(props);
            run.AppendChild(
                new DocumentFormat.OpenXml.Wordprocessing.Text(text)
                { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });

            para.AppendChild(run);
            body.AppendChild(para);
        }
    }
}