using LGCNS.axink.App.ViewModels;
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

namespace LGCNS.axink.App.Windows
{
    /// <summary>
    /// AppSettingsWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AppSettingsWindow : Window
    {
        public AppSettingsWindow(AppSettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.RequestClose += dr =>
            {
                DialogResult = dr;
                Close();
            };
        }
    }
}
