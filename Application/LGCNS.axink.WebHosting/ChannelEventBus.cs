using LGCNS.axink.Common.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace LGCNS.axink.WebHosting
{
    public sealed class ChannelEventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Guid, Channel<string>> _subs = new();

        // 이벤트는 유실돼도 되는 성격이 많으므로 bounded + DropOldest 권장
        private static Channel<string> NewSubChannel() =>
            Channel.CreateBounded<string>(new BoundedChannelOptions(200)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        // ✅ 기존 유지
        public void Publish(string jsonEvent)
        {
            foreach (var kv in _subs)
                kv.Value.Writer.TryWrite(jsonEvent);
        }

        // ✅ 신규: typed publish
        public void Publish<TPayload>(string type, TPayload payload)
        {
            var msg = new
            {
                type,
                payload,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Newtonsoft가 아니라 System.Text.Json 사용 (가벼움/속도)
            var json = JsonSerializer.Serialize(msg);
            Publish(json);
        }

        // ✅ 기존 유지
        public async IAsyncEnumerable<string> Subscribe(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var ch = NewSubChannel();
            _subs.TryAdd(id, ch);

            try
            {
                while (await ch.Reader.WaitToReadAsync(ct))
                {
                    while (ch.Reader.TryRead(out var msg))
                        yield return msg;
                }
            }
            finally
            {
                _subs.TryRemove(id, out _);
                ch.Writer.TryComplete();
            }
        }

        // ✅ 신규: 이벤트 파싱 스트림
        public async IAsyncEnumerable<BusEvent> SubscribeEvents(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var raw in Subscribe(ct))
            {
                if (TryParse(raw, out var ev))
                    yield return ev;
            }
        }

        // ✅ 신규: type 필터링 스트림
        public async IAsyncEnumerable<BusEvent> SubscribeEvents(
            string type,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var ev in SubscribeEvents(ct))
            {
                if (string.Equals(ev.Type, type, StringComparison.OrdinalIgnoreCase))
                    yield return ev;
            }
        }

        private static bool TryParse(string raw, out BusEvent ev)
        {
            ev = default;

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeEl)) return false;

                var type = typeEl.GetString() ?? "";
                long ts = 0;

                if (root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number)
                    ts = tsEl.GetInt64();

                // payload는 그대로 JSON 문자열로 전달
                string payloadJson = "{}";
                if (root.TryGetProperty("payload", out var payloadEl))
                    payloadJson = payloadEl.GetRawText();

                ev = new BusEvent(type, payloadJson, ts, raw);
                return true;
            }
            catch
            {
                // 깨진 JSON은 무시
                return false;
            }
        }
    }
}
