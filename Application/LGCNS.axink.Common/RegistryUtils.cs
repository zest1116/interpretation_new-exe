using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common
{
    public class RegistryUtils
    {
        private const string RegistryPath = @$"Software\{Consts.APP_COMPANY}\{Consts.APP_NAME}";
        private const string RegistryKeyCompanyCode = "CompanyCode";

        public static string? ReadCompanyCode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(RegistryKeyCompanyCode) as string;
        }

        public static void SaveCompanyCode(string code)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue(RegistryKeyCompanyCode, code, RegistryValueKind.String);
        }
    }
}
