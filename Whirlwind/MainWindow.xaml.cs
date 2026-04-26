using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Toolkit.Uwp.Notifications;


namespace Whirlwind
{
    public partial class MainWindow : Window
    {
        private Native.PassDelegate SendOk;
        private Native.GetBytes SendErr;
        private Native.GetBytes ListenMessage;

        public static string connectionString = "Data Source=../../../../Data/data.db";
        private static string SavedMessage = null;
        private static string CurrentInterlocutor = null;
        string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "multimedia-message-arrival-sound.wav");

        System.Windows.Forms.NotifyIcon trayIcon = new System.Windows.Forms.NotifyIcon();

        public MainWindow()
        {
            InitializeComponent();
            Native.init_module();

            SendOk = on_send_ok;
            SendErr = on_send_err;
            ListenMessage = on_listen_message;

            change_ip_address();
            show_tray_icon();

            if (App.IsAutostart)
            {
                this.Hide();
            }
        }

        private void on_send_ok()
        {
            Dispatcher.Invoke(() =>
            {
                add_message_to_db(Properties.Settings.Default.ip_sender, CurrentInterlocutor, DateTime.Now.ToString("yy-M-dd-HH-mm-ss"), SavedMessage);

                send_button.IsEnabled = true;
                message.IsEnabled = true;
            });
        }

