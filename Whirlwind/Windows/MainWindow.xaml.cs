using Microsoft.Toolkit.Uwp.Notifications;
using NAudio.CoreAudioApi;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Whirlwind.Classes;
using Whirlwind.Interop;
using Whirlwind.Views;
using Windows.Devices.Usb;
using Windows.Media.DialProtocol;

namespace Whirlwind
{
    public partial class MainWindow : Window
    {
        private Native.GetBytes ListenMessage;
        private Native.GetBytes ListenFiles;
        private Native.GetBytes ListenUDP;

        internal enum NotificationMode
        {
            Normal,
            Muted,
            Disabled,
            SoundOnly
        }

        enum DeviceBaseState
        {
            Offline,
            Online
        }

        enum DeviceOverlayState
        {
            None,
            Red,
            ThreeDots
        }

        private static List<(byte type, ushort version, byte[] extra_data, string message, string ip_addressee)> SavedMessages = 
            new List<(byte, ushort, byte[], string, string)>();
        internal string CurrentInterlocutor = null;
        private readonly List<string> attachedFiles = new List<string>();

        System.Windows.Forms.NotifyIcon trayIcon = new System.Windows.Forms.NotifyIcon();

        private bool FullClose = false;

        private NotificationMode current_muted_mode = NotificationMode.Normal;

        private bool current_blocked_mode = false;

        internal List<DeviceItem> device_item_list = new List<DeviceItem>();

        Dictionary<int, DeviceBaseState> BaseStates = new();

        Dictionary<int, DeviceOverlayState> OverlayStates = new();

        private readonly Dictionary<int, CancellationTokenSource> BaseTimers = new();

        private readonly Dictionary<int, CancellationTokenSource> OverlayTimers = new();

        private readonly Dictionary<int, DeviceOverlayState> OverlayUnderDots = new();

        private List<string> sent_unviewed_messages = new();

        private List<string> was_read_messages = new();

        public MainWindow()
        {
            InitializeComponent();

            Native.init_module();

            ListenMessage = on_listen_message;
            ListenFiles = on_listen_file;
            ListenUDP = on_listen_udp;

            change_ip_address();
            show_tray_icon();

            bool red_exist = false;
            foreach (DeviceItem dev in device_item_list)
            {
                if (QueryToSQL.get_received_unviewed_by_ip(dev.Ip))
                {
                    set_red(dev.Id);
                    red_exist = true;
                }
            }
            if (red_exist)
            {
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"), System.IO.Path.GetFullPath("../Pictures/NoticeDot.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMiniNotice.ico");
            }

            render_device_list();
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
            byte[] extra_data;
            string message;

            switch (protocol_type)
            {
                case 0:
                    (seconds, extra_data) = NetworkProtocols.on_parse_system_packet(packet);

                    handle_system_packet(sender_ip, protocol_version, seconds, extra_data);

                    break;
                case 1:
                    (seconds, device_type, message_type, extra_data, message) = NetworkProtocols.on_parse_text_packet(packet);

                    Native.remove_expected_protocol(protocol_type, protocol_version, NetworkProtocols.ip_to_bytes(sender_ip));
                     var device = QueryToSQL.get_device_by_ip(sender_ip);
                    if (device != null) ShowNotification.ShowToast(message, device, device.Muted);

                    show_window_minimized();

                    handle_text_packet(sender_ip, seconds, device_type, message_type, extra_data, message);
                    break;
                default:
                    //Неизвестный тип протокола
                    break;
            }
        }

        private void on_listen_file(IntPtr ptr, int len)
        {
            byte[] packet = new byte[len];
            Marshal.Copy(ptr, packet, 0, len);

            byte protocol_type = packet[0];
            ushort protocol_version = (ushort)((packet[1] << 8) | packet[2]);

            string sender_ip = $"{packet[3]}.{packet[4]}.{packet[5]}.{packet[6]}";

            var (seconds, device_type, message_type, extra_data, file_name, file_content) = NetworkProtocols.on_parse_file_packet(packet);

            Native.remove_expected_protocol(protocol_type, protocol_version, NetworkProtocols.ip_to_bytes(sender_ip));
            var device = QueryToSQL.get_device_by_ip(sender_ip);
            if (device != null) ShowNotification.ShowToast(file_name, device, device.Muted);

            show_window_minimized();

            handle_file_packet(sender_ip, seconds, device_type, message_type, extra_data, file_name, file_content);
        }

