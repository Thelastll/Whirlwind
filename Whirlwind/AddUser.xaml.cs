using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Логика взаимодействия для AddUser.xaml
    /// </summary>
    public partial class AddUser : Window
    {
        public string IpAddresssee { get; private set; } = null;
        public string NameAddresssee { get; private set; } = null;

        public AddUser()
        {
            InitializeComponent();
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
            if (add_ip_address.Text.Trim() == "") return;
            if (add_name.Text.Trim() == "") add_name.Text = "_";

            IpAddresssee = add_ip_address.Text.Trim();
            NameAddresssee = add_name.Text.Trim();

            Close();
        }
    }
}
