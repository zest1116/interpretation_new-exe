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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace LGCNS.axink.App
{
    public partial class MainWindow : Window
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        #region WIN32 메시지

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        #endregion

        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly ISettingsMonitor<AppSettings> _appSettings;
        private readonly IMessenger _messenger;
        private readonly DeviceChangeHub _deviceChangeHub;

        public MainWindow(
            ISettingsMonitor<SystemSettings> sysSettings,
            ISettingsMonitor<AppSettings> appSettings,
            IMessenger messenger,
            DeviceChangeHub hub)
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
                await InitializeWebView2Async();

                if (string.IsNullOrWhiteSpace(_appSettings.Current.WebViewSource))
                {
                    var ok = App.ShowWebViewSourceSettingsDialog(this);
                    if (ok != true)
                    {
                        Close();
                        return;
                    }
                }

                UpdateVisualState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                WebView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Consts.APP_COMPANY,
                    Consts.APP_NAME,
                    "WebView2");

                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await WebView.EnsureCoreWebView2Async(env);

                NavigateIfPossible(_appSettings.Current.WebViewSource);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 초기화 실패: {ex.Message}");
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
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

            MainFrame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
            TitleBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10, 10, 0, 0);
            ContentBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 10, 10);
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
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();

                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT workArea = monitorInfo.rcWork;
                    RECT monitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
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