        private void on_listen_udp(IntPtr ptr, int len)
        {
            Task.Run(() =>
            {
                byte[] packet = new byte[len];
                Marshal.Copy(ptr, packet, 0, len);

                byte protocol_type = packet[0];
                ushort protocol_version = (ushort)((packet[1] << 8) | packet[2]);

                string sender_ip = $"{packet[3]}.{packet[4]}.{packet[5]}.{packet[6]}";

                long seconds;
                byte[] extra_data;

                switch (protocol_type)
                {
                    case 0:
                        (seconds, extra_data) = NetworkProtocols.on_parse_system_packet(packet);
                        handle_system_packet(sender_ip, protocol_version, seconds, extra_data);
                        break;
                    default:
                        //Неизвестный тип протокола
                        break;
                }
            });
        }

        void handle_system_packet(string sender_ip, ushort protocol_version, long seconds, byte[] extra_data)
        {
            switch (protocol_version)
            {
                case 0:
                    handle_system_packet_v0(sender_ip, seconds, extra_data);
                    break;
                case 1:
                    handle_system_packet_v1(sender_ip, seconds, extra_data);
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
                case 2:
                    handle_system_reject(sender_ip, future_type, future_version);
                    break;
                default:
                    //Неизвестная версия системного протокола
                    break;
            }
        }

