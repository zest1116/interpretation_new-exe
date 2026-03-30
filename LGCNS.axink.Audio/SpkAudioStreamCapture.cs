using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Settings;

namespace LGCNS.axink.Audio
{
    public sealed class SpkAudioStreamCapture : MicAudioStreamCapture
    {
        public SpkAudioStreamCapture(bool isInputMode, ISettingsMonitor<SystemSettings> sysSettings, ISettingsMonitor<AppSettings> appSettings) : base(isInputMode, sysSettings, appSettings)
        {
        }
    }
}
