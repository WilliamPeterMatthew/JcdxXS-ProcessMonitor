using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ProcessMonitor
{
    class Program
    {
        // 保持与之前提供的代码相同
        // [原有代码内容...]
        
        // 添加Windows版本检查
        static bool CheckWindowsVersion()
        {
            var os = Environment.OSVersion;
            if (os.Platform != PlatformID.Win32NT || os.Version.Major < 6 || (os.Version.Major == 6 && os.Version.Minor < 1))
            {
                Console.WriteLine("This application requires Windows 7 or later.");
                return false;
            }
            return true;
        }

        static void Main(string[] args)
        {
            if (!CheckWindowsVersion()) return;
            // [原有主程序逻辑...]
        }
    }

    public class ProcessRecord
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}
