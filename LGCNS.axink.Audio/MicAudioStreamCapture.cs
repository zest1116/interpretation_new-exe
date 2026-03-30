using LGCNS.axink.Audio.Capture;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Settings;

namespace LGCNS.axink.Audio
{
    public class MicAudioStreamCapture : AudioStreamCapture
    {
        public MicAudioStreamCapture(bool isInputMode, ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings) : base(isInputMode, sysSettings, appSettings)
        {
        }
    }
}
