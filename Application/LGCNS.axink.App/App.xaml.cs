using CommunityToolkit.Mvvm.Messaging;
using LGCNS.axink.App.Services;
using LGCNS.axink.App.ViewModels;
using LGCNS.axink.App.Windows;
using LGCNS.axink.Audio;
using LGCNS.axink.Audio.Devices;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Interfaces;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Devices;
using LGCNS.axink.Models.Settings;
using LGCNS.axink.WebHosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;

namespace LGCNS.axink.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        // 앱 고유값으로
        private const string MutexName = "LGCNS.axink.SingleInstance";

        // 커스텀 메시지
        private const int WM_SHOWME = 0x0400 + 0x777;

        //서비스
        public static ServiceProvider _serviceProvider { get; private set; } = default!;

        //Web Hosting
        private LocalServerHost? _server;

        /// <summary>
        /// 회사코드
        /// </summary>
        public static string? CompanyCode { get; private set; }

        #region WIN32 메시지

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion

        protected override async void OnStartup(StartupEventArgs e)
        {
            new Mutex(initiallyOwned: true, MutexName, out bool isNew);

            if (!isNew)
            {
                // 이미 실행 중 → 기존 창을 앞으로 올리라고 신호 보내고 종료
                SignalFirstInstance();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            //언어설정

            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // 지원 언어가 아니면 기본값
            var supported = new[] { "ko", "en", "zh" };
            if (!supported.Contains(lang)) lang = "en";

            var dict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Strings.{lang}.xaml", UriKind.Relative)
            };
            Resources.MergedDictionaries.Add(dict);

            //회사코드
            CompanyCode = RegistryUtils.ReadCompanyCode();

            // 1. 환경별 기본값 로드 (appsettings.json)
            var defaultAppSettings = AppConfigLoader.LoadSection<AppSettings>("AppSettings");

            if (!string.IsNullOrEmpty(CompanyCode))
            {
                defaultAppSettings.CompanyCode = CompanyCode;
            }

            // 테마
            ThemeManager.Apply(AppTheme.Dark);

            //로깅 초기화
            Logging.Init(Consts.APP_NAME, Consts.APP_COMPANY);

            //앱 시작
            Logging.Info($"앱 시작 - Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            Logging.Info($"환경: {Environment.GetEnvironmentVariable("AXINK_ENVIRONMENT") ?? "Production"}");

            var sc = new ServiceCollection();

            sc.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            sc.AddSingleton<IEventBus, ChannelEventBus>();

            // Device change notification
            sc.AddSingleton<DeviceNotificationListener>();

            // 2. JsonFileStore — 로컬 파일 없으면 기본값으로 시드
            sc.AddSingleton(sp =>
            {
                var store = new JsonFileStore<AppSettings>(Consts.APP_NAME, Consts.FILE_NAME_APP_SETTINGS);
                store.LoadOrCreate(defaultAppSettings);  // 최초 1회만 파일 생성
                return store;
            }); 
            sc.AddSingleton(sp => new JsonFileStore<UserSettings>(Consts.APP_NAME, Consts.FILE_NAME_USER_SETTINGS));
            sc.AddSingleton(sp => new JsonFileStore<SystemSettings>(Consts.APP_NAME, Consts.FILE_NAME_SYS_SETTINGS));

            // Monitors
            sc.AddSingleton<ISettingsMonitor<AppSettings>, SettingsMonitor<AppSettings>>();
            sc.AddSingleton<ISettingsMonitor<UserSettings>, SettingsMonitor<UserSettings>>();
            sc.AddSingleton<ISettingsMonitor<SystemSettings>, SettingsMonitor<SystemSettings>>();

            // Settings popup MVVM
            sc.AddTransient<UserSettingsViewModel>();
            sc.AddTransient<UserSettingsWindow>();

            // Main
            sc.AddSingleton<MainWindow>();

            _serviceProvider = sc.BuildServiceProvider();

            InitializeAndPersistSystemSettings();

            // ✅ 같은 인스턴스 확보
            //SystemSettings 읽기
            var systemSettingsMon = _serviceProvider.GetRequiredService<ISettingsMonitor<SystemSettings>>();
            //AppSettings 읽기
            var appSettingsMon = _serviceProvider.GetRequiredService<ISettingsMonitor<AppSettings>>();
            //UserSettings 읽기
            var userSettingsMon = _serviceProvider.GetRequiredService<ISettingsMonitor<UserSettings>>();
            //EvenBus
            var bus = _serviceProvider.GetRequiredService<IEventBus>();


            _server = new LocalServerHost();
            await _server.StartAsync(new ServerOptions(Port: 5123), services =>
            {
                //같은 bus인스턴스 공유
                services.AddSingleton(_ => bus);
                //같은 SystemSettings인스턴스 공유
                services.AddSingleton(_ => systemSettingsMon);
                //같은 AppSettings인스턴스 공유
                services.AddSingleton(_ => appSettingsMon);

                services.AddSingleton<IDeviceService, WebDeviceService>();

                services.AddSingleton<MicAudioStreamCapture>(sp =>
                {
                    return new MicAudioStreamCapture(
                    isInputMode: true,
                    sysSettings: sp.GetRequiredService<ISettingsMonitor<SystemSettings>>(),
                    appSettings: sp.GetRequiredService<ISettingsMonitor<AppSettings>>());
                });

                services.AddSingleton<SpkAudioStreamCapture>(sp =>
                {
                    return new SpkAudioStreamCapture(
                    isInputMode: false,
                    sysSettings: sp.GetRequiredService<ISettingsMonitor<SystemSettings>>(),
                    appSettings: sp.GetRequiredService<ISettingsMonitor<AppSettings>>());
                });

                services.AddSingleton<WebAudioCaptureService>(sp =>
                {
                    return new WebAudioCaptureService(
                        sysSettings: sp.GetRequiredService<ISettingsMonitor<SystemSettings>>(),
                        micCapture: sp.GetRequiredService<MicAudioStreamCapture>(),
                        spkCapture: sp.GetRequiredService<SpkAudioStreamCapture>(),
                        hub: sp.GetRequiredService<IChannelAudioHub>(),
                        bus: sp.GetRequiredService<IEventBus>()
                        );
                });

                //Hosting이 요구하는 인터페이스로도 노출
                services.AddSingleton<IWebAudioCaptureService>(sp => sp.GetRequiredService<WebAudioCaptureService>());
            });

            // ✅ Kestrel DI에서 캡처 서비스 꺼내오기
            var webCapture = _server.Services.GetRequiredService<WebAudioCaptureService>();

            // ✅ Kestrel DI에서 디바이스 서비스 꺼내오기
            var deviceService = _server.Services.GetRequiredService<IDeviceService>();

            // ✅ WPF DI에서 디바이스 리스너 꺼내기
            var deviceListener = _serviceProvider.GetRequiredService<DeviceNotificationListener>();

            var deviceChangeHub = new DeviceChangeHub(deviceListener, deviceService, webCapture, bus);
            deviceChangeHub.Start(); //deviceListner.start()도 실행

            // WebView2 ↔ SPA 양방향 통신 브릿지
            var webViewBridge = new WebViewBridge(deviceService, webCapture);

            var messenger = _serviceProvider.GetRequiredService<IMessenger>();

            if (!string.IsNullOrEmpty(CompanyCode))
            {
                var main = new MainWindow(userSettingsMon, systemSettingsMon, appSettingsMon, messenger, deviceService, deviceChangeHub, webViewBridge);
                MainWindow = main;
                main.Show();
            }
            else
            {
                var selector = new Windows.CompanySelectWindow(defaultAppSettings.TenantListUrl, string.Empty);
                if (selector.ShowDialog() == true)
                {
                    CompanyCode = selector.SelectedCompanyCode;
                    RegistryUtils.SaveCompanyCode(CompanyCode);
                    appSettingsMon.Current.CompanyCode = CompanyCode;
                    var main = new MainWindow(userSettingsMon, systemSettingsMon, appSettingsMon, messenger, deviceService, deviceChangeHub, webViewBridge);
                    MainWindow = main;
                    main.Show();
                }
                else
                {
                    Shutdown();
                }
            }


            // 글로벌 예외 처리
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"오류가 발생했습니다: {args.Exception.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show($"도메인 예외: {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                MessageBox.Show($"Task 예외: {e.Exception}");
                e.SetObserved();
            };
        }

        /// <summary>
        /// 기존창을 맨앞으로 올리라고 보냄
        /// </summary>
        private static void SignalFirstInstance()
        {
            var current = Process.GetCurrentProcess();

            foreach (var p in Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id == current.Id) continue;

                // MainWindowHandle이 0일 수 있어서 약간의 보강이 필요할 때도 있음(아래 팁 참고)
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    PostMessage(p.MainWindowHandle, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                }
                break;
            }
        }

        private void InitializeAndPersistSystemSettings()
        {
            var systemMon = _serviceProvider.GetRequiredService<ISettingsMonitor<SystemSettings>>();

            // 사용자가 말한 정책: "내가 직접 코드 수정해서 기본값을 정할거야"
            // → 여기 값만 바꾸면 됨 (UI 없음)
            var defaults = systemMon.Current;
            defaults.SaveOption = SaveOption.None;
            defaults.SpaStreamMode = SpaStreamMode.SttText;


            // 저장만 해주면 됨 (다른 서비스는 systemMon.Current로 접근)
            systemMon.UpdateAndSave(defaults);
        }

        public static int ShowMeMessage => WM_SHOWME;

        public static bool? ShowWebViewSourceSettingsDialog(Window owner)
        {
            var win = _serviceProvider.GetRequiredService<UserSettingsWindow>();
            win.Owner = owner;
            return win.ShowDialog();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_server != null)
                await _server.StopAsync();
            Logging.Info("앱 종료");
            Serilog.Log.CloseAndFlush();
            _serviceProvider?.Dispose();

            base.OnExit(e);
        }
    }

}
