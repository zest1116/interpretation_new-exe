using LGCNS.axink.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio
{
    public sealed class PcmFramePump
    {
        // 16kHz * 20ms = 320 samples, 16-bit mono => 320 * 2 = 640 bytes
        public const int BytesPerFrame = 640;

        private readonly object _gate = new();

        private byte[] _micBuf = new byte[BytesPerFrame * 8];
        private int _micCount;

        private byte[] _spkBuf = new byte[BytesPerFrame * 8];
        private int _spkCount;

        private readonly Action<AudioSourceType, ReadOnlyMemory<byte>> _onFrame;

        public PcmFramePump(Action<AudioSourceType, ReadOnlyMemory<byte>> onFrame)
            => _onFrame = onFrame;

        public void Push(AudioSourceType ch, byte[] chunk)
        {
            if (chunk == null || chunk.Length == 0) return;

            lock (_gate)
            {
                if (ch == AudioSourceType.Mic)
                    PushCore(ref _micBuf, ref _micCount, chunk, AudioSourceType.Mic);
                else
                    PushCore(ref _spkBuf, ref _spkCount, chunk, AudioSourceType.Spk);
            }
        }

        public void Reset(AudioSourceType ch)
        {
            lock (_gate)
            {
                if (ch == AudioSourceType.Mic) _micCount = 0;
                else _spkCount = 0;
            }
        }

        private void PushCore(ref byte[] buffer, ref int count, byte[] chunk, AudioSourceType ch)
        {
            EnsureCapacity(ref buffer, count + chunk.Length);
            Buffer.BlockCopy(chunk, 0, buffer, count, chunk.Length);
            count += chunk.Length;

            while (count >= BytesPerFrame)
            {
                var frame = new byte[BytesPerFrame];
                Buffer.BlockCopy(buffer, 0, frame, 0, BytesPerFrame);

                var remaining = count - BytesPerFrame;
                if (remaining > 0)
                    Buffer.BlockCopy(buffer, BytesPerFrame, buffer, 0, remaining);
                count = remaining;

                _onFrame(ch, frame);
            }
        }

        private static void EnsureCapacity(ref byte[] buffer, int needed)
        {
            if (buffer.Length >= needed) return;
            var newSize = buffer.Length;
            while (newSize < needed) newSize *= 2;

            var newBuf = new byte[newSize];
            Buffer.BlockCopy(buffer, 0, newBuf, 0, Math.Min(buffer.Length, newBuf.Length));
            buffer = newBuf;
        }

    }
}
