using LGCNS.axink.Common;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LGCNS.axink.App.Windows
{
    public static class AlertDialog
    {
        public static AlertDialogResult ShowOk(
            Window? owner,
            string title,
            string message,
            string? dialogTitle = null)
        {
            dialogTitle = Application.Current.Resources["Dic_Common_Information"].ToString() ?? "알림";
            var dialog = new AlertDialogWindow(
                dialogTitle: dialogTitle,
                messageTitle: title,
                messageBody: message,
                buttonSet: AlertDialogButtonSet.Ok,
                owner: owner);

            dialog.ShowDialog();
            return dialog.Result;
        }

        public static AlertDialogResult ShowYesNo(
            Window? owner,
            string title,
            string message,
            string? dialogTitle = null)
        {
            dialogTitle = Application.Current.Resources["Dic_Common_Confirm"].ToString() ?? "확인";
            var dialog = new AlertDialogWindow(
                dialogTitle: dialogTitle,
                messageTitle: title,
                messageBody: message,
                buttonSet: AlertDialogButtonSet.YesNo,
                owner: owner);

            dialog.ShowDialog();
            return dialog.Result;
        }
    }

    /// <summary>
    /// AlertDialogWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AlertDialogWindow : Window
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;

        public string DialogTitle { get; }
        public string MessageTitle { get; }
        public string MessageBody { get; }
        
        public AlertDialogResult Result { get; private set; } = AlertDialogResult.None;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);

        public AlertDialogWindow(
            string dialogTitle,
            string messageTitle,
            string messageBody,
            AlertDialogButtonSet buttonSet,
            Window? owner = null)
        {
            InitializeComponent();
            dialogTitle = Application.Current.Resources["Dic_Common_Information"].ToString() ?? "알림";
            DialogTitle = dialogTitle;
            MessageTitle = messageTitle;
            MessageBody = messageBody;

            DataContext = this;
            Owner = owner;

            ConfigureButtons(buttonSet);

            SourceInitialized += AlertDialogWindow_SourceInitialized;
            Loaded += AlertDialogWindow_Loaded;
        }

        private void ConfigureButtons(AlertDialogButtonSet buttonSet)
        {
            switch (buttonSet)
            {
                case AlertDialogButtonSet.Ok:
                    OkButton.Visibility = Visibility.Visible;
                    OkButton.IsDefault = true;
                    OkButton.Focus();
                    break;

                case AlertDialogButtonSet.YesNo:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    YesButton.IsDefault = true;
                    NoButton.IsCancel = true;
                    YesButton.Focus();
                    break;
            }
        }

        private void AlertDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
        }

        private void AlertDialogWindow_SourceInitialized(object? sender, EventArgs e)
        {
            //ApplyCornerPreference();
            //UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool maximized = WindowState == WindowState.Maximized;

            MainFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
            MainFrame.Margin = maximized ? new Thickness(0) : new Thickness(1);
        }

        private void ApplyCornerPreference()
        {
            if (Environment.OSVersion.Version.Build < 22000)
                return;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int pref = WindowState == WindowState.Maximized ? DWMWCP_DONOTROUND : DWMWCP_ROUND;

            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, Marshal.SizeOf<int>());
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertDialogResult.Ok;
            DialogResult = true;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertDialogResult.Yes;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AlertDialogResult.No;
            DialogResult = false;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (YesButton.Visibility == Visibility.Visible || NoButton.Visibility == Visibility.Visible)
            {
                Result = AlertDialogResult.No;
                DialogResult = false;
            }
            else
            {
                Result = AlertDialogResult.Ok;
                DialogResult = true;
            }

            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (YesButton.Visibility == Visibility.Visible || NoButton.Visibility == Visibility.Visible)
                {
                    Result = AlertDialogResult.No;
                    DialogResult = false;
                }
                else
                {
                    Result = AlertDialogResult.Ok;
                    DialogResult = true;
                }

                Close();
            }
        }
    }
}
