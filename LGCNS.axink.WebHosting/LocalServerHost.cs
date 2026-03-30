using LGCNS.axink.Audio.Devices;
using LGCNS.axink.Common;
using LGCNS.axink.Common.Interfaces;
using LGCNS.axink.WebHosting.Communication;
using LGCNS.axink.WebHosting.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LGCNS.axink.WebHosting
{
    public sealed record ServerOptions(
        int Port,
        string? AllowedOrigin = null // 필요 시 SPA Origin 제한
        );

    public sealed record ServerRuntime(
        int Port,
        string Token
    );

    public sealed record ServerContext(int Port, string Token);

    public sealed class LocalServerHost : IAsyncDisposable
    {
        private WebApplication? _app;

        public IServiceProvider Services
        => _app?.Services ?? throw new InvalidOperationException("Server not started.");

        public ServerRuntime Runtime { get; private set; } = default!;

        public async Task StartAsync(ServerOptions options, Action<IServiceCollection> registerExternalServices, CancellationToken ct = default)
        {
            if (_app != null) return;

            var token = Guid.NewGuid().ToString("N");

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");

            // 내부 기본 서비스 (EventBus, AudioHub)
            builder.Services.AddSingleton<IEventBus, ChannelEventBus>();
            builder.Services.AddSingleton<IChannelAudioHub, ChannelAudioHub>();
            //builder.Services.AddSingleton<DeviceManger>();
            builder.Services.AddSingleton<WebSocketConnectionCounter>();

            // WPF에서 제공하는 서비스 주입 (DeviceService/SettingsService/AudioControlService 등)
            registerExternalServices(builder.Services);

            builder.Services.AddSingleton(new ServerContext(options.Port, token));
            builder.Services.AddCors(opt =>
            {
                opt.AddPolicy("SpaDev", p =>
                {
                    p.AllowAnyOrigin()   // "http://localhost:5173"
                     .AllowAnyHeader()
                     .AllowAnyMethod();                // WS/쿠키/인증 포함 시 필요
                });
            });

            var app = builder.Build();
            app.UseCors("SpaDev");

            app.UseWebSockets();

            // HTTP
            app.MapHealthEndpoints();
            app.MapDevicesEndpoints();
            app.MapAudioControlEndpoints();

            // WS
            app.MapAudioWebSocket();

            await app.StartAsync(ct);

            _app = app;
            Runtime = new ServerRuntime(options.Port, token);
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (_app == null) return;
            await _app.StopAsync(ct);
            await _app.DisposeAsync();
            _app = null;
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        private static bool TokenOk(HttpContext http, string token)
            => string.Equals(http.Request.Query["token"], token, StringComparison.Ordinal);
    }
}
