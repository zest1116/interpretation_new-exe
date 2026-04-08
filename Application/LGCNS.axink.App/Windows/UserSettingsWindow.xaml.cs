using LGCNS.axink.App.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace LGCNS.axink.App.Windows
{
    public partial class UserSettingsWindow : Window
    {
        public UserSettingsWindow(UserSettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.RequestClose += dr =>
            {
                DialogResult = dr;
                Close();
            };
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}