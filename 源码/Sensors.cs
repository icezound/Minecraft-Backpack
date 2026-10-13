// 温度监测: GPU 用 nvidia-smi, CPU 用 Windows 性能计数器里的 ACPI 热区。
using System;
using System.Diagnostics;
using System.IO;

namespace MCBag
{
    public static class Sensors
    {
        public static string CpuText = "--";
        public static string GpuText = "--";
        public static string GpuName = "";

        static string smiPath;
        static bool smiSearched;
        static string zoneInstance;
        static bool zoneSearched;
        static readonly object gate = new object();

        /// <summary>刷新一次读数 (放后台线程里调, 别卡界面)。</summary>
        public static void Refresh()
        {
            string gpu = ReadGpu();
            string cpu = ReadCpu();
            lock (gate)
            {
                if (gpu != null) GpuText = gpu;
                if (cpu != null) CpuText = cpu;
            }
        }

        // ---------------------------------------------------------- GPU
        static string FindSmi()
        {
            if (smiSearched) return smiPath;
            smiSearched = true;
            string[] candidates = new string[]
            {
                Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe"),
            };
            foreach (string path in candidates)
            {
                try { if (File.Exists(path)) { smiPath = path; return smiPath; } }
                catch (Exception) { }
            }
            return null;
        }

        static string ReadGpu()
        {
            string smi = FindSmi();
            if (smi == null) return null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(smi, "--query-gpu=name,temperature.gpu --format=csv,noheader,nounits");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                using (Process proc = Process.Start(info))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(2500)) { try { proc.Kill(); } catch (Exception) { } return null; }
                    string[] lines = output.Split('\n');
                    foreach (string raw in lines)
                    {
                        string line = raw.Trim();
                        if (line.Length == 0) continue;
                        string[] parts = line.Split(',');
                        if (parts.Length < 2) continue;
                        string value = parts[parts.Length - 1].Trim();
                        int temp;
                        if (!int.TryParse(value, out temp)) continue;
                        GpuName = parts[0].Trim();
                        return temp + "°";
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        // ---------------------------------------------------------- CPU
        static string ReadCpu()
        {
            try
            {
                if (!zoneSearched)
                {
                    zoneSearched = true;
                    if (PerformanceCounterCategory.Exists("Thermal Zone Information"))
                    {
                        string[] names = new PerformanceCounterCategory("Thermal Zone Information").GetInstanceNames();
                        if (names != null && names.Length > 0) zoneInstance = names[0];
                    }
                }
                if (zoneInstance == null) return null;
                using (PerformanceCounter counter = new PerformanceCounter("Thermal Zone Information", "Temperature", zoneInstance, true))
                {
                    counter.NextValue();                     // 第一次读总是 0, 丢掉
                    System.Threading.Thread.Sleep(60);
                    float kelvin = counter.NextValue();
                    if (kelvin <= 0) return null;
                    double celsius = kelvin - 273.15;
                    if (celsius < -20 || celsius > 150) return null;
                    return ((int)Math.Round(celsius)) + "°";
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
