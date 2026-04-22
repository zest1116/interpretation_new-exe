using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App.Updater
{
    public class UpdateInfo
    {
        public string CompanyCd { get; set; }

        public string VersionCode { get; set; }

        public string VersionName { get; set; }

        public string DownloadUrl { get; set; }

        public string DeviceType { get; set; }

        public string Changes { get; set; }

        public string PublishDate { get; set; }

        public string SpeechProvider { get; set; }

        public string CompanyName { get; set; }

        public string ServiceName { get; set; }
    }
}
