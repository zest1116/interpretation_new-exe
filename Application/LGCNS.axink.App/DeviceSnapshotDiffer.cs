using LGCNS.axink.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.App
{
    public static class DeviceSnapshotDiffer
    {
        public static DeviceChangeSummary Diff(
            DeviceSnapshotDto? previous,
            DeviceSnapshotDto current)
        {
            var prevInputs = previous?.Inputs ?? [];
            var prevOutputs = previous?.Outputs ?? [];

            var prevInputIds = prevInputs.Select(d => d.Id).ToHashSet();
            var prevOutputIds = prevOutputs.Select(d => d.Id).ToHashSet();
            var currInputIds = current.Inputs.Select(d => d.Id).ToHashSet();
            var currOutputIds = current.Outputs.Select(d => d.Id).ToHashSet();

            var addedInputs = current.Inputs.Where(d => !prevInputIds.Contains(d.Id)).ToList();
            var removedInputs = prevInputs.Where(d => !currInputIds.Contains(d.Id)).ToList();

            var addedOutputs = current.Outputs.Where(d => !prevOutputIds.Contains(d.Id)).ToList();
            var removedOutputs = prevOutputs.Where(d => !currOutputIds.Contains(d.Id)).ToList();

            var defaultInputChange =
                previous?.CurrentInputId != current.CurrentInputId
                    ? new DefaultDeviceChange(previous?.CurrentInputId, current.CurrentInputId)
                    : null;

            var defaultOutputChange =
                previous?.CurrentOutputId != current.CurrentOutputId
                    ? new DefaultDeviceChange(previous?.CurrentOutputId, current.CurrentOutputId)
                    : null;

            return new DeviceChangeSummary(
                addedInputs, addedOutputs,
                removedInputs, removedOutputs,
                defaultInputChange, defaultOutputChange
            );
        }
    }
}
