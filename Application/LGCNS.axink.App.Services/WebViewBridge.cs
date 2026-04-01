using LGCNS.axink.Common;
using LGCNS.axink.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LGCNS.axink.App.Services
{
    /// <summary>
    /// WebView2 ↔ React SPA 간 양방향 메시지 브릿지
    /// 
    /// [SPA → WPF 요청]
    ///   { "type": "requestDevices" }
    ///   { "type": "setDefaultDevice", "data": { "deviceType": "input", "deviceId": "..." } }
    ///
    /// [WPF → SPA 응답/이벤트]
    ///   { "type": "devicesResponse",  "data": { inputs: [...], outputs: [...], ... } }
    ///   { "type": "deviceChanged",    "data": { ... } }          ← 기존 DeviceChangeHub에서도 발행
    ///   { "type": "error",            "data": { "message": "..." } }
    /// </summary>
    public sealed class WebViewBridge
    {
        private bool _isRunning = false;
        private readonly IDeviceService _deviceService;
        private readonly WebAudioCaptureService _captureService;

        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public WebViewBridge(
            IDeviceService deviceService,
            WebAudioCaptureService captureService)
        {
            _deviceService = deviceService;
            _captureService = captureService;
        }

        /// <summary>
        /// SPA에서 postMessage로 보낸 JSON을 파싱하여 적절한 핸들러로 라우팅합니다.
        /// 반환값은 SPA로 PostWebMessageAsJson할 응답 JSON입니다.
        /// </summary>
        public async Task<string?> HandleMessageAsync(string rawJson, CancellationToken ct = default)
        {
            try
            {
                var msg = JObject.Parse(rawJson);
                var type = msg.Value<string>("type");

                if (string.IsNullOrEmpty(type))
                {
                    Logging.Debug($"[WebViewBridge] type이 없는 메시지 무시: {rawJson}");
                    return null;
                }

                return type.ToLowerInvariant() switch
                {
                    "requestdevices" => await HandleRequestDevicesAsync(ct),
                    "setdefaultdevice" => await HandleSetDefaultDeviceAsync(msg["data"], ct),
                    "startcapture" => await HandleStartCaptureAsync(msg["data"], ct),
                    "stopcapture" => await HandleStopCaptureAsync(ct),
                    _ => null // 알 수 없는 타입은 무시 (기존 메시지와 충돌 방지)
                };
            }
            catch (JsonReaderException ex)
            {
                Logging.Error(ex, "[WebViewBridge] JSON 파싱 실패");
                return BuildErrorResponse($"잘못된 JSON 형식: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "[WebViewBridge] 메시지 처리 실패");
                return BuildErrorResponse(ex.Message);
            }
        }

        #region Handlers

        /// <summary>
        /// 1. 디바이스 목록 조회
        /// </summary>
        private async Task<string> HandleRequestDevicesAsync(CancellationToken ct)
        {
            var snapshot = await _deviceService.GetSnapshotAsync(ct);

            Logging.Debug($"[WebViewBridge] 디바이스 목록 응답: inputs={snapshot.Inputs.Count}, outputs={snapshot.Outputs.Count}");

            return BuildResponse("devicesResponse", snapshot);
        }

        /// <summary>
        /// 2. 기본 디바이스 변경
        /// </summary>
        private async Task<string> HandleSetDefaultDeviceAsync(JToken? data, CancellationToken ct)
        {
            if (data == null)
                return BuildErrorResponse("data가 필요합니다.");

            var deviceType = data.Value<string>("deviceType");
            var deviceId = data.Value<string>("deviceId");

            if (string.IsNullOrEmpty(deviceType) || string.IsNullOrEmpty(deviceId))
                return BuildErrorResponse("deviceType과 deviceId가 필요합니다.");

            Logging.Debug($"[WebViewBridge] 기본 디바이스 변경 요청: {deviceType} → {deviceId}");

            var snapshot = await _deviceService.SetDefaultDevice(deviceType, deviceId, ct);

            return BuildResponse("devicesResponse", snapshot);
        }

        /// <summary>
        /// 4. 캡처 시작
        /// </summary>
        private async Task<string> HandleStartCaptureAsync(JToken? data, CancellationToken ct)
        {
            if (data == null)
                return BuildErrorResponse("data가 필요합니다.");

            var deviceType = data.Value<string>("deviceType") ?? "all";
            var accessToken = data.Value<string>("accessToken") ?? string.Empty;
            var roomId = data.Value<int?>("roomId") ?? -1;
            var sourceLang = data.Value<string>("sourceLang") ?? string.Empty;
            var targetLang = data.Value<string>("targetLang") ?? string.Empty;
            var platform = data.Value<string>("platform") ?? string.Empty;
            var roomType = data.Value<string>("roomType") ?? string.Empty;

            Logging.Debug($"[WebViewBridge] 캡처 시작 요청: deviceType={deviceType}");

            if (!_isRunning)
            {
                await _captureService.StartAsync(deviceType, accessToken, roomId, sourceLang, targetLang, platform, roomType, ct);
                _isRunning = true;
            }
            return BuildResponse("captureResponse", new { ok = true, running = _captureService.IsRunning });
        }

        /// <summary>
        /// 캡처 중지
        /// </summary>
        private async Task<string> HandleStopCaptureAsync(CancellationToken ct)
        {
            Logging.Debug("[WebViewBridge] 캡처 중지 요청");

            await _captureService.StopAsync(ct);
            _isRunning = false;
            return BuildResponse("captureResponse", new { ok = true, running = false });
        }

        #endregion

        #region Response Builders

        private static string BuildResponse(string type, object data)
        {
            var envelope = new { type, data };
            return JsonConvert.SerializeObject(envelope, _jsonSettings);
        }

        private static string BuildErrorResponse(string message)
        {
            return BuildResponse("error", new { message });
        }

        #endregion

        #region Push helpers (WPF → SPA 방향)

        /// <summary>
        /// 3. 디바이스 변경 알림용 JSON 빌드
        /// DeviceChangeHub에서 이 메서드를 호출하여 일관된 형식으로 push할 수 있습니다.
        /// </summary>
        public static string BuildDeviceChangedMessage(string devicesJson)
        {
            var data = JsonConvert.DeserializeObject(devicesJson);
            return BuildResponse("deviceChanged", data!);
        }

        #endregion
    }
}