using CommunityToolkit.Mvvm.Messaging;
using LGCNS.axink.App.Services;
using LGCNS.axink.App.Windows;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.ApiResponse;
using LGCNS.axink.Models.Devices;
using LGCNS.axink.Models.Settings;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

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

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

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


        private const int WM_SIZE = 0x0005;
        private const int WM_DPICHANGED = 0x02E0;

        // 창 둥근 모서리 반경 (XAML의 Border.CornerRadius와 동일해야 함)
        private const int CORNER_RADIUS = 10;

        // Windows 11(빌드 22000+)은 DWM이 네이티브로 둥근 모서리를 그려줌.
        // 그 이하 버전은 SetWindowRgn 폴백이 필요.
        private bool UseRegionFallback => Environment.OSVersion.Version.Build < 22000;
        #endregion

        private readonly ISettingsMonitor<UserSettings> _userSettings;
        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly ISettingsMonitor<AppSettings> _appSettings;
        private readonly IMessenger _messenger;
        private readonly IDeviceService _deviceService;
        private readonly DeviceChangeHub _deviceChangeHub;
        private readonly WebViewBridge _webViewBridge;
        private DeviceSnapshotDto? _lastDeviceSnapshot;
        private readonly TenantInfo _tenantInfo;

        public MainWindow(
            ISettingsMonitor<UserSettings> userSettings,
            ISettingsMonitor<SystemSettings> sysSettings,
            ISettingsMonitor<AppSettings> appSettings,
            IMessenger messenger,
            IDeviceService deviceService,
            DeviceChangeHub hub,
            WebViewBridge webViewBridge,
            TenantInfo tenantInfo)
        {
            InitializeComponent();
            SyncThemeMenu();

            _userSettings = userSettings;
            _sysSettings = sysSettings;
            _appSettings = appSettings;
            _messenger = messenger;
            _deviceService = deviceService;
            _deviceChangeHub = hub;
            _webViewBridge = webViewBridge;
            _deviceChangeHub.DeviceListChanged += DeviceChangeHub_DeviceListChanged;
            _tenantInfo = tenantInfo;

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;

            // 창 위치가 바뀔 때
            this.LocationChanged += (s, e) => NotificationHelper.RearrangeNotifications(this);

            // 창 크기가 바뀔 때 (알림이 창 밖으로 나가는 것 방지)
            this.SizeChanged += (s, e) => NotificationHelper.RearrangeNotifications(this);

            _messenger.Register<SettingsChangedMessage<UserSettings>>(this, (_, msg) =>
            {
                NavigateIfPossible(msg.Value.WebViewSource);
            });

            _lastDeviceSnapshot = deviceService.GetSnapshotAsync(CancellationToken.None).Result;
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
                    var win = new DeviceControllerWindow(_deviceService, _deviceChangeHub, this);
                    win.ShowDialog();
                }
                else if (type == "initCompany")
                {
                    var result = AlertDialog.ShowOk(
                        this,
                        title: Application.Current.Resources["Msg_Init_Company"].ToString() ?? "프로그램을 초기화 했습니다.",
                        message: "",
                        dialogTitle: Application.Current.Resources["Dic_Common_Information"].ToString());

                    if (result == AlertDialogResult.Ok)
                    {
                        RegistryUtils.SaveCompanyCode("");
                        Application.Current.Shutdown();
                    }
                }
                else if(type == "changeTheme")
                {
                    JToken? data = msg["data"];
                    if (data != null)
                    {
                        var theme = data.Value<string>("theme") ?? "BLACK";

                        AppTheme appTheme = AppTheme.Light;
                        switch (theme)
                        {
                            case "BLUE":
                            case "WHITE":
                                ThemeManager.Apply(AppTheme.Light);
                                appTheme = AppTheme.Light;
                                break;
    
                            case "BLACK":
                                ThemeManager.Apply(AppTheme.Dark);
                                appTheme = AppTheme.Dark;
                                break;
                        }

                        var store = new JsonFileStore<SystemSettings>(Consts.APP_NAME, Consts.FILE_NAME_SYS_SETTINGS);
                        _sysSettings.Current.AppTheme = appTheme;
                        store.UpdateProperty(x => x.AppTheme, appTheme);

                        SyncThemeMenu();

                    }
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
                Logging.Debug(JsonConvert.SerializeObject(e.Devices));
                var deviceSnapshot = JsonConvert.DeserializeObject<DeviceSnapshotDto>(e.Devices);
                if (deviceSnapshot == null)
                    return;

                var summary = DeviceSnapshotDiffer.Diff(_lastDeviceSnapshot, deviceSnapshot);

                _lastDeviceSnapshot = deviceSnapshot;

                if (!summary.HasAnyChange)
                    return;

                // ── 기본 장치 변경 ──
                if (summary.DefaultInputChange is { NewId: { } newId })
                {
                    var device = deviceSnapshot.Inputs.FirstOrDefault(d => d.Id == newId);
                    if (device is not null)
                    {
                        var tpl = Application.Current.Resources["Msg_Notification_Mic"] as string;
                        NotificationHelper.Show(this, string.Format(tpl!, device.Name));
                    }
                }

                if (summary.DefaultOutputChange is { NewId: { } newId2 })
                {
                    var device = deviceSnapshot.Outputs.FirstOrDefault(d => d.Id == newId2);
                    if (device is not null)
                    {
                        var tpl = Application.Current.Resources["Msg_Notification_Speaker"] as string;
                        NotificationHelper.Show(this, string.Format(tpl!, device.Name));
                    }
                }

                // ── 장치 추가 ──
                foreach (var d in summary.AddedInputs)
                {
                    var tpl = Application.Current.Resources["Msg_Notification_Device_Added"] as string;
                    NotificationHelper.Show(this, string.Format(tpl!, d.Name));
                }
                foreach (var d in summary.AddedOutputs)
                {
                    var tpl = Application.Current.Resources["Msg_Notification_Device_Added"] as string;
                    NotificationHelper.Show(this, string.Format(tpl!, d.Name));
                }

                // ── 장치 제거 ──
                foreach (var d in summary.RemovedInputs)
                {
                    var tpl = Application.Current.Resources["Msg_Notification_Device_Removed"] as string;
                    NotificationHelper.Show(this, string.Format(tpl!, d.Name));
                }
                foreach (var d in summary.RemovedOutputs)
                {
                    var tpl = Application.Current.Resources["Msg_Notification_Device_Removed"] as string;
                    NotificationHelper.Show(this, string.Format(tpl!, d.Name));
                }

                /* WebView2로 전달하지 않음
                if (WebView?.CoreWebView2 != null)
                {
                    var data = new
                    {
                        type = "deviceChanged",
                        data = JsonConvert.DeserializeObject(e.Devices)
                    };

                    WebView.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(data));
                }
                */
            });
        }

        /**
         * 폰트확인을 위해
         */
        private void DumpEmbeddedFontNames()
        {
            var candidates = new[]
            {
        "./Assets/#LG EI Text TTF Regular",
        "./Assets/LGEITextTTF-Regular#LG EI Text",
        "./Assets/LGEITextTTF-Regular#LG EI Text TTF Regular",
        "./Assets/LGEITextTTF-Regular#LG EI Text Regular"
    };

            foreach (var candidate in candidates)
            {
                try
                {
                    var family = new FontFamily(new Uri("pack://application:,,,/"), candidate);

                    Debug.WriteLine($"[FONT TEST] Candidate: {candidate}");

                    foreach (var typeface in family.GetTypefaces())
                    {
                        if (typeface.TryGetGlyphTypeface(out var glyph))
                        {
                            var familyName = glyph.FamilyNames.Values.FirstOrDefault();
                            var faceName = glyph.FaceNames.Values.FirstOrDefault();

                            Debug.WriteLine($"  Family={familyName}, Face={faceName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FONT TEST] Failed: {candidate} / {ex.Message}");
                }
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            //DumpEmbeddedFontNames();
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
            if (Height < 100) Height = 1000;
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

                NavigateIfPossible(_tenantInfo.Url);

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
            TitleBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(8, 8, 0, 0);
            ContentBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 9, 9);
            //MainFrame.Margin = maximized ? new Thickness(0) : new Thickness(1);

            // 최대화 시 보더 숨김 (화면 가장자리에 어색한 1px 보이는 것 방지)
            MainFrame.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);

            if (MaximizeGlyph != null && RestoreGlyph != null)
            {
                MaximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
                RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyCornerPreference()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (!UseRegionFallback)
            {
                // Windows 11+: DWM에 맡김 (앤티앨리어싱 적용되어 가장 깔끔)
                int pref = WindowState == WindowState.Maximized
                    ? DWMWCP_DONOTROUND
                    : DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, Marshal.SizeOf<int>());
            }
            else
            {
                // Windows 10 / Server 2019·2022: HRGN으로 OS 레벨 클리핑
                ApplyWindowRegion(hwnd);
            }
        }

        private void ApplyWindowRegion(IntPtr hwnd)
        {
            // 최대화 상태에서는 region 제거 (전체 화면 영역 사용)
            if (WindowState == WindowState.Maximized)
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            // 실제 디바이스 픽셀 기준 창 크기
            if (!GetWindowRect(hwnd, out RECT rect))
                return;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
                return;

            // DPI 배율 반영 (100%=1.0, 150%=1.5)
            double dpiScale = 1.0;
            var src = PresentationSource.FromVisual(this);
            if (src?.CompositionTarget != null)
                dpiScale = src.CompositionTarget.TransformToDevice.M11;

            // CreateRoundRectRgn은 "타원의 가로/세로"를 받으므로 반경의 2배를 넘김
            int ellipse = (int)Math.Round(CORNER_RADIUS * 2 * dpiScale);

            // +1은 CreateRoundRectRgn이 우·하단을 배타적으로 처리하기 때문
            IntPtr hrgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, ellipse, ellipse);

            // SetWindowRgn은 hrgn 소유권을 가져가므로 DeleteObject 호출하면 안 됨
            SetWindowRgn(hwnd, hrgn, true);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            else if ((msg == WM_SIZE || msg == WM_DPICHANGED) && UseRegionFallback)
            {
                // 리사이즈/DPI 변경 시 region 재계산
                ApplyWindowRegion(hwnd);
            }

            return IntPtr.Zero;
        }

        private  void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
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

            // 현재 창의 DPI 배율 계산 (100%일 때 1.0, 150%일 때 1.5)
            double dpiX = 1.0;
            double dpiY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // XAML에 설정한 MinWidth(600), MinHeight(800) 값에 DPI를 곱해서 OS에 전달
            mmi.ptMinTrackSize.X = (int)(600 * dpiX);
            mmi.ptMinTrackSize.Y = (int)(800 * dpiY);

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
            var selector = new CompanySelectWindow(_appSettings.Current.TenantListUrl, _tenantInfo.CompanyCd);
            if (selector.ShowDialog() == true)
            {
                if (_tenantInfo.CompanyCd != selector.SelectedCompany.CompanyCd)
                {
                    RegistryUtils.SaveCompanyCode(selector.SelectedCompany.CompanyCd);
                    Application.Current.Shutdown();
                }
            }
        }

        private void SyncThemeMenu()
        {
            var theme = ThemeManager.Current;

            LightThemeMenuItem.IsChecked = theme == AppTheme.Light;
            DarkThemeMenuItem.IsChecked = theme == AppTheme.Dark;
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string theme)
            {
                AppTheme appTheme = AppTheme.Light;
                switch (theme)
                {
                    case "WHITE":
                        ThemeManager.Apply(AppTheme.Light);
                        appTheme = AppTheme.Light;
                        break;

                    case "BLACK":
                        ThemeManager.Apply(AppTheme.Dark);
                        appTheme = AppTheme.Dark;
                        break;
                }

                var store = new JsonFileStore<SystemSettings>(Consts.APP_NAME, Consts.FILE_NAME_SYS_SETTINGS);
                _sysSettings.Current.AppTheme = appTheme;
                store.UpdateProperty(x => x.AppTheme, appTheme);

                SyncThemeMenu();
            }
        }
    }
}