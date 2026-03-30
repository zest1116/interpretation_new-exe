using LGCNS.axink.Audio;
using LGCNS.axink.Audio.Capture;
using LGCNS.axink.Audio.Devices;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Messages;
using LGCNS.axink.Medels.Settings;
using LGCNS.axink.WebHosting;
using LGCNS.axink.WebHosting.Communication;
using LGCNS.axink.WebHosting.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;

namespace LGCNS.axink.App.Services
{
    public sealed class WebAudioCapture : IAsyncDisposable
    {
        private readonly AudioStreamCapture _capture;

        private readonly IChannelAudioHub _hub;
        private readonly IEventBus _bus;

        private readonly PcmFramePump _pump;

        private bool _isRunning;


        private readonly ConcurrentQueue<byte[]> _audioQueue;
        private readonly WebSocketService _webSocket;

        private CancellationTokenSource? _sendCts;
        public bool isConnected => _webSocket.IsConnected;


        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly AudioSourceType _audioSourceType;

        public WebAudioCapture(
            ISettingsMonitor<SystemSettings> sysSettings,
            AudioSourceType audioSourceType,
            AudioStreamCapture capture,
            IChannelAudioHub hub,
            IEventBus bus)
        {
            _sysSettings = sysSettings;
            _audioSourceType = audioSourceType;
            _capture = capture;
            _hub = hub;
            _bus = bus;

            _pump = new PcmFramePump(OnFrameReady);
            _webSocket = new WebSocketService();
            _audioQueue = new ConcurrentQueue<byte[]>();
            InitializeEventHandlers();
        }

        private void OnFrameReady(AudioSourceType ch, ReadOnlyMemory<byte> frame640)
        {
            // 오디오를 SPA로 보내는 옵션
            if (_sysSettings.Current.SpaStreamMode == SpaStreamMode.AudioBinary)
            {
                // Binary 패킷: 2바이트 헤더 + PCM 640
                var packet = new byte[2 + frame640.Length];
                packet[0] = Consts.AUDIO_PACKET_PCM16;
                packet[1] = (byte)ch;
                frame640.CopyTo(packet.AsMemory(2));

                _hub.Publish(WsOutMessage.FromBinary(packet));
            }


        }

        private void InitializeEventHandlers()
        {
            _webSocket.OnConnected += () =>
            {

            };

            _webSocket.OnDisconnected += () =>
            {

            };

            _webSocket.OnMessageReceived += (message) =>
            {
                var stt = new SttEnvelope(
                   Type: _audioSourceType == AudioSourceType.Mic ? "input" : "output",
                   Payload: JsonConvert.DeserializeObject(message)
                   );
                _hub.Publish(WsOutMessage.FromText(JsonConvert.SerializeObject(stt, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                })));

            };

            _webSocket.OnError += (error) =>
            {

            };
        }

        public async Task StartAsync(string? token, int roomId, string sourceLang, string targetLang, string platform, string roomType, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_sysSettings.Current.SpaStreamMode == SpaStreamMode.SttText && token == null) return;


            _sendCts = new CancellationTokenSource();

            if (_isRunning) return;
            string? defaultId;
            if (_audioSourceType == AudioSourceType.Mic)
                defaultId = DeviceManger.GetDefaultInputDeviceId();
            else
                defaultId = DeviceManger.GetDefaultOutputDeviceId();


            _capture.OnAudioDataAvailable += OnAudio;
            _pump.Reset(_audioSourceType);


            if (_sysSettings.Current.SpaStreamMode == SpaStreamMode.SttText)
            {
                InitMessage initData = new InitMessage()
                {
                    Type = "INIT",
                    AccessToken = token!,
                    RoomId = roomId,
                    SourceLanguage = sourceLang,
                    TargetLanguage = targetLang,
                    Platform = platform,
                    RoomType = roomType,
                    AudioInfo = new AudioInfo()
                    {
                        AudioFormat = "pcm_s16le",
                        SampleRate = 16000,
                        NumChannels = 1
                    }
                };

                // WebSocket 연결
                await _webSocket.ConnectAsync("wss://qa-new.dt.lgcns.com:9083/ws/stt");

                // 초기 설정 메시지 전송
                await _webSocket.SendInitMessageAsync(JsonConvert.SerializeObject(initData, Formatting.None));

                // 오디오 전송 태스크 시작
                _ = SendAudioLoopAsync(_sendCts.Token);
            }
            // ✅ deviceId 반영
            _capture.StartCapture(defaultId);



            _isRunning = true;


        }

        private void OnAudio(byte[] data)
        {
            try
            {
                if (data.Length == 0) return;
                if (_sysSettings.Current.SpaStreamMode == SpaStreamMode.SttText)
                    _audioQueue.Enqueue(data);
                else if (_sysSettings.Current.SpaStreamMode == SpaStreamMode.AudioBinary)
                {
                    _pump.Push(_audioSourceType, data);
                }
            }
            catch (Exception ex)
            {
                //PublishState("mic", "error", new { message = ex.Message });
            }
        }

        private async Task SendAudioLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.IsConnected)
            {
                try
                {
                    if (_audioQueue.TryDequeue(out byte[]? audioData) && audioData != null)
                    {
                        await _webSocket.SendAudioDataAsync(audioData, cancellationToken);

                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //AppendLog($"[{StreamName}] 전송 오류: {ex.Message}");
                }
            }

        }

        public async Task TryStop(AudioStreamCapture capture)
        {
            try
            {

                var mi = capture.GetType().GetMethod("StopCapture");
                mi?.Invoke(capture, null);
                _sendCts?.Cancel();
                if (_pump != null)
                {
                    _pump.Reset(_audioSourceType);
                }
            }
            catch { /* ignore */ }
        }

        public async ValueTask DisposeAsync()
        {
            try { _capture.OnAudioDataAvailable -= OnAudio; } catch { }

            await TryStop(_capture);

            (_capture as IDisposable)?.Dispose();
        }
    }
}
