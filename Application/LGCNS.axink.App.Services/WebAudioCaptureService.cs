using LGCNS.axink.Audio.Capture;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Interfaces;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Settings;
using LGCNS.axink.WebHosting;

namespace LGCNS.axink.App.Services
{
    public sealed class WebAudioCaptureService : IWebAudioCaptureService, IDisposable
    {
        private readonly object _gate = new();

        private CaptureSession _mic = new(false, null, true);
        private CaptureSession _spk = new(false, null, true);

        private readonly IChannelAudioHub _hub;
        private readonly IEventBus _bus;
        private readonly AudioStreamCapture _micCapture;
        private readonly AudioStreamCapture _spkCapture;
        private WebAudioCapture? _micWebAudioCapture;
        private WebAudioCapture? _spkWebAudioCapture;
        private bool _enableMicCapture = false;
        private bool _enableSpkCapture = false;

        // ✅ 재시작을 위해 "마지막 Start 요청" 저장
        private StartArgs? _lastStart;
        private bool _isRunning = false;

        private CancellationTokenSource _sendCts;

        private readonly ISettingsMonitor<SystemSettings> _sysSettings;

        private sealed record StartArgs(
            string DeviceType,
            string Token,
            int RoomId,
            string SourceLang,
            string TargetLang,
            string Platform,
            string RoomType
        );

        public bool IsRunning
        {
            get { lock (_gate) return _isRunning; }
        }

        public WebAudioCaptureService(
           ISettingsMonitor<SystemSettings> sysSettings,
           AudioStreamCapture micCapture,
           AudioStreamCapture spkCapture,
           IChannelAudioHub hub,
           IEventBus bus)
        {
            _sysSettings = sysSettings;
            _micCapture = micCapture;
            _spkCapture = spkCapture;
            _hub = hub;
            _bus = bus;

            _spkCapture.OnStalled += SpkCapture_OnStalled;

            _sendCts = new CancellationTokenSource();
        }

        private void SpkCapture_OnStalled(string reason)
        {
            // 여기서 직접 RestartAsync() 하지 말고  
            // DeviceChangeHub가 디바운스/중복방지 포함해서 재시작하게 넘기는 게 베스트
            _bus.Publish("captureStalled", new { source = "spk", reason });
        }

        public async Task StartAsync(string deviceType, string token, int roomId, string sourceLang, string targetLang, string platform, string roomType, CancellationToken ct)
        {

            // ✅ 마지막 Start 인자 저장 (Restart에 사용)
            lock (_gate)
            {
                _lastStart = new StartArgs(deviceType, token, roomId, sourceLang, targetLang, platform, roomType);
            }

            if (string.IsNullOrEmpty(token))
            {
                var defaults = _sysSettings.Current;
                defaults.SpaStreamMode = SpaStreamMode.AudioBinary;
                _sysSettings.UpdateAndSave(defaults);
            }

            var dt = deviceType.ToLowerInvariant();
            _enableMicCapture = dt == "all" || dt == "input";
            _enableSpkCapture = dt == "all" || dt == "output";


            // ✅ 이전 실행이 남아있다면 안전하게 정리 (중복 start 방지)
            await StopAsync(ct);

            // ✅ Start용 CTS 새로 생성
            lock (_gate)
            {
                _sendCts?.Dispose();
                _sendCts = new CancellationTokenSource();
            }

            var tasks = new List<Task>();
            if (_enableMicCapture)
            {
                _micWebAudioCapture = new WebAudioCapture(_sysSettings, AudioSourceType.Mic, _micCapture, _hub, _bus);
                tasks.Add(_micWebAudioCapture.StartAsync(token, roomId, sourceLang, targetLang, platform, roomType, _sendCts.Token));
            }

            if (_enableSpkCapture)
            {
                _spkWebAudioCapture = new WebAudioCapture(_sysSettings, AudioSourceType.Spk, _spkCapture, _hub, _bus);
                tasks.Add(_spkWebAudioCapture.StartAsync(token, roomId, sourceLang, targetLang, platform, roomType, _sendCts.Token));
            }

            await Task.WhenAll(tasks);

            lock (_gate)
            {
                _isRunning = true;
                UpdateSessionsUnsafe();
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {

            WebAudioCapture? mic;
            WebAudioCapture? spk;

            lock (_gate)
            {
                mic = _micWebAudioCapture;
                spk = _spkWebAudioCapture;
                _micWebAudioCapture = null;
                _spkWebAudioCapture = null;
                _isRunning = false;

                // ✅ 송신/처리 루프 끊기
                if (!_sendCts.IsCancellationRequested)
                    _sendCts.Cancel();

                UpdateSessionsUnsafe();
            }

            var tasks = new List<Task>();

            if (mic != null) tasks.Add(mic.TryStop(_micCapture));
            if (spk != null) tasks.Add(spk.TryStop(_spkCapture));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);

            if (mic != null) await mic.DisposeAsync();
            if (spk != null) await spk.DisposeAsync();
        }

        // ✅ 디바이스 변경 시 coordinator가 호출할 API
        public async Task RestartAsync(CancellationToken ct = default)
        {
            StartArgs? args;
            lock (_gate) args = _lastStart;

            // 마지막 Start 정보가 없으면 재시작할 근거가 없음
            if (args == null) return;
            Logging.Debug("[ReStart]");
            await StopAsync(ct);
            await StartAsync(args.DeviceType, args.Token, args.RoomId, args.SourceLang, args.TargetLang, args.Platform, args.RoomType, ct);
        }

        public CaptureSession GetSession(CaptureTarget target)
        {
            lock (_gate)
            {
                return target == CaptureTarget.Mic ? _mic : _spk;
            }
        }

        private void UpdateSessionsUnsafe()
        {
            _mic = new CaptureSession(
                IsRunning: _micWebAudioCapture != null,
                DeviceId: null,
                UseDefaultDevice: true
            );

            _spk = new CaptureSession(
                IsRunning: _spkWebAudioCapture != null,
                DeviceId: null,
                UseDefaultDevice: true
            );
        }

        public Task<object> GetStateAsync(CancellationToken ct)
            => Task.FromResult<object>(new
            {
                running = IsRunning,
                mic = _micWebAudioCapture != null,
                spk = _spkWebAudioCapture != null
            });

        public void Dispose()
        {
            // 필요 시 StopAsync 호출
        }
    }
}
