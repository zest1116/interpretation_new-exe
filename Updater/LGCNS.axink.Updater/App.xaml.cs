using System.Configuration;
using System.Data;
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

            // ── 단일 인스턴스 보장 ──
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "업데이트가 이미 진행 중입니다.",
                    "axink Updater",
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
                    "이 프로그램은 axink 자동 업데이트 시 실행됩니다.\n직접 실행할 수 없습니다.",
                    "axink Updater",
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
