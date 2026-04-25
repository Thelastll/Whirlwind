using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Whirlwind
{
    /// <summary>
    /// Логика взаимодействия для EnterWindow.xaml
    /// </summary>
    public partial class EnterWindow : Window
    {
        public EnterWindow()
        {
            InitializeComponent();
        }

        public string IpSender { get; private set; } = null;
        private bool ChangeButtonClicked = false;

        private void enter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                enter_button.IsEnabled = false;

                Native.test_ip_port_sender(ip.Text == "" ? "_" : ip.Text, Properties.Settings.Default.port_sender,
                (ptr, len) =>
                {
                    byte[] buffer = new byte[len];
                    Marshal.Copy(ptr, buffer, 0, len);
                    string result = Encoding.UTF8.GetString(buffer);

                    Dispatcher.Invoke(() =>
                    {
                        IpSender = result;
                        enter_button.IsEnabled = true;
                        ChangeButtonClicked = true;
                    });

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Close();
                    }));
                },
                (ptr, len) =>
                {
                    byte[] buffer = new byte[len];
                    Marshal.Copy(ptr, buffer, 0, len);
                    string result = Encoding.UTF8.GetString(buffer);

                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(result);
                        enter_button.IsEnabled = true;
                    });
                });
            }
            catch {
                enter_button.IsEnabled = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IpSender = Properties.Settings.Default.ip_sender == null || ChangeButtonClicked ? IpSender : Properties.Settings.Default.ip_sender;
        }
    }
}
