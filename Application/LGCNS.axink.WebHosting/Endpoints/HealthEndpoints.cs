using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LGCNS.axink.WebHosting.Endpoints
{
    public static class HealthEndpoints
    {
        public static void MapHealthEndpoints(this WebApplication app)
        {
            app.MapGet("/api/health", () => Results.Ok(new { ok = true }));
        }
    }
}
