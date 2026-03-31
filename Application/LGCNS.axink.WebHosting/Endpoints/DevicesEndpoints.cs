using LGCNS.axink.Models.Devices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LGCNS.axink.WebHosting.Endpoints
{
    public static class DevicesEndpoints
    {
        public static void MapDevicesEndpoints(this WebApplication app)
        {
            app.MapGet("/api/devices", async (IDeviceService devices, CancellationToken ct) =>
            {
                var snap = await devices.GetSnapshotAsync(ct);
                return Results.Ok(snap);
            });

            app.MapPost("/api/defaultDevice", async (DefaultDeviceRequest request, IDeviceService devices, CancellationToken ct) =>
            {
                var snap = await devices.SetDefaultDevice(request.deviceType, request.deviceId, ct);

                return Results.Ok(snap);
            });
        }
    }
}
