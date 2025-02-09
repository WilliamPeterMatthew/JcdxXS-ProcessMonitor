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
        private static readonly Dictionary<int, ProcessRecord> processDict = new Dictionary<int, ProcessRecord>();
        private static readonly string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProcessMonitor",
            "ProcessLog.csv");
        private static readonly object dictLock = new object();
        private static ManualResetEventSlim exitEvent = new ManualResetEventSlim(false);

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"日志文件路径：{logFilePath}");
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                
                if (!CheckWindowsVersion()) return;
                if (!CheckAdministrator())
                {
                    Console.WriteLine("需要管理员权限，请右键以管理员身份运行");
                    return;
                }

                InitializeProcessLog();
                StartMonitoring();

                Console.WriteLine("监控运行中，按Q退出...");
                while (Console.ReadKey(true).Key != ConsoleKey.Q)
                {
                    // 保持循环
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"致命错误：{ex}");
            }
            finally
            {
                exitEvent.Set();
                Console.WriteLine("正在退出...");
                Thread.Sleep(1000); // 等待最后一次保存
            }
        }

        static bool CheckAdministrator()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        static void InitializeProcessLog()
        {
            try
            {
                Console.WriteLine("正在初始化进程列表...");
                // [保持原有初始化逻辑]
                Console.WriteLine($"已初始化 {processDict.Count} 个进程");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化失败：{ex.Message}");
                throw;
            }
        }

        static void StartMonitoring()
        {
            var timer = new Timer(1000);
            timer.Elapsed += (sender, e) => 
            {
                try
                {
                    CheckProcessChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"监控出错：{ex.Message}");
                }
            };
            timer.Change(0, 1000);

            exitEvent.Wait();
            timer.Dispose();
        }

        static void CheckProcessChanges()
        {
            // [保持原有监控逻辑]
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} 检测到 {changesCount} 处变更");
        }

        static void SaveToFile()
        {
            try
            {
                // [保持原有保存逻辑]
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} 成功保存日志");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存失败：{ex.Message}");
            }
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
