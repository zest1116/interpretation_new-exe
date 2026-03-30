using LGCNS.axink.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting.Endpoints
{
    public sealed record StartSttRequest(string DeviceType, string AccessToken, int RoomId, string SourceLang, string TargetLang, string Platform, string RoomType);


    public static class AudioControlEndpoints
    {
        public static void MapAudioControlEndpoints(this WebApplication app)
        {
            app.MapPost("/api/stt/start", async (StartSttRequest req, IWebAudioCaptureService audio, CancellationToken ct) =>
            {
                try
                {
                    await audio.StartAsync(req.DeviceType, req.AccessToken, req.RoomId, req.SourceLang, req.TargetLang, req.Platform, req.RoomType, ct);

                    return Results.Ok(new { ok = true });
                }
                catch (Exception)
                {
                    throw;
                }
            });

            app.MapPost("/api/stt/stop", async (IWebAudioCaptureService audio, CancellationToken ct) =>
            {
                try
                {
                    await audio.StopAsync(ct);


                    return Results.Ok(new { ok = true });
                }
                catch (Exception)
                {
                    throw;
                }
            });

            app.MapPost("/api/stt/capture", async (IWebAudioCaptureService audio, CancellationToken ct) =>
            {
                try
                {
                    await audio.StartAsync("all", "", 0, "", "", "", "", ct);


                    return Results.Ok(new { ok = true });
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }
    }
}