        private void on_send_err(IntPtr ptr, int len)
        {
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);
            string result = Encoding.UTF8.GetString(buffer);

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(result);
                send_button.IsEnabled = true;
                message.IsEnabled = true;
            });
        }

        private void on_listen_message(IntPtr ptr, int len)
        {
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);

            var (senderIp, seconds, message) = NetworkProtocols.parse_packet(buffer);

            if (senderIp == Properties.Settings.Default.ip_sender || senderIp == "Error") return;

            Dispatcher.Invoke(() =>
            {
                add_message_to_db(senderIp, Properties.Settings.Default.ip_sender, DateTime.MinValue.AddSeconds(seconds).ToString("yy-M-dd-HH-mm-ss"), message);
                ShowMessageNotification(get_username_by_ip(senderIp), message);
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
        }

        private void change_ip_address()
        {
            var enter_window = new EnterWindow();
            enter_window.ShowDialog();
            Properties.Settings.Default.ip_sender = enter_window.IpSender == null ? null : enter_window.IpSender.Trim();

            if (Properties.Settings.Default.ip_sender == null) {
                Close();
                return;
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string update = $@"UPDATE Device SET ip = '{Properties.Settings.Default.ip_sender}' WHERE type = '1'";

                using (var cmd = new SqliteCommand(update, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            start_listening(Properties.Settings.Default.ip_sender);

            ChatTitle.Text = "Личный чат";
            IpTitle.Text = Properties.Settings.Default.ip_sender;
            CurrentInterlocutor = Properties.Settings.Default.ip_sender;

            load_users();
            load_messages(CurrentInterlocutor);
        }

        private void ShowMessageNotification(string senderName, string text)
        {
            string soundPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "data",
                "sounds",
                "message.wav"
            );

            ShowToast($"{senderName}", text, soundPath);
        }

        public void ShowToast(string title, string message, string soundFile = null)
        {
            var builder = new ToastContentBuilder().AddText(title).AddText(message);

            if (soundPath != null)
                builder.AddAudio(new Uri(soundPath));

            builder.Show();
        }

        private void show_tray_icon()
        {
            trayIcon.Icon = new System.Drawing.Icon("../../../../Pictures/WhirlwindSilver.ico");

            trayIcon.Visible = true;
            trayIcon.Text = "Whirlwind";

            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();

            menu.Items.Add("Изменить IP", null, (s, t) =>
            {
                change_ip_address();
            });

            menu.Items.Add("Выход", null, (s, t) =>
            {
                trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            trayIcon.ContextMenuStrip = menu;


            trayIcon.MouseClick += (s, t) =>
            {
                if (t.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    this.Show();
                    this.WindowState = System.Windows.WindowState.Normal;
                    this.Activate();
                }
            };
        }

        private void load_users()
        {
            DeviceList.Items.Clear();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string query = $@"SELECT id, name, ip FROM Device";

                using (var command = new SqliteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var device = new DeviceItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Ip = reader.GetString(2)
                        };

                        DeviceList.Items.Add(device);
                    }
                }
            }
        }

        private void load_messages(string ip)
        {
            MessagesPanel.Children.Clear();

            if (ip == null) return;

            string query = "";

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                if (ip != Properties.Settings.Default.ip_sender)
                {
                    query = $@"SELECT Message.ID, text, date, (SELECT ip FROM Device WHERE ID = addressee) as IP FROM Message
                            JOIN Device ON sender = (SELECT ID FROM Device WHERE ip = '{ip}') or addressee = (SELECT ID FROM Device WHERE ip = '{ip}')
                            WHERE message_type = '1'
                            GROUP BY Message.ID";
                }else
                {
                    query = $@"SELECT Message.ID, text, date, (SELECT ip FROM Device WHERE ID = addressee) as IP FROM Message
                            JOIN Device ON sender = (SELECT ID FROM Device WHERE ip = '{ip}') or addressee = (SELECT ID FROM Device WHERE ip = '{ip}')
                            WHERE message_type = '1' and sender = addressee
                            GROUP BY Message.ID";
                }

                using (var command = new SqliteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        add_message(new ChatMessage
                        {
                            Id = reader.GetInt32(0),
                            Text = reader.GetString(1),
                            Date = reader.GetString(2),
                            IsMyMessage = reader.GetString(3) == ip
                        });
                    }
                }
            }
        }

        private void start_listening(string ip_address)
        {
            Native.listening_port(ip_address, Properties.Settings.Default.port_sender, ListenMessage);
        }

        private void change_sender_ipaddress_Click(object sender, RoutedEventArgs e)
        {
            change_ip_address();
        }

        private void window_KeyDown(object sender, KeyEventArgs e)
        {
            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (e.Key == Key.Enter && !isShift)
            {
                send_button_Click(send_button, null);
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
        }

        private void message_KeyDown(object sender, KeyEventArgs e)
        {
            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (e.Key == Key.Enter && isShift)
            {
                int caret = message.CaretIndex;

                string nl = Environment.NewLine;

                message.Text = message.Text.Insert(caret, nl);

                message.CaretIndex = caret + nl.Length;

                e.Handled = true;
            }
        }

        private void send_button_Click(object sender, RoutedEventArgs e)
        {
            if (this.message.Text.Trim() == "" || CurrentInterlocutor == null) return;

            send_button.IsEnabled = false;
            this.message.IsEnabled = false;
            string message = this.message.Text.Trim();
            SavedMessage = message;
            byte[] send = NetworkProtocols.build_packet_v0(0, Properties.Settings.Default.ip_sender, (long)(DateTime.UtcNow - DateTime.MinValue).TotalSeconds, message);
            this.message.Text = "";

            Native.send_message(CurrentInterlocutor, Properties.Settings.Default.port_sender, send, send.Length, SendOk, SendErr);
        }

        private void add_user_Click(object sender, RoutedEventArgs e)
        {
            var add_user_window = new AddUser();
            add_user_window.Owner = this;
            add_user_window.ShowDialog();

            if (add_user_window.IpAddresssee == null || add_user_window.NameAddresssee == null) return;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string insert = $@"INSERT INTO Device (ip, type, name) 
                                VALUES ('{add_user_window.IpAddresssee}', '2', '{add_user_window.NameAddresssee}')";

                using (var cmd = new SqliteCommand(insert, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            load_users();
        }

        private void update_device_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is DeviceItem device)
            {
                int id = device.Id;

                var add_user_window = new AddUser();
                add_user_window.Owner = this;

                add_user_window.add_ip_address.Text = device.Ip;
                add_user_window.add_name.Text = device.Name;

                add_user_window.ShowDialog();

                if (add_user_window.IpAddresssee == null || add_user_window.NameAddresssee == null) return;

                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    string update = $@"UPDATE Device SET ip = '{add_user_window.IpAddresssee}',
                                    name = '{add_user_window.NameAddresssee}' WHERE id = '{id}' and type = '2'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                ChatTitle.Text = add_user_window.add_name.Text;
                IpTitle.Text = add_user_window.add_ip_address.Text;
                CurrentInterlocutor = add_user_window.add_ip_address.Text;

                load_users();
                load_messages(CurrentInterlocutor);
            }
        }

        private void delete_device_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is DeviceItem device)
            {
                int id = device.Id;
                string delete = "";

                DeviceList.Items.Remove(device);

                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    if (device.Ip != Properties.Settings.Default.ip_sender)
                    {
                        delete = $@"DELETE FROM Message WHERE addressee = '{id}' or sender = '{id}';
                                       DELETE FROM Device WHERE id = '{id}' and type = '2'";
                    }
                    else
                    {
                        delete = $@"DELETE FROM Message WHERE (addressee = '{id}' or sender = '{id}') and addressee = sender";
                    }

                    using (var cmd = new SqliteCommand(delete, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                ChatTitle.Text = "_";
                IpTitle.Text = "_";
                CurrentInterlocutor = null;
            }

            load_users();
            load_messages(CurrentInterlocutor);
        }

        private void device_item_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null)
                return;


            if (border.DataContext is DeviceItem device)
            {
                int id = device.Id;
                ChatTitle.Text = device.Name;
                IpTitle.Text = device.Ip;
                CurrentInterlocutor = device.Ip;
                load_messages(device.Ip);
            }
        }

        private void add_message(ChatMessage msg)
        {
            var border = new Border
            {
                Background = msg.IsMyMessage ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                                  : new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                Padding = new Thickness(10),
                Margin = msg.IsMyMessage ? new Thickness(100, 0, 0, 2)
                              : new Thickness(0, 0, 100, 2),
                CornerRadius = new CornerRadius(5),
                Tag = msg.Id
            };

            var text = new TextBlock
            {
                Text = msg.Text,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = text;

            var contextMenu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "Удалить" };

            deleteItem.Click += (s, e) =>
            {
                int index = MessagesPanel.Children.IndexOf(border);

                if (index >= 0)
                {
                    MessagesPanel.Children.RemoveAt(index);

                    if (index < MessagesPanel.Children.Count)
                        MessagesPanel.Children.RemoveAt(index);

                    int id = (int)border.Tag;

                    string delete = $@"DELETE FROM Message WHERE ID = {id}";

                    using (var connection = new SqliteConnection(connectionString))
                    {
                        connection.Open();
                        using (var cmd = new SqliteCommand(delete, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            };

            contextMenu.Items.Add(deleteItem);
            border.ContextMenu = contextMenu;

            MessagesPanel.Children.Add(border);

            var dateText = new TextBlock
            {
                Text = msg.Date,
                Foreground = Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = msg.IsMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(5, 0, 5, 10)
            };

            MessagesPanel.Children.Add(dateText);

            MessagesScroll.Dispatcher.InvokeAsync(() =>
            {
                MessagesScroll.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void add_message_to_db(string sender, string addressee, string seconds, string message)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string check = $@"SELECT count(*) from Device WHERE ip = '{sender}'";

                string add_device = $@"INSERT INTO Device (ip, type, name) VALUES ('{sender}', 2, 'Неизвестный')";

                string insert = $@"INSERT INTO Message(sender, addressee, message_type, text, date)
                                VALUES ((SELECT ID FROM Device WHERE ip = '{sender}'), 
                                (SELECT ID FROM Device WHERE ip = '{addressee}'), '1', '{message}', 
                                '{seconds}')";
                using (var command = new SqliteCommand(check, connection))
                {
                    if ((long)command.ExecuteScalar() <= 0)
                    {
                        using (var cmd = new SqliteCommand(add_device, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                using (var cmd = new SqliteCommand(insert, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            load_users();
            load_messages(CurrentInterlocutor);
        }

        private string get_username_by_ip(string ip)
        {
            try
            {
                string query = $@"SELECT name FROM Device WHERE ip = '{ip}' LIMIT 1";

                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new Microsoft.Data.Sqlite.SqliteCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return result?.ToString() ?? ip;
                    }
                }
            }
            catch
            {
                return ip;
            }
        }
    }
    public class DeviceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Date { get; set; }
        public bool IsMyMessage { get; set; }
    }

}
