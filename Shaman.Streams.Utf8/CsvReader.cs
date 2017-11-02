using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;

namespace Shaman.Runtime
{
    public class CsvReader : IDisposable
    {
        private Utf8StreamReader reader;

        public CsvReader(Utf8StreamReader reader)
        {
            this.reader = reader;
        }

        public CsvReader(string path)
            : this(new Utf8StreamReader(path))
        {
        }


        private StringSection[] arr = new StringSection[10];
        private byte[] scratchpad = new byte[100];


        public string[] ReadHeader()
        {
            var l = ReadLine();
            var arr = new string[l.Length];
            for (int i = 0; i < l.Length; i++)
            {
                arr[i] = l[i].ToString();
            }
            return arr;
        }
        public byte Separator { get; set; } = (byte)',';
        public Utf8SpanArray ReadLine()
        {
            if (reader.IsCompleted) return default(Utf8SpanArray);
            var originalLine = new Utf8SpanWithIndex(reader.ReadLine(), 0);
            if (originalLine.IsEmpty) return default(Utf8SpanArray);
            var line = originalLine;
            var scratchpadUsed = 0;
            if (scratchpad.Length < line.Length)
                scratchpad = new byte[Math.Min(line.Length, scratchpad.Length * 2)];
            Utf8Span data2 = Utf8Span.Empty;
            var num = 0;
            while (true)
            {
                var idx = line.Span.IndexOf(Separator);
                var val = idx == -1 ? line : line.Substring(0, idx);

                if (!val.IsEmpty && val.Span.CharAt(0) == (byte)'"')
                {
                    val = line.Substring(1);
                    var mustUnescapeQuotes = false;
                    int quotidx = 0;
                    while (true)
                    {
                        var k = val.Span.Bytes.Slice(quotidx).IndexOf((byte)'"');
                        if (quotidx == -1) throw new InvalidDataException();
                        quotidx += k;
                        if (quotidx + 1 < val.Length)
                        {
                            if (val.Span.CharAt(quotidx + 1) == (byte)'"')
                            {
                                quotidx += 2;
                                mustUnescapeQuotes = true;
                                continue;
                            }
                        }
                        line = val.Substring(quotidx + 2);
                        val = val.Substring(0, quotidx);
                        break;
                    }
                    if (mustUnescapeQuotes)
                    {
                        var len = 0;
                        var p = scratchpad.Slice(scratchpadUsed);
                        while (true)
                        {
                            var pos = val.Span.IndexOf((byte)'"');
                            if (pos == -1) break;
                            val.Substring(0, pos).Span.Bytes.CopyTo(p.Slice(len));
                            val = val.Substring(pos + 2);
                            len += pos;
                            p[len] = (byte)'"';
                            len++;
                        }
                        val.Span.Bytes.CopyTo(p.Slice(len));
                        len += val.Length;
                        arr[num] = new StringSection(scratchpadUsed, -len);
                        scratchpadUsed += len;
                    }
                    else
                    {
                        arr[num] = new StringSection(val.Index, val.Length);
                    }
                }
                else
                {
                    line = line.Substring(idx + 1);
                    arr[num] = new StringSection(val.Index, val.Length);
                }

                if (arr.Length == num)
                    Array.Resize(ref arr, arr.Length * 2);
                num++;
                if (idx == -1) break;
            }

            Utf8SpanArray result = default(Utf8SpanArray);
            result.count = num;
            result.data1 = originalLine.Span;
            result.data2 = new Utf8Span(scratchpad);
            result.boundaries = arr;
            return result;
        }

        public void Dispose()
        {
            scratchpad = null;
            reader.Dispose();
        }



    }



}
