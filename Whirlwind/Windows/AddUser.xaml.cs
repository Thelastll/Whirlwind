using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Whirlwind
{
    public partial class AddUser : Window
    {
        public string IpAddresssee { get; private set; } = null;
        public string NameAddresssee { get; private set; } = null;

        public AddUser()
        {
            InitializeComponent();
        }

        public void SetIp(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                ip1.Text = parts[0];
                ip2.Text = parts[1];
                ip3.Text = parts[2];
                ip4.Text = parts[3];
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
            if (string.IsNullOrWhiteSpace(ip1.Text) ||
                string.IsNullOrWhiteSpace(ip2.Text) ||
                string.IsNullOrWhiteSpace(ip3.Text) ||
                string.IsNullOrWhiteSpace(ip4.Text))
            {
                MessageBox.Show("Заполните все части IP-адреса.",
                                "Ошибка ввода",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            if (add_name.Text.Contains("'"))
            {
                MessageBox.Show("Имя не должно содержать символ '.",
                                "Ошибка имени",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            if (GetFullIp().Trim() == "") return;
            if (add_name.Text.Trim() == "") add_name.Text = "_";

            IpAddresssee = GetFullIp().Trim();
            NameAddresssee = add_name.Text.Trim();

            Close();
        }

        private string GetFullIp()
        {
            return $"{ip1.Text}.{ip2.Text}.{ip3.Text}.{ip4.Text}";
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
