using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Medels.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App.ViewModels
{
    public sealed partial class AppSettingsViewModel : ObservableObject
    {
        private readonly ISettingsMonitor<AppSettings> _appSettings;

        [ObservableProperty]
        private string webViewSource = "";

        public event Action<bool?>? RequestClose;

        public AppSettingsViewModel(ISettingsMonitor<AppSettings> appSettings)
        {
            _appSettings = appSettings;
            webViewSource = _appSettings.Current.WebViewSource ?? "";
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        [RelayCommand]
        private void Save()
        {
            // 실무형 최소 검증: 공백 불가 + Uri로 파싱 가능해야 함
            var input = (webViewSource ?? "").Trim();
            if (string.IsNullOrWhiteSpace(input))
                return;

            if (!Uri.TryCreate(input, UriKind.Absolute, out _))
                return;

            var newSettings = new AppSettings { WebViewSource = input };
            _appSettings.UpdateAndSave(newSettings);

            RequestClose?.Invoke(true);
        }
    }
}
