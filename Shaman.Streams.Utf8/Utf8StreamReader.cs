using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Utf8;

namespace Shaman.Runtime
{
    public class Utf8StreamReader : Stream
    {
        private int position;

        public Utf8StreamReader(Stream stream)
            : this(stream, DefaultBufferSize, false)
        {

        }
        private const int DefaultBufferSize = 16 * 1024;


        public Utf8StreamReader(string path)
            : this(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
        {
        }

        public Utf8StreamReader(string path, int initialBufferSize)
            : this(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read), initialBufferSize)
        {
        }

        public Utf8StreamReader(Stream stream, bool leaveOpen)
            :this(stream, DefaultBufferSize, leaveOpen)
        {
        }
        public Utf8StreamReader(Stream stream, int initialBufferSize)
            :this(stream, initialBufferSize, false)
        {
        }
        public Utf8StreamReader(Stream stream, int initialBufferSize, bool leaveOpen)
        {
            this.stream = stream;
            this.bufferHalfSize = initialBufferSize;
            this.bufferFullSize = bufferHalfSize * 2;
            this.buffer = new byte[bufferFullSize];

            this.bufferStart = bufferHalfSize;
            this.leaveOpen = leaveOpen;
            
            FillBuffer();
        }
        private bool leaveOpen;
        private int bufferHalfSize;
        private int bufferFullSize;
        private Stream stream;
        private byte[] buffer;
        private int bufferStart;
        private bool isEndOfStream;
        private bool isCompleted;
        public bool IsCompleted => isCompleted;
        private int bufferDataLength;

        private readonly static Utf8String[] LF = new Utf8String[] { (Utf8String)"\n" };
        public Utf8Span ReadLine()
        {
            int dummy;
            var line = ReadTo(LF, out dummy);
            if (!line.IsEmpty && line.CharAt(line.Length() - 1) == 13)
                line = line.Substring(0, line.Length() - 1);
            return line;
        }

        public Utf8Span ReadTo(Utf8String separator)
        {
            singleStringArray[0] = separator;
            return ReadTo(singleStringArray, out var dummy);
        }

        private Utf8String[] singleStringArray = new Utf8String[1] { Utf8String.Empty };
        private bool hasPendingEmptyString;

        public ArraySegment<byte> Read(int maxSize)
        {
            if (isCompleted) return new ArraySegment<byte>(buffer, 0, 0);
            if (bufferDataLength != bufferStart) 
            {
                var len = Math.Min(maxSize, bufferDataLength - bufferStart);
                var r = new ArraySegment<byte>(buffer, bufferStart, len);
                bufferStart += len;
                position += len;
                return r;
            }
            else
            {
                FillBuffer();
                bufferStart = bufferHalfSize;
                var len = Math.Min(maxSize, bufferDataLength - bufferStart);
                var result = new ArraySegment<byte>(buffer, bufferStart, len);
                bufferStart += len;
                position += len;
                return result;
            }
        }

        public override int Read(byte[] b, int offset, int count)
        {
            if (isCompleted) return 0;
            if (bufferDataLength != bufferStart) 
            {
                var len = Math.Min(count, bufferDataLength - bufferStart);
                Array.Copy(buffer, bufferStart, b, offset, len);
                bufferStart += len;
                position += len;
                return len;
            }
            else
            {
                var r = stream.Read(b, offset, count);
                if(r == 0) isCompleted = true;
                position += r;
                return r;
            }
        }

        override public int ReadByte()
        {
            if (isCompleted) return -1;
            if (bufferDataLength != bufferStart) 
            {
                position++;
                return buffer[bufferStart++];
            }
            else
            {
                var r = stream.ReadByte();
                if (r == -1) isCompleted = true;
                position++;
                return r;
            }
        }

        public ReadOnlySpan<byte> RemainingBufferedData => buffer.Slice(bufferStart, bufferDataLength - bufferStart);

