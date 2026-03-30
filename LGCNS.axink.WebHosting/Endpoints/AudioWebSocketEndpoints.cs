using LGCNS.axink.Common.Interfaces;
using LGCNS.axink.Medels.Messages;
using LGCNS.axink.WebHosting.Communication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting.Endpoints
{
    public static class AudioWebSocketEndpoints
    {
        public static void MapAudioWebSocket(this WebApplication app)
        {
            app.Map("/ws/audio", async (HttpContext ctx, IChannelAudioHub hub, IWebAudioCaptureService audio, WebSocketConnectionCounter counter) =>
            {

                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }

                var ct = ctx.RequestAborted;
                var ws = await ctx.WebSockets.AcceptWebSocketAsync();

                counter.OnConnected();

                try
                {
                    // 1) 클라이언트가 Close/끊김 되는지 감시
                    var receiveTask = ReceiveUntilCloseAsync(ws, audio, ct);

                    // 2) 기존 로직: hub에서 오는 메시지를 Push
                    var sendTask = SendFromHubAsync(ws, hub, ct);

                    // 둘 중 하나라도 끝나면 종료 처리로 넘어감
                    await Task.WhenAny(receiveTask, sendTask);
                }
                finally
                {
                    counter.OnDisconnected();

                    await audio.StopAsync(ct);
                    // 소켓 닫기/정리 (예외는 무시)
                    try
                    {
                        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        {
                            await ws.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Closing",
                                CancellationToken.None);
                        }
                    }
                    catch { }

                    ws.Dispose();
                }
            });
        }

        private static async Task ReceiveUntilCloseAsync(WebSocket ws, IWebAudioCaptureService audio, CancellationToken ct)
        {
            var buffer = new byte[4 * 1024];

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(buffer, ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var message = JsonConvert.DeserializeObject<SocketMessage>(text);
                        if (message != null && message.Type.ToLower() == "init")
                        {
                            await audio.StartAsync(message.DeviceType, string.Empty, -1, string.Empty, string.Empty, string.Empty, string.Empty, ct);
                        }
                    }


                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 네트워크 단절/브라우저 강제 종료 등
                    break;
                }
            }
        }

        private static async Task SendFromHubAsync(WebSocket ws, IChannelAudioHub hub, CancellationToken ct)
        {
            await foreach (var msg in hub.Subscribe(ct))
            {
                if (ct.IsCancellationRequested) break;
                if (ws.State != WebSocketState.Open) break;

                if (msg.Kind == WsPayloadKind.Binary)
                {
                    await ws.SendAsync(
                        msg.Binary,
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        cancellationToken: ct
                    );
                }
                else
                {
                    var bytes = Encoding.UTF8.GetBytes(msg.Text ?? "");
                    await ws.SendAsync(
                        bytes,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: ct
                    );
                }
            }
        }

    }
}
