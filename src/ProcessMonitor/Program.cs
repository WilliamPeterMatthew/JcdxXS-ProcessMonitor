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
        private static Timer timer;

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
                timer?.Dispose();
                Console.WriteLine("正在退出...");
                Thread.Sleep(1000); // 等待最后一次保存
            }
        }

        // 添加缺失的方法定义
        static bool CheckWindowsVersion()
        {
            var os = Environment.OSVersion;
            if (os.Platform != PlatformID.Win32NT || os.Version.Major < 6 || (os.Version.Major == 6 && os.Version.Minor < 1))
            {
                Console.WriteLine("需要Windows 7或更高版本");
                return false;
            }
            return true;
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
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        lock (dictLock)
                        {
                            processDict[process.Id] = new ProcessRecord
                            {
                                Pid = process.Id,
                                ProcessName = GetProcessNameSafe(process),
                                StartTime = "-",
                                EndTime = "-"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"初始化时跳过进程 {process.Id}: {ex.Message}");
                    }
                }
                SaveToFile();
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
            timer = new Timer(CheckProcessChangesWrapper, null, 0, 1000);
            exitEvent.Wait();
        }

        // 修正Timer回调方法
        private static void CheckProcessChangesWrapper(object state)
        {
            try
            {
                CheckProcessChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"监控出错：{ex.Message}");
            }
        }

        static void CheckProcessChanges()
        {
            try
            {
                var currentProcesses = Process.GetProcesses();
                var currentPids = new HashSet<int>();
                int changesCount = 0;

                foreach (var process in currentProcesses)
                {
                    try
                    {
                        currentPids.Add(process.Id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"跳过无效进程: {ex.Message}");
                    }
                }

                lock (dictLock)
                {
                    // 检查退出的进程
                    var exitedPids = processDict.Keys.Except(currentPids).ToList();
                    if (exitedPids.Count > 0)
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        foreach (var pid in exitedPids)
                        {
                            processDict[pid].EndTime = timestamp;
                        }
                        changesCount += exitedPids.Count;
                    }

                    // 检查新进程
                    foreach (var process in currentProcesses)
                    {
                        try
                        {
                            if (!processDict.ContainsKey(process.Id))
                            {
                                processDict[process.Id] = new ProcessRecord
                                {
                                    Pid = process.Id,
                                    ProcessName = GetProcessNameSafe(process),
                                    StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    EndTime = "-"
                                };
                                changesCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"跳过新进程 {process.Id}: {ex.Message}");
                        }
                    }
                }

                if (changesCount > 0)
                {
                    SaveToFile();
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss} 检测到 {changesCount} 处变更");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"监控出错: {ex.Message}");
            }
        }

        static void SaveToFile()
        {
            try
            {
                List<ProcessRecord> records;
                lock (dictLock)
                {
                    records = processDict.Values.ToList();
                }

                using (var writer = new StreamWriter(logFilePath, false))
                {
                    writer.WriteLine("PID,ProcessName,StartTime,EndTime");
                    foreach (var record in records.OrderBy(r => r.Pid))
                    {
                        writer.WriteLine($"{record.Pid}," +
                                       $"{EscapeCsv(record.ProcessName)}," +
                                       $"{EscapeCsv(record.StartTime)}," +
                                       $"{EscapeCsv(record.EndTime)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存失败: {ex.Message}");
            }
        }

        static string GetProcessNameSafe(Process process)
        {
            try
            {
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }
            return input;
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
