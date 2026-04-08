using CommunityToolkit.Mvvm.Messaging;
using LGCNS.axink.App.Services;
using LGCNS.axink.App.Windows;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Devices;
using LGCNS.axink.Models.Settings;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

        private readonly ISettingsMonitor<UserSettings> _userSettings;
        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly ISettingsMonitor<AppSettings> _appSettings;
        private readonly IMessenger _messenger;
        private readonly IDeviceService _deviceService;
        private readonly DeviceChangeHub _deviceChangeHub;
        private readonly WebViewBridge _webViewBridge;

        public MainWindow(
            ISettingsMonitor<UserSettings> userSettings,
            ISettingsMonitor<SystemSettings> sysSettings,
            ISettingsMonitor<AppSettings> appSettings,
            IMessenger messenger,
            IDeviceService deviceService,
            DeviceChangeHub hub,
            WebViewBridge webViewBridge)
        {
            InitializeComponent();

            _userSettings = userSettings;
            _sysSettings = sysSettings;
            _appSettings = appSettings;
            _messenger = messenger;
            _deviceService = deviceService;
            _deviceChangeHub = hub;
            _webViewBridge = webViewBridge;
            _deviceChangeHub.DeviceListChanged += DeviceChangeHub_DeviceListChanged;

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;

            _messenger.Register<SettingsChangedMessage<UserSettings>>(this, (_, msg) =>
            {
                NavigateIfPossible(msg.Value.WebViewSource);
            });
        }


        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var rawJson = e.WebMessageAsJson;

                Logging.Debug($"[WebViewBridge] SPA → WPF 수신: {rawJson}");

                var msg = JObject.Parse(rawJson);
                var type = msg.Value<string>("type");

                if (type == "showDeviceWindow")
                {
                    var win = new Windows.DeviceControllerWindow(_deviceService, _deviceChangeHub, this);
                    win.ShowDialog();
                }
                else
                {

                    var response = await _webViewBridge.HandleMessageAsync(rawJson);

                    // 응답이 있으면 SPA로 전달
                    if (response != null && WebView?.CoreWebView2 != null)
                    {
                        Logging.Debug($"[WebViewBridge] WPF → SPA 응답: {response}");
                        WebView.CoreWebView2.PostWebMessageAsJson(response);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "[WebViewBridge] WebMessageReceived 처리 중 오류");
            }
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
                // 저장된 위치/크기 복원
                Left = _sysSettings.Current.WindowLeft;
                Top = _sysSettings.Current.WindowTop;
                Width = _sysSettings.Current.WindowWidth;
                Height = _sysSettings.Current.WindowHeight;
                WindowState = (WindowState)_sysSettings.Current.WindowState;

                // 화면 밖으로 나가지 않도록 보정
                EnsureWindowIsVisible();

                await InitializeWebView2Async();

                if (string.IsNullOrWhiteSpace(_userSettings.Current.WebViewSource))
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

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = _sysSettings.Current;

            // 최대화/최소화 상태면 복원 후 저장
            if (WindowState == WindowState.Normal)
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }
            else
            {
                settings.WindowLeft = RestoreBounds.Left;
                settings.WindowTop = RestoreBounds.Top;
                settings.WindowWidth = RestoreBounds.Width;
                settings.WindowHeight = RestoreBounds.Height;
            }

            settings.WindowState = (int)WindowState;
            _sysSettings.UpdateAndSave(settings);
        }

        private void EnsureWindowIsVisible()
        {
            // 모든 모니터 영역 확인
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            // 창이 화면 밖에 있으면 기본 위치로
            if (Left < virtualScreen.Left - Width + 50 ||
                Left > virtualScreen.Right - 50 ||
                Top < virtualScreen.Top ||
                Top > virtualScreen.Bottom - 50)
            {
                Left = 100;
                Top = 100;
            }

            // 최소 크기 보장
            if (Width < 200) Width = 700;
            if (Height < 100) Height = 800;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_deviceChangeHub != null)
                _deviceChangeHub.DeviceListChanged -= DeviceChangeHub_DeviceListChanged;
            Application.Current.Shutdown();
            base.OnClosed(e);
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

                NavigateIfPossible(_userSettings.Current.WebViewSource);
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
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
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

        private void AuditoDevice_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.DeviceControllerWindow(_deviceService, _deviceChangeHub, this);
            win.ShowDialog();
        }

        private void CompanySelect_Click(object sender, RoutedEventArgs e)
        {
            var selector = new CompanySelectWindow(_appSettings.Current.TenantListUrl, _appSettings.Current.CompanyCode);
            if (selector.ShowDialog() == true)
            {
                if (_appSettings.Current.CompanyCode != selector.SelectedCompanyCode)
                {
                    RegistryUtils.SaveCompanyCode(selector.SelectedCompanyCode);
                    Application.Current.Shutdown();
                }
            }
        }
    }
}