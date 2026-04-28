using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LGCNS.axink.Common.Localization
{
    public sealed class LocalizationManager
    {
        private static readonly Lazy<LocalizationManager> _instance = new(() => new());
        public static LocalizationManager Instance => _instance.Value;

        private const string DictionaryUriBase =
            "pack://application:,,,/Common.Localization;component/Languages/Strings.{0}.xaml";

        private string _currentLang = "ko";

        private LocalizationManager() { }

        public string CurrentLanguage => _currentLang;

        /// <summary>
        /// 언어 전환. Application.Current.Resources.MergedDictionaries에서
        /// 기존 언어 사전을 교체한다.
        /// </summary>
        public void SetLanguage(string lang)
        {
            if (_currentLang == lang) return;

            var uri = new Uri(string.Format(DictionaryUriBase, lang), UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = uri };

            var merged = Application.Current.Resources.MergedDictionaries;

            // 기존 언어 사전 제거
            var old = merged.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("/Languages/Strings.") == true);

            if (old != null)
                merged.Remove(old);

            merged.Add(newDict);
            _currentLang = lang;

            LanguageChanged?.Invoke(this, lang);
        }

        public event EventHandler<string>? LanguageChanged;

        public static string GetString(string key)
        {
            try
            {
                if (Application.Current?.FindResource(key) is string value)
                    return value;
            }
            catch (ResourceReferenceKeyNotFoundException)
            {
                Debug.WriteLine($"[Localization] Key not found: {key}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Localization] Error resolving key '{key}': {ex.Message}");
            }

            return $"[{key}]";
        }

        // 포맷 지원 오버로드
        public static string GetString(string key, params object[] args)
        {
            var template = GetString(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                Debug.WriteLine($"[Localization] Format error: key={key}, args={string.Join(",", args)}");
                return template;
            }
        }
    }
}
