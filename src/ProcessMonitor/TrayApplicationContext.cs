using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

namespace ProcessMonitor
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private static Mutex instanceMutex;
        private readonly string logFilePath;

        public TrayApplicationContext()
        {
            // 单实例检测
            bool isNewInstance;
            instanceMutex = new Mutex(true, "Global\\ProcessMonitorMutex", out isNewInstance);

            if (!isNewInstance)
            {
                ShowErrorMessage("程序已经在后台运行");
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
                Icon = new Icon(GetEmbeddedResource("ProcessMonitor.app.ico")),
                Text = "进程监控器 v1.0",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            trayIcon.ShowBalloonTip(3000, "监控启动", "后台监控已开始运行", ToolTipIcon.Info);

            // 启动监控
            try
            {
                Program.StartMonitoring(logFilePath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"监控启动失败: {ex.Message}");
                ExitThread();
            }
        }

        private Icon GetEmbeddedResource(string name)
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(name);
            return new Icon(stream);
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("退出", null, ExitApplication)
            });
            return menu;
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Program.StopMonitoring();
            instanceMutex?.ReleaseMutex();
            Application.Exit();
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
