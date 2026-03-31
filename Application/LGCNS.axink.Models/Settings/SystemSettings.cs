using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LGCNS.axink.Models.Settings
{
    public enum SaveOption
    {
        None,
        Raw,
        ReSampling,
    }

    public enum SpaStreamMode
    {
        AudioBinary,
        SttText

    }
    public sealed class SystemSettings
    {
        public SaveOption SaveOption { get; set; } = SaveOption.None;
        public SpaStreamMode SpaStreamMode { get; set; } = SpaStreamMode.SttText;

        public double WindowLeft { get; set; }

        public double WindowTop { get; set; }

        public double WindowWidth { get; set; }

        public double WindowHeight { get; set; }

        public int WindowState { get; set; }

    }
}
