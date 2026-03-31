using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Settings;

namespace LGCNS.axink.App.ViewModels
{
    public sealed partial class AppSettingsViewModel : ObservableObject
    {
        private readonly ISettingsMonitor<AppSettings> _appSettings;

        [ObservableProperty]
        private string webViewSource = "";

        [ObservableProperty]
        private string validationMessage = "";

        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        public event Action<bool?>? RequestClose;

        public AppSettingsViewModel(ISettingsMonitor<AppSettings> appSettings)
        {
            _appSettings = appSettings;
            webViewSource = _appSettings.Current.WebViewSource ?? "";
        }


        partial void OnWebViewSourceChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(ValidationMessage))
            {
                ValidationMessage = "";
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }

        partial void OnValidationMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasValidationMessage));
        }


        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        [RelayCommand]
        private void Save()
        {
            // 실무형 최소 검증: 공백 불가 + Uri로 파싱 가능해야 함
            var input = (webViewSource ?? "").Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                ValidationMessage = "URL을 입력해주세요.";
                return;
            }

            if (string.Equals(input, "https://", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(input, "http://", StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage = "전체 URL을 입력해주세요. 예: https://example.com";
                return;
            }

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                ValidationMessage = "올바른 URL 형식이 아닙니다.";
                return;
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                ValidationMessage = "http 또는 https URL만 사용할 수 있습니다.";
                return;
            }

            ValidationMessage = "";

            var newSettings = new AppSettings { WebViewSource = input };
            _appSettings.UpdateAndSave(newSettings);

            RequestClose?.Invoke(true);
        }
    }
}
