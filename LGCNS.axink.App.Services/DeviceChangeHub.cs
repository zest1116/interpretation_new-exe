using LGCNS.axink.Audio.Devices;
using LGCNS.axink.Common;
using LGCNS.axink.Medels.Devices;
using LGCNS.axink.WebHosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App.Services
{
    public sealed class DeviceChangeHub : IDisposable
    {
        private readonly DeviceNotificationListener _listener;
        private readonly IDeviceService _deviceService;          // 현재 장치 목록을 가져오는 서비스(이미 가지고 계신 WebDeviceService 등)
        private readonly WebAudioCaptureService _capture;        // RestartAsync 보유

        private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(700);
        private readonly TimeSpan _warmupSuppress = TimeSpan.FromSeconds(1);


        // ✅ Start 직후 “튐” 이벤트 억제 (등록 직후 defaultChanged/stateChanged가 1회 발생하는 케이스 방어)
        private DateTimeOffset _suppressUntilUtc = DateTimeOffset.MinValue;

        private readonly object _gate = new();
        private CancellationTokenSource? _cts;
        private bool _started;

        // ✅ 디바운스 동안 들어온 요청을 누적(OR)해서 1회 처리
        private bool _pendingRestart;
        private bool _pendingList;
        private string _pendingReason = "unknown";

        private readonly IEventBus _bus;
        private CancellationTokenSource? _busCts;
        private Task? _busLoopTask;

        // ✅ “외부로 내보내는” 단일 이벤트 (MainWindow는 이것만 구독)
        public event EventHandler<DeviceListChangedEventArgs>? DeviceListChanged;

        // ✅ 마지막 payload 캐시 (WebView 준비된 뒤 초기 1회 push용)
        private string? _lastPayloadJson;

        // ✅ PropertyChange 폭주방지
        private DateTime _lastPropertyListSentUtc = DateTime.MinValue;
        private readonly TimeSpan _propertyListMinInterval = TimeSpan.FromSeconds(2);


        public DeviceChangeHub(
            DeviceNotificationListener listener,
            IDeviceService deviceService,
            WebAudioCaptureService capture, IEventBus bus)
        {
            _listener = listener;
            _deviceService = deviceService;
            _capture = capture;
            _bus = bus;
            _busCts = new CancellationTokenSource();
            _busLoopTask = Task.Run(() => RunBusLoopAsync(_busCts.Token));
        }

        private async Task RunBusLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var ev in _bus.SubscribeEvents("captureStalled", ct))
                {
                    // 필요하면 payload에서 reason/source 파싱해서 로그/분기 가능
                    NotifyCaptureStalled(); // ✅ 여기서만 호출
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logging.Error(ex, "Bus loop crashed");
                // 선택: 재시작 정책(너무 공격적으로 재시작하지 않도록 backoff)
            }
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            // listener 이벤트 이름은 실제 타입에 맞게 연결하세요.
            // 예: _listener.Changed += OnRawChanged;
            // 예: _listener.DevicesChanged += OnRawChanged;
            _listener.DeviceChanged += OnDeviceChanged;

            // 2) 리스너 Start는 Hub만 수행 (중복 Start 방지)
            _listener.Start();

            // 3) Start 직후 1초 정도 이벤트 억제
            _suppressUntilUtc = DateTimeOffset.UtcNow.Add(_warmupSuppress);

            // (선택) 앱 시작 시점에 현재 목록을 한 번 만들어 캐시해두기
            _ = BuildAndEmitPayloadAsync(CancellationToken.None);
        }

        public string? GetLastPayloadJson() => _lastPayloadJson;

        /// <summary>
        /// ✅ 핵심: 캡처가 실제로 멈춘 경우(Loopback Stalled) 외부에서 이 메서드만 호출해주면
        /// 허브가 디바운스/중복방지 포함해서 재시작+목록전송까지 한 번에 처리
        /// </summary>
        public void NotifyCaptureStalled()
        {
            Enqueue(restartNeeded: true, listNeeded: true, reason: "loopbackStalled");
        }

        private bool ShouldSendListForProperty()
        {
            var now = DateTime.UtcNow;
            if (now - _lastPropertyListSentUtc < _propertyListMinInterval)
                return false;

            _lastPropertyListSentUtc = now;
            return true;
        }

        private void OnDeviceChanged(DeviceChangeEvent ev)
        {
            // ✅ 시작 직후 튐 방어: 특히 propertyChanged 폭주 방지에 중요
            if (DateTimeOffset.UtcNow < _suppressUntilUtc)
            {
                // 단, 진짜 토폴로지 변경(added/removed)까지 막고 싶지 않으면 여기서 예외 처리 가능
                if (ev.Action == "added" || ev.Action == "removed") { /* 통과시키고 싶으면 return 제거 */ }
                else return;
            }

            // ✅ 판단 로직
            // - propertyChanged: 목록 갱신만
            // - 나머지(added/removed/defaultChanged/stateChanged): 재시작 + 목록 갱신
            bool restartNeeded =
                ev.Action == "added" ||
                ev.Action == "removed" ||
                ev.Action == "defaultChanged" ||
                ev.Action == "stateChanged";

            bool listNeeded = true; // 어떤 이벤트든 SPA에는 목록 최신화가 유용

            if (ev.Action == "propertyChanged")
            {
                // ✅ 목록도 너무 자주 보내지 않기
                if (!ShouldSendListForProperty())
                    return;
                restartNeeded = false; // ✅ property로는 재시작 금지
            }
            Enqueue(restartNeeded, listNeeded, ev.Action);
        }

        private async Task BuildAndEmitPayloadAsync(CancellationToken ct)
        {
            try
            {

                var devices = await _deviceService.GetSnapshotAsync(CancellationToken.None);


                var json = JsonConvert.SerializeObject(devices);
                if (json == _lastPayloadJson)
                    return; // ✅ 동일하면 전송 안 함
                _lastPayloadJson = json;
                DeviceListChanged?.Invoke(this, new DeviceListChangedEventArgs(json));
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "BuildAndEmitPayloadAsync failed");
                throw;
            }
        }


        private void Enqueue(bool restartNeeded, bool listNeeded, string reason)
        {
            CancellationToken token;
            lock (_gate)
            {
                _pendingRestart |= restartNeeded;
                _pendingList |= listNeeded;
                _pendingReason = reason; // 마지막 이유로 덮어쓰기(원하면 배열로 누적도 가능)

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                token = _cts.Token;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounce, token);

                    bool doRestart;
                    bool doList;
                    string why;

                    lock (_gate)
                    {
                        doRestart = _pendingRestart;
                        doList = _pendingList;
                        why = _pendingReason;

                        _pendingRestart = false;
                        _pendingList = false;
                        _pendingReason = "unknown";
                    }

                    // (A) ✅ 재시작은 “토폴로지 변경” 또는 “스톨”일 때만
                    if (doRestart && _capture.IsRunning)
                    {
                        await _capture.RestartAsync(token);
                    }

                    // (B) ✅ 목록 전송(=propertyChanged 포함)
                    if (doList)
                    {
                        Logging.Debug($"Call BuildAndEmitPayloadAsync by doList");
                        await BuildAndEmitPayloadAsync(token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logging.Error(ex, "DeviceChangeHub failed");
                }
            }, token);
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;

            _listener.DeviceChanged -= OnDeviceChanged;
            _listener.Stop();

            lock (_gate)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _pendingRestart = false;
                _pendingList = false;
            }

            _busCts?.Cancel();
            _busCts?.Dispose();
            _busCts = null;

            // _busLoopTask는 await 안 해도 되지만, 안전하게 하려면 StopAsync 패턴 추천
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public sealed class DeviceListChangedEventArgs : EventArgs
    {
        public string Devices { get; } // 실제 타입(List<DeviceDto> 등)으로 바꾸세요.
        public DeviceListChangedEventArgs(string devices) => Devices = devices;
    }
}
