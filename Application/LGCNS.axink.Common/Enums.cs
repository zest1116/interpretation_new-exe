using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common
{
    public enum AppTheme { Light, Dark }


    public enum AudioSourceType
    {
        Mic = 1,
        Spk = 2
    }

    public enum AlertDialogButtonSet
    {
        Ok,
        YesNo
    }

    public enum AlertDialogResult
    {
        None,
        Ok,
        Yes,
        No
    }
}
