using LGCNS.axink.App.Services;
using LGCNS.axink.Common;
using LGCNS.axink.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LGCNS.axink.App.Windows
{
    /// <summary>
    /// DeviceControllerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DeviceControllerWindow : Window
    {
        private readonly IDeviceService _deviceService;
        private readonly DeviceChangeHub _deviceChangeHub;

        public DeviceControllerWindow(
            IDeviceService deviceService,
            DeviceChangeHub deviceChangeHub,
            Window owner)
        {
            InitializeComponent();

            _deviceService = deviceService;
            _deviceChangeHub = deviceChangeHub;
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
                SpeakerList.ItemsSource = snapshot.Outputs;
                MicList.ItemsSource = snapshot.Inputs;
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "[AudioDeviceWindow] 장치 목록 조회 실패");
            }
        }

        private async void Device_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is AudioDeviceDto device && !device.IsDefault)
            {
                try
                {
                    // Outputs(스피커) 목록에 있으면 output, 아니면 input
                    var deviceType = (SpeakerList.ItemsSource as IReadOnlyList<AudioDeviceDto>)?
                        .Any(d => d.Id == device.Id) == true ? "output" : "input";

                    await _deviceService.SetDefaultDevice(deviceType, device.Id, CancellationToken.None);
                    await RefreshDevicesAsync();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "[AudioDeviceWindow] 기본 장치 변경 실패");
                }
            }
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
