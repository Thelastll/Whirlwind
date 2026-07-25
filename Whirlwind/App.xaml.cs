using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Whirlwind.Interop;
using Whirlwind.Views;

namespace Whirlwind
{
    public partial class App : Application
    {
        public static bool IsAutostart { get; private set; } = false;
        public static MainWindow MainWindowInstance;

        // -------------------- Startup --------------------

        protected override void OnStartup(StartupEventArgs e)
        {
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;

            base.OnStartup(e);

            if (e.Args.Length > 0 && e.Args[0].StartsWith("-ToastActivated"))
            {
                string arguments = e.Args[0].Substring("-ToastActivated".Length).Trim();

                if (arguments.Contains("action=open"))
                {
                    MainWindowInstance = new MainWindow();
                    MainWindowInstance.Show();
                    MainWindowInstance.Activate();
                    return;
                }
            }

            CreateStartMenuShortcut();

            if (e.Args.Contains("--autostart"))
                IsAutostart = true;

            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            Environment.CurrentDirectory = exeDir;

            AddAutostart();

            MainWindowInstance = new MainWindow();
            MainWindowInstance.Show();
        }

        private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindowInstance = (MainWindow)Application.Current.MainWindow;

                // Показать окно, если скрыто
                if (MainWindowInstance.Visibility != Visibility.Visible)
                    MainWindowInstance.Show();

                // Развернуть, если свернуто
                if (MainWindowInstance.WindowState == WindowState.Minimized)
                    MainWindowInstance.WindowState = WindowState.Normal;

                // Вывести на передний план
                MainWindowInstance.Topmost = true;
                MainWindowInstance.Topmost = false;
                MainWindowInstance.Activate();
                MainWindowInstance.Focus();

                // -----------------------------
                // ВЫЗОВ device_item_Click
                // -----------------------------

                var args = ToastArguments.Parse(e.Argument);

                if (!args.Contains("deviceId"))
                    return;

                int id = int.Parse(args["deviceId"]);

                // Ищем нужный DeviceItem
                var device = MainWindowInstance.DeviceList.Items
                    .OfType<DeviceItem>()
                    .FirstOrDefault(d => d.Id == id);

                MainWindowInstance.ChatTitle.Text = device.Name;
                MainWindowInstance.IpTitle.Text = device.Ip;
                MainWindowInstance.CurrentInterlocutor = device.Ip;
                MainWindowInstance.load_messages(device.Ip);
                MainWindowInstance.change_muted_mode(device.Ip);
                MainWindowInstance.change_blocked_mode(device.Ip);
            });
        }

        // -------------------- Ярлык Start Menu --------------------

        private void CreateStartMenuShortcut()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            string shortcutPath =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "Whirlwind.lnk");

            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);

            CreateShortcut(shortcutPath, exePath);
        }

        private void CreateShortcut(string shortcutPath, string exePath)
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath));
            link.SetDescription("Whirlwind");

            // Прописываем AppID
            var propStore = (IPropertyStore)link;

            var pv = new PROPVARIANT();
            pv.vt = 8; // VT_BSTR
            pv.p = Marshal.StringToBSTR("Whirlwind.App");

            PROPERTYKEY key = new PROPERTYKEY
            {
                fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                pid = 5
            };

            propStore.SetValue(ref key, ref pv);
            propStore.Commit();

            var persistFile = (IPersistFile)link;
            persistFile.Save(shortcutPath, true);
        }



        // -------------------- Автозапуск --------------------

        private void AddAutostart()
        {
            try
            {
                string appName = "Whirlwind";
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                key?.SetValue(appName, $"\"{exePath}\" --autostart");
            }
            catch { }
        }

        private void RemoveAutostart()
        {
            try
            {
                string appName = "Whirlwind";

                var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (key?.GetValue(appName) != null)
                    key.DeleteValue(appName);
            }
            catch { }
        }
    }
}
