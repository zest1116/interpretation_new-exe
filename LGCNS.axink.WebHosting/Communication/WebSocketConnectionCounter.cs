using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.WebHosting.Communication
{
    public sealed class WebSocketConnectionCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void OnConnected() => Interlocked.Increment(ref _count);

        public void OnDisconnected() => Interlocked.Decrement(ref _count);
    }
}
