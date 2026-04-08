using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Models.Settings
{
    public sealed class AppSettings
    {
        public string? CompanyCode { get;set;  }

        public string? WebViewSource { get; set; }

        public string? SavedAudioFileRoot { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public string? TenantListUrl { get; set; }
    }
}
