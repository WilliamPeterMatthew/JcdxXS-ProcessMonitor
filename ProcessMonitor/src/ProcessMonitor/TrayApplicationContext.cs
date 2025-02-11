using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Reflection; // 添加关键引用

namespace ProcessMonitor
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private static Mutex appMutex;
        private readonly string logFilePath;

        public TrayApplicationContext()
        {
            // 单实例检测（添加Global前缀确保跨会话）
            bool createdNew;
            appMutex = new Mutex(true, @"Global\ProcessMonitor", out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // 完全退出应用程序
                Application.Exit();
                Environment.Exit(0);
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
                Icon = GetEmbeddedIcon(),
                Text = "进程监控器",
                Visible = true,
                ContextMenuStrip = CreateContextMenu()
            };

            trayIcon.ShowBalloonTip(3000, "监控启动", "后台监控已运行", ToolTipIcon.Info);

            // 启动监控
            Program.StartMonitoring(logFilePath);
        }

        private Icon GetEmbeddedIcon()
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("ProcessMonitor.Resources.app.ico");
                return new Icon(stream);
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
            
            // 立即退出不等待
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
