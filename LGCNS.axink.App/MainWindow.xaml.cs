using CommunityToolkit.Mvvm.Messaging;
using LGCNS.axink.App.Services;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Settings;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.IO;
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

        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly ISettingsMonitor<AppSettings> _appSettings;
        private readonly IMessenger _messenger;
        private readonly DeviceChangeHub _deviceChangeHub;

        public MainWindow(ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings, IMessenger messenger, DeviceChangeHub hub)
        {
            InitializeComponent();

            _sysSettings = sysSettings;
            _appSettings = appSettings;
            _messenger = messenger;
            _deviceChangeHub = hub;
            _deviceChangeHub.DeviceListChanged += DeviceChangeHub_DeviceListChanged;

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;

            _messenger.Register<SettingsChangedMessage<AppSettings>>(this, (_, msg) =>
            {
                NavigateIfPossible(msg.Value.WebViewSource);
            });
        }

        private void DeviceChangeHub_DeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {

                if (WebView?.CoreWebView2 != null)
                {
                    var data = new
                    {
                        type = "deviceChanged",
                        data = JsonConvert.DeserializeObject(e.Devices)
                    };
                    Logging.Debug(JsonConvert.SerializeObject(data));
                    WebView.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(data));
                }
            });

        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //await WebView.EnsureCoreWebView2Async();
                //WebView.Source = new Uri("https://example.com");

                await InitializeWebView2Async();

                // 1) MainWindow 시작 시 저장된 WebViewSource가 없으면 팝업
                if (string.IsNullOrWhiteSpace(_appSettings.Current.WebViewSource))
                {
                    var ok = App.ShowWebViewSourceSettingsDialog(this);
                    if (ok != true)
                    {
                        // 정책: 필수값이므로 취소하면 종료(원하면 그냥 빈 화면 유지로 변경 가능)
                        Close();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                WebView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Consts.APP_COMPANY, Consts.APP_NAME, "WebView2"
                );

                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder
                );

                await WebView.EnsureCoreWebView2Async(env);
                NavigateIfPossible(_appSettings.Current.WebViewSource);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 초기화 실패: {ex.Message}");
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            WebView.CoreWebView2.Settings.UserAgent = "wpf-axink";
        }

        private void NavigateIfPossible(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var trimmed = url.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return;

            WebView.Source = uri;

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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            App.ShowWebViewSourceSettingsDialog(this);
        }
    }
}