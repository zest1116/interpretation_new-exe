using LGCNS.axink.Audio.Capture;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio
{
    public class MicAudioStreamCapture : AudioStreamCapture
    {
        public MicAudioStreamCapture(bool isInputMode, ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings) : base(isInputMode, sysSettings, appSettings)
        {
        }
    }
}
