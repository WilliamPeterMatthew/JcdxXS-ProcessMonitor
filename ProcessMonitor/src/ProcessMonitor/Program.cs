using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace ProcessMonitor
{
    static class Program
    {
        private static Dictionary<int, ProcessRecord> processDict = new Dictionary<int, ProcessRecord>();
        private static System.Threading.Timer? monitoringTimer;
        private static string currentLogPath = string.Empty;
        private static readonly object syncLock = new object();
        private static DateTime _monitorStartTime;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }

        public static void StartMonitoring(string logPath)
        {
            if (string.IsNullOrWhiteSpace(logPath))
                throw new ArgumentException("日志路径不能为空", nameof(logPath));

            currentLogPath = logPath;
            _monitorStartTime = DateTime.UtcNow;
            
            var dir = Path.GetDirectoryName(logPath)!;
            Directory.CreateDirectory(dir);

            InitializeProcessLog();
            monitoringTimer = new System.Threading.Timer(CheckProcessChanges, null, 0, 1000);
        }

        public static void StopMonitoring()
        {
            monitoringTimer?.Dispose();
            SaveToFile();
            SecurityPackager.PackageLogs(LogDirectory);
        }

        private static void InitializeProcessLog()
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (IsSystemProcess(process)) continue;

                        lock (syncLock)
                        {
                            processDict[process.Id] = new ProcessRecord
                            {
                                Pid = process.Id,
                                ProcessName = GetProcessNameSafe(process),
                                StartTime = FormatUtcTime(DateTime.UtcNow),
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

        private static void CheckProcessChanges(object? state)
        {
            try
            {
                var currentProcesses = Process.GetProcesses();
                var activePids = new HashSet<int>();
                bool hasChanges = false;

                foreach (var process in currentProcesses)
                {
                    try 
                    { 
                        if (IsSystemProcess(process)) continue;
                        activePids.Add(process.Id); 
                    }
                    catch { /* 忽略无效进程 */ }
                }

                lock (syncLock)
                {
                    if (processDict == null) return;

                    var exitedPids = processDict.Keys.Except(activePids).ToList();
                    if (exitedPids.Count > 0)
                    {
                        var timestamp = FormatUtcTime(DateTime.UtcNow);
                        foreach (var pid in exitedPids)
                        {
                            if (processDict.TryGetValue(pid, out var record) && record.EndTime == "-")
                            {
                                record.EndTime = timestamp;
                                hasChanges = true;
                            }
                        }
                    }

                    foreach (var process in currentProcesses)
                    {
                        try
                        {
                            if (IsSystemProcess(process)) continue;

                            if (!processDict.ContainsKey(process.Id))
                            {
                                processDict[process.Id] = new ProcessRecord
                                {
                                    Pid = process.Id,
                                    ProcessName = GetProcessNameSafe(process),
                                    StartTime = FormatUtcTime(DateTime.UtcNow),
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
                    if (string.IsNullOrEmpty(currentLogPath)) return;

                    var records = processDict.Values
                        .Select(r => new {
                            r.Pid,
                            ProcessName = Path.GetFileName(r.ProcessName),
                            r.StartTime,
                            r.EndTime
                        })
                        .ToList();

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

        private static bool IsSystemProcess(Process process)
        {
            try
            {
                var name = process.ProcessName.ToLower();
                return name.StartsWith("system") || 
                       name == "svchost" ||
                       name == "dllhost" ||
                       name == "runtimebroker" ||
                       name == "taskhostw";
            }
            catch
            {
                return true;
            }
        }

        private static string GetProcessNameSafe(Process process)
        {
            try { return process.ProcessName; }
            catch { return "Unknown"; }
        }

        private static string FormatUtcTime(DateTime time)
        {
            return time.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Contains(",") || input.Contains("\"") || input.Contains("\n") 
                ? $"\"{input.Replace("\"", "\"\"")}\"" 
                : input;
        }

        public static string LogDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProcessMonitor");
    }
}
