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


        private Utf8String[] arr = new Utf8String[10];
        private Scratchpad scratchpad = new Scratchpad();


        public string[] ReadHeader()
        {
            return ReadLine().Select(x => x.ToString()).ToArray();
        }
        public byte Separator { get; set; } = (byte)',';
        public Utf8String[] ReadLine()
        {
            if (reader.IsCompleted) return null;
            var line = reader.ReadLine();
            if (line.Length == 0) return null;
            scratchpad.Reset();

            var num = 0;
            while (true)
            {
                var idx = line.IndexOf(Separator);
                var val = idx == -1 ? line : line.Substring(0, idx);

                if (val.Length > 0 && val[0] == (byte)'"')
                {
                    val = line.Substring(1);
                    var mustUnescapeQuotes = false;
                    int quotidx = 0;
                    while (true)
                    {
                        quotidx = val.IndexOf((byte)'"', quotidx);
                        if (quotidx == -1) throw new InvalidDataException();
                        if (quotidx + 1 < val.Length)
                        {
                            if (val[quotidx + 1] == (byte)'"')
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
                        var p = scratchpad.Use(val.Length);
                        var len = 0;
                        var view = new Utf8String(p);
                        while (true)
                        {
                            var pos = val.IndexOf((byte)'"');
                            if (pos == -1) break;
                            val.Substring(0, pos).CopyTo(p.Slice(len));
                            val = val.Substring(pos + 2);
                            len += pos;
                            p[len] = (byte)'"';
                            len++;
                        }
                        val.CopyTo(p.Slice(len));
                        len += val.Length;
                        val = new Utf8String(p.Slice(0, len));
                    }
                }
                else
                {
                    line = line.Substring(idx + 1);
                }

                if (arr.Length == num)
                    Array.Resize(ref arr, arr.Length * 2);
                arr[num] = val;
                num++;
                if (idx == -1) break;
            }

            if (arr.Length != num)
            {
                var b = new Utf8String[num];
                arr.Slice(0, num).CopyTo(b);
                arr = b;
            }

            return arr;
        }

        public void Dispose()
        {
            scratchpad.Dispose();
            reader.Dispose();
        }



    }



}