        public Utf8Span ReadTo(Utf8String[] separator, out int foundSeparator)
        {
            tryagain:
            if (isCompleted) throw new EndOfStreamException();
            if (hasPendingEmptyString)
            {
                foundSeparator = -1;
                isCompleted = true;
                return Utf8Span.Empty;
            }
            var bufferview = new Utf8Span(buffer);

            var haystack = new Utf8Span(buffer.Slice(bufferStart, bufferDataLength - bufferStart));
            var lf = FindSeparator(haystack, separator, out foundSeparator);
            if (lf != -1)
            {
                var u = new Utf8Span(buffer.Slice(bufferStart, lf));
                var delta = lf + separator[foundSeparator].Length();
                position += delta;
                bufferStart += delta;
                hasPendingEmptyString = isEndOfStream && bufferStart == bufferDataLength;
                
                return u;
            }
            else
            {
                if (isEndOfStream)
                {
                    isCompleted = true;
                    foundSeparator = -1;
                    var delta = bufferDataLength - bufferStart;
                    position += delta;
                    return new Utf8Span(buffer.Slice(bufferStart, delta));
                }
                var tocopy = bufferFullSize - bufferStart;
                Array.Copy(buffer, bufferFullSize - tocopy, buffer, bufferHalfSize - tocopy, tocopy);
                FillBuffer();

                var start = bufferHalfSize - tocopy;
                haystack = new Utf8Span(buffer.Slice(start, bufferDataLength - start));
                lf = FindSeparator(haystack, separator, out foundSeparator);

                if (lf != -1)
                {
                    var delta = lf + separator[foundSeparator].Length();
                    position += delta;
                    bufferStart = start + delta;
                    var u = new Utf8Span(buffer.Slice(start, lf));
                    hasPendingEmptyString = isEndOfStream && bufferStart == bufferDataLength;
                    
                    return u;
                }
                else
                {
                    var u = new Utf8Span(buffer.Slice(start, bufferDataLength - start));
                    if (isEndOfStream)
                    {
                        isCompleted = true;
                        position += u.Length();
                        return u;
                    }
                    else
                    {

                        var newbuffer = new byte[bufferFullSize * 2];
                        var datalength = bufferDataLength - bufferHalfSize + tocopy;
                        Array.Copy(buffer, start, newbuffer, bufferFullSize, datalength);
                        var newview = new Utf8Span(newbuffer);
                        bufferDataLength = bufferFullSize + datalength;
                        bufferFullSize *= 2;
                        bufferHalfSize *= 2;
                        this.buffer = newbuffer;

                        FillBuffer(bufferDataLength);
                        
                        bufferStart = bufferHalfSize;
                        goto tryagain;
                    }
                }

            }

        }
/*
        internal static bool IsWhiteSpace(Utf8Span str)
        {
            str.Trim()
            if (str.Length == 0) return true;
            for (int i = 0; i < str.Length; i++)
            {
                if (Utf8Span.IsWhiteSpace(str[i])) return false;
            }
            return true;
        }
        */
        private unsafe int FindSeparator(Utf8Span haystack, Utf8String[] separator, out int foundSeparator)
        {
            foundSeparator = 0;
            if (separator.Length == 1)
            {
                var sep = separator[0];
                if (sep.Length() == 1) return haystack.IndexOf(sep.CharAt(0));
                else return haystack.IndexOf(sep);
            }
            else
            {




                byte* charMap = stackalloc byte[32];
                InitializeProbabilisticMap(charMap, separator);


                //ref byte pCh = ref haystack.Bytes.DangerousGetPinnableReference();
                //var charthere = haystack.Bytes[0];
                //var fond = pCh;

                for (int i = 0; i < haystack.Length(); i++)
                {
                    //byte thisChar = Unsafe.Add<byte>(ref pCh, i);
                    byte thisChar = haystack.CharAt(i);

                    if (ProbablyContains(charMap, thisChar))
                    {
                        var substr = new Utf8Span(haystack.Bytes.Slice(i));
                        for (int j = 0; j < separator.Length; j++)
                        {
                            if (substr.StartsWith(separator[j]))
                            {
                                foundSeparator = j;
                                return i;
                            }
                        }
                        //if (ArrayContains(thisChar, anyOf) >= 0)
                            //return i;
                    }
                }




                /*
                var maxlen = 0;
                for (int i = 0; i < separator.Length; i++)
                {
                    maxlen = Math.Max(maxlen, separator[i].Length);
                }


                int expected = -1;
                for (int i = 0; i < haystack.Length; i++)
                {
                    var substr = new Utf8Span(haystack.Bytes.Slice(i));
                    for (int j = 0; j < separator.Length; j++)
                    {
                        if (substr.StartsWith(separator[j]))
                        {
                            expected = i;
                            break;
                        }
                    }
                    if (expected != -1) break;
                }


                var min = int.MaxValue;
                var minsep = -1;
                const int STEP_SIZE = 100;
                for (int j = 0; j < haystack.Length; j += STEP_SIZE)
                {

                    var subhaystack = haystack.Substring(j, Math.Min(STEP_SIZE + maxlen, haystack.Length - j));
                    for (int i = 0; i < separator.Length; i++)
                    {
                        var pos = subhaystack.IndexOf(separator[i]);
                        if (pos != -1)
                        {
                            if (pos < min)
                            {
                                min = pos;
                                minsep = i;
                                subhaystack = subhaystack.Substring(0, Math.Min(min + maxlen, subhaystack.Length));
                            }
                        }
                    }

//#if DEBUG
//                    if (minsep != -1)
//                    {
//                        foundSeparator = minsep;
//                        var v = j + min;
//                        Debug.Assert(expected == v);
//                        return v;
//                    }
//#endif
                }

    */





                return -1;
            }
        }

