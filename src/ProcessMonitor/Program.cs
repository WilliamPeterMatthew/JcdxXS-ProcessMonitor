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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }

        public static void StartMonitoring(string logPath)
        {
            currentLogPath = logPath;
            processDict = new Dictionary<int, ProcessRecord>();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            InitializeProcessLog();
            monitoringTimer = new Timer(CheckProcessChanges, null, 0, 1000);
        }

        public static void StopMonitoring()
        {
            monitoringTimer?.Dispose();
            SaveToFile();
        }

        private static void InitializeProcessLog()
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        lock (syncLock)
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
                    catch { /* 忽略无效进程 */ }
                }
                SaveToFile();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CheckProcessChanges(object state)
        {
            try
            {
                var currentProcesses = Process.GetProcesses();
                var activePids = new HashSet<int>();
                bool hasChanges = false;

                foreach (var process in currentProcesses)
                {
                    try { activePids.Add(process.Id); }
                    catch { /* 忽略无效进程 */ }
                }

                lock (syncLock)
                {
                    // 处理退出的进程
                    var exitedPids = processDict.Keys.Except(activePids).ToList();
                    if (exitedPids.Count > 0)
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        foreach (var pid in exitedPids)
                        {
                            processDict[pid].EndTime = timestamp;
                        }
                        hasChanges = true;
                    }

                    // 处理新进程
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
                                hasChanges = true;
                            }
                        }
                        catch { /* 忽略无效进程 */ }
                    }
                }

                if (hasChanges) SaveToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"监控错误: {ex}");
            }
        }

        private static void SaveToFile()
        {
            lock (syncLock)
            {
                try
                {
                    var records = processDict.Values.ToList();
                    using var writer = new StreamWriter(currentLogPath, false);
                    writer.WriteLine("PID,ProcessName,StartTime,EndTime");
                    foreach (var record in records.OrderBy(r => r.Pid))
                    {
                        writer.WriteLine($"{record.Pid}," +
                                       $"{EscapeCsv(record.ProcessName)}," +
                                       $"{EscapeCsv(record.StartTime)}," +
                                       $"{EscapeCsv(record.EndTime)}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"保存失败: {ex}");
                }
            }
        }

        private static string GetProcessNameSafe(Process process)
        {
            try { return process.ProcessName; }
            catch { return "Unknown"; }
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Contains(",") || input.Contains("\"") || input.Contains("\n") 
                ? $"\"{input.Replace("\"", "\"\"")}\"" 
                : input;
        }
    }
}
