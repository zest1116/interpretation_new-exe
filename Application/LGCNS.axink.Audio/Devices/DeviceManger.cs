using LGCNS.axink.Models.Devices;
using NAudio.CoreAudioApi;

namespace LGCNS.axink.Audio.Devices
{
    public class DeviceManger
    {
        public List<AudioDeviceInfo> GetInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            var enumerator = new MMDeviceEnumerator();
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            if (defaultDevice != null)
            {
                var defaultInputId = defaultDevice.ID;
                var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                foreach (var device in collection)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        IsInput = true,
                        isDefault = device.ID == defaultInputId,
                        Endpoint = device.Properties[PropertyKeys.PKEY_Device_DeviceDesc].Value.ToString(),
                        DeviceFriendlyName = device.Properties[PropertyKeys.PKEY_DeviceInterface_FriendlyName].Value.ToString()
                    });
                }
            }
            return devices;
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            var enumerator = new MMDeviceEnumerator();
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (defaultDevice != null)
            {
                var defaultOutputId = defaultDevice.ID;
                var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in collection)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        IsInput = false,
                        isDefault = device.ID == defaultOutputId,
                        Endpoint = device.Properties[PropertyKeys.PKEY_Device_DeviceDesc].Value.ToString(),
                        DeviceFriendlyName = device.Properties[PropertyKeys.PKEY_DeviceInterface_FriendlyName].Value.ToString()
                    });
                }
            }
            return devices;
        }

        public static string? GetDefaultInputDeviceId()
        {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID;
        }

        public static string? GetDefaultOutputDeviceId()
        {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }

        /// <summary>
        /// 선택 장치를 "기본 출력"으로 설정 (역할 3종)
        /// 필요 시: 보이기/활성화도 함께 시도
        /// </summary>
        public static bool TryEnableAndSetDefaultOutput(string deviceId, out string? error)
        {
            // 보이기 + 활성화 + 기본 역할 설정
            return AudioDevicePolicy.TryEnsureEnabledAndSetDefault(deviceId, out error);
        }

        /// <summary>
        /// 선택 장치를 "기본 입력"으로 설정 (역할 3종)
        /// 필요 시: 보이기/활성화도 함께 시도
        /// </summary>
        public static bool TryEnableAndSetDefaultInput(string deviceId, out string? error)
        {
            return AudioDevicePolicy.TryEnsureEnabledAndSetDefault(deviceId, out error);
        }

        /// <summary>
        /// 활성화 시도 없이(안전) 기본 설정만 적용하고 싶을 때
        /// </summary>
        public static bool TrySetDefaultOnly(string deviceId, out string? error)
        {
            error = null;

            // 숨김 해제는 같이 하는 게 UX상 좋음
            AudioDevicePolicy.TrySetVisible(deviceId, true, out _);

            return AudioDevicePolicy.TrySetDefaultAllRoles(deviceId, out error);
        }
    }
}
