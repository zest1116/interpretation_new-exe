using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common.Interfaces
{
    public enum CaptureTarget { Mic, Spk }

    public sealed record CaptureSession(
        bool IsRunning,
        string? DeviceId,
        bool UseDefaultDevice);

    public interface IWebAudioCaptureService
    {
        CaptureSession GetSession(CaptureTarget target);

        Task StartAsync(string deviceType, string token, int roomId, string sourceLang, string targetLang, string platform, string roomType, CancellationToken ct); // "mic" | "spk"
        Task StopAsync(CancellationToken ct);
        Task<object> GetStateAsync(CancellationToken ct);     // 필요하면 typed로 변경
    }
}
