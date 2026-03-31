using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio.Devices
{
    public sealed class DeviceNotificationListener : IMMNotificationClient, IDisposable
    {
        private MMDeviceEnumerator? _enumerator;

        public event Action<DeviceChangeEvent>? DeviceChanged;

        public void Start()
        {
            if (_enumerator != null) return;

            _enumerator = new MMDeviceEnumerator();
            _enumerator.RegisterEndpointNotificationCallback(this);
        }

        public void Stop()
        {
            if (_enumerator == null) return;

            try { _enumerator.UnregisterEndpointNotificationCallback(this); } catch { }
            try { _enumerator.Dispose(); } catch { }
            _enumerator = null;
        }

        public void OnDeviceAdded(string deviceId)
        {
            Raise(new DeviceChangeEvent("added", deviceId));
        }

        public void OnDeviceRemoved(string deviceId)
        {
            Raise(new DeviceChangeEvent("removed", deviceId));
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            Raise(new DeviceChangeEvent("stateChanged", deviceId, Extra: new { state = newState.ToString() }));
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            Raise(new DeviceChangeEvent("defaultChanged", defaultDeviceId, Extra: new { flow = flow.ToString(), role = role.ToString() }));
        }

        private void Raise(DeviceChangeEvent ev) => DeviceChanged?.Invoke(ev);

        public void OnPropertyValueChanged(string deviceId, NAudio.CoreAudioApi.PropertyKey key)
        {
            Raise(new DeviceChangeEvent("propertyChanged", deviceId, Extra: new { property = key.ToString() }));
        }

        public void Dispose() => Stop();

    }

    public sealed record DeviceChangeEvent(string Action, string? DeviceId, object? Extra = null);
}
