using Microsoft.Data.Sqlite;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Whirlwind
{
    /// <summary>
    /// Логика взаимодействия для EnterWindow.xaml
    /// </summary>
    public partial class EnterWindow : Window
    {
        private Native.GetBytes SendOk;
        private Native.GetBytes SendErr;
        public string IpSender { get; private set; } = null;
        private bool ChangeButtonClicked = false;

        public EnterWindow()
        {
            InitializeComponent();

            SendOk = on_send_ok;
            SendErr = on_send_err;
        }

        private void on_send_ok(IntPtr ptr, int len)
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
        }

        private void on_send_err(IntPtr ptr, int len)
        {
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);
            string result = Encoding.UTF8.GetString(buffer);

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(result);
                ip.Text = "";
                enter_button.IsEnabled = true;
            });
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (!Properties.Settings.Default.window_was_opened)
            {
                    string get_ip = $@"SELECT ip FROM Device WHERE type = '1'";

                    using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                    {
                        connection.Open();

                        using (var command = new SqliteCommand(get_ip, connection))
                        using (var reader = command.ExecuteReader())
                        {
                            reader.Read();
                            if (reader.GetString(0) == "_") return;
                            ip.Text = reader.GetString(0);
                        }
                    }

                    enter_Click(enter_button, null);
                Properties.Settings.Default.window_was_opened = true;
            }
            
        }

        private void enter_text_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                enter_Click(enter_button, null);
                e.Handled = true;
            }
        }

        private void enter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                enter_button.IsEnabled = false;

                Native.test_ip_port_sender(
                    ip.Text == "" ? "_" : ip.Text,
                    Properties.Settings.Default.port_sender,
                    SendOk,
                    SendErr
                );
            }
            catch
            {
                enter_button.IsEnabled = false;
                MessageBox.Show("Ошибка ввода!");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IpSender = Properties.Settings.Default.ip_sender == null || ChangeButtonClicked ? IpSender : Properties.Settings.Default.ip_sender;
        }
    }
}
