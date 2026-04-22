using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

        // ═══════════════════════════════════════════════════════
        //  전체 파이프라인
        // ═══════════════════════════════════════════════════════
        private async Task RunUpdatePipelineAsync()
        {
            _cts = new CancellationTokenSource();

            try
            {
                // ── 1. WPF 앱 프로세스 종료 대기 ──
                SetPhase("앱 종료를 기다리고 있습니다...", isIndeterminate: true);
                _logger.Log($"Waiting for process {_options.Pid} to exit...");

                await WaitForProcessExitAsync(_options.Pid, _cts.Token);
                _logger.Log("Main process exited");

                // ── 2. 다운로드 (필요한 경우) ──
                var msiPath = _options.MsiPath;

                if (_options.NeedsDownload)
                {
                    SetPhase("업데이트 다운로드 중...", isIndeterminate: false);
                    TitleText.Text = "업데이트 다운로드 중";

                    msiPath = await DownloadMsiAsync(_cts.Token);

                    if (string.IsNullOrEmpty(msiPath))
                    {
                        ShowError("다운로드에 실패했습니다.");
                        return;
                    }

                    // ── 3. 해시 검증 ──
                    if (!string.IsNullOrEmpty(_options.FileHash))
                    {
                        SetPhase("파일 검증 중...", isIndeterminate: true);
                        if (!await VerifyFileHashAsync(msiPath, _options.FileHash))
                        {
                            ShowError("다운로드된 파일이 손상되었습니다.");
                            TryDeleteFile(msiPath);
                            return;
                        }
                        _logger.Log("Hash verification passed");
                    }
                }

                // ── 4. MSI 설치 ──
                if (string.IsNullOrEmpty(msiPath) || !File.Exists(msiPath))
                {
                    ShowError("설치 파일을 찾을 수 없습니다.");
                    return;
                }

                SetPhase("업데이트를 설치하고 있습니다...", isIndeterminate: true);
                TitleText.Text = "업데이트 설치 중";
                DetailText.Text = "잠시만 기다려 주세요...";

                var installResult = await RunMsiInstallAsync(msiPath);

                if (installResult != 0)
                {
                    _logger.Log($"MSI install failed with exit code {installResult}");
                    ShowError($"설치에 실패했습니다. (코드: {installResult})");
                    LaunchApp(); // 실패해도 기존 버전으로 재시작
                    return;
                }

                _logger.Log("MSI install completed successfully");

                // ── 5. 정리 ──
                if (_options.Cleanup)
                    TryDeleteFile(msiPath);

                // ── 6. 앱 재시작 ──
                SetPhase("업데이트 완료! 앱을 다시 시작합니다...", isIndeterminate: true);
                TitleText.Text = "업데이트 완료";
                DetailText.Text = "";

                await Task.Delay(1500); // 완료 메시지 잠깐 보여주기
                LaunchApp();
                Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Update cancelled");
                ShowError("업데이트가 취소되었습니다.");
            }
            catch (Exception ex)
            {
                _logger.Log($"FATAL: {ex}");
                ShowError($"예기치 않은 오류가 발생했습니다.\n{ex.Message}");
                LaunchApp(); // 오류 시에도 기존 버전으로 재시작
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
                "LGCNS", "axink Translator", "Updates");
            Directory.CreateDirectory(downloadFolder);

            var fileName = $"LGCNS-axink-{_options.Version}.msi";
            var filePath = Path.Combine(downloadFolder, fileName);
            var tempPath = filePath + ".tmp";

            try
            {
                _logger.Log($"Downloading: {_options.DownloadUrl}");

                using var response = await _httpClient.GetAsync(
                    _options.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    token);
                // 디버그 확인용
                Debug.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                Debug.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");
                Debug.WriteLine($"Content-Length: {response.Content.Headers.ContentLength}");


                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;

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
        private void LaunchApp()
        {
            if (string.IsNullOrEmpty(_options.AppPath) || !File.Exists(_options.AppPath))
            {
                _logger.Log($"App not found: {_options.AppPath}");
                return;
            }

            _logger.Log($"Launching: {_options.AppPath}");

            Process.Start(new ProcessStartInfo
            {
                FileName = _options.AppPath,
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
                TitleText.Text = "업데이트 실패";
                StatusText.Text = message;
                DetailText.Text = $"로그: {_logger.CurrentLogFile}";
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
            LaunchApp(); // 기존 버전으로 재시작
            Application.Current.Shutdown();
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}