        void handle_system_request(string sender_ip, byte future_type, ushort future_version)
        {
            byte[] handshake;

            if (device_item_list.FirstOrDefault(d => d.Ip == sender_ip) == null || device_item_list.FirstOrDefault(d => d.Ip == sender_ip).Blocked == 0)
            {
                Native.add_expected_protocol(
                    future_type,
                    future_version,
                    NetworkProtocols.ip_to_bytes(sender_ip)
                );

                handshake = NetworkProtocols.build_system_packet(
                    Properties.Settings.Default.ip_sender,
                    (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                    NetworkProtocols.build_system_extra_data_v0(
                        1,
                        future_type,
                        future_version
                    )
                );
            }
            else
            {
                handshake = NetworkProtocols.build_system_packet(
                    Properties.Settings.Default.ip_sender,
                    (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                    NetworkProtocols.build_system_extra_data_v0(
                        2,
                        future_type,
                        future_version
                    )
                );
            }

            Native.send_message(
               sender_ip,
               Properties.Settings.Default.port_sender,
               handshake,
               handshake.Length
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

                    Native.send_message(
                        sender_ip,
                        Properties.Settings.Default.port_sender,
                        send,
                        send.Length
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

                    Native.send_file_message(
                        sender_ip,
                        Properties.Settings.Default.port_file_sender,
                        send,
                        send.Length
                    );
                    break;
            }

            Dispatcher.InvokeAsync(() =>
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

        private void handle_system_reject(string sender_ip, byte future_type, ushort future_version)
        {
            if (message == null) return;
            delete_saved_message(future_type, future_version, sender_ip);

            MessageBox.Show($"Не удалось отправить сообщение. Вы были ЗАБЛОКИРОВАНЫ пользователем {QueryToSQL.get_username_by_ip(sender_ip)}.");
        }

        void handle_system_packet_v1(string sender_ip, long seconds, byte[] extra_data)
        {
            var action = NetworkProtocols.on_parse_system_extra_data_v1(extra_data);

            var dbDevice = QueryToSQL.get_device_by_ip(sender_ip);
            if (dbDevice == null)
                return;

            switch (action)
            {
                case 0:
                    set_online(dbDevice.Id, 8000);
                    render_device_list();
                    break;
                case 1:
                    set_dots(dbDevice.Id, 1000);
                    render_device_list();
                    break;
                case 2:
                    if (!QueryToSQL.get_received_unviewed_by_ip(sender_ip) && was_read_messages.Contains(sender_ip))
                    {
                        try
                        {
                            byte[] handshake = NetworkProtocols.build_system_packet(
                                Properties.Settings.Default.ip_sender,
                                (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                                NetworkProtocols.build_system_extra_data_v1(
                                    3
                                )
                            );

                            Native.send_udp(
                                sender_ip,
                                Properties.Settings.Default.port_udp_sender,
                                handshake,
                                handshake.Length
                            );

                            was_read_messages.Remove(sender_ip);
                        }
                        catch
                        {
                        }
                    }
                    break;
                case 3:

                    QueryToSQL.set_sent_viewed(sender_ip);
                    load_sent_unviewed();
                    load_messages(CurrentInterlocutor);
                    break;
            }

        }

        private void handle_text_packet(string sender_ip, long seconds, byte device_type, byte message_type, byte[] extra_data, string message)
        {
            Dispatcher.InvokeAsync(() =>
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
            Dispatcher.InvokeAsync(() =>
            {
                string username = QueryToSQL.get_username_by_ip(sender_ip);
                string dir = $"../Files/{username}";

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
                CurrentInterlocutor = null;
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
                            $"../Files/{username}"
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

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    attachedFiles.Add(file);

                    refresh_attached_files_list();
                }
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
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

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                ChatTitle.Text = "Личный чат";
                IpTitle.Text = Properties.Settings.Default.ip_sender;
                CurrentInterlocutor = Properties.Settings.Default.ip_sender;

                load_devices();
                load_messages(CurrentInterlocutor);
                load_sent_unviewed();
            }
            else
            {
                CurrentInterlocutor = null;
            }
        }

        private void show_window_minimized()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (Visibility != Visibility.Visible)
                {
                    WindowState = WindowState.Minimized;
                    Show();
                }
            });
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

            load_devices();
            load_messages(CurrentInterlocutor);
            load_sent_unviewed();
        }

        private void show_tray_icon()
        {
            trayIcon.Icon = new System.Drawing.Icon("../Pictures/WhirlwindSilverMini.ico");

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

        internal void set_overlay_icon(string baseIconPath, string overlayIconPath = null)
        {
            using var baseIcon = new System.Drawing.Icon(baseIconPath);

            using var bmp = new System.Drawing.Bitmap(32, 32);
            using var g = System.Drawing.Graphics.FromImage(bmp);

            g.DrawIcon(baseIcon, 0, 0);

            if (overlayIconPath == null) return;

            using var overlay = new System.Drawing.Bitmap(overlayIconPath);

            int overlaySize = 12;

            int x = 32 - overlaySize;
            int y = 32 - overlaySize;

            g.DrawImage(overlay, x, y, overlaySize, overlaySize);

            IntPtr hIcon = bmp.GetHicon();

            var hwnd = new WindowInteropHelper(this).Handle;

            Win32.SendMessage(hwnd, Win32.WM_SETICON, Win32.ICON_SMALL, hIcon);
            Win32.SendMessage(hwnd, Win32.WM_SETICON, Win32.ICON_BIG, hIcon);

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            this.Icon = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        internal void set_tray_icon(string ip)
        {
            trayIcon.Icon = new System.Drawing.Icon(ip);
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            var iconHandle = new System.Drawing.Icon(
                System.IO.Path.GetFullPath(ip)
            ).Handle;

            Win32.SendMessage(hwnd, Win32.WM_SETICON, Win32.ICON_SMALL, iconHandle);
            Win32.SendMessage(hwnd, Win32.WM_SETICON, Win32.ICON_BIG, iconHandle);

            this.Icon = new BitmapImage(new Uri(System.IO.Path.GetFullPath(ip)));
        }

        internal void load_devices()
        {
            device_item_list = QueryToSQL.get_devices();
            render_device_list();
        }

        internal void load_messages(string ip)
        {
            Dispatcher.InvokeAsync(() =>
            {
                MessagesPanel.Children.Clear();

                if (ip == null) return;

                foreach (ChatMessage message in QueryToSQL.get_messages(ip))
                {
                    control_messages(message);
                }
                for (int i = SavedMessages.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var s = SavedMessages[i];
                        if (s.ip_addressee == ip)
                        {
                            control_sending_message(s);
                        }
                    }
                    catch { }
                }
            });
        }

        internal void load_sent_unviewed()
        {
            sent_unviewed_messages.Clear();
            foreach (DeviceItem item in device_item_list)
            {
                if (QueryToSQL.get_sent_unviewed_by_ip(item.Ip))
                {
                    sent_unviewed_messages.Add(item.Ip);
                }
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
            Native.listening_port_messages(ip_address, Properties.Settings.Default.port_sender, ListenMessage);
            Native.listening_port_files(ip_address, Properties.Settings.Default.port_file_sender, ListenFiles);
            Native.listening_udp(ip_address, Properties.Settings.Default.port_udp_sender, ListenUDP);
            sand_online_note();
            sand_viewed_question();
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
                        handshake.Length
                    );

                }

                if (hasFiles)
                {
                    foreach (var filePath in attachedFiles)
                    {
                        (ushort version, byte[] extra) fileExtra = NetworkProtocols.build_file_extra_data_v1(0, 0);

                        SavedMessages.Add((2, fileExtra.version, fileExtra.extra, filePath, CurrentInterlocutor));

                        byte[] fileHandshake = NetworkProtocols.build_system_packet(
                            Properties.Settings.Default.ip_sender,
                            (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                            NetworkProtocols.build_system_extra_data_v0(
                                0,
                                2,
                                fileExtra.version
                            )
                        );

                        Native.send_message(
                            CurrentInterlocutor,
                            Properties.Settings.Default.port_sender,
                            fileHandshake,
                            fileHandshake.Length
                        );
                    }
                }
            }

            this.message.Text = "";
            attachedFiles.Clear();
            refresh_attached_files_list();
            load_messages(CurrentInterlocutor);
        }

        private void sand_online_note()
        {
            Task.Run(async () =>
            {
                //int i = 0;
                while (true)
                {
                    try
                    {
                        byte[] handshake = NetworkProtocols.build_system_packet(
                            Properties.Settings.Default.ip_sender,
                            (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                            NetworkProtocols.build_system_extra_data_v1(
                                0
                            )
                        );

                        foreach (DeviceItem device in device_item_list)
                        {
                            Native.send_udp(
                                device.Ip,
                                Properties.Settings.Default.port_udp_sender,
                                handshake,
                                handshake.Length
                            );
                        }
                    }
                    catch
                    {
                    }
                    await Task.Delay(4000);
                }
            });
            
        }

        private void sand_writing_note()
        {
            byte[] handshake = NetworkProtocols.build_system_packet(
                Properties.Settings.Default.ip_sender,
                (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                NetworkProtocols.build_system_extra_data_v1(
                    1
                )
            );

            Native.send_udp(
                CurrentInterlocutor,
                Properties.Settings.Default.port_udp_sender,
                handshake,
                handshake.Length
            );
        }

        private void sand_viewed_question()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    foreach (string ip in sent_unviewed_messages)
                    {
                        try
                        {
                            byte[] handshake = NetworkProtocols.build_system_packet(
                                Properties.Settings.Default.ip_sender,
                                (long)(DateTime.Now - DateTime.MinValue).TotalSeconds,
                                NetworkProtocols.build_system_extra_data_v1(
                                    2
                                )
                            );

                            Native.send_udp(
                                ip,
                                Properties.Settings.Default.port_udp_sender,
                                handshake,
                                handshake.Length
                            );
                        }
                        catch
                        {
                        }
                    }
                    await Task.Delay(1000);
                }
            });

        }

        private void add_device_Click(object sender, RoutedEventArgs e)
        {
            QueryToSQL.add_device();

            load_devices();
        }

        private void update_device_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var device = border?.Tag as DeviceItem;

            if (device == null)
                return;

            var devices = QueryToSQL.update_device(device);

            if (devices == (null, null, null))
                return;

            ChatTitle.Text = devices.chat_title;
            IpTitle.Text = devices.ip_title;
            CurrentInterlocutor = devices.current_interlocutor;

            load_devices();
            load_messages(CurrentInterlocutor);
        }

        private void delete_device_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var border = contextMenu?.PlacementTarget as Border;
            var device = border?.Tag as DeviceItem;

            if (device == null)
                return;

            try
            {
                string username = QueryToSQL.get_username_by_ip(device.Ip);
                string dir = System.IO.Path.GetFullPath($"../Files/{username}");

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

            QueryToSQL.delete_device(device);

            ChatTitle.Text = "_";
            IpTitle.Text = "_";
            CurrentInterlocutor = null;

            if (QueryToSQL.get_received_unviewed())
            {
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"), System.IO.Path.GetFullPath("../Pictures/NoticeDot.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMiniNotice.ico");
            }
            else
            {
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMini.ico");
            }

            mute_button.Visibility = Visibility.Collapsed;
            block_button.Visibility = Visibility.Collapsed;

            load_devices();
            load_messages(CurrentInterlocutor);
        }

        private void device_item_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var device = border?.Tag as DeviceItem;

            if (device == null)
                return;

            CurrentInterlocutor = device.Ip;

            clear_extra(device.Id);
            load_messages(device.Ip);
            change_muted_mode(device.Ip);
            change_blocked_mode(device.Ip);
            QueryToSQL.set_received_viewed(device.Ip);
            render_device_list();

            if (QueryToSQL.get_received_unviewed())
            {
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"), System.IO.Path.GetFullPath("../Pictures/NoticeDot.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMiniNotice.ico");
            }
            else
            {
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMini.ico");
            }

            if (CurrentInterlocutor == Properties.Settings.Default.ip_sender)
            {
                mute_button.Visibility = Visibility.Collapsed;
                block_button.Visibility = Visibility.Collapsed;
            }
            else
            {
                mute_button.Visibility = Visibility.Visible;
                block_button.Visibility = Visibility.Visible;
            }
        }

