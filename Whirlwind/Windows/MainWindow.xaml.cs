using Microsoft.Toolkit.Uwp.Notifications;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Whirlwind.Classes;
using Whirlwind.Views;


namespace Whirlwind
{
    public partial class MainWindow : Window
    {
        private Native.PassDelegate SystemSendOk;
        private Native.PassDelegate SendOk;
        private Native.GetBytes SendErr;
        private Native.GetBytes ListenMessage;

        public enum NotificationMode
        {
            Normal,
            Muted,
            Disabled,
            SoundOnly
        }

        private static List<(byte type, ushort version, byte[] extra_data, string message, string ip_addressee)> SavedMessages = 
            new List<(byte, ushort, byte[], string, string)>();
        public string CurrentInterlocutor = null;
        private readonly List<string> attachedFiles = new List<string>();

        System.Windows.Forms.NotifyIcon trayIcon = new System.Windows.Forms.NotifyIcon();

        private bool FullClose = false;

        private NotificationMode current_muted_mode = NotificationMode.Normal;

        public MainWindow()
        {
            InitializeComponent();

            this.Icon = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Pictures", "WhirlWindSilverMini.ico")));

            Native.init_module();

            SystemSendOk = on_system_send_ok;
            SendOk = on_send_ok;
            SendErr = on_send_err;
            ListenMessage = on_listen_message;

            change_ip_address();
            show_tray_icon();
        }

        private void on_system_send_ok()
        {
        }

        private void on_send_ok()
        {
            Dispatcher.Invoke(() =>
            {
                
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
            });
        }

        private void on_listen_message(IntPtr ptr, int len)
        {
            byte[] packet = new byte[len];
            Marshal.Copy(ptr, packet, 0, len);

            byte protocol_type = packet[0];
            ushort protocol_version = (ushort)((packet[1] << 8) | packet[2]);

            string sender_ip = $"{packet[3]}.{packet[4]}.{packet[5]}.{packet[6]}";

            long seconds;
            byte device_type, message_type;
            byte[] extra_data, fileContent;
            string message, fileName;

            switch (protocol_type)
            {
                case 0:
                    (seconds, extra_data) = NetworkProtocols.on_parse_system_packet(packet);
                    handle_system_packet(sender_ip, protocol_version, seconds, extra_data);
                    break;

                case 1:
                    (seconds, device_type, message_type, extra_data, message) = NetworkProtocols.on_parse_text_packet(packet);
                    handle_text_packet(sender_ip, seconds, device_type, message_type, extra_data, message);
                    Native.remove_expected_protocol(protocol_type, protocol_version, NetworkProtocols.ip_to_bytes(sender_ip));
                    ShowNotification.ShowToast(QueryToSQL.get_username_by_ip(sender_ip), message, QueryToSQL.get_device_by_ip(sender_ip), QueryToSQL.get_device_muted(sender_ip));
                    break;
                case 2:
                    (seconds, device_type, message_type, extra_data, fileName, fileContent) = NetworkProtocols.on_parse_file_packet(packet);
                    handle_file_packet(sender_ip, seconds, device_type, message_type, extra_data, fileName, fileContent);
                    Native.remove_expected_protocol(protocol_type, protocol_version, NetworkProtocols.ip_to_bytes(sender_ip));
                    ShowNotification.ShowToast(QueryToSQL.get_username_by_ip(sender_ip), fileName, QueryToSQL.get_device_by_ip(sender_ip), QueryToSQL.get_device_muted(sender_ip));
                    break;
                default:
                    //Неизвестный тип протокола
                    break;
            }
        }

        void handle_system_packet(string sender_ip, ushort protocol_version, long seconds, byte[] extra_data)
        {
            switch (protocol_version)
            {
                case 0:
                    handle_system_packet_v0(sender_ip, seconds, extra_data);
                    break;

                default:
                    //Неизвестный тип протокола
                    break;
            }
        }

        void handle_system_packet_v0(string sender_ip, long seconds, byte[] extra_data)
        {
            var (action, future_type, future_version) = NetworkProtocols.on_parse_system_extra_data_v0(extra_data);

            switch (action)
            {
                case 0:
                    handle_system_request(sender_ip, future_type, future_version);
                    break;

                case 1:
                    handle_system_accept(sender_ip, future_type, future_version);
                    break;
                default:
                    //Неизвестная версия системного протокола
                    break;
            }
        }

