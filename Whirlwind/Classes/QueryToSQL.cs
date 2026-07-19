using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Whirlwind.Views;

namespace Whirlwind
{
    internal class QueryToSQL
    {
        public static void set_default_ip_address()
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string update = $@"UPDATE Device SET ip = '{Properties.Settings.Default.ip_sender}' WHERE type = '1'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в set_default_ip_address:\n{ex.Message}");
            }
        }

        public static List<DeviceItem> get_devices()
        {
            var fin = new List<DeviceItem>();

            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string query = $@"SELECT id, name, ip FROM Device";

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fin.Add(new DeviceItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Ip = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в get_devices:\n{ex.Message}");
            }

            return fin;
        }

        public static DeviceItem get_device_by_ip(string ip)
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string query = $@"SELECT id, name, ip FROM Device WHERE ip = '{ip}' LIMIT 1";

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            return new DeviceItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Ip = reader.GetString(2)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в get_device_by_ip:\n{ex.Message}");
            }

            return null;
        }

        public static List<ChatMessage> get_messages(string ip)
        {
            var fin = new List<ChatMessage>();

            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string query;

                    if (ip == Properties.Settings.Default.ip_sender)
                    {
                        query = $@"SELECT 
                        Message.ID,
                        Message.text,
                        Message.date,
                        Message.message_type,
                        (SELECT ip FROM Device WHERE ID = Message.sender) AS sender_ip
                    FROM Message
                    WHERE 
                        Message.sender = (SELECT ID FROM Device WHERE ip = '{ip}')
                        AND Message.addressee = (SELECT ID FROM Device WHERE ip = '{ip}')
                    ORDER BY Message.ID;";
                    }
                    else
                    {
                        query = $@"SELECT 
                        Message.ID,
                        Message.text,
                        Message.date,
                        Message.message_type,
                        (SELECT ip FROM Device WHERE ID = Message.sender) AS sender_ip
                    FROM Message
                    WHERE 
                        Message.sender = (SELECT ID FROM Device WHERE ip = '{ip}')
                        OR Message.addressee = (SELECT ID FROM Device WHERE ip = '{ip}')
                    ORDER BY Message.ID;";
                    }

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sender_ip = reader.GetString(4);
                            var p = reader.GetString(2).Split('-');

                            fin.Add(new ChatMessage
                            {
                                Id = reader.GetInt32(0),
                                Text = reader.GetString(1),
                                Date = $"{p[2]}/{p[1]}/{p[0]} {p[3]}:{p[4]}:{p[5]}",
                                MessageType = reader.GetInt32(3),
                                IsMyMessage = sender_ip == Properties.Settings.Default.ip_sender
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в get_messages:\n{ex.Message}");
            }

            return fin;
        }

        public static string get_message_text(int id)
        {
            try
            {
                string query = $@"SELECT text FROM Message WHERE ID = {id} LIMIT 1";

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    using (var cmd = new SqliteCommand(query, connection))
                    {
                        object result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "";
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        public static byte get_device_type(string ip)
        {
            try
            {
                string query = $@"SELECT type FROM Device WHERE ip = '{ip}' LIMIT 1";

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    using (var command = new SqliteCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return byte.Parse(result.ToString());
                    }
                }
            }
            catch
            {
                return 2;
            }
        }

        public static bool IsValidDirectoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            char[] invalidChars = Path.GetInvalidFileNameChars();

            if (name.Any(c => invalidChars.Contains(c)))
                return false;

            string[] reserved =
            {
                "CON","PRN","AUX","NUL",
                "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
                "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
            };

            if (reserved.Contains(name.ToUpper()))
                return false;

            return true;
        }



        public static void add_device()
        {
            try
            {
                var add_user_window = new AddUser();
                add_user_window.ShowDialog();

                string ip = add_user_window.IpAddresssee;
                string name = add_user_window.NameAddresssee;

                if (ip == null || name == null)
                    return;

                if (!IsValidDirectoryName(name))
                {
                    MessageBox.Show(
                        "Имя устройства содержит недопустимые символы.\n" +
                        "Папка не может быть создана.",
                        "Ошибка имени",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string insert = @"INSERT INTO Device (ip, type, name) 
                              VALUES (@ip, '2', @name)";

                    using (var cmd = new SqliteCommand(insert, connection))
                    {
                        cmd.Parameters.AddWithValue("@ip", ip);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Создаём директорию
                string baseDir = Path.GetFullPath("Files");
                Directory.CreateDirectory(Path.Combine(baseDir, name));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в add_user:\n{ex.Message}");
            }
        }



        public static (string chat_title, string ip_title, string current_interlocutor) update_device(DeviceItem device)
        {
            try
            {
                int id = device.Id;
                var add_user_window = new AddUser();

                add_user_window.add_ip_address.Text = device.Ip;
                add_user_window.add_name.Text = device.Name;

                add_user_window.ShowDialog();

                string newIp = add_user_window.IpAddresssee;
                string newName = add_user_window.NameAddresssee;

                if (newIp == null || newName == null)
                    return (null, null, null);

                if (!IsValidDirectoryName(newName))
                {
                    MessageBox.Show(
                        "Новое имя устройства содержит недопустимые символы.\n" +
                        "Переименование отменено.",
                        "Ошибка имени",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return (null, null, null);
                }

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string update = @"UPDATE Device 
                              SET ip = @ip, name = @name 
                              WHERE id = @id AND type = '2'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.Parameters.AddWithValue("@ip", newIp);
                        cmd.Parameters.AddWithValue("@name", newName);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                string baseDir = Path.GetFullPath("Files");
                string oldDir = Path.Combine(baseDir, device.Name);
                string newDir = Path.Combine(baseDir, newName);

                if (Directory.Exists(oldDir))
                {
                    Directory.Move(oldDir, newDir);
                }

                return (newName, newIp, newIp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в update_device:\n{ex.Message}");
                return (null, null, null);
            }
        }



        public static void delete_device(DeviceItem device)
        {
            try
            {
                int id = device.Id;
                string delete;

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    if (device.Ip != Properties.Settings.Default.ip_sender)
                    {
                        delete = $@"DELETE FROM Message WHERE addressee = '{id}' or sender = '{id}';
                                DELETE FROM Device WHERE id = '{id}' and type = '2'";
                    }
                    else
                    {
                        delete = $@"DELETE FROM Message WHERE (addressee = '{id}' or sender = '{id}') 
                                and addressee = sender";
                    }

                    using (var cmd = new SqliteCommand(delete, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в delete_device:\n{ex.Message}");
            }
        }

        public static void add_message_to_db(string sender, string addressee, string seconds, byte device_type, byte message_type, string message)
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string check = $@"SELECT count(*) FROM Device WHERE ip = '{sender}'";

                    using (var command = new SqliteCommand(check, connection))
                    {
                        if ((long)command.ExecuteScalar() <= 0)
                        {
                            string baseName = "Неизвестный";
                            string finalName = baseName;

                            string nameCheckQuery = $@"SELECT count(*) FROM Device WHERE name = '{finalName}'";

                            using (var nameCmd = new SqliteCommand(nameCheckQuery, connection))
                            {
                                long count = (long)nameCmd.ExecuteScalar();

                                int counter = 1;

                                while (count > 0)
                                {
                                    finalName = $"{baseName} {counter}";
                                    nameCmd.CommandText = $@"SELECT count(*) FROM Device WHERE name = '{finalName}'";
                                    count = (long)nameCmd.ExecuteScalar();
                                    counter++;
                                }
                            }

                            string add_device = $@"INSERT INTO Device (ip, type, name) 
                                           VALUES ('{sender}', '{device_type}', '{finalName}')";

                            using (var cmd = new SqliteCommand(add_device, connection))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    string insert = $@"INSERT INTO Message(sender, addressee, message_type, text, date)
                               VALUES ((SELECT ID FROM Device WHERE ip = '{sender}'), 
                               (SELECT ID FROM Device WHERE ip = '{addressee}'), '{message_type}', '{message}', 
                               '{seconds}')";

                    using (var cmd = new SqliteCommand(insert, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в add_message_to_db:\n{ex.Message}");
            }
        }


        public static void delete_message(int id)
        {
            try
            {
                string delete = $@"DELETE FROM Message WHERE ID = {id}";

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();
                    using (var cmd = new SqliteCommand(delete, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в delete_message:\n{ex.Message}");
            }
        }

        public static string get_username_by_ip(string ip)
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string query = $@"SELECT name FROM Device WHERE ip = '{ip}' LIMIT 1";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();

                        if (result != null)
                            return result.ToString();
                    }

                    string baseName = "Неизвестный";
                    string finalName = baseName;

                    string nameCheckQuery = $@"SELECT count(*) FROM Device WHERE name = '{finalName}'";

                    using (var nameCmd = new SqliteCommand(nameCheckQuery, connection))
                    {
                        long count = (long)nameCmd.ExecuteScalar();
                        int counter = 1;

                        while (count > 0)
                        {
                            finalName = $"{baseName} {counter}";
                            nameCmd.CommandText = $@"SELECT count(*) FROM Device WHERE name = '{finalName}'";
                            count = (long)nameCmd.ExecuteScalar();
                            counter++;
                        }
                    }

                    string insert = $@"INSERT INTO Device (ip, type, name)
                               VALUES ('{ip}', '2', '{finalName}')";

                    using (var cmd = new SqliteCommand(insert, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    return finalName;
                }
            }
            catch
            {
                return "Неизвестный";
            }
        }

        public static sbyte get_device_muted(string ip)
        {
            try
            {
                string query = $@"SELECT muted FROM Device WHERE ip = '{ip}' LIMIT 1";

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    using (var command = new SqliteCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return sbyte.Parse(result.ToString());
                    }
                }
            }
            catch
            {
                return 2;
            }
        }

        public static void set_device_muted(string ip, sbyte muted)
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string update = $@"UPDATE Device SET muted = '{muted}' WHERE ip = '{ip}'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в set_default_ip_address:\n{ex.Message}");
            }
        }

        public static sbyte get_device_blocked(string ip)
        {
            try
            {
                string query = $@"SELECT blocked FROM Device WHERE ip = '{ip}' LIMIT 1";

                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    using (var command = new SqliteCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return sbyte.Parse(result.ToString());
                    }
                }
            }
            catch
            {
                return 2;
            }
        }

        public static void set_device_blocked(string ip, sbyte muted)
        {
            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connection_string))
                {
                    connection.Open();

                    string update = $@"UPDATE Device SET blocked = '{muted}' WHERE ip = '{ip}'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка SQL в set_default_ip_address:\n{ex.Message}");
            }
        }
    }
}
