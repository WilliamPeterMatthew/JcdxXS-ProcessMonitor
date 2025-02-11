using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Reflection;

namespace ProcessMonitor
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private static Mutex appMutex;
        private readonly string logFilePath;

        public TrayApplicationContext()
        {
            // 单实例检测
            bool createdNew;
            appMutex = new Mutex(true, @"Global\ProcessMonitor", out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExitThread();
                return;
            }

            // 初始化日志路径
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProcessMonitor",
                $"ProcessLog_{timestamp}.csv");

            // 创建托盘图标
            trayIcon = new NotifyIcon
            {
                Icon = GetEmbeddedIcon("ProcessMonitor.Resources.app.ico"),
                Text = "进程监控器",
                Visible = true,
                ContextMenuStrip = CreateContextMenu()
            };

            trayIcon.ShowBalloonTip(3000, "监控启动", "后台监控已运行", ToolTipIcon.Info);

            // 启动监控
            Program.StartMonitoring(logFilePath);
        }

        private Icon GetEmbeddedIcon(string resourceName)
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                return stream != null ? new Icon(stream) : SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(exitItem);
            return menu;
        }

        private void ExitApplication()
        {
            trayIcon.Visible = false;
            Program.StopMonitoring();
            appMutex?.ReleaseMutex();
            Application.Exit();
            Environment.Exit(0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                appMutex?.Close();
            }
            base.Dispose(disposing);
        }
    }
}
