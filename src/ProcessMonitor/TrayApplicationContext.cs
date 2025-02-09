using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

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
            bool isNewInstance;
            appMutex = new Mutex(true, "Global\\ProcessMonitor", out isNewInstance);

            if (!isNewInstance)
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
                Icon = new Icon(GetType().Assembly.GetManifestResourceStream("ProcessMonitor.app.ico")),
                Text = "进程监控器",
                Visible = true,
                ContextMenuStrip = CreateContextMenu()
            };

            // 显示启动提示
            trayIcon.ShowBalloonTip(3000, "进程监控", "监控已后台运行", ToolTipIcon.Info);

            // 启动监控
            Program.StartMonitoring(logFilePath);
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("退出", null, OnExit);
            return menu;
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Program.StopMonitoring();
            appMutex?.ReleaseMutex();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            trayIcon?.Dispose();
            base.Dispose(disposing);
        }
    }
}
