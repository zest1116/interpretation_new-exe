using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Models.Devices
{
    public sealed record DeviceChangeSummary(
        // 추가된 장치
        IReadOnlyList<AudioDeviceDto> AddedInputs,
        IReadOnlyList<AudioDeviceDto> AddedOutputs,
        // 제거된 장치
        IReadOnlyList<AudioDeviceDto> RemovedInputs,
        IReadOnlyList<AudioDeviceDto> RemovedOutputs,
        // 기본 장치 변경 (null이면 변경 없음)
        DefaultDeviceChange? DefaultInputChange,
        DefaultDeviceChange? DefaultOutputChange
    )
    {
        public bool HasAnyChange =>
            AddedInputs.Count > 0 || AddedOutputs.Count > 0 ||
            RemovedInputs.Count > 0 || RemovedOutputs.Count > 0 ||
            DefaultInputChange is not null || DefaultOutputChange is not null;
    }

    public sealed record DefaultDeviceChange(string? OldId, string? NewId);
}
