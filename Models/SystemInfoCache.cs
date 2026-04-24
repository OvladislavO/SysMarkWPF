namespace SysMarkWPF.Models
{
    public static class SystemInfoCache
    {
        public static string CpuName { get; set; } = "—";
        public static string CpuCores { get; set; } = "—";
        public static string CpuSpeed { get; set; } = "—";

        public static string RamTotal { get; set; } = "—";
        public static string RamSpeed { get; set; } = "—";
        public static string RamAvailable { get; set; } = "—";

        public static string GpuName { get; set; } = "—";
        public static string GpuVram { get; set; } = "—";
        public static string GpuDriver { get; set; } = "—";

        public static string DiskName { get; set; } = "—";
        public static string DiskType { get; set; } = "—";
        public static string DiskFree { get; set; } = "—";

        public static string OsName { get; set; } = "—";
        public static string OsBuild { get; set; } = "—";

        public static string ComputerName { get; set; } = "—";
        public static string UserName { get; set; } = "—";

        public static string NetworkAdapter { get; set; } = "—";
        public static string NetworkIp { get; set; } = "—";

        public static bool IsLoaded { get; set; } = false;
    }
}