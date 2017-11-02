using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;

namespace Shaman.Runtime
{
    public class SqlDumpReader : IDisposable
    {

        private readonly static Utf8String INSERT = (Utf8String)"INSERT";
        private readonly static Utf8String[] separators = new[] {
            (Utf8String)"\n",
            (Utf8String)") VALUES (",
            (Utf8String)"` VALUES (",
            (Utf8String)"),(",
            (Utf8String)");"
        };

        private Utf8StreamReader reader;


        public SqlDumpReader(Stream utf8Stream)
            : this(new Utf8StreamReader(utf8Stream))
        {

        }

        public SqlDumpReader(Utf8StreamReader reader)
        {
            this.reader = reader;
            this.scratchpad = new Scratchpad(8192);
        }
        private MemoryBuffer ms;
        private Scratchpad scratchpad;


        public bool IsCompleted => reader.IsCompleted;

        public void Dispose()
        {
            ms?.Dispose();
            reader.Dispose();
        }

        bool isInInsert;

        private StringSection[] fields = new StringSection[32];
        private Utf8String currentTableName;
        public Utf8String CurrentTableName => currentTableName;

        public Utf8SpanArray TryReadRow(out int readfields)
        {


            while (!reader.IsCompleted)
            {

                var line = reader.ReadTo(separators, out var s);
                if (s == 1 || s == 2) // )|` VALUES (
                {
                    isInInsert = line.StartsWith(INSERT);
                    currentTableName = null;
                    if (isInInsert)
                    {
                        if (line.TrySubstringFrom((byte)'`', out var p))
                        {
                            p = p.Substring(1);
                            if (p.TrySubstringTo((byte)'`', out var k))
                                currentTableName = new Utf8String(k);
                            else
                                currentTableName = new Utf8String(p);

                        }
                    }
                    continue;

                }
                if (s < 3) continue;
                if (!isInInsert) continue;


                scratchpad.Reset();

                int endpos;

                if (ms != null && ms.Length != 0)
                {
                    ms.Write(line);
                    line = new Utf8Span(ms.Bytes);
                }

                readfields = TryReadSqlFields(line, out endpos);

                if (readfields == -1)
                {
                    if (ms == null)
                    {
                        ms = new MemoryBuffer();
                    }
                    if (ms.Length == 0)
                        ms.Write(line);
                    ms.Write(separators[s]);
                    continue;
                }
                ms?.Clear();
                Utf8SpanArray r = default(Utf8SpanArray);
                r.boundaries = fields;
                r.count = readfields;
                r.data1 = line;
            }
            readfields = 0;
            return default(Utf8SpanArray);
        }






        private int TryReadSqlFields(Utf8Span input, out int endposition)
        {
            var idx = 0;
            endposition = 0;
            while (true)
            {
                if (input.IsEmpty) break;

                int end;
                if (input.CharAt(0) == (byte)'\'')
                {
                    end = EndOfQuotedString(input);
                    if (end == -1) return -1;
                }
                else
                {
                    end = input.IndexOf((byte)',');
                    if (end == -1) end = input.Length();
                }

                
                if (idx == fields.Length)
                {
                    var newsize = fields.Length * 2;
                    var newarr = new StringSection[newsize];

                    fields.AsSpan().CopyTo(newarr);
                    fields = newarr;
                }

                fields[idx] = new StringSection(0, end);
                idx++;
                endposition += end + 1;
                if (end == input.Length()) break;
                input = input.Substring(end + 1);

            }
            return idx;
        }

        private static int EndOfQuotedString(Utf8Span input)
        {
            var pos = 1;
            while (true)
            {
                var end = input.Substring(pos).IndexOf((byte)'\'');
                if (end == -1) return -1;
                end += pos;

                var precedingBackslashes = 0;
                for (int i = end - 1; i >= 1; i--)
                {
                    if (input.CharAt(i) == (byte)'\\')
                    {
                        precedingBackslashes++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (precedingBackslashes % 2 == 0) return end + 1;
                pos = end + 1;
            }
        }

        public Scratchpad Scratchpad => scratchpad;


        public Utf8Span UnescapeSql(Utf8Span str)
        {
            if (str.CharAt(0) == (byte)'\'')
            {
                var slash = str.IndexOf((byte)'\\');
                if (slash == -1) return str.Substring(1, str.Length() - 2);
                else
                {
                    var output = scratchpad.Use(str.Length() - 2);
                    str.Substring(1, slash - 1).Bytes.CopyTo(output);
                    var len = slash - 1;
                    for (int i = slash; i < str.Length() - 1; i++)
                    {
                        var ch = str.CharAt(i);
                        if (ch == (byte)'\\')
                        {
                            i++;
                            var next = str.CharAt(i);
                            ch = UnescapeSqlSpecialChar(next);
                            output[len++] = ch;
                        }
                        else
                        {
                            output[len++] = ch;
                        }
                    }
                    return new Utf8Span(output.Slice(0, len));
                }
            }
            else return str;
        }

        private byte UnescapeSqlSpecialChar(byte next)
        {
            // https://dev.mysql.com/doc/refman/8.0/en/string-literals.html

            if (next == (byte)'\\' || next == (byte)'\'' || next == (byte)'"') return next;
            if (next == (byte)'r') return (byte)'\r';
            if (next == (byte)'n') return (byte)'\n';
            if (next == (byte)'t') return (byte)'\t';
            if (next == (byte)'0') return (byte)'\0';
            if (next == (byte)'b') return (byte)'\b';
            if (next == (byte)'t') return (byte)'\t';
            if (next == (byte)'Z') return (byte)26;
            if (next == (byte)'%') return (byte)'%';
            if (next == (byte)'_') return (byte)'_';
            else throw new InvalidDataException("Unknown escape sequence: \\" + (char)next);
        }
    }
}
