using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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
                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

        public static List<ChatMessage> get_messages(string ip)
        {
            var fin = new List<ChatMessage>();

            try
            {
                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

        public static void add_user()
        {
            try
            {
                var add_user_window = new AddUser();
                add_user_window.ShowDialog();

                if (add_user_window.IpAddresssee == null || add_user_window.NameAddresssee == null) return;

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
                {
                    connection.Open();

                    string insert = $@"INSERT INTO Device (ip, type, name) 
                            VALUES ('{add_user_window.IpAddresssee}', '2', '{add_user_window.NameAddresssee}')";

                    using (var cmd = new SqliteCommand(insert, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
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

                if (add_user_window.IpAddresssee == null || add_user_window.NameAddresssee == null)
                    return (null, null, null);

                string oldName = device.Name;
                string newName = add_user_window.NameAddresssee;

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
                {
                    connection.Open();

                    string update = $@"UPDATE Device SET ip = '{add_user_window.IpAddresssee}',
                name = '{add_user_window.NameAddresssee}' WHERE id = '{id}' and type = '2'";

                    using (var cmd = new SqliteCommand(update, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (oldName != newName)
                {
                    try
                    {
                        string baseDir = System.IO.Path.GetFullPath("../../../../Files");

                        string oldDir = System.IO.Path.Combine(baseDir, oldName);
                        string newDir = System.IO.Path.Combine(baseDir, newName);

                        if (Directory.Exists(oldDir))
                        {
                            if (Directory.Exists(newDir))
                            {
                                int counter = 2;
                                string uniqueDir;

                                do
                                {
                                    uniqueDir = System.IO.Path.Combine(baseDir, $"{newName} ({counter})");
                                    counter++;
                                }
                                while (Directory.Exists(uniqueDir));

                                newDir = uniqueDir;
                            }

                            Directory.Move(oldDir, newDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при обновлении директории пользователя:\n{ex.Message}",
                                        "Обновление директории",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                    }
                }

                return (add_user_window.add_name.Text, add_user_window.add_ip_address.Text, add_user_window.add_ip_address.Text);
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

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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
                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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
                using (var connection = new SqliteConnection(Properties.Settings.Default.connectionString))
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

    }

}
