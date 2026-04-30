using LGCNS.axink.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace LGCNS.axink.App.Updater
{
    public enum UpdateStatus
    {
        UpToDate,
        OptionalUpdateAvailable,
        MandatoryUpdateRequired,
        Error
    }

    /// <summary>
    /// 서버에서 업데이트 정보를 확인하고, Updater.exe를 실행하는 서비스.
    /// 다운로드 + 설치는 Updater.exe가 처리합니다.
    /// </summary>
    public class UpdateService
    {
        private const string UpdaterExeName = "axink Translator Updater.exe";
        private const string UpdaterMutexName = "LGCNS.axink.Updater.SingleInstance";

        private readonly string _updateCheckUrl;
        private readonly string _updaterPath;

        public UpdateStatus Status { get; private set; } = UpdateStatus.UpToDate;
        public UpdateInfo? LatestUpdate { get; private set; }

        public event EventHandler<UpdateStatus>? StatusChanged;

        public UpdateService(string updateCheckUrl)
        {
            _updateCheckUrl = updateCheckUrl;

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _updaterPath = Path.Combine(appDir, UpdaterExeName);
        }

        // ═══════════════════════════════════════════════════════
        //  1. 서버에서 버전 체크
        // ═══════════════════════════════════════════════════════
        public async Task<UpdateStatus> CheckForUpdateAsync()
        {
            try
            {
                var currentVersion = RegistryUtils.ReadVersion();
                var companyCode = RegistryUtils.ReadCompanyCode();
                //MSI는 회사별도 동일하게 운영되므로 LGCNS로 고정(향후 회사별로 변경되면 이부분 제거)
                companyCode = "GIM006"; 
                var url = string.Format(_updateCheckUrl, companyCode, "MSI");
                var updateInfo = await ApiClient.GetAsync<UpdateInfo>(url);

                if (updateInfo == null)
                {
                    SetStatus(UpdateStatus.UpToDate);
                    return Status;
                }

                LatestUpdate = updateInfo;

                var latest = NormalizeVersion(updateInfo.VersionCode);
                var current = NormalizeVersion(currentVersion);

                if (current >= latest)
                {
                    SetStatus(UpdateStatus.UpToDate);
                }
                else
                {
                    var minimum = NormalizeVersion(updateInfo.VersionCode);
                    SetStatus(current < minimum
                        ? UpdateStatus.MandatoryUpdateRequired
                        : UpdateStatus.OptionalUpdateAvailable);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Check failed: {ex.Message}");
                SetStatus(UpdateStatus.Error);
            }

            return Status;
        }

        // ═══════════════════════════════════════════════════════
        //  2. Updater.exe 실행 → 앱 종료
        //
        //  [핵심] Updater.exe를 %TEMP%에 복사 후 실행합니다.
        //  원본 Updater.exe가 설치 폴더에 남아있으면
        //  MSI가 해당 파일을 교체할 수 없습니다 (파일 잠금).
        // ═══════════════════════════════════════════════════════
        public bool LaunchUpdaterAndExit()
        {
            if (LatestUpdate == null || string.IsNullOrEmpty(LatestUpdate.DownloadUrl))
            {
                Debug.WriteLine("[UpdateService] No update info available");
                return false;
            }

            if (!File.Exists(_updaterPath))
            {
                Debug.WriteLine($"[UpdateService] Updater not found: {_updaterPath}");
                return false;
            }

            // ── Mutex 체크: Updater가 이미 실행 중인지 확인 ──
            bool createdNew;
            try
            {
                using var mutex = new Mutex(false, UpdaterMutexName, out createdNew);
                if (!createdNew)
                {
                    Debug.WriteLine("[UpdateService] Updater is already running");
                    return false;
                }
                // Mutex를 바로 해제 — Updater가 직접 잡을 것
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Mutex check failed: {ex.Message}");
            }

            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var appExePath = Environment.ProcessPath
                                 ?? currentProcess.MainModule?.FileName
                                 ?? string.Empty;

                // ── %TEMP%에 Updater.exe 복사 (자기 잠금 방지) ──
                var tempUpdaterDir = Path.Combine(Path.GetTempPath(), "LGCNS_axink_updater");
                Directory.CreateDirectory(tempUpdaterDir);
                var tempUpdaterPath = Path.Combine(tempUpdaterDir, UpdaterExeName);
                File.Copy(_updaterPath, tempUpdaterPath, overwrite: true);

                // ── 인자 구성 ──
                var args = $"--pid {currentProcess.Id} " +
                           $"--download-url \"{LatestUpdate.DownloadUrl}\" " +
                           $"--app \"{appExePath}\" " +
                           $"--version \"{LatestUpdate.VersionCode}\" ";
                
                           //(string.IsNullOrEmpty(LatestUpdate.FileHash)
                           //    ? ""
                           //    : $"--hash \"{LatestUpdate.FileHash}\" ") +
                           //$"--cleanup";

                // ── Updater 실행 (TEMP 경로에서) ──
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempUpdaterPath,
                    Arguments = args,                    
                    WorkingDirectory = tempUpdaterDir
                });

                // Process.Start 성공 확인 후에만 앱 종료
                if (process == null)
                {
                    Debug.WriteLine("[UpdateService] Failed to start updater process");
                    return false;
                }

                // WPF 앱 종료
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });

                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED: 사용자가 UAC를 거부함
                Debug.WriteLine("[UpdateService] User cancelled UAC elevation");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Failed to launch updater: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  유틸리티
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 3자리(1.2.0) / 4자리(1.2.0.0) 버전 문자열을 4자리로 정규화
        /// </summary>
        private static Version NormalizeVersion(string versionStr)
        {
            if (Version.TryParse(versionStr, out var ver))
            {
                return new Version(
                    Math.Max(ver.Major, 0),
                    Math.Max(ver.Minor, 0),
                    Math.Max(ver.Build, 0),
                    Math.Max(ver.Revision, 0));
            }
            return new Version(0, 0, 0, 0);
        }


        private void SetStatus(UpdateStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
        }
    }
}
