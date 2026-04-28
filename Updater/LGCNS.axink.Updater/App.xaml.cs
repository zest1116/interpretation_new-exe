using LGCNS.axink.Common;
using LGCNS.axink.Common.Localization;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;

namespace LGCNS.axink.Updater
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "LGCNS.axink.Updater.SingleInstance";

        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //언어설정
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            // 지원 언어가 아니면 기본값
            var supported = new[] { "ko", "en", "zh" };
            if (!supported.Contains(lang)) lang = "en";

            LocalizationManager.Instance.SetLanguage(lang);

            // ── 단일 인스턴스 보장 ──
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource(LangKeys.Msg_Update_AlreadyInProgress),
                    Consts.APP_NAME,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // ── 인자 파싱 ──
            var options = UpdateOptions.Parse(e.Args);

            if (options == null)
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource(LangKeys.Msg_Update_CannotRunDirectly),
                    Consts.APP_NAME,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var window = new UpdateWindow(options);
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }

}
