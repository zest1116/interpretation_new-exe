using LGCNS.axink.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App.ViewModels
{
    public sealed class DeviceItemView
    {
        public required AudioDeviceDto Device { get; init; }

        // 템플릿 텍스트 바인딩용 프록시
        public string Id => Device.Id;
        public string Endpoint => Device.Endpoint;
        public string DeviceFriendlyName => Device.DeviceFriendlyName;

        // 시각 상태
        public bool IsSelected { get; init; }        // 라디오 ● 강조
        public bool ShowDefaultBadge { get; init; }  // "기본 장치" 배지
        public double DefaultBadgeOpacity { get; init; } = 1.0;
        public bool IsSentinel { get; init; }        // "사용 안 함" 여부

        public bool ShowSubtitle => !IsSentinel;
    }
}
