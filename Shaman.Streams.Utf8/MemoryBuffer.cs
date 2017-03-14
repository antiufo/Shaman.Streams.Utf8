using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;

namespace Shaman.Runtime
{
    public class MemoryBuffer : IDisposable
    {

        public MemoryBuffer()
        {
            data = new byte[32];
        }
        private byte[] data;
        private int length;

        public void Write(ReadOnlySpan<byte> b)
        {
            if (data.Length < length + b.Length)
            {
                var newlength = Math.Max(length + b.Length, data.Length * 2);
                var newarr = new byte[newlength];
                Array.Copy(data, newarr, length);
                data = newarr;
            }
            b.CopyTo(data.Slice(length));
            length += b.Length;
        }
        public void Clear()
        {
            length = 0;
        }
        public Span<byte> Bytes => data.Slice(0, length);

        public void Dispose()
        {
            data = null;
            length = 0;
        }

        public int Length => length;
    }

    public class Scratchpad : IDisposable
    {
        public Scratchpad()
            : this(4096)
        {

        }
        public Scratchpad(int size)
        {
            buffer = new byte[size];
        }
        private byte[] buffer;
        private int used;

        public Span<byte> Use(int size)
        {
            if (used + size > buffer.Length)
            {
                var newbuffer = new byte[Math.Max(size, buffer.Length * 2)];
                buffer = newbuffer;
                used = 0;
            }

            var slice = buffer.Slice(used, size);
            used += size;
            return slice;
        }

        public Utf8String Copy(Utf8String source)
        {
            var v = Use(source.Length);
            source.CopyTo(v);
            return new Utf8String(v);
        }
        public Span<byte> Copy(ReadOnlySpan<byte> source)
        {
            var v = Use(source.Length);
            source.CopyTo(v);
            return v;
        }

        public void Reset()
        {
            used = 0;
        }

        public void Dispose()
        {
            buffer = null;
        }
    }
}
