using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting.Communication
{
    /// <summary>
    /// WebSocket 통신을 담당하는 서비스 클래스
    /// </summary>
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private bool _disposed;
        private bool _isConnected;
        private bool _initMessageSent;

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnError;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        /// <summary>
        /// WebSocket 서버에 연결
        /// </summary>
        public async Task ConnectAsync(string serverUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _initMessageSent = false;

                await _webSocket.ConnectAsync(new Uri(serverUrl), cancellationToken);
                _isConnected = true;

                OnConnected?.Invoke();

                // 수신 태스크 시작
                _receiveCts = new CancellationTokenSource();
                _ = ReceiveLoopAsync(_receiveCts.Token);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnError?.Invoke($"연결 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 초기 설정 메시지 전송 (JSON 텍스트)
        /// </summary>
        public async Task SendInitMessageAsync(string initMessage, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                OnError?.Invoke("WebSocket이 연결되지 않았습니다.");
                return;
            }

            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(initMessage);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                _initMessageSent = true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"초기 메시지 전송 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 오디오 데이터 전송 (바이너리)
        /// </summary>
        public async Task SendAudioDataAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                return;
            }

            if (!_initMessageSent)
            {
                OnError?.Invoke("초기 설정 메시지가 전송되지 않았습니다.");
                return;
            }

            try
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(audioData),
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"오디오 데이터 전송 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 메시지 수신 루프
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];
            StringBuilder messageBuilder = new StringBuilder();

            try
            {
                while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "서버에서 연결 종료",
                            CancellationToken.None);
                        _isConnected = false;
                        OnDisconnected?.Invoke();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(chunk);

                        if (result.EndOfMessage)
                        {
                            string message = messageBuilder.ToString();
                            messageBuilder.Clear();
                            OnMessageReceived?.Invoke(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (WebSocketException ex)
            {
                OnError?.Invoke($"WebSocket 오류: {ex.Message}");
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"수신 오류: {ex.Message}");
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// 연결 종료
        /// </summary>
        public async Task DisconnectAsync()
        {
            _receiveCts?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "클라이언트에서 연결 종료",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"연결 종료 중 오류: {ex.Message}");
                }
            }

            _isConnected = false;
            _initMessageSent = false;
            OnDisconnected?.Invoke();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _webSocket?.Dispose();
            }
        }
    }
}
