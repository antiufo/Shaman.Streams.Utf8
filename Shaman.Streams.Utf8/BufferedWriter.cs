using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;

namespace Shaman.Runtime
{

    public class Utf8StreamWriter : IDisposable
    {
        private Stream outstream;
        private byte[] buffer;
        private int bytesInBuffer;
        private int bufferSize;
        public Stream BaseStream => outstream;
        private bool leaveOpen;
        private const int DefaultBufferSize = 64 * 1024;
        public Utf8StreamWriter(Stream outstream)
            : this(outstream, DefaultBufferSize, false)
        {
        }

        public Utf8StreamWriter(string path)
            : this(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read))
        {
        }

        public Utf8StreamWriter(string path, int bufferSize)
            : this(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Delete | FileShare.Read), bufferSize)
        {
        }

        public Utf8StreamWriter(Stream outstream, bool leaveOpen)
            : this(outstream, DefaultBufferSize, leaveOpen)
        {
        }

        public Utf8StreamWriter(Stream outstream, int bufferSize)
            : this(outstream, bufferSize, false)
        {
        }

        public Utf8StreamWriter(Stream outstream, int bufferSize, bool leaveOpen)
        {
            this.outstream = outstream;
            this.bufferSize = bufferSize;
            this.buffer = new byte[bufferSize];
            this.leaveOpen = leaveOpen;
        }

        public void Write(Utf8String str)
        {
            Write(str.Bytes);
        }
        public void WriteClrStringLine(string str)
        {
            WriteClrString(str);
            WriteLine();
        }
        public void WriteClrString(string str)
        {
            if (str == null) return;
            if (IsAscii(str))
            {
                if (MaybeFlushBuffer(str.Length))
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        buffer[bytesInBuffer++] = (byte)str[i];
                    }
                }
                else
                {
                    var remaining = str.Slice();
                    while(remaining.Length != 0)
                    {
                        var tocopy = Math.Min(remaining.Length, bufferSize);
                        for (int i = 0; i < tocopy; i++)
                        {
                            buffer[bytesInBuffer++] = (byte)remaining[i];
                        }
                        outstream.Write(buffer, 0, tocopy);
                        bytesInBuffer = 0;
                        remaining = remaining.Slice(tocopy);
                    }
                }
            }
            else
            {
                Write(new Utf8String(str));

            }
        }

        private static bool IsAscii(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] >= 128) return false;
            }
            return true;
        }

        public void Write(byte b)
        {
            MaybeFlushBuffer(1);
            buffer[bytesInBuffer] = b;
            bytesInBuffer++;
        }

        public void Write(byte[] bytes)
        {
            Write(bytes, 0, bytes.Length);
        }

        public void Write(byte[] bytes, int start, int count)
        {
            if (MaybeFlushBuffer(count))
            {
                var slice = bytes.Slice(start, count);
                slice.CopyTo(buffer.Slice(bytesInBuffer));
                bytesInBuffer += count;
            }
            else
            {
                outstream.Write(bytes, start, count);
            }
        }


        private bool MaybeFlushBuffer(int nextWriteLength)
        {
            if (bytesInBuffer + nextWriteLength > bufferSize)
            {
                outstream.Write(buffer, 0, bytesInBuffer);
                bytesInBuffer = 0;
                return nextWriteLength <= bufferSize;
            }
            return true;
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            if (MaybeFlushBuffer(bytes.Length))
            {
                bytes.CopyTo(buffer.Slice(bytesInBuffer));
                bytesInBuffer += bytes.Length;
            }
            else
            {
                while (bytes.Length != 0)
                {
                    var tocopy = bytes.Slice(0, Math.Min(bytes.Length, bufferSize));
                    tocopy.CopyTo(buffer);
                    outstream.Write(buffer, 0, tocopy.Length);
                    bytes = bytes.Slice(tocopy.Length);
                }
            }
        }

        public void Flush()
        {
            FlushBuffer();
            outstream.Flush();
        }

        public void FlushBuffer()
        {
            if (bytesInBuffer != 0) outstream.Write(buffer, 0, bytesInBuffer);
            bytesInBuffer = 0;
        }

        public void Dispose()
        {
            FlushBuffer();
            if (!leaveOpen)
                outstream.Dispose();
        }

        public void WriteLine(Utf8String value)
        {
            Write(value);
            WriteLine();
        }

        public void WriteLine()
        {
            Write((byte)'\r');
            Write((byte)'\n');
        }


        public void Write(long int_val)
        {
            // Deal with negative numbers
            if (int_val < 0)
            {
                Write((byte)'-');
                ulong uint_val = ulong.MaxValue - ((ulong)int_val) + 1; //< This is to deal with Int32.MinValue
                Write(uint_val);
            }
            else
            {
                Write((ulong)int_val);
            }
        }


        public void Write(int int_val)
        {
            // Deal with negative numbers
            if (int_val < 0)
            {
                Write((byte)'-');
                uint uint_val = uint.MaxValue - ((uint)int_val) + 1; //< This is to deal with Int32.MinValue
                Write(uint_val);
            }
            else
            {
                Write((uint)int_val);
            }
        }

        public void Write(uint uint_val)
        {
            Write((ulong)uint_val);
        }

        public void Write(ulong uint_val)
        {

            const uint base_val = 10;
            // Calculate length of integer when written out
            var length = 0;
            ulong length_calc = uint_val;

            do
            {
                length_calc /= base_val;
                length++;
            }
            while (length_calc > 0);

            MaybeFlushBuffer(length);

            // Pad out space for writing.


            bytesInBuffer += length;
            var strpos = bytesInBuffer;

            // We're writing backwards, one character at a time.
            while (length > 0)
            {
                strpos--;

                // Lookup from static char array, to cover hex values too
                buffer[strpos] = (byte)('0' + uint_val % base_val);

                uint_val /= base_val;
                length--;
            }

        }



    }
}
