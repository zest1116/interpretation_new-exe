using CommunityToolkit.Mvvm.Messaging;
using LGCNS.axink.App.Services;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Settings;
using Microsoft.Web.WebView2.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LGCNS.axink.App
{
    public partial class MainWindow : Window
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;


        #region WIN32 메시지

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        #endregion

        public MainWindow(ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings, IMessenger messenger, DeviceChangeHub hub)
        {
            InitializeComponent();

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //await WebView.EnsureCoreWebView2Async();
                //WebView.Source = new Uri("https://example.com");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            ApplyCornerPreference();
            UpdateVisualState();

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource? source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            ApplyCornerPreference();
            UpdateVisualState();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsFromCaptionButton(e.OriginalSource))
                return;

            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private static bool IsFromCaptionButton(object? originalSource)
        {
            DependencyObject? current = originalSource as DependencyObject;

            while (current != null)
            {
                if (current is Button)
                    return true;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void UpdateVisualState()
        {
            bool maximized = WindowState == WindowState.Maximized;

            MainFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12);
            TitleBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12, 12, 0, 0);
            ContentBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 12, 12);
            MainFrame.Margin = maximized ? new Thickness(0) : new Thickness(1);

            if (MaximizeGlyph != null && RestoreGlyph != null)
            {
                MaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
                RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyCornerPreference()
        {
            if (Environment.OSVersion.Version.Build < 22000)
                return;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int pref = WindowState == WindowState.Maximized
                ? DWMWCP_DONOTROUND
                : DWMWCP_ROUND;

            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, Marshal.SizeOf<int>());
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            return IntPtr.Zero;
        }

    }
}