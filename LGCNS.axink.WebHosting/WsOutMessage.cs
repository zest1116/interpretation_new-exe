using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting
{
    public enum WsPayloadKind { Binary, Text }

    public readonly record struct WsOutMessage(
        WsPayloadKind Kind,
        ReadOnlyMemory<byte> Binary,
        string? Text
    )
    {
        public static WsOutMessage FromBinary(ReadOnlyMemory<byte> data)
            => new(WsPayloadKind.Binary, data, null);

        public static WsOutMessage FromText(string text)
            => new(WsPayloadKind.Text, ReadOnlyMemory<byte>.Empty, text);
    }
}
