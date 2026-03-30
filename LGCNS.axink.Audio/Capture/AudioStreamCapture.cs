using LGCNS.axink.Audio.Recording;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Settings;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LGCNS.axink.Audio.Capture
{
    /// <summary>
    /// 단일 오디오 스트림 캡처를 담당하는 클래스
    /// </summary>
    public class AudioStreamCapture : IDisposable
    {
        private WasapiCapture? _capture;
        private readonly AudioResampler _resampler;
        private AudioFileWriter? _fileWriter;
        private bool _disposed;
        private bool _isCapturing;
        private readonly bool _isInputMode;
        private readonly string _streamName;

        public event Action<byte[]>? OnAudioDataAvailable;
        public event Action<string>? OnError;
        public event Action<string>? OnStatusChanged;

        private DateTime _lastDataAtUtc;
        private System.Threading.Timer? _watchdog;
        private int _stalledRaised;

        private readonly TimeSpan _stallThreshold = TimeSpan.FromMilliseconds(1500);
        private readonly TimeSpan _watchdogInterval = TimeSpan.FromMilliseconds(500);
        public event Action<string>? OnStalled;

        public bool IsCapturing => _isCapturing;
        public bool IsInputMode => _isInputMode;
        public string StreamName => _isInputMode ? "입력" : "출력";
        private WaveFormat? _captureFormat;

        private readonly ISettingsMonitor<SystemSettings> _sysSettings;
        private readonly ISettingsMonitor<AppSettings> _appSettings;
        public AudioStreamCapture(bool isInputMode, ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings)
        {
            _isInputMode = isInputMode;
            _sysSettings = sysSettings;
            _appSettings = appSettings;
            _streamName = isInputMode ? "입력" : "출력";
            _resampler = new AudioResampler(16000, 1);

        }

        private void InitializeFileWriter()
        {

            if (_sysSettings.Current.SaveOption == SaveOption.None)
                return;

            _fileWriter = new AudioFileWriter(_streamName, _appSettings.Current.SavedAudioFileRoot);


            _fileWriter.StartRecording(_sysSettings.Current.SaveOption == SaveOption.Raw ? _captureFormat : _resampler.TargetFormat);
        }

        public void StartCapture(string? deviceId)
        {
            StopCapture();

            try
            {
                var enumerator = new MMDeviceEnumerator();
                MMDevice? device = null;

                if (!string.IsNullOrEmpty(deviceId))
                {
                    try
                    {
                        device = enumerator.GetDevice(deviceId);
                    }
                    catch
                    {
                        device = _isInputMode
                            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                            : enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    }
                }
                else
                {
                    device = _isInputMode
                        ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                        : enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }

                if (device == null)
                {
                    OnError?.Invoke($"[{_streamName}] 오디오 장치를 찾을 수 없습니다.");
                    return;
                }

                _capture = _isInputMode
                    ? new WasapiCapture(device)
                    : new WasapiLoopbackCapture(device);

                _captureFormat = _capture.WaveFormat;

                if (_sysSettings.Current.SaveOption != SaveOption.None) InitializeFileWriter();

                _capture.DataAvailable += (sender, e) =>
                {
                    _lastDataAtUtc = DateTime.UtcNow;   // ✅ 여기 추가

                    if (e.BytesRecorded > 0)
                    {
                        byte[] buffer = new byte[e.BytesRecorded];
                        Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                        if (_sysSettings.Current.SaveOption == SaveOption.Raw) _fileWriter?.WriteRawData(buffer);

                        byte[] resampledData;
                        if (_captureFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                        {

                            resampledData = _resampler.ResampleFromFloat32(buffer, _captureFormat);
                        }
                        else
                        {

                            resampledData = _resampler.ResampleFrom16BitPcm(buffer, _captureFormat);
                        }

                        if (resampledData.Length > 0)
                        {
                            if (_sysSettings.Current.SaveOption == SaveOption.ReSampling) _fileWriter?.WriteRawData(resampledData);

                            OnAudioDataAvailable?.Invoke(resampledData);
                        }
                    }
                };

                _capture.RecordingStopped += (sender, e) =>
                {
                    if (e.Exception != null)
                    {
                        OnError?.Invoke($"[{_streamName}] 캡처 오류: {e.Exception.Message}");
                    }
                    RaiseStalledOnce("recording_stopped"); // ✅ 추가
                };

                _capture.StartRecording();
                _isCapturing = true;

                _stalledRaised = 0;
                _lastDataAtUtc = DateTime.UtcNow;
                StartWatchdog(); // ✅ 추가

                OnStatusChanged?.Invoke($"[{_streamName}] 캡처 시작됨");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"[{_streamName}] 캡처 시작 오류: {ex.Message}");
                StopCapture();
                throw;
            }
        }


        private void StartWatchdog()
        {
            // 출력(loopback)에만 적용하고 싶으면:
            if (_isInputMode) return;
            _watchdog?.Dispose();
            _watchdog = new System.Threading.Timer(_ =>
            {
                if (!_isCapturing) return;

                var elapsed = DateTime.UtcNow - _lastDataAtUtc;
                if (elapsed >= _stallThreshold)
                {
                    RaiseStalledOnce("data_timeout");
                }
            }, null, _watchdogInterval, _watchdogInterval);
        }

        private void StopWatchdog()
        {
            _watchdog?.Dispose();
            _watchdog = null;
        }

        private void RaiseStalledOnce(string reason)
        {
            if (System.Threading.Interlocked.Exchange(ref _stalledRaised, 1) == 1)
                return;

            OnStalled?.Invoke(reason);
        }

        public void StopCapture()
        {
            try
            {
                _isCapturing = false;
                StopWatchdog(); // ✅ 추가

                if (_fileWriter != null)
                {
                    _fileWriter.StopRecording();

                    _fileWriter.Dispose();
                    _fileWriter = null;
                }

                if (_capture != null)
                {
                    try
                    {
                        _capture.StopRecording();
                        _capture.Dispose();
                    }
                    catch { }
                    _capture = null;
                    OnStatusChanged?.Invoke($"[{_streamName}] 캡처 중지됨");
                }
            }
            catch
            {
                //무시
            }
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopCapture();
                _resampler.Dispose();
            }
        }
    }
}