        private void FillBuffer()
        {
            FillBuffer(bufferHalfSize);
        }
        private void FillBuffer(int t)
        {
            if (isEndOfStream)
            {
                bufferDataLength = t;
                return;
            }
            while (t < bufferFullSize)
            {
                var read = stream.Read(buffer, t, bufferFullSize - t);
                if (read == 0)
                {
                    isEndOfStream = true;
                    break;
                }
                t += read;
            }
            bufferDataLength = t;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
                stream.Dispose();
        }





        private static unsafe void InitializeProbabilisticMap(byte* charMap, Utf8String[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; ++i)
            {
                byte c = anyOf[i].CharAt(0);

                var pos = c / 8;
                var offset = c % 8;

                var v = charMap[pos];
                v |= (byte)(1 << offset);
                charMap[pos] = v;
            }
        }


        private static unsafe void InitializeProbabilisticMap(byte* charMap, byte[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; ++i)
            {
                byte c = anyOf[i];

                var pos = c / 8;
                var offset = c % 8;

                var v = charMap[pos];
                v |= (byte)(1 << offset);
                charMap[pos] = v;
            }
        }


        public static unsafe int IndexOfAny(Utf8Span str, byte[] anyOf)
        {
            byte* charMap = stackalloc byte[32];
            InitializeProbabilisticMap(charMap, anyOf);


            ref byte pCh = ref str.Bytes.DangerousGetPinnableReference();

            for (int i = 0; i < str.Length(); i++)
            {
                byte thisChar = Unsafe.Add<byte>(ref pCh, i);

                if (ProbablyContains(charMap, thisChar))
                    if (ArrayContains(thisChar, anyOf) >= 0)
                        return i;
            }

            return -1;
        }

        private static unsafe bool ProbablyContains(byte* charMap, byte searchValue)
        {

            var pos = searchValue / 8;
            var offset = searchValue % 8;

            var v = charMap[pos];
            return (v & (1 << offset)) != 0;
        }

        
        private static int ArrayContains(byte searchChar, byte[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; i++)
            {
                if (anyOf[i] == searchChar)
                    return i;
            }
            return -1;
        }
        /*
        private static int ArrayContains(byte searchChar, Utf8Span[] anyOf)
        {
            for (int i = 0; i < anyOf.Length; i++)
            {
                if (anyOf[i][0] == searchChar)
                    return i;
            }
            return -1;
        }
        */



        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => stream.Length;

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }


    }
}
