using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ProcessMonitor
{
    static class Program
    {
        private static Dictionary<int, ProcessRecord> processDict;
        private static Timer monitoringTimer;
        private static string currentLogPath;
        private static readonly object syncLock = new object();

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }

        public static void StartMonitoring(string logPath)
        {
            currentLogPath = logPath;
            processDict = new Dictionary<int, ProcessRecord>();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            InitializeProcessLog();
            monitoringTimer = new Timer(CheckProcessUpdates, null, 0, 1000);
        }

        public static void StopMonitoring()
        {
            monitoringTimer?.Dispose();
            SaveLogToFile();
        }

        private static void InitializeProcessLog()
        {
            try
            {
                var initialProcesses = Process.GetProcesses();
                foreach (var proc in initialProcesses)
                {
                    try
                    {
                        lock (syncLock)
                        {
                            processDict[proc.Id] = new ProcessRecord
                            {
                                Pid = proc.Id,
                                ProcessName = SafeGetProcessName(proc),
                                StartTime = "-",
                                EndTime = "-"
                            };
                        }
                    }
                    catch { /* 忽略无效进程 */ }
                }
                SaveLogToFile();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("初始化进程列表失败: " + ex.Message);
            }
        }

        private static void CheckProcessUpdates(object state)
        {
            try
            {
                var currentProcesses = Process.GetProcesses();
                var activePids = new HashSet<int>();
                bool hasChanges = false;

                // 收集当前有效PID
                foreach (var proc in currentProcesses)
                {
                    try { activePids.Add(proc.Id); }
                    catch { /* 忽略无效进程 */ }
                }

                lock (syncLock)
                {
                    // 处理已退出的进程
                    var exited = processDict.Keys.Except(activePids).ToArray();
                    if (exited.Length > 0)
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        foreach (var pid in exited)
                        {
                            processDict[pid].EndTime = timestamp;
                        }
                        hasChanges = true;
                    }

                    // 处理新进程
                    foreach (var proc in currentProcesses)
                    {
                        try
                        {
                            if (!processDict.ContainsKey(proc.Id))
                            {
                                processDict[proc.Id] = new ProcessRecord
                                {
                                    Pid = proc.Id,
                                    ProcessName = SafeGetProcessName(proc),
                                    StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    EndTime = "-"
                                };
                                hasChanges = true;
                            }
                        }
                        catch { /* 忽略无效进程 */ }
                    }
                }

                if (hasChanges) SaveLogToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"监控异常: {ex}");
            }
        }

        private static void SaveLogToFile()
        {
            try
            {
                lock (syncLock)
                {
                    using var writer = new StreamWriter(currentLogPath, false);
                    writer.WriteLine("PID,ProcessName,StartTime,EndTime");
                    foreach (var record in processDict.Values.OrderBy(r => r.Pid))
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
                Debug.WriteLine($"日志保存失败: {ex}");
            }
        }

        private static string SafeGetProcessName(Process process)
        {
            try { return process.ProcessName; }
            catch { return "Unknown"; }
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }
            return input;
        }
    }
}
