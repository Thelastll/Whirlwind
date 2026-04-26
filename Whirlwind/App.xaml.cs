using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Whirlwind
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool IsAutostart { get; private set; } = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyNotificationActivator>("Whirlwind.App");
            DesktopNotificationManagerCompat.RegisterActivator<MyNotificationActivator>();

            if (e.Args.Contains("--autostart")) IsAutostart = true;

            string exeDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location
            );

            Environment.CurrentDirectory = exeDir;

            AddAutostart();
        }


        private void AddAutostart()
        {
            try
            {
                string appName = "Whirlwind";
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                key.SetValue(appName, exePath);
                key.SetValue(appName, $"\"{exePath}\" --autostart");
            }
            catch (Exception ex)
            {
            }
        }

        private void RemoveAutostart()
        {
            try
            {
                string appName = "Whirlwind";

                Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (key.GetValue(appName) != null)
                    key.DeleteValue(appName);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
