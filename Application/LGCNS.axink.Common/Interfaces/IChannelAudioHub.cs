using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common.Interfaces
{
    public interface IChannelAudioHub
    {

        void Publish(WsOutMessage msg);

        IAsyncEnumerable<WsOutMessage> Subscribe(CancellationToken ct);
    }
}
