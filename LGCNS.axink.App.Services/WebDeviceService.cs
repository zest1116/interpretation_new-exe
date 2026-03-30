using LGCNS.axink.Audio.Devices;
using LGCNS.axink.Medels.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App.Services
{
    public sealed class WebDeviceService : IDeviceService, IDisposable
    {
        private readonly DeviceManger _deviceManager;

        public WebDeviceService()
        {
            _deviceManager = new DeviceManger();
        }

        public Task<DeviceSnapshotDto> GetSnapshotAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var allInputs = _deviceManager.GetInputDevices() ?? new List<AudioDeviceInfo>();
                var allOutputs = _deviceManager.GetOutputDevices() ?? new List<AudioDeviceInfo>();

                var inputs = allInputs
                .Select(d => new AudioDeviceDto(
                    Id: d.Id,
                    Name: d.Name,
                    IsDefault: d.isDefault
                ))
                .ToList();

                var outputs = allOutputs
                    .Select(d => new AudioDeviceDto(
                        Id: d.Id,
                        Name: d.Name,
                        IsDefault: d.isDefault
                    ))
                    .ToList();

                var currentInputId = allInputs.FirstOrDefault(d => d.isDefault)?.Id;
                var currentOutputId = allOutputs.FirstOrDefault(d => d.isDefault)?.Id;

                var snapshot = new DeviceSnapshotDto(
                    Inputs: inputs,
                    Outputs: outputs,
                    CurrentInputId: currentInputId,
                    CurrentOutputId: currentOutputId
                );

                return Task.FromResult(snapshot);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public Task<DeviceSnapshotDto> SetDefaultDevice(string deviceType, string deviceId, CancellationToken ct)
        {
            try
            {
                if (deviceType == "input")
                {
                    DeviceManger.TrySetDefaultOnly(deviceId, out string? err);
                }
                else if (deviceType == "output")
                {
                    DeviceManger.TrySetDefaultOnly(deviceId, out string? err);
                }
                return GetSnapshotAsync(ct);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Dispose()
        {

        }
    }
}
