using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Models.Devices
{
    /// <summary>
    /// 오디오 장치 정보
    /// </summary>
    public class AudioDeviceInfo
    {

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsInput { get; set; }

        public bool isDefault { get; set; }

        public override string ToString() => Name;
    }
}
