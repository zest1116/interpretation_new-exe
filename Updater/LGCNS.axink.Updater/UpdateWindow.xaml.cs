using LGCNS.axink.Common.Localization;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;

namespace LGCNS.axink.Updater
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateOptions _options;
        private readonly UpdateLogger _logger;
        private CancellationTokenSource? _cts;

        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true     // 리다이렉트 자동 추적
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        public UpdateWindow(UpdateOptions options)
        {
            InitializeComponent();
            _options = options;
            _logger = new UpdateLogger();

            if (!string.IsNullOrEmpty(options.Version))
                VersionText.Text = $"v{options.Version}";

            MouseLeftButtonDown += (s, e) => DragMove();
            Loaded += async (s, e) => await RunUpdatePipelineAsync();
        }

        private bool HasEnoughDiskSpace(string downloadFolder, long requiredBytes)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(downloadFolder)!);
                // 필요 용량의 2배 (다운로드 + 설치 임시 파일)
                var needed = Math.Max(requiredBytes * 2, 100 * 1024 * 1024); // 최소 100MB
                return drive.AvailableFreeSpace > needed;
            }
            catch
            {
                return true; // 확인 불가 시 진행
            }
        }

        // ═══════════════════════════════════════════════════════
        //  전체 파이프라인
        // ═══════════════════════════════════════════════════════
        private async Task RunUpdatePipelineAsync()
        {
            _cts = new CancellationTokenSource();

            try
            {
                // ── 1. WPF 앱 프로세스 종료 대기 ──
                SetPhase(LocalizationManager.GetString(LangKeys.Msg_Update_WaitingForExit), isIndeterminate: true);
                _logger.Log($"Waiting for process {_options.Pid} to exit...");

                await WaitForProcessExitAsync(_options.Pid, _cts.Token);
                _logger.Log("Main process exited");

                // ── 2. 다운로드 (필요한 경우) ──
                var msiPath = _options.MsiPath;

                if (_options.NeedsDownload)
                {
                    SetPhase(LocalizationManager.GetString(LangKeys.Msg_Update_Downloading), isIndeterminate: false);
                    TitleText.Text = LocalizationManager.GetString(LangKeys.Msg_Update_Downloading);

                    msiPath = await DownloadMsiAsync(_cts.Token);

                    if (string.IsNullOrEmpty(msiPath))
                    {
                        ShowError(LocalizationManager.GetString(LangKeys.Msg_Update_DownloadFailed));
                        return;
                    }

                    // ── 3. 해시 검증 ──
                    if (!string.IsNullOrEmpty(_options.FileHash))
                    {
                        SetPhase(LocalizationManager.GetString(LangKeys.Msg_Update_Verifying), isIndeterminate: true);
                        if (!await VerifyFileHashAsync(msiPath, _options.FileHash))
                        {
                            ShowError(LocalizationManager.GetString(LangKeys.Msg_Update_FileCorrupted));
                            TryDeleteFile(msiPath);
                            return;
                        }
                        _logger.Log("Hash verification passed");
                    }
                }

                // ── 4. MSI 설치 ──
                if (string.IsNullOrEmpty(msiPath) || !File.Exists(msiPath))
                {
                    ShowError(LocalizationManager.GetString(LangKeys.Msg_Update_FileNotFound));
                    return;
                }

                SetPhase(LocalizationManager.GetString(LangKeys.Msg_Update_Installing), isIndeterminate: true);
                TitleText.Text = LocalizationManager.GetString(LangKeys.Msg_Update_InstallingTitle);
                DetailText.Text = LocalizationManager.GetString(LangKeys.Msg_Update_PleaseWait);

                var installResult = await RunMsiInstallAsync(msiPath);

                if (installResult != 0)
                {
                    _logger.Log($"MSI install failed with exit code {installResult}");
                    ShowError($"{LocalizationManager.GetString(LangKeys.Msg_Update_InstallFailed)} (Code: {installResult})");
                    LaunchApp("--update-failed"); // ← 실패 인자 전달
                    return;
                }

                _logger.Log("MSI install completed successfully");

                // ── 5. 정리 ──
                if (_options.Cleanup)
                    TryDeleteFile(msiPath);

                // ── 6. 앱 재시작 ──
                SetPhase($"{LocalizationManager.GetString(LangKeys.Msg_Update_Complete)}! {LocalizationManager.GetString(LangKeys.Msg_Update_Restarting)}", isIndeterminate: true);
                TitleText.Text = LocalizationManager.GetString(LangKeys.Msg_Update_Complete);
                DetailText.Text = "";

                await Task.Delay(1500); // 완료 메시지 잠깐 보여주기
                LaunchApp("--update-success"); // ← 성공 인자 전달
                Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Update cancelled");
                ShowError(LocalizationManager.GetString(LangKeys.Msg_Update_Cancelled));
            }
            catch (Exception ex)
            {
                _logger.Log($"FATAL: {ex}");
                ShowError($"{LocalizationManager.GetString(LangKeys.Msg_Update_UnexpectedError)}.\n{ex.Message}");
                LaunchApp("--update-failed"); // ← 오류 시에도 실패 인자 전달
            }
        }

        // ═══════════════════════════════════════════════════════
        //  1. 프로세스 종료 대기
        // ═══════════════════════════════════════════════════════
        private static async Task WaitForProcessExitAsync(int pid, CancellationToken token)
        {
            if (pid == 0) return;

            Process process;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return; // 이미 종료됨
            }

            // ── 1단계: 정상 종료 요청 (WM_CLOSE) ──
            try
            {
                process.CloseMainWindow();
            }
            catch { }

            // 10초 대기
            if (await WaitWithTimeout(process, 10_000, token))
                return;

            // ── 2단계: 프로세스 강제 종료 ──
            try
            {
                process.Kill(true);
            }
            catch { }

            // 5초 대기
            if (await WaitWithTimeout(process, 5_000, token))
                return;

            // 여기까지 오면 종료 실패 — 그래도 진행 시도
        }

        private static async Task<bool> WaitWithTimeout(Process process, int timeoutMs, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(timeoutMs);
                await process.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return true; // 프로세스 접근 불가 = 이미 종료된 것으로 간주
            }
        }

        // ═══════════════════════════════════════════════════════
        //  2. MSI 다운로드
        // ═══════════════════════════════════════════════════════
        private async Task<string?> DownloadMsiAsync(CancellationToken token)
        {
            var downloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Common.Consts.APP_NAME, Common.Consts.DIR_NAME_UPDATE);
            Directory.CreateDirectory(downloadFolder);

            var fileName = $"{Common.Consts.APP_COMPANY}-{Common.Consts.APP_NAME}-{_options.Version}.msi";
            var filePath = Path.Combine(downloadFolder, fileName);
            var tempPath = filePath + ".tmp";

            try
            {
                CleanupOldDownloads(downloadFolder);

                _logger.Log($"Downloading: {_options.DownloadUrl}");

                using var response = await _httpClient.GetAsync(
                    _options.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    token);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;

                // 다운로드 전 호출
                if (!HasEnoughDiskSpace(downloadFolder, totalBytes))
                {
                    ShowError($"{LocalizationManager.GetString(LangKeys.Msg_Update_DiskSpaceLow)}\n{LocalizationManager.GetString(LangKeys.Msg_Update_DiskSpaceRetry)}");
                    return null;
                }

                using (var contentStream = await response.Content.ReadAsStreamAsync(token))
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        totalRead += bytesRead;

                        // UI 업데이트
                        UpdateDownloadProgress(totalRead, totalBytes);
                    }
                }

                // 임시 파일 → 최종 파일
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(tempPath, filePath);

                _logger.Log($"Download completed: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.Log($"Download failed: {ex.Message}");
                TryDeleteFile(tempPath);
                return null;
            }
        }

        private void UpdateDownloadProgress(long downloaded, long total)
        {
            Dispatcher.Invoke(() =>
            {
                if (total > 0)
                {
                    var pct = (double)downloaded / total * 100;
                    MainProgress.Value = pct;
                    PercentText.Text = $"{pct:F0}%";
                }

                var mbDown = downloaded / 1024.0 / 1024.0;
                if (total > 0)
                {
                    var mbTotal = total / 1024.0 / 1024.0;
                    SizeText.Text = $"{mbDown:F1} / {mbTotal:F1} MB";
                }
                else
                {
                    SizeText.Text = $"{mbDown:F1} MB";
                }
            });
        }

        /**
         * 이전 다운로드 삭제
         */
        private void CleanupOldDownloads(string downloadFolder, string? currentFile = null)
        {
            try
            {
                foreach (var file in Directory.GetFiles(downloadFolder, "*.msi"))
                {
                    if (file == currentFile) continue;
                    try { File.Delete(file); } catch { }
                }
                // .tmp 파일도 정리
                foreach (var file in Directory.GetFiles(downloadFolder, "*.tmp"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Cleanup warning: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  3. 해시 검증
        // ═══════════════════════════════════════════════════════
        private static async Task<bool> VerifyFileHashAsync(string filePath, string expectedHash)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            var actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════
        //  4. MSI 사일런트 설치
        // ═══════════════════════════════════════════════════════
        //private async Task<int> RunMsiInstallAsync(string msiPath)
        //{
        //    var logPath = Path.Combine(
        //        _logger.LogFolder,
        //        $"msi-install-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        //    var msiArgs = $"/i \"{msiPath}\" /qn /norestart /l*v \"{logPath}\"";
        //    _logger.Log($"Running: msiexec {msiArgs}");

        //    var psi = new ProcessStartInfo
        //    {
        //        FileName = "msiexec",
        //        Arguments = msiArgs,
        //        CreateNoWindow = true,
        //        WorkingDirectory = Path.GetDirectoryName(msiPath) ?? "."
        //    };

        //    using var process = Process.Start(psi);
        //    if (process == null) return -1;

        //    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        //    try
        //    {
        //        await process.WaitForExitAsync(timeoutCts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Log("msiexec timed out");
        //        try { process.Kill(true); } catch { }
        //        return -2;
        //    }

        //    _logger.Log($"msiexec exit code: {process.ExitCode}");

        //    // 3010 = ERROR_SUCCESS_REBOOT_REQUIRED (성공으로 처리)
        //    return process.ExitCode == 3010 ? 0 : process.ExitCode;
        //}

        private async Task<int> RunMsiInstallAsync(string msiPath)
        {
            var logPath = Path.Combine(
                _logger.LogFolder,
                $"msi-install-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            _logger.Log($"Installing: {msiPath}");

            Dispatcher.Invoke(() =>
            {
                MainProgress.IsIndeterminate = false;
                MainProgress.Value = 0;
            });

            var installer = new MsiInstaller();

            installer.ProgressChanged += (s, e) =>
            {
                // 디버그 로그로 콜백 호출 여부 확인
                _logger.Log($"[MSI Progress] {e.ProgressPercentage}% - {e.Message}");

                // Invoke → BeginInvoke (비동기, 교착 방지)
                Dispatcher.BeginInvoke(() =>
                {
                    MainProgress.Value = e.ProgressPercentage;
                    PercentText.Text = $"{e.ProgressPercentage}%";

                    if (!string.IsNullOrEmpty(e.Message))
                    {
                        StatusText.Text = e.Message;
                    }
                });
            };

            var result = await installer.InstallAsync(msiPath, logPath);

            _logger.Log($"MSI install result: {result}");
            return result;
        }

        // ═══════════════════════════════════════════════════════
        //  5. 앱 재시작
        // ═══════════════════════════════════════════════════════
        private void LaunchApp(string resultArg = "")
        {
            if (string.IsNullOrEmpty(_options.AppPath) || !File.Exists(_options.AppPath))
            {
                _logger.Log($"App not found: {_options.AppPath}");
                return;
            }

            var args = resultArg;

            // 실패 시 실패한 버전도 전달
            if (resultArg == "--update-failed" && !string.IsNullOrEmpty(_options.Version))
            {
                args += $" --failed-version {_options.Version}";
            }

            _logger.Log($"Launching: {_options.AppPath} {args}");

            Process.Start(new ProcessStartInfo
            {
                FileName = _options.AppPath,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(_options.AppPath) ?? "."
            });
        }

        // ═══════════════════════════════════════════════════════
        //  UI 헬퍼
        // ═══════════════════════════════════════════════════════
        private void SetPhase(string status, bool isIndeterminate)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                MainProgress.IsIndeterminate = isIndeterminate;

                if (isIndeterminate)
                {
                    PercentText.Text = "";
                    SizeText.Text = "";
                }
            });
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TitleText.Text = LocalizationManager.GetString(LangKeys.Msg_Update_Failed);
                StatusText.Text = message;
                DetailText.Text = $"{LocalizationManager.GetString(LangKeys.Msg_Update_Log)}: {_logger.CurrentLogFile}";
                MainProgress.IsIndeterminate = false;
                MainProgress.Value = 0;
                PercentText.Text = "";
                SizeText.Text = "";
                ButtonPanel.Visibility = Visibility.Visible;
            });
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ButtonPanel.Visibility = Visibility.Collapsed;
            await RunUpdatePipelineAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            LaunchApp("--update-failed"); // ← 닫기도 실패로 처리
            Application.Current.Shutdown();
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}