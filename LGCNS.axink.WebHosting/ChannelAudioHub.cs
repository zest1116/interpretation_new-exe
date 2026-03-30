using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting
{
    public interface IChannelAudioHub
    {

        void Publish(WsOutMessage msg);

        IAsyncEnumerable<WsOutMessage> Subscribe(CancellationToken ct);
    }

    public sealed class ChannelAudioHub : IChannelAudioHub
    {
        private readonly Channel<WsOutMessage> _audio = Channel.CreateUnbounded<WsOutMessage>();

        public void Publish(WsOutMessage msg) => _audio.Writer.TryWrite(msg);

        public async IAsyncEnumerable<WsOutMessage> Subscribe(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            while (await _audio.Reader.WaitToReadAsync(ct))
                while (_audio.Reader.TryRead(out var msg))
                    yield return msg;
        }
    }
}
