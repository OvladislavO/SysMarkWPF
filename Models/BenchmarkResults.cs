namespace SysMarkWPF.Models
{
    public static class BenchmarkResults
    {
        // CPU
        public static int CpuTotalScore { get; set; }
        public static int CpuMathScore { get; set; }
        public static int CpuSortScore { get; set; }
        public static int CpuAesScore { get; set; }
        public static double CpuMathSpeed { get; set; }
        public static long CpuSortTime { get; set; }
        public static double CpuAesSpeed { get; set; }

        // Memory
        public static int MemoryTotalScore { get; set; }
        public static int MemoryReadScore { get; set; }
        public static int MemoryWriteScore { get; set; }
        public static int MemoryLatencyScore { get; set; }
        public static double MemoryReadSpeed { get; set; }
        public static double MemoryWriteSpeed { get; set; }
        public static double MemoryLatencyNs { get; set; }

        // Disk
        public static int DiskTotalScore { get; set; }
        public static int DiskSeqReadScore { get; set; }
        public static int DiskSeqWriteScore { get; set; }
        public static int DiskRandScore { get; set; }
        public static double DiskSeqReadSpeed { get; set; }
        public static double DiskSeqWriteSpeed { get; set; }
        public static double DiskRandReadSpeed { get; set; }
        public static double DiskRandWriteSpeed { get; set; }

        // Network
        public static int NetworkTotalScore { get; set; }
        public static int NetworkPingScore { get; set; }
        public static int NetworkDnsScore { get; set; }
        public static int NetworkAdapterScore { get; set; }
        public static double NetworkAvgPing { get; set; }
        public static double NetworkAvgDns { get; set; }
        public static long NetworkLinkSpeed { get; set; }
        public static bool NetworkCompleted { get; set; }

        // Flags
        public static bool CpuCompleted { get; set; }
        public static bool MemoryCompleted { get; set; }
        public static bool DiskCompleted { get; set; }

        public static void Reset()
        {
            CpuTotalScore = 0; CpuMathScore = 0;
            CpuSortScore = 0; CpuAesScore = 0;
            CpuMathSpeed = 0; CpuSortTime = 0; CpuAesSpeed = 0;
            MemoryTotalScore = 0; MemoryReadScore = 0;
            MemoryWriteScore = 0; MemoryLatencyScore = 0;
            MemoryReadSpeed = 0; MemoryWriteSpeed = 0; MemoryLatencyNs = 0;
            DiskTotalScore = 0; DiskSeqReadScore = 0;
            DiskSeqWriteScore = 0; DiskRandScore = 0;
            DiskSeqReadSpeed = 0; DiskSeqWriteSpeed = 0;
            DiskRandReadSpeed = 0; DiskRandWriteSpeed = 0;
            NetworkTotalScore = 0; NetworkPingScore = 0;
            NetworkDnsScore = 0; NetworkAdapterScore = 0;
            NetworkAvgPing = 0; NetworkAvgDns = 0;
            NetworkLinkSpeed = 0; 
            NetworkCompleted = false;
            CpuCompleted = false;
            MemoryCompleted = false;
            DiskCompleted = false;
        }
    }
}