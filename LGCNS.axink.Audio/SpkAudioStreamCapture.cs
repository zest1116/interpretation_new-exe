using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio
{
    public sealed class SpkAudioStreamCapture : MicAudioStreamCapture
    {
        public SpkAudioStreamCapture(bool isInputMode, ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings) : base(isInputMode, sysSettings, appSettings)
        {
        }
    }
}