        private void control_messages(ChatMessage msg)
        {
            bool isFile = msg.MessageType == 2;

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
                    filePath = System.IO.Path.GetFullPath($"../Files/{username}/{msg.DisplayText}");
                    fileNameOnly = msg.DisplayText;
                }
            }

            UIElement bubble;

            if (isFile)
            {
                string ext = System.IO.Path.GetExtension(fileNameOnly).ToLower();
                string[] imageExts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                bool isImage = imageExts.Contains(ext);

                if (isImage)
                {
                    bubble = build_image_bubble(msg, filePath);
                }
                else
                {
                    bubble = build_file_bubble(msg, filePath, fileNameOnly);
                }
            }
            else
            {
                bubble = build_text_bubble(msg);
            }

            MessagesPanel.Children.Add(bubble);

            var dateText = new TextBlock
            {
                Text = msg.Date,
                Foreground = Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = msg.IsMyMessage
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 3, 3)
            };

            MessagesPanel.Children.Add(dateText);

            MessagesScroll.Dispatcher.InvokeAsync(() =>
            {
                MessagesScroll.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private Border build_image_bubble(ChatMessage msg, string filePath)
        {
            var border = new Border
            {
                Background = msg.IsMyMessage
                    ? new SolidColorBrush(Color.FromRgb(0, 122, 204))
                    : new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                Padding = new Thickness(4),
                Margin = msg.IsMyMessage
                    ? new Thickness(80, 0, 0, 2)
                    : new Thickness(0, 0, 80, 2),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                HorizontalAlignment = msg.IsMyMessage
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left
            };

            var panel = new StackPanel { Orientation = Orientation.Vertical };

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0),
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

            panel.Children.Add(img);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(panel, 0);
            grid.Children.Add(panel);

            if (msg.IsMyMessage)
            {
                var indicator = new TextBlock
                {
                    Text = msg.Viewed ? "✔✔" : "✔",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 3, 0)
                };

                Grid.SetRow(indicator, 1);
                grid.Children.Add(indicator);
            }

            border.Child = grid;

            border.Tag = new FileBubbleTag
            {
                MessageId = msg.Id,
                FilePath = filePath,
                ProgressBar = null
            };

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
                    MessageBox.Show("Не удалось открыть файл:\n" + ex.Message);
                }
            };

            var contextMenu = new ContextMenu();

            var copyItem = new MenuItem { Header = "Копировать" };
            copyItem.Click += (s, e) =>
            {
                var files = new System.Collections.Specialized.StringCollection();
                files.Add(filePath);
                Clipboard.SetFileDropList(files);
            };
            contextMenu.Items.Add(copyItem);

            var locationItem = new MenuItem { Header = "Расположение файла" };
            locationItem.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
            };
            contextMenu.Items.Add(locationItem);

            var deleteItem = new MenuItem { Header = "Удалить" };
            deleteItem.Click += (s, e) =>
            {
                int index = MessagesPanel.Children.IndexOf(border);

                if (index >= 0)
                {
                    MessagesPanel.Children.RemoveAt(index);

                    if (index < MessagesPanel.Children.Count &&
                        MessagesPanel.Children[index] is TextBlock)
                    {
                        MessagesPanel.Children.RemoveAt(index);
                    }
                }

                QueryToSQL.delete_message(msg.Id);

                string username = QueryToSQL.get_username_by_ip(CurrentInterlocutor);
                string userDir = System.IO.Path.GetFullPath($"../Files/{username}");

                if (filePath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
            };
            contextMenu.Items.Add(deleteItem);

            border.ContextMenu = contextMenu;

            return border;
        }

        private Border build_file_bubble(ChatMessage msg, string filePath, string fileNameOnly)
        {
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
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var fileRow = new StackPanel { Orientation = Orientation.Horizontal };

            fileRow.Children.Add(new TextBlock
            {
                Text = "📄",
                FontSize = 20,
                Margin = new Thickness(0, 0, 8, 0)
            });

            fileRow.Children.Add(new TextBlock
            {
                Text = fileNameOnly,
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(fileRow);

            border.Tag = new FileBubbleTag
            {
                MessageId = msg.Id,
                FilePath = filePath,
                ProgressBar = null
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(stack, 0);
            grid.Children.Add(stack);

            if (msg.IsMyMessage)
            {
                var indicator = new TextBlock
                {
                    Text = msg.Viewed ? "✔✔" : "✔",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 3, 0)
                };

                Grid.SetRow(indicator, 1);
                grid.Children.Add(indicator);
            }

            border.Child = grid;

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
                    MessageBox.Show("Не удалось открыть файл:\n" + ex.Message);
                }
            };

            var contextMenu = new ContextMenu();

            var copyItem = new MenuItem { Header = "Копировать" };
            copyItem.Click += (s, e) =>
            {
                var files = new System.Collections.Specialized.StringCollection();
                files.Add(filePath);
                Clipboard.SetFileDropList(files);
            };
            contextMenu.Items.Add(copyItem);

            var locationItem = new MenuItem { Header = "Расположение файла" };
            locationItem.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
            };
            contextMenu.Items.Add(locationItem);

            var deleteItem = new MenuItem { Header = "Удалить" };
            deleteItem.Click += (s, e) =>
            {
                int index = MessagesPanel.Children.IndexOf(border);

                if (index >= 0)
                {
                    MessagesPanel.Children.RemoveAt(index);

                    if (index < MessagesPanel.Children.Count &&
                        MessagesPanel.Children[index] is TextBlock)
                    {
                        MessagesPanel.Children.RemoveAt(index);
                    }
                }

                QueryToSQL.delete_message(msg.Id);

                string username = QueryToSQL.get_username_by_ip(CurrentInterlocutor);
                string userDir = System.IO.Path.GetFullPath($"../Files/{username}");

                if (filePath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                MessagesPanel.Children.Remove(border);
            };
            contextMenu.Items.Add(deleteItem);

            border.ContextMenu = contextMenu;

            return border;
        }

        private Border build_text_bubble(ChatMessage msg)
        {
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
                Tag = msg.Id
            };

            var textBlock = new TextBlock
            {
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            string[] parts = msg.DisplayText.Split(' ');
            foreach (string part in parts)
            {
                if (part.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var hyperlink = new Hyperlink(new Run(part))
                    {
                        NavigateUri = new Uri(part),
                        Foreground = msg.IsMyMessage ? Brushes.LightGray : Brushes.DeepSkyBlue
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

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            if (msg.IsMyMessage)
            {
                var indicator = new TextBlock
                {
                    Text = msg.Viewed ? "✔✔" : "✔",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 3, 0)
                };

                Grid.SetRow(indicator, 1);
                grid.Children.Add(indicator);
            }

            border.Child = grid;

            var contextMenu = new ContextMenu();

            var copyItem = new MenuItem { Header = "Копировать" };
            copyItem.Click += (s, e) =>
            {
                try
                {
                    string textFromDb = QueryToSQL.get_message_text(msg.Id);
                    Clipboard.SetText(textFromDb);
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

            var deleteItem = new MenuItem { Header = "Удалить" };
            deleteItem.Click += (s, e) =>
            {
                try
                {
                    int index = MessagesPanel.Children.IndexOf(border);

                    if (index >= 0)
                    {
                        MessagesPanel.Children.RemoveAt(index);

                        if (index < MessagesPanel.Children.Count &&
                            MessagesPanel.Children[index] is TextBlock)
                        {
                            MessagesPanel.Children.RemoveAt(index);
                        }
                    }

                    QueryToSQL.delete_message((int)border.Tag);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при удалении сообщения:\n{ex.Message}",
                        "Удаление сообщения",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            };
            contextMenu.Items.Add(deleteItem);

            border.ContextMenu = contextMenu;

            return border;
        }

        private Border build_downloading_file_bubble(ChatMessage msg, string filePath, string fileNameOnly)
        {
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
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var fileRow = new StackPanel { Orientation = Orientation.Horizontal };

            fileRow.Children.Add(new TextBlock
            {
                Text = "📄",
                FontSize = 20,
                Margin = new Thickness(0, 0, 8, 0)
            });

            fileRow.Children.Add(new TextBlock
            {
                Text = fileNameOnly,
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(fileRow);

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 6,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                Value = 0
            };

            border.Tag = new FileBubbleTag
            {
                MessageId = msg.Id,
                FilePath = filePath,
                ProgressBar = progressBar
            };

            stack.Children.Add(progressBar);

            border.Child = stack;

            return border;
        }

        private void control_sending_message((byte type, ushort version, byte[] extra_data, string message, string ip_addressee) saved)
        {
            UIElement bubble;

            if (is_existing_path(saved.message))
            {
                if (is_image_path(saved.message))
                    bubble = build_sending_image_bubble(saved);
                else
                    bubble = build_sending_file_bubble(saved);
            }
            else
            {
                bubble = build_sending_text_bubble(saved);
            }

            MessagesPanel.Children.Add(bubble);

            var dateText = new TextBlock
            {
                Text = DateTime.Now.ToString("dd/MM/yy HH:mm:ss"),
                Foreground = Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(3, 0, 3, 3)
            };

            MessagesPanel.Children.Add(dateText);

            MessagesScroll.Dispatcher.InvokeAsync(() =>
            {
                MessagesScroll.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private Border build_sending_text_bubble((byte type, ushort version, byte[] extra_data, string message, string ip_addressee) saved)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Padding = new Thickness(10),
                Margin = new Thickness(100, 0, 0, 2),
                CornerRadius = new CornerRadius(5),
            };

            var textBlock = new TextBlock
            {
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Text = saved.message,
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            var indicator = new TextBlock
            {
                Text = "●",
                Foreground = Brushes.White,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 3, 0)
            };

            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);

            border.Child = grid;


            return border;
        }

        private Border build_sending_file_bubble((byte type, ushort version, byte[] extra_data, string message, string ip_addressee) saved)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Padding = new Thickness(10),
                Margin = new Thickness(100, 0, 0, 2),
                CornerRadius = new CornerRadius(5),
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var fileRow = new StackPanel { Orientation = Orientation.Horizontal };

            fileRow.Children.Add(new TextBlock
            {
                Text = "📄",
                FontSize = 20,
                Margin = new Thickness(0, 0, 8, 0)
            });

            fileRow.Children.Add(new TextBlock
            {
                Text = System.IO.Path.GetFileName(saved.message),
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(fileRow);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(stack, 0);
            grid.Children.Add(stack);

            var indicator = new TextBlock
            {
                Text = "●",
                Foreground = Brushes.White,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 3, 0)
            };

            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);

            border.Child = grid;

            return border;
        }

        private Border build_sending_image_bubble((byte type, ushort version, byte[] extra_data, string message, string ip_addressee) saved)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Padding = new Thickness(4),
                Margin = new Thickness(80, 0, 0, 2),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var panel = new StackPanel { Orientation = Orientation.Vertical };

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0),
                MaxWidth = 350,
                MaxHeight = 350
            };

            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(saved.message, UriKind.Absolute);
                bmp.EndInit();

                img.Source = bmp;
            }
            catch
            {
                img.Source = null;
            }

            panel.Children.Add(img);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(panel, 0);
            grid.Children.Add(panel);

            var indicator = new TextBlock
            {
                Text = "●",
                Foreground = Brushes.White,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 3, 0)
            };

            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);

            border.Child = grid;

            return border;
        }

        private bool is_image_path(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLower();
            string[] imageExts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            return imageExts.Contains(ext);
        }

        private bool is_existing_path(string path)
        {
            return File.Exists(path);
        }

        private void add_message_to_db(string sender, string addressee, string seconds, byte device_type, byte message_type, string message)
        {
            if (sender == Properties.Settings.Default.ip_sender && CurrentInterlocutor != sender)
            {
                QueryToSQL.add_message_to_db(sender, addressee, seconds, device_type, message_type, message, 0);
                load_sent_unviewed();
            }
            else if (CurrentInterlocutor == null || CurrentInterlocutor != sender)
            {
                QueryToSQL.add_message_to_db(sender, addressee, seconds, device_type, message_type, message, 0);

                set_red(QueryToSQL.get_device_by_ip(sender).Id);
                set_overlay_icon(System.IO.Path.GetFullPath("../Pictures/WhirlWindSilverMini.ico"), System.IO.Path.GetFullPath("../Pictures/NoticeDot.ico"));
                set_tray_icon("../Pictures/WhirlwindSilverMiniNotice.ico");

                if (!was_read_messages.Contains(sender))
                    was_read_messages.Add(sender);
            }
            else
            {
                QueryToSQL.add_message_to_db(sender, addressee, seconds, device_type, message_type, message, 1);
                if (!was_read_messages.Contains(sender))
                    was_read_messages.Add(sender);
            }

            render_device_list();
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
                if (e.Delta > 0)
                {
                    listScroll.LineUp();
                    listScroll.LineUp();
                }
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
            load_devices();
        }

        private void block_button_Click(object sender, RoutedEventArgs e)
        {
            current_blocked_mode = !current_blocked_mode;

            if (current_blocked_mode)
            {
                block_button.Content = "🔓";
                block_button.ToolTip = "Разблокировать";

                QueryToSQL.set_device_blocked(CurrentInterlocutor, true);
            }
            else
            {
                block_button.Content = "⛔";
                block_button.ToolTip = "Заблокировать";

                QueryToSQL.set_device_blocked(CurrentInterlocutor, false);
            }
            load_devices();
        }

        internal void change_muted_mode(string ip)
        {
            switch (device_item_list.FirstOrDefault(d => d.Ip == ip).Muted)
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

        internal void change_blocked_mode(string ip)
        {
            if (device_item_list.FirstOrDefault(d => d.Ip == ip).Blocked == 1)
            {
                block_button.Content = "🔓";
                block_button.ToolTip = "Разблокировать";

                current_blocked_mode = true;
            }
            else
            {
                block_button.Content = "⛔";
                block_button.ToolTip = "Заблокировать";

                current_blocked_mode = false;
            }
        }

        private DateTime _lastWriteNoteTime = DateTime.MinValue;

        private void message_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentInterlocutor == Properties.Settings.Default.ip_sender) return;

            var now = DateTime.Now;

            if ((now - _lastWriteNoteTime).TotalMilliseconds < 500)
                return;

            _lastWriteNoteTime = now;

            sand_writing_note();
        }

        internal void set_online(int id, int ms = 3000)
        {
            BaseStates[id] = DeviceBaseState.Online;

            if (BaseTimers.TryGetValue(id, out var old))
                old.Cancel();

            var cts = new CancellationTokenSource();
            BaseTimers[id] = cts;
            var token = cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ms, token);

                    BaseStates[id] = DeviceBaseState.Offline;
                    render_device_list();
                }
                catch { }
            });
        }

        internal void set_red(int id)
        {
            OverlayStates[id] = DeviceOverlayState.Red;
        }

        internal void clear_extra(int id)
        {
            OverlayStates[id] = DeviceOverlayState.None;
        }

        internal void set_dots(int id, int ms = 2000)
        {
            if (!OverlayUnderDots.ContainsKey(id))
            {
                var prev = OverlayStates.TryGetValue(id, out var p)
                    ? p
                    : DeviceOverlayState.None;

                OverlayUnderDots[id] = prev;
            }

            OverlayStates[id] = DeviceOverlayState.ThreeDots;

            if (OverlayTimers.TryGetValue(id, out var old))
                old.Cancel();

            var cts = new CancellationTokenSource();
            OverlayTimers[id] = cts;
            var token = cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ms, token);

                    if (OverlayStates[id] == DeviceOverlayState.ThreeDots)
                    {
                        var previous = OverlayUnderDots.TryGetValue(id, out var prev)
                            ? prev
                            : DeviceOverlayState.None;

                        if (previous == DeviceOverlayState.Red)
                            OverlayStates[id] = DeviceOverlayState.Red;
                        else
                            OverlayStates[id] = DeviceOverlayState.None;
                    }

                    OverlayUnderDots.Remove(id);

                    render_device_list();
                }
                catch { }
            });
        }

        internal void render_device_list()
        {
            Dispatcher.InvokeAsync(() =>
            {
                DeviceList.Items.Clear();

                foreach (var device in device_item_list)
                {
                    DeviceList.Items.Add(create_device_row(device));
                }
                render_header_state(CurrentInterlocutor);
            });
        }

        public void render_header_state(string ip)
        {
            var device = device_item_list.FirstOrDefault(d => d.Ip == ip);
            if (device == null)
            {
                HeaderEllipse.Fill = Brushes.Transparent;
                HeaderEllipse.Stroke = Brushes.Silver;
                return;
            }
                
            var baseState = BaseStates.TryGetValue(device.Id, out var b)
                ? b
                : DeviceBaseState.Offline;

            var overlayState = OverlayStates.TryGetValue(device.Id, out var o)
                ? o
                : DeviceOverlayState.None;

            if (baseState == DeviceBaseState.Online)
            {
                HeaderEllipse.Fill = Brushes.LimeGreen;
                HeaderEllipse.Stroke = Brushes.LimeGreen;
            }
            else
            {
                HeaderEllipse.Fill = Brushes.Transparent;
                HeaderEllipse.Stroke = Brushes.Silver;
            }

            switch (overlayState)
            {
                case DeviceOverlayState.Red:
                    HeaderEllipse.Fill = Brushes.Red;
                    HeaderEllipse.Stroke = Brushes.Red;
                    break;

                case DeviceOverlayState.ThreeDots:
                    HeaderEllipse.Fill = (Brush)FindResource("DotsBrush");
                    HeaderEllipse.Stroke = Brushes.Transparent;
                    break;
            }

            ChatTitle.Text = device.Name;
            IpTitle.Text = device.Ip;
        }

        private Border create_device_row(DeviceItem device)
        {
            var baseState = BaseStates.TryGetValue(device.Id, out var b)
                ? b
                : DeviceBaseState.Offline;

            var overlayState = OverlayStates.TryGetValue(device.Id, out var o)
                ? o
                : DeviceOverlayState.None;

            var ellipse = new Ellipse
            {
                Width = 18,
                Height = 18,
                StrokeThickness = 2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            if (baseState == DeviceBaseState.Online)
            {
                ellipse.Fill = Brushes.LimeGreen;
                ellipse.Stroke = Brushes.LimeGreen;
            }
            else
            {
                ellipse.Fill = Brushes.Transparent;
                ellipse.Stroke = Brushes.Silver;
            }

            switch (overlayState)
            {
                case DeviceOverlayState.Red:
                    ellipse.Fill = Brushes.Red;
                    ellipse.Stroke = Brushes.Red;
                    break;

                case DeviceOverlayState.ThreeDots:
                    ellipse.Fill = (Brush)FindResource("DotsBrush");
                    ellipse.Stroke = Brushes.Transparent;
                    break;
            }

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = device.Name, Foreground = Brushes.White, FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = device.Ip, Foreground = Brushes.Gray, FontSize = 12 });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

            Grid.SetColumn(stack, 0);
            Grid.SetColumn(ellipse, 1);

            grid.Children.Add(stack);
            grid.Children.Add(ellipse);

            var contextMenu = new ContextMenu();

            var updateItem = new MenuItem { Header = "Изменить" };
            updateItem.Click += update_device_Click;

            var deleteItem = new MenuItem { Header = "Удалить" };
            deleteItem.Click += delete_device_Click;

            contextMenu.Items.Add(updateItem);
            contextMenu.Items.Add(deleteItem);

            var border = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                Child = grid,
                Tag = device,
                ContextMenu = contextMenu
            };

            border.MouseLeftButtonUp += device_item_Click;

            return border;
        }
    }
}