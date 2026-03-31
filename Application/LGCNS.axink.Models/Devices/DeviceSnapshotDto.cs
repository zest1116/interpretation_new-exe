using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Models.Devices
{
    public sealed record AudioDeviceDto(string Id, string Name, bool IsDefault);

    public sealed record DefaultDeviceRequest(string? deviceType, string? deviceId);

    public sealed record DeviceSnapshotDto(
        IReadOnlyList<AudioDeviceDto> Inputs,
        IReadOnlyList<AudioDeviceDto> Outputs,
        string? CurrentInputId,
        string? CurrentOutputId
    );

    public interface IDeviceService
    {
        Task<DeviceSnapshotDto> GetSnapshotAsync(CancellationToken ct);

        Task<DeviceSnapshotDto> SetDefaultDevice(string? deviceType, string? deviceId, CancellationToken ct);
    }
}
