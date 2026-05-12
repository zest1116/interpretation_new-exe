using CommunityToolkit.Mvvm.Input;
using LGCNS.axink.App.Services;
using LGCNS.axink.App.ViewModels;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Localization;
using LGCNS.axink.Common.Monitors;
using LGCNS.axink.Models.Devices;
using LGCNS.axink.Models.Settings;
using System.Windows;
using System.Windows.Input;

namespace LGCNS.axink.App.Windows
{
    /// <summary>
    /// DeviceControllerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DeviceControllerWindow : Window
    {
        private readonly IDeviceService _deviceService;
        private readonly DeviceChangeHub _deviceChangeHub;
        private readonly ISettingsMonitor<SystemSettings> _sysSettings;

        public ICommand SelectDeviceCommand { get; }

        public DeviceControllerWindow(
            IDeviceService deviceService,
            DeviceChangeHub deviceChangeHub,
            ISettingsMonitor<SystemSettings> sysSettings,
            Window owner)
        {
            InitializeComponent();

#pragma warning disable CA1416 // 플랫폼 호환성 유효성 검사
            SelectDeviceCommand = new RelayCommand<DeviceItemView>(async device =>
            {
                if (device == null || device.IsSelected) return;
                await HandleDeviceSelectionAsync(device);

            });
#pragma warning restore CA1416 // 플랫폼 호환성 유효성 검사

            _deviceService = deviceService;
            _deviceChangeHub = deviceChangeHub;
            _sysSettings = sysSettings;
            Owner = owner;

            // 장치 변경 이벤트 구독 (자동 갱신)
            _deviceChangeHub.DeviceListChanged += DeviceChangeHub_DeviceListChanged;

            Loaded += async (_, _) => await RefreshDevicesAsync();
            Closed += (_, _) => _deviceChangeHub.DeviceListChanged -= DeviceChangeHub_DeviceListChanged;
        }

        private void DeviceChangeHub_DeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        {
            Dispatcher.Invoke(async () => await RefreshDevicesAsync());
        }

        private async Task RefreshDevicesAsync()
        {
            try
            {
                var snapshot = await _deviceService.GetSnapshotAsync(CancellationToken.None);

                SpeakerList.ItemsSource = BuildItemViews(
                    snapshot.Outputs, _sysSettings.Current.OutputDeviceDisabled, Consts.DISABLED_OUTPUT_ID);
                MicList.ItemsSource = BuildItemViews(
                    snapshot.Inputs, _sysSettings.Current.InputDeviceDisabled, Consts.DISABLED_INPUT_ID);
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "[AudioDeviceWindow] 장치 목록 조회 실패");
            }
        }

        private static IReadOnlyList<DeviceItemView> BuildItemViews(
            IEnumerable<AudioDeviceDto> source,
            bool disabled,
            string sentinelId)
        {
            var views = source.Select(d => new DeviceItemView
            {
                Device = d,
                IsSelected = !disabled && d.IsDefault, // disabled면 라디오 해제
                ShowDefaultBadge = d.IsDefault,        // 배지는 OS 기본장치면 항상 표시
                DefaultBadgeOpacity = disabled ? 0.4 : 1.0, // disabled면 연하게
                IsSentinel = false,
            }).ToList();

            var label = Application.Current.Resources["Dic_Device_Disabled"] as string ?? "사용 안 함";

            views.Add(new DeviceItemView
            {
                Device = new AudioDeviceDto ( sentinelId, "", false, label, ""),
                IsSelected = disabled,        // disabled면 사용안함이 라디오 ●
                ShowDefaultBadge = false,     // 사용안함에는 배지 절대 표시 안 함
                IsSentinel = true,
            });

            return views;
        }

        /// <summary>
        /// 실제 장치 목록 끝에 "사용 안 함" sentinel 항목을 덧붙여 반환.
        /// disabled=true 면 모든 실제 장치의 IsDefault를 false로 만들고 sentinel만 강조.
        /// </summary>
        private static IReadOnlyList<AudioDeviceDto> BuildDeviceList(
            IEnumerable<AudioDeviceDto> source,
            bool disabled,
            string sentinelId)
        {
            var list = source.ToList();

            var label = Application.Current.Resources[LangKeys.Dic_Device_Disabled] as string ?? "Disabled";

            list.Add(new AudioDeviceDto
            (
                sentinelId,
                label,
                disabled,
                label,
                label
            ));

            return list;
        }

        private async void Device_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe ||
                fe.Tag is not DeviceItemView item ||
                item.IsSelected)
            {
                return;
            }
            await HandleDeviceSelectionAsync(item);
        }

        private async Task HandleDeviceSelectionAsync(DeviceItemView item)
        {
            try
            {
                bool isOutput = ResolveIsOutput(item.Id);
                var store = new JsonFileStore<SystemSettings>(Consts.APP_NAME, Consts.FILE_NAME_SYS_SETTINGS);

                if (item.IsSentinel)
                {
                    // "사용 안 함" 선택 → disabled=true 저장 (OS 기본장치는 그대로)
                    if (isOutput)
                    {
                        _sysSettings.Current.OutputDeviceDisabled = true;
                        store.UpdateProperty(x => x.OutputDeviceDisabled, true);
                    }
                    else
                    {
                        _sysSettings.Current.InputDeviceDisabled = true;
                        store.UpdateProperty(x => x.InputDeviceDisabled, true);
                    }
                }
                else
                {
                    // 실제 장치 선택 → disabled 해제 + OS 기본장치 변경
                    if (isOutput && _sysSettings.Current.OutputDeviceDisabled)
                    {
                        _sysSettings.Current.OutputDeviceDisabled = false;
                        store.UpdateProperty(x => x.OutputDeviceDisabled, false);
                    }
                    else if (!isOutput && _sysSettings.Current.InputDeviceDisabled)
                    {
                        _sysSettings.Current.InputDeviceDisabled = false;
                        store.UpdateProperty(x => x.InputDeviceDisabled, false);
                    }

                    await _deviceService.SetDefaultDevice(
                        isOutput ? "output" : "input",
                        item.Device.Id,
                        CancellationToken.None);
                }

                await RefreshDevicesAsync();
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "[AudioDeviceWindow] 기본 장치 변경 실패");
            }
        }

        private bool ResolveIsOutput(string deviceId)
        {
            if (deviceId == Consts.DISABLED_OUTPUT_ID) return true;
            if (deviceId == Consts.DISABLED_INPUT_ID) return false;

            return (SpeakerList.ItemsSource as IEnumerable<DeviceItemView>)?
                .Any(v => v.Id == deviceId) == true;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        
    }
}
