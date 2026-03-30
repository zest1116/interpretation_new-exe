using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting
{
    public interface IEventBus
    {
        void Publish(string jsonEvent);
        IAsyncEnumerable<string> Subscribe(CancellationToken ct);

        // ✅ 신규: 타입/페이로드 기반 publish
        void Publish<TPayload>(string type, TPayload payload);

        // ✅ 신규: JSON을 BusEvent로 파싱해서 구독
        IAsyncEnumerable<BusEvent> SubscribeEvents(CancellationToken ct);

        // ✅ 신규: 특정 type만 필터링해서 구독
        IAsyncEnumerable<BusEvent> SubscribeEvents(string type, CancellationToken ct);
    }

    public readonly record struct BusEvent(string Type, string PayloadJson, long Ts, string RawJson);
}