        void handle_system_request(string sender_ip, byte future_type, ushort future_version)
        {
            Native.add_expected_protocol(
                future_type,
                future_version,
                NetworkProtocols.ip_to_bytes(sender_ip)
            );

            byte[] handshake = NetworkProtocols.build_system_packet(
                Properties.Settings.Default.ip_sender,
                (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                NetworkProtocols.build_system_extra_data_v0(
                    1,
                    future_type,
                    future_version
                )
            );

            Native.send_message(
                sender_ip,
                Properties.Settings.Default.port_sender,
                handshake,
                handshake.Length,
                SystemSendOk,
                SendErr
            );
        }

        void handle_system_accept(string sender_ip, byte future_type, ushort future_version)
        {
            (byte type, ushort version, byte[] extra_data, string message, string ip_addressee) =
                SavedMessages.FirstOrDefault(x => x.type == future_type && x.version == future_version && x.ip_addressee == sender_ip);

            if (message == null) return;
            delete_saved_message(future_type, future_version, sender_ip);
            byte[] send = new byte[0];

            switch (type)
            {
                case 1:
                    send = NetworkProtocols.build_text_packet(
                        Properties.Settings.Default.ip_sender,
                        (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                        QueryToSQL.get_device_type(sender_ip),
                        1,
                        (version, extra_data),
                        message
                    );
                    break;
                case 2:
                    send = NetworkProtocols.build_file_packet(
                        Properties.Settings.Default.ip_sender,
                        (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                        QueryToSQL.get_device_type(sender_ip),
                        2,
                        (version, extra_data),
                        System.IO.Path.GetFileName(message),
                        File.ReadAllBytes(message)

                    );
                    break;
            }

            Native.send_message(
                sender_ip,
                Properties.Settings.Default.port_sender,
                send,
                send.Length,
                SendOk,
                SendErr
            );

            Dispatcher.Invoke(() =>
            {
                switch (type) {
                    case 1:
                        add_message_to_db(Properties.Settings.Default.ip_sender, sender_ip,
                        DateTime.Now.ToString("yy-M-dd-HH-mm-ss"), QueryToSQL.get_device_type(sender_ip), 1, message);
                        break; 
                    case 2:
                        add_message_to_db(Properties.Settings.Default.ip_sender, sender_ip,
                        DateTime.Now.ToString("yy-M-dd-HH-mm-ss"), QueryToSQL.get_device_type(sender_ip), 2, message);
                        break; 
                    default:
                        break;
            }
            });
        }

        void handle_text_packet(string sender_ip, long seconds, byte device_type, byte message_type, byte[] extra_data, string message)
        {
            Dispatcher.Invoke(() =>
            {
                add_message_to_db(
                    sender_ip,
                    Properties.Settings.Default.ip_sender,
                    DateTime.MinValue.AddSeconds(seconds).ToString("yy-M-dd-HH-mm-ss"),
                    device_type,
                    message_type,
                    message
                );
            });
        }

        void handle_file_packet(string sender_ip, long seconds, byte device_type, byte message_type, byte[] extra_data, string fileName, byte[] fileContent)
        {
            Dispatcher.Invoke(() =>
            {
                string username = QueryToSQL.get_username_by_ip(sender_ip);
                string dir = $"Files/{username}";

                Directory.CreateDirectory(dir);

                string finalName = get_unique_file_name(dir, fileName);

                File.WriteAllBytes(System.IO.Path.Combine(dir, finalName), fileContent);

                add_message_to_db(
                    sender_ip,
                    Properties.Settings.Default.ip_sender,
                    DateTime.MinValue.AddSeconds(seconds).ToString("yy-M-dd-HH-mm-ss"),
                    device_type,
                    message_type,
                    $"{username}/{finalName}"
                );
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (App.IsAutostart)
            {
                this.Hide();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (FullClose)
            {
                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                e.Handled = true;
                handle_paste_action();
                return;
            }

            if (!message.IsKeyboardFocusWithin)
                return;

            if (e.Key == Key.Enter && isShift)
            {
                int caret = message.CaretIndex;
                string nl = Environment.NewLine;

                message.Text = message.Text.Insert(caret, nl);
                message.CaretIndex = caret + nl.Length;

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && !isShift)
            {
                e.Handled = true;
                send_button_Click(send_button, new RoutedEventArgs());
                return;
            }
        }

        private void handle_paste_action()
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                foreach (string file in files)
                {
                    attachedFiles.Add(file);
                }

                refresh_attached_files_list();
                return;
            }

            if (Clipboard.ContainsImage())
            {
                try
                {
                    var image = Clipboard.GetImage();
                    if (image != null)
                    {
                        string username = QueryToSQL.get_username_by_ip(CurrentInterlocutor);

                        string dir = System.IO.Path.GetFullPath(
                            $"Files/{username}"
                        );

                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        string filePath = System.IO.Path.Combine(
                            dir,
                            $"clipboard_image_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                        );

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(image));
                            encoder.Save(fileStream);
                        }

                        attachedFiles.Add(filePath);
                        refresh_attached_files_list();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при вставке изображения:\n{ex.Message}",
                                    "Ошибка",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }

            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                message.Text += text;
                message.CaretIndex = message.Text.Length;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (e.Key == Key.Enter && !isShift)
            {
                send_button_Click(send_button, null);
                e.Handled = true;
            }
        }

        private void change_ip_address()
        {
            var enter_window = new EnterWindow();
            enter_window.ShowDialog();
            Properties.Settings.Default.ip_sender = enter_window.IpSender == null ? null : enter_window.IpSender.Trim();

            if (Properties.Settings.Default.ip_sender == null) {
                FullClose = true;
                trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
                return;
            }

            QueryToSQL.set_default_ip_address();

            start_listening(Properties.Settings.Default.ip_sender);

            ChatTitle.Text = "Личный чат";
            IpTitle.Text = Properties.Settings.Default.ip_sender;
            CurrentInterlocutor = Properties.Settings.Default.ip_sender;

            load_users();
            load_messages(CurrentInterlocutor);
        }

        private void show_tray_icon()
        {
            trayIcon.Icon = new System.Drawing.Icon("Pictures/WhirlwindSilverMini.ico");

            trayIcon.Visible = true;
            trayIcon.Text = "Whirlwind";

            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();

            menu.Items.Add("Изменить IP", null, (s, t) =>
            {
                change_ip_address();
            });

            menu.Items.Add("Выход", null, (s, t) =>
            {
                FullClose = true;
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

            foreach (DeviceItem device in QueryToSQL.get_devices())
            {
                DeviceList.Items.Add(device);
            }
        }

        public void load_messages(string ip)
        {
            MessagesPanel.Children.Clear();

            if (ip == null) return;

            foreach (ChatMessage message in QueryToSQL.get_messages(ip))
            {
                control_messages(message);
            }
        }

        private void refresh_attached_files_list()
        {
            if (attachedFiles.Count == 0)
            {
                AttachedFilesList.Visibility = Visibility.Collapsed;
                AttachedFilesList.ItemsSource = null;
                return;
            }

            AttachedFilesList.Visibility = Visibility.Visible;
            AttachedFilesList.ItemsSource = null;
            AttachedFilesList.ItemsSource = attachedFiles;
        }

        private void start_listening(string ip_address)
        {
            Native.listening_port(ip_address, Properties.Settings.Default.port_sender, ListenMessage);
        }

        private void change_sender_ip_address_Click(object sender, RoutedEventArgs e)
        {
            change_ip_address();
        }

        private void send_button_Click(object sender, RoutedEventArgs e)
        {
            bool hasText = !string.IsNullOrWhiteSpace(this.message.Text);
            bool hasFiles = attachedFiles.Count > 0;

            if (CurrentInterlocutor == null)
                return;

            if (CurrentInterlocutor == Properties.Settings.Default.ip_sender)
            {
                if (hasText)
                {
                    add_message_to_db(
                    Properties.Settings.Default.ip_sender,
                    Properties.Settings.Default.ip_sender,
                    DateTime.Now.ToString("yy-M-dd-HH-mm-ss"),
                    1,
                    1,
                    this.message.Text.Trim());
                }

                if (hasFiles)
                {
                    foreach (var filePath in attachedFiles)
                    {
                        add_message_to_db(
                       Properties.Settings.Default.ip_sender,
                       Properties.Settings.Default.ip_sender,
                       DateTime.Now.ToString("yy-M-dd-HH-mm-ss"),
                       1,
                       2,
                       filePath);
                    }
                }
            }
            else
            {
                if (hasText)
                {
                    string textMessage = this.message.Text.Trim();

                    (ushort version, byte[] extra) textExtra = NetworkProtocols.build_text_extra_data_v0();

                    SavedMessages.Add((1, textExtra.version, textExtra.extra, textMessage, CurrentInterlocutor));

                    byte[] handshake = NetworkProtocols.build_system_packet(
                        Properties.Settings.Default.ip_sender,
                        (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                        NetworkProtocols.build_system_extra_data_v0(
                            0,
                            1,
                            textExtra.version
                        )
                    );

                    Native.send_message(
                        CurrentInterlocutor,
                        Properties.Settings.Default.port_sender,
                        handshake,
                        handshake.Length,
                        SystemSendOk,
                        SendErr
                    );
                }

                if (hasFiles)
                {
                    foreach (var filePath in attachedFiles)
                    {
                        (ushort version, byte[] extra) fileExtra = NetworkProtocols.build_text_extra_data_v0();

                        byte[] fileHandshake = NetworkProtocols.build_system_packet(
                            Properties.Settings.Default.ip_sender,
                            (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                            NetworkProtocols.build_system_extra_data_v0(
                                0,
                                2,
                                fileExtra.version
                            )
                        );

                        SavedMessages.Add((2, fileExtra.version, fileExtra.extra, filePath, CurrentInterlocutor));

                        Native.send_message(
                            CurrentInterlocutor,
                            Properties.Settings.Default.port_sender,
                            fileHandshake,
                            fileHandshake.Length,
                            SystemSendOk,
                            SendErr
                        );
                    }
                }
            }

            this.message.Text = "";
            attachedFiles.Clear();
            refresh_attached_files_list();
        }

        private void add_user_Click(object sender, RoutedEventArgs e)
        {
            QueryToSQL.add_user();

            load_users();
        }

        private void update_device_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is DeviceItem device)
            {
                var devices = QueryToSQL.update_device(device);

                if (devices == (null, null, null)) return;

                ChatTitle.Text = devices.chat_title;
                IpTitle.Text = devices.ip_title;
                CurrentInterlocutor = devices.current_interlocutor;

                load_users();
                load_messages(CurrentInterlocutor);
            }
        }

        private void delete_device_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is DeviceItem device)
            {
                try
                {
                    string username = QueryToSQL.get_username_by_ip(device.Ip);
                    string dir = System.IO.Path.GetFullPath($"Files/{username}");

                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении файлов пользователя:\n{ex.Message}",
                                    "Удаление файлов",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }

                DeviceList.Items.Remove(device);

                QueryToSQL.delete_device(device);

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
                ChatTitle.Text = device.Name;
                IpTitle.Text = device.Ip;
                CurrentInterlocutor = device.Ip;
                load_messages(device.Ip);

                change_muted_mode(device.Ip);
            }

            if (CurrentInterlocutor == Properties.Settings.Default.ip_sender) mute_button.Visibility = Visibility.Collapsed;
            else mute_button.Visibility = Visibility.Visible;
        }

        private void control_messages(ChatMessage msg)
        {
            bool isFile = msg.MessageType == 2;

            var border = new Border
            {
                Background = msg.IsMyMessage
                    ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                    : new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                Padding = new Thickness(10),
                Margin = msg.IsMyMessage
                    ? new Thickness(100, 0, 0, 2)
                    : new Thickness(0, 0, 100, 2),
                CornerRadius = new CornerRadius(5),
                Tag = msg.Id,
                Cursor = isFile ? Cursors.Hand : Cursors.Arrow
            };

            UIElement content;

            string filePath = null;
            string fileNameOnly = msg.DisplayText;

            if (isFile)
            {
                if (System.IO.Path.IsPathRooted(msg.DisplayText))
                {
                    filePath = msg.DisplayText;
                    fileNameOnly = System.IO.Path.GetFileName(msg.DisplayText);
                }
                else
                {
                    string username = QueryToSQL.get_username_by_ip(CurrentInterlocutor);
                    filePath = System.IO.Path.GetFullPath(
                        $"Files/{username}/{msg.DisplayText}"
                    );
                    fileNameOnly = msg.DisplayText;
                }
            }

            bool isImage = false;

            if (isFile)
            {
                string ext = System.IO.Path.GetExtension(fileNameOnly).ToLower();

                string[] imageExts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

                isImage = imageExts.Contains(ext);
            }

            if (isFile)
            {
                if (isImage)
                {
                    var img = new Image
                    {
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 0, 0, 5),
                        MaxWidth = 350,
                        MaxHeight = 350
                    };

                    try
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                        bmp.EndInit();

                        img.Source = bmp;
                    }
                    catch
                    {
                        img.Source = null;
                    }

                    border.MaxWidth = 400;
                    border.HorizontalAlignment = msg.IsMyMessage
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left;

                    border.MouseLeftButtonUp += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Не удалось открыть изображение:\n" + ex.Message,
                                            "Ошибка",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                        }
                    };

                    content = img;
                }
                else
                {
                    var stack = new StackPanel { Orientation = Orientation.Horizontal };

                    var icon = new TextBlock
                    {
                        Text = "📄",
                        FontSize = 20,
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    var fileNameText = new TextBlock
                    {
                        Text = fileNameOnly,
                        Foreground = Brushes.White,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    };

                    stack.Children.Add(icon);
                    stack.Children.Add(fileNameText);

                    border.MouseLeftButtonUp += (s, e) =>
                    {
                        try
                        {
                            if (!File.Exists(filePath))
                            {
                                MessageBox.Show("Файл не найден:\n" + filePath,
                                                "Ошибка",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Warning);
                                return;
                            }

                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Не удалось открыть файл:\n" + ex.Message,
                                            "Ошибка",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                        }
                    };

                    content = stack;
                }
            }
            else
            {
                var textBlock = new TextBlock
                {
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                };

                string[] parts = msg.DisplayText.Split(' ');
                foreach (string part in parts)
                {
                    if (part.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || part.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        var hyperlink = new Hyperlink(new Run(part))
                        {
                            NavigateUri = new Uri(part),
                            Foreground = msg.IsMyMessage
                                ? Brushes.LightGray
                                : Brushes.DeepSkyBlue
                        };

                    hyperlink.RequestNavigate += (s, e) =>
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = e.Uri.AbsoluteUri,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Не удалось открыть ссылку:\n" + ex.Message,
                                                "Ошибка",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Error);
                            }
                        };

                        textBlock.Inlines.Add(hyperlink);
                    }
                    else
                    {
                        textBlock.Inlines.Add(new Run(part + " "));
                    }
                }

                content = textBlock;

            }

