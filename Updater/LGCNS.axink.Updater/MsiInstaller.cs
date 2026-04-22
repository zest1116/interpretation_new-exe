using System.Runtime.InteropServices;

namespace LGCNS.axink.Updater
{
    public class MsiProgressEventArgs : EventArgs
    {
        public int ProgressPercentage { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MsiInstaller
    {
        // ── P/Invoke ────────────────────────────────────────
        private delegate int MsiInstallUIHandler(
            IntPtr context,
            int messageType,
            [MarshalAs(UnmanagedType.LPWStr)] string message);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        private static extern int MsiInstallProductW(string packagePath, string commandLine);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        private static extern MsiInstallUIHandler MsiSetExternalUIW(
            MsiInstallUIHandler handler, int messageFilter, IntPtr context);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        private static extern int MsiSetInternalUI(int uiLevel, IntPtr hwnd);

        // ──────────────────────────────────────────────────
        //  [FIX #1] MsiEnableLogW 추가
        //
        //  문제: commandLine에 /l*v를 넣으면 무시됩니다.
        //        MsiInstallProductW의 commandLine은 MSI 프로퍼티만 받고,
        //        /l*v 같은 msiexec 커맨드라인 스위치는 인식하지 않습니다.
        //  해결: MsiEnableLogW API를 별도로 호출하여 로깅을 활성화합니다.
        // ──────────────────────────────────────────────────
        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        private static extern int MsiEnableLogW(int logMode, string logFile, int logAttributes);

        private const int INSTALLLOGMODE_VERBOSE =
            0x00000001 | // FATALEXIT
            0x00000002 | // ERROR
            0x00000004 | // WARNING
            0x00000008 | // USER
            0x00000010 | // INFO
            0x00000020 | // RESOLVESOURCE
            0x00000040 | // OUTOFDISKSPACE
            0x00000080 | // ACTIONSTART
            0x00000100 | // ACTIONDATA
            0x00000200 | // COMMONDATA
            0x00000400 | // PROPERTYDUMP
            0x00001000;  // VERBOSE

        // ── 상수 ────────────────────────────────────────────

        // ── 메시지 필터용 상수 (MsiSetExternalUIW에 전달) ────
        private const int INSTALLLOGMODE_PROGRESS = 0x00000400;
        private const int INSTALLLOGMODE_ACTIONSTART = 0x00000080;
        private const int INSTALLLOGMODE_ACTIONDATA = 0x00000100;
        private const int INSTALLLOGMODE_COMMONDATA = 0x00000200;

        private const int INSTALLUILEVEL_NONE = 2;

        // ── 콜백 수신 판별용 상수 (OnInstallMessage에서 사용) ──
        private const int INSTALLMESSAGE_ACTIONSTART = 0x08000000;
        private const int INSTALLMESSAGE_ACTIONDATA = 0x09000000;

        private const int INSTALLMESSAGE_PROGRESS = 0x0A000000;
        private const int INSTALLMESSAGE_COMMONDATA = 0x0B000000;
        private const int IDOK = 1;

       

        // ──────────────────────────────────────────────────
        //  [FIX #2] 콜백 delegate를 인스턴스 필드에 저장
        //
        //  문제: 로컬 변수로 선언하면 MsiInstallProductW 실행 중에
        //        GC가 delegate를 수거할 수 있습니다.
        //        GC.KeepAlive는 finally 블록에서만 루팅하므로
        //        JIT 최적화에 따라 try 블록 내에서는 보호가 불완전합니다.
        //  해결: 인스턴스 필드에 저장하면 객체가 살아있는 동안
        //        확실하게 루팅됩니다.
        // ──────────────────────────────────────────────────
        private MsiInstallUIHandler? _handler;

        // ── 진행률 추적 ─────────────────────────────────────
        private int _total;
        private int _completed;
        private bool _moveForward = true;

        public event EventHandler<MsiProgressEventArgs>? ProgressChanged;

        // ══════════════════════════════════════════════════
        //  동기 설치 (내부용)
        // ══════════════════════════════════════════════════
        private int Install(string msiPath, string logPath)
        {
            _total = 0;
            _completed = 0;

            // [FIX #1] 로깅을 별도 API로 활성화
            MsiEnableLogW(INSTALLLOGMODE_VERBOSE, logPath, 0);

            MsiSetInternalUI(INSTALLUILEVEL_NONE, IntPtr.Zero);

            // [FIX #2] 필드에 저장하여 GC 방지
            _handler = new MsiInstallUIHandler(OnInstallMessage);

            var messageFilter =
                INSTALLLOGMODE_PROGRESS |
                INSTALLLOGMODE_ACTIONSTART |
                INSTALLLOGMODE_ACTIONDATA |
                INSTALLLOGMODE_COMMONDATA;

            var previousHandler = MsiSetExternalUIW(_handler, messageFilter, IntPtr.Zero);

            try
            {
                // 프로퍼티만 전달 (msiexec 스위치 아님)
                var commandLine = "REBOOT=ReallySuppress";
                var result = MsiInstallProductW(msiPath, commandLine);

                // 3010 = ERROR_SUCCESS_REBOOT_REQUIRED (성공 처리)
                return result == 3010 ? 0 : result;
            }
            finally
            {
                MsiSetExternalUIW(previousHandler, 0, IntPtr.Zero);
            }
        }

        // ══════════════════════════════════════════════════
        //  [FIX #3] STA 스레드에서 비동기 실행
        //
        //  문제: Task.Run은 ThreadPool(MTA)을 사용합니다.
        //        MSI 엔진은 내부적으로 COM을 사용하며,
        //        일부 커스텀 액션이 STA를 요구하면
        //        MTA 스레드에서 교착 상태가 발생합니다.
        //  해결: 별도 STA 스레드를 생성하여 실행합니다.
        //
        //  [FIX #4] 타임아웃 보호 (5분)
        //
        //  문제: MSI가 무한 대기하면 Updater도 멈춥니다.
        //  해결: 5분 타임아웃 후 -2를 반환합니다.
        // ══════════════════════════════════════════════════
        public Task<int> InstallAsync(string msiPath, string logPath,
                                      CancellationToken token = default)
        {
            var tcs = new TaskCompletionSource<int>();

            var staThread = new Thread(() =>
            {
                try
                {
                    var result = Install(msiPath, logPath);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Name = "MsiInstallerThread";
            staThread.Start();

            // [FIX #4] 타임아웃 보호
            _ = Task.Run(async () =>
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

                try
                {
                    await Task.Delay(Timeout.Infinite, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        tcs.TrySetCanceled(token);
                    else
                        tcs.TrySetResult(-2); // 타임아웃 코드
                }
            });

            return tcs.Task;
        }

        // ── MSI 메시지 콜백 ─────────────────────────────────
        private int OnInstallMessage(IntPtr context, int messageType, string message)
        {
            try
            {
                var type = messageType & 0x7F000000;

                switch (type)
                {
                    case INSTALLMESSAGE_PROGRESS:
                        HandleProgressMessage(message);
                        break;

                    case INSTALLMESSAGE_ACTIONSTART:
                        HandleActionStart(message);
                        break;
                }
            }
            catch
            {
                // 콜백 예외는 무시 (MSI 엔진 크래시 방지)
            }

            return IDOK;
        }

        // ── Progress 메시지 파싱 ────────────────────────────
        private void HandleProgressMessage(string message)
        {
            var fields = ParseFields(message);
            if (fields == null || fields.Length < 2) return;

            switch (fields[0])
            {
                case 0: // 마스터 리셋
                    _total = fields[1];
                    _completed = 0;
                    _moveForward = fields.Length > 2 && fields[2] == 0;
                    RaiseProgress();
                    break;

                case 2: // 진행률 증가
                    if (_total > 0)
                    {
                        if (_moveForward)
                            _completed += fields[1];
                        else
                            _completed -= fields[1];

                        RaiseProgress();
                    }
                    break;
            }
        }

        private void HandleActionStart(string message)
        {
            var actionName = ExtractActionName(message);
            if (string.IsNullOrEmpty(actionName)) return;

            var displayName = actionName switch
            {
                "InstallValidate" => "설치 준비 중...",
                "InstallInitialize" => "설치 시작...",
                "RemoveFiles" => "이전 파일 제거 중...",
                "RemoveFolders" => "이전 폴더 제거 중...",
                "InstallFiles" => "파일 설치 중...",
                "CreateShortcuts" => "바로가기 생성 중...",
                "WriteRegistryValues" => "레지스트리 등록 중...",
                "RegisterProduct" => "제품 등록 중...",
                "PublishFeatures" => "기능 등록 중...",
                "PublishProduct" => "제품 게시 중...",
                "InstallFinalize" => "설치 마무리 중...",
                "RemoveExistingProducts" => "이전 버전 제거 중...",
                _ => null
            };

            if (displayName != null)
            {
                RaiseProgress(displayName);
            }
        }

        // ── 유틸리티 ────────────────────────────────────────
        private void RaiseProgress(string? message = null)
        {
            var pct = _total > 0
                ? Math.Clamp((int)((double)_completed / _total * 100), 0, 100)
                : 0;

            ProgressChanged?.Invoke(this, new MsiProgressEventArgs
            {
                ProgressPercentage = pct,
                Message = message ?? string.Empty
            });
        }

        private static int[]? ParseFields(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;

            var parts = message.Split(' ');
            var result = new System.Collections.Generic.List<int>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].EndsWith(':') && i + 1 < parts.Length)
                {
                    if (int.TryParse(parts[i + 1], out var val))
                        result.Add(val);
                    i++;
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private static string? ExtractActionName(string message)
        {
            var colonCount = 0;
            var startIdx = -1;

            for (int i = 0; i < message.Length; i++)
            {
                if (message[i] == ':')
                {
                    colonCount++;
                    if (colonCount == 3)
                    {
                        startIdx = i + 2;
                        break;
                    }
                }
            }

            if (startIdx < 0 || startIdx >= message.Length) return null;

            var dotIdx = message.IndexOf('.', startIdx);
            if (dotIdx < 0) return null;

            return message.Substring(startIdx, dotIdx - startIdx).Trim();
        }
    }
}