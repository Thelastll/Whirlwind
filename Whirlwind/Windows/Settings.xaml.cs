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
using Whirlwind.Classes;

namespace Whirlwind.Windows
{
    /// <summary>
    /// Логика взаимодействия для Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        bool WindowFirstOpen = true;
        public Settings()
        {
            InitializeComponent();

            VolumeSlider.Value = Properties.Settings.Default.Volume;

            WindowFirstOpen = false;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WindowFirstOpen) return;

            float v = (float)e.NewValue;

            PlaySounds.GlobalVolume = v;

            Properties.Settings.Default.Volume = v;
            Properties.Settings.Default.Save();
        }
    }
}
