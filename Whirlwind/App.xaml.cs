using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Whirlwind.Interop;
using Whirlwind.Views;

namespace Whirlwind
{
    public partial class App : Application
    {
        public static bool IsAutostart { get; private set; } = false;

        public static MainWindow MainWindowInstance;
        
        private static Mutex _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;

            _singleInstanceMutex = new Mutex(true, "WhirlwindSingleInstanceMutex", out createdNew);

            if (!createdNew)
            {
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);

            CreateStartMenuShortcut();

            if (e.Args.Contains("--autostart"))
                IsAutostart = true;

            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            Environment.CurrentDirectory = exeDir;

            AddAutostart();

            MainWindowInstance = new MainWindow();
            Application.Current.MainWindow = MainWindowInstance;
            MainWindowInstance.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }


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
        }

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