            border.Child = content;

            var contextMenu = new ContextMenu();

            var copyItem = new MenuItem { Header = "Копировать" };

            copyItem.Click += (s, e) =>
            {
                try
                {
                    string textFromDb = QueryToSQL.get_message_text(msg.Id);

                    if (isFile)
                    {
                        Clipboard.SetText(filePath);
                    }
                    else
                    {
                        Clipboard.SetText(textFromDb);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось скопировать:\n{ex.Message}",
                                    "Ошибка",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            };

            contextMenu.Items.Add(copyItem);


            if (isFile)
            {
                var locationItem = new MenuItem { Header = "Расположение файла" };

                locationItem.Click += (s, e) =>
                {
                    try
                    {
                        if (!File.Exists(filePath))
                        {
                            MessageBox.Show("Файл не найден:\n" + filePath,
                                            "Ошибка",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Warning);
                            return;
                        }

                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Не удалось открыть проводник:\n" + ex.Message,
                                        "Ошибка",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                    }
                };

                contextMenu.Items.Add(locationItem);
            }

            var deleteItem = new MenuItem { Header = "Удалить" };

            deleteItem.Click += (s, e) =>
            {
                int index = MessagesPanel.Children.IndexOf(border);

                if (index >= 0)
                {
                    MessagesPanel.Children.RemoveAt(index);

                    if (index < MessagesPanel.Children.Count)
                        MessagesPanel.Children.RemoveAt(index);

                    QueryToSQL.delete_message((int)border.Tag);

                    if (msg.MessageType == 2)
                    {
                        try
                        {
                            string username = QueryToSQL.get_username_by_ip(CurrentInterlocutor);

                            string userDir = System.IO.Path.GetFullPath(
                                $"Files/{username}"
                            );

                            if (filePath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
                            {
                                if (File.Exists(filePath))
                                    File.Delete(filePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Ошибка при удалении файла:\n{ex.Message}",
                                "Удаление файла",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
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
                HorizontalAlignment = msg.IsMyMessage
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left,
                Margin = new Thickness(5, 0, 5, 10)
            };

            MessagesPanel.Children.Add(dateText);

            MessagesScroll.Dispatcher.InvokeAsync(() =>
            {
                MessagesScroll.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void add_message_to_db(string sender, string addressee, string seconds, byte device_type, byte message_type, string message)
        {
            QueryToSQL.add_message_to_db(sender, addressee, seconds, device_type, message_type, message);

            load_users();
            load_messages(CurrentInterlocutor);
        }

        private void delete_saved_message(byte type, ushort version, string ip)
        {
            for (int i = 0; i < SavedMessages.Count; i++)
            {
                var msg = SavedMessages[i];

                if (msg.type == type &&
                    msg.version == version &&
                    msg.ip_addressee == ip)
                {
                    SavedMessages.RemoveAt(i);
                    return;
                }
            }
        }

        private void attach_file_button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файлы",
                Filter = "Все файлы (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    attachedFiles.Add(file);
                }

                refresh_attached_files_list();
            }
        }

        private void attached_file_delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is string filePath)
            {
                attachedFiles.Remove(filePath);
                refresh_attached_files_list();
            }
        }

        private void attached_files_list_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (VisualTreeHelper.GetChild(AttachedFilesList, 0) is Border border &&
                VisualTreeHelper.GetChild(border, 0) is ScrollViewer listScroll)
            {
                // Прокрутка вверх
                if (e.Delta > 0)
                {
                    listScroll.LineUp();
                    listScroll.LineUp();
                }
                // Прокрутка вниз
                else
                {
                    listScroll.LineDown();
                    listScroll.LineDown();
                }

                e.Handled = true;
            }
        }

        private string get_unique_file_name(string directory, string fileName)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string ext = System.IO.Path.GetExtension(fileName);

            string fullPath = System.IO.Path.Combine(directory, fileName);

            int counter = 1;

            while (File.Exists(fullPath))
            {
                string newName = $"{name} ({counter}){ext}";
                fullPath = System.IO.Path.Combine(directory, newName);
                counter++;
            }

            return System.IO.Path.GetFileName(fullPath);
        }

        private void settings_button_Click(object sender, RoutedEventArgs e)
        {
            var settings = new Windows.Settings();
            settings.ShowDialog();
        }

        private void mute_button_Click(object sender, RoutedEventArgs e)
        {
            switch (current_muted_mode)
            {
                // 🔊 → 🔇
                case NotificationMode.Normal:
                    current_muted_mode = NotificationMode.Muted;
                    QueryToSQL.set_device_muted(CurrentInterlocutor, 1);
                    mute_button.Content = "🔇";
                    mute_button.ToolTip = "Уведомления заглушены";
                    break;

                // 🔇 → 🔔
                case NotificationMode.Muted:
                    current_muted_mode = NotificationMode.SoundOnly;
                    QueryToSQL.set_device_muted(CurrentInterlocutor, 2);
                    mute_button.Content = "🔔";
                    mute_button.ToolTip = "Только звуки включены";
                    break;

                // 🔔 → 🚫
                case NotificationMode.SoundOnly:
                    current_muted_mode = NotificationMode.Disabled;
                    QueryToSQL.set_device_muted(CurrentInterlocutor, 3);
                    mute_button.Content = "🚫";
                    mute_button.ToolTip = "Уведомления отключены";
                    break;

                // 🚫 → 🔊
                case NotificationMode.Disabled:
                    current_muted_mode = NotificationMode.Normal;
                    QueryToSQL.set_device_muted(CurrentInterlocutor, 0);
                    mute_button.Content = "🔊";
                    mute_button.ToolTip = "Уведомления включены";
                    break;
            }
        }

        public void change_muted_mode(string ip)
        {
            switch (QueryToSQL.get_device_muted(ip))
            {
                case 0:
                    current_muted_mode = NotificationMode.Normal;
                    mute_button.Content = "🔊";
                    mute_button.ToolTip = "Уведомления включены";
                    break;
                case 1:
                    current_muted_mode = NotificationMode.Muted;
                    mute_button.Content = "🔇";
                    mute_button.ToolTip = "Уведомления заглушены";
                    break;
                case 2:
                    current_muted_mode = NotificationMode.SoundOnly;
                    QueryToSQL.set_device_muted(CurrentInterlocutor, 2);
                    mute_button.Content = "🔔";
                    mute_button.ToolTip = "Только звуки включены";
                    break;
                case 3:
                    current_muted_mode = NotificationMode.Disabled;
                    mute_button.Content = "🚫";
                    mute_button.ToolTip = "Уведомления отключены";
                    break;
            }
        }

    }
}
