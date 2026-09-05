using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace Whirlwind
{
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

            this.ShowInTaskbar = true;
        }
        private string GetFullIp()
        {
            string a = ip1.Text.Trim();
            string b = ip2.Text.Trim();
            string c = ip3.Text.Trim();
            string d = ip4.Text.Trim();

            if (a == "" || b == "" || c == "" || d == "")
                return "_";

            return $"{a}.{b}.{c}.{d}";
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

                ip1.Text = "";
                ip2.Text = "";
                ip3.Text = "";
                ip4.Text = "";

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
                        string full = reader.GetString(0);

                        if (full == "_") return;

                        var parts = full.Split('.');
                        if (parts.Length == 4)
                        {
                            ip1.Text = parts[0];
                            ip2.Text = parts[1];
                            ip3.Text = parts[2];
                            ip4.Text = parts[3];
                        }
                    }
                }

                enter_Click(enter_button, null);
                Properties.Settings.Default.window_was_opened = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                string text = Clipboard.GetText().Trim();

                if (string.IsNullOrWhiteSpace(text))
                    return;

                var parts = text.Split('.');

                if (parts.Length != 4)
                    return;

                for (int i = 0; i < 4; i++)
                {
                    if (!int.TryParse(parts[i], out int num))
                        return;

                    if (num < 0 || num > 255)
                        return;
                }

                ip1.Text = parts[0];
                ip2.Text = parts[1];
                ip3.Text = parts[2];
                ip4.Text = parts[3];

                e.Handled = true;
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
                    GetFullIp(),
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
            IpSender =
                Properties.Settings.Default.ip_sender == null || ChangeButtonClicked
                ? IpSender
                : Properties.Settings.Default.ip_sender;
        }

        private void IpBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void IpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null) return;

            if (int.TryParse(box.Text, out int value))
            {
                if (value > 255)
                {
                    box.Text = "255";
                    box.CaretIndex = box.Text.Length;
                }
            }
        }
    }
}
