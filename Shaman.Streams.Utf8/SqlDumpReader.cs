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
        


        private readonly static Utf8String[] separators = new[] {
            (Utf8String)"\n",
            (Utf8String)";",
            (Utf8String)"),"
        };

        const int SEPARATOR_NEWLINE = 0;
        const int SEPARATOR_SEMICOLON = 1;
        const int SEPARATOR_CLOSE_AND_COMMA = 2;

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
        private MemoryBuffer ms = new MemoryBuffer();
        private Scratchpad scratchpad;


        public bool IsCompleted => reader.IsCompleted;

        public void Dispose()
        {
            ms?.Dispose();
            reader.Dispose();
        }


        private StringSection[] fields = new StringSection[32];
        private Utf8String currentTableName = Utf8String.Empty;
        public Utf8String CurrentTableName => currentTableName;

        private Utf8String currentTableInsertPrefix;


        private Utf8String[] currentTableColumnNames;
        public Utf8String[] CurrentTableColumnNames => currentTableColumnNames;

        private static readonly Utf8String OPEN_PARENS = (Utf8String)"(";
        private static readonly Utf8String INSERT_INTO = (Utf8String)"INSERT INTO";
        private static readonly Utf8String VALUES = (Utf8String)"VALUES";
        private static readonly Utf8String CREATE_TABLE = (Utf8String)"CREATE TABLE";

        private static StringSection[] colnamesBoundariesScratchpad;
        private static Utf8String previousInsertIntoColumns = Utf8String.Empty;

        public Utf8SpanArray TryReadRow()
        {
            StartNewStringHere(true);
            var r = TryReadRowEscaped();
            for (int i = 0; i < r.Length; i++)
            {
                var (start, length) = UnescapeSql(r[i]);
                ref var boundary = ref r.boundaries[i];
                boundary.Length = length;
                if (length < 0)
                {
                    boundary.Start = start;
                }
                else
                {
                    boundary.Start += start;
                }
            }
            r.data2 = new Utf8Span(ms.Bytes);
            return r;
        }

        public IReadOnlyDictionary<string, Utf8String[]> KnownTableSchemas => tableSchemasFromCreateTables;

        private Dictionary<string, Utf8String[]> tableSchemasFromCreateTables = new Dictionary<string, Utf8String[]>();

        private bool parsingMultiinsert;
        public Utf8SpanArray TryReadRowEscaped()
        {


            while (!reader.IsCompleted)
            {
                StartNewStringHere(true);

                var line = reader.ReadTo(separators, out int s);


                if (parsingMultiinsert && line.TrimStartSimple().TryCharAt(0).GetValueOrDefault() == (byte)'(')
                {
                    var arrs = ReadValues(line.TrimStartSimple().SubstringRaw(1), reader, ref s);
                    if (arrs.Length == 0) continue;
                    parsingMultiinsert = s == SEPARATOR_CLOSE_AND_COMMA;
                    return arrs;
                }

                parsingMultiinsert = false;

                int openparens;

                if (line.TrimStartSimple().StartsWithRaw(CREATE_TABLE, StringComparison.OrdinalIgnoreCase))
                {
                    scratchpad.Reset();

                    openparens = AppendUntilFind(ref line, ref s, (byte)'(');
                    if (openparens == -1) continue;
                    var tableName = RemoveBackticks(line.CaptureBetween(CREATE_TABLE, OPEN_PARENS, StringComparison.OrdinalIgnoreCase).TrimSimple());

                    var depth = 1;
                    var idx = openparens + 1;
                    if (AppendUntilFind(ref line, ref s, (byte)')') == -1) break;

                    var ready = true;
                    var columnNames = new List<Utf8String>();
                    while (true)
                    {
                        if (idx == line.Length())
                            if (!AppendString(ref line, ref s))
                                break;
                        var ch = line.CharAt(idx);
                        if (Utf8Utils.IsWhiteSpace(ch)) { idx++; continue; }
                        if (IsQuoteOrSquareBracket(ch) && ready)
                        {
                            var end = AppendUntilFind(ref line, ref s, ch == (byte)'[' ? (byte)']' : ch, idx + 1);
                            if (end == -1) break;
                            if (ready)
                            {
                                var name = RemoveBackticks(line.SubstringRaw(idx, end + 1 - idx));
                                columnNames.Add(new Utf8String(name));
                                ready = false;
                            }
                            idx = end + 1;
                        }
                        else if (IsOpenParens(ch))
                        {
                            depth++;
                            idx++;
                            ready = false;
                        }
                        else if (IsCloseParens(ch))
                        {
                            depth--;
                            if (depth == 0) break;
                            idx++;
                            ready = false;
                        }
                        else if (ch == (byte)',' && depth == 1)
                        {
                            ready = true;
                            idx++;
                        }
                        else
                        {
                            if (!ready) { idx++; continue; }
                            var size = Utf8Utils.IsLetterOrDigitOrUnderscore(line, idx);
                            if (size == 0) { ready = false; idx++; continue; }
                            var startIdx = idx;
                            while (true)
                            {
                                size = Utf8Utils.IsLetterOrDigitOrUnderscore(line, idx);
                                idx += size;
                                if (size == 0)
                                {
                                    var name = line.SubstringRaw(startIdx, idx - startIdx).ToString();
                                    if (!invalidUnquotedColumnNames.Contains(name))
                                    {
                                        columnNames.Add(new Utf8String(name));
                                    }
                                    ready = false;
                                    break;
                                }
                            }

                        }

                    }

                    if (columnNames.Count != 0)
                        tableSchemasFromCreateTables[tableName.ToString()] = columnNames.ToArray();
                    continue;
                }


                if (!line.TrimStartSimple().StartsWithRaw(INSERT_INTO, StringComparison.OrdinalIgnoreCase)) continue;

                if (s != SEPARATOR_CLOSE_AND_COMMA && AppendUntilFind(ref line, ref s, (byte)')') == -1) continue;

                

                openparens = line.IndexOfRaw((byte)'(');
                if (openparens == -1) continue;
                var firstpart = line.SubstringRaw(0, openparens).TrimStartSimple().SubstringRaw(INSERT_INTO.Length()).Trim();
                if (firstpart.EndsWithRaw(VALUES, StringComparison.OrdinalIgnoreCase) && firstpart.Length() > VALUES.Length() && firstpart.CharAt(firstpart.Length() - VALUES.Length() - 1) is byte b && (b == (byte)' ' || b == (byte)')'))
                {
                    var tableName = RemoveBackticks(firstpart.SubstringRaw(0, firstpart.Length() - VALUES.Length()).Trim());
                    if (tableName != currentTableName)
                    {
                        currentTableName = new Utf8String(tableName);

                        tableSchemasFromCreateTables.TryGetValue(currentTableName.ToString(), out currentTableColumnNames);
                    }
                    var arrs = ReadValues(line.SubstringRaw(openparens + 1), reader, ref s);
                    if (arrs.Length == 0) continue;
                    if (s == SEPARATOR_CLOSE_AND_COMMA) parsingMultiinsert = true;
                    return arrs;
                }
                else
                {
                    var rest = line.SubstringRaw(openparens + 1);
                    
                    var z = AppendUntilFind(ref rest, ref s, (byte)')');
                    if (z == -1) continue;
                    var colsStr = rest.SubstringRaw(0, z);

                    
                    if (previousInsertIntoColumns != colsStr)
                    {
                        currentTableName = new Utf8String(RemoveBackticks(firstpart));
                        

                        Utf8SpanArray colnamesarr = default;
                        colnamesarr.boundaries = colnamesBoundariesScratchpad;
                        colsStr.Split((byte)',', StringSplitOptions.None, ref colnamesarr);
                        RemoveQuotesFromColumnNames(ref colnamesarr);
                        colnamesBoundariesScratchpad = colnamesarr.boundaries;
                        previousInsertIntoColumns = new Utf8String(colsStr);
                        currentTableColumnNames = colnamesarr.ToUtf8StringArray();
                    }

                    openparens = AppendUntilFind(ref rest, ref s, (byte)'(');
                    if (openparens == -1) continue;
                    
                    

                    var r = rest.SubstringRaw(openparens + 1);
                    var arrs = ReadValues(r, reader, ref s);
                    if (arrs.Length == 0) continue;
                    if (s == SEPARATOR_CLOSE_AND_COMMA) parsingMultiinsert = true;
                    return arrs;
                }

                
                
            }
            return default(Utf8SpanArray);
        }

        private static bool IsOpenParens(byte ch)
        {
            return ch == (byte)'(' || ch == (byte)'[' || ch == (byte)'{';
        }

        private static bool IsCloseParens(byte ch)
        {
            return ch == (byte)')' || ch == (byte)']' || ch == (byte)'}';
        }

        private static bool IsQuote(byte ch)
        {
            return ch == (byte)'"' || ch == (byte)'\'' || ch == (byte)'`';
        }
        private static bool IsQuoteOrSquareBracket(byte ch)
        {
            return IsQuote(ch) || ch == (byte)'[' || ch == (byte)']';
        }

        private int currentStringStart = 0;
        
        private void StartNewStringHere(bool resetAll = false)
        {
            if (resetAll)
            {
                ms.Clear();
                currentStringStart = 0;
            }
            else
            {
                currentStringStart = ms.Length;
            }
        }

        private bool AppendString(ref Utf8Span line, ref int s)
        {
            if (ms.Length == currentStringStart) ms.Write(line);
            ms.Write(separators[s]);
            var read = reader.ReadTo(separators, out s);
            if (s == -1) return false;
            ms.Write(read);
            line = new Utf8Span(ms.Bytes.Slice(currentStringStart));
            return true;
        }

        

        private void RemoveQuotesFromColumnNames(ref Utf8SpanArray colnamesarr)
        {
            
            for (int i = 0; i < colnamesarr.Length; i++)
            {
                var k = colnamesarr[i];
                ref var boundary = ref colnamesarr.boundaries[i];
                var j = 0;
                while (true)
                {
                    var b = k.CharAt(j);
                    if (!Utf8Utils.IsWhiteSpace(b) && !IsQuoteOrSquareBracket(b)) break;
                    j++;
                    boundary.Start++;
                    boundary.Length--;
                }
                j = k.Length() - 1;
                while (true)
                {
                    var b = k.CharAt(j);
                    if (!Utf8Utils.IsWhiteSpace(b) && !IsQuoteOrSquareBracket(b)) break;
                    j--;
                    boundary.Length--;
                }
            }
        }

        int ctx;

        private static string[] invalidUnquotedColumnNames = new[] { "UNIQUE", "PRIMARY", "KEY", "INDEX", "CONSTRAINT" };

        private Utf8SpanArray ReadValues(Utf8Span str, Utf8StreamReader reader, ref int terminatorOfThis)
        {

            int endpos;
            int readfields;
            if (terminatorOfThis != SEPARATOR_CLOSE_AND_COMMA && AppendUntilFind(ref str, ref terminatorOfThis, (byte)')') == 1)
                return default;
            while (true)
            {
                scratchpad.Reset();
                var m = ctx++;
                readfields = TryReadSqlFields(ref str, out endpos, ref terminatorOfThis);
                if (readfields == 0) return default;
                if (readfields == -1)
                {
                    if (!AppendString(ref str, ref terminatorOfThis))
                        throw new InvalidDataException();
                }
                else break;
            }
            Utf8SpanArray r = default(Utf8SpanArray);
            r.boundaries = fields;
            r.count = readfields;
            r.data1 = str;
            return r;
        }

        private int AppendUntilFind(ref Utf8Span str, ref int s, byte v, int start = 0)
        {
            StartNewStringHere();
            var checkedSoFar = start;
            while (true)
            {
                if (str.Length() >= start)
                {
                    var idx = str.IndexOfRaw(v, checkedSoFar);
                    if (idx != -1) return idx;
                }

                checkedSoFar = Math.Max(str.Length(), start);
                if (!AppendString(ref str, ref s)) return -1;
            }
        }

        private Utf8Span RemoveBackticks(Utf8Span t)
        {
            var m = t.LastIndexOfRaw((byte)'[');
            if (m != -1) t = t.Substring(m);
            if (IsQuoteOrSquareBracket(t.CharAt(0)))
                t = t.SubstringRaw(1, t.Length() - 2);
            if (t.IndexOfRaw((byte)'"') != -1 || t.IndexOfRaw((byte)'.') != -1)
            {
                var q = t.ToString().Replace("\"", string.Empty);
                var j = q.LastIndexOf("dbo.");
                if (j != -1) q = q.Substring(j + 4);
                t = new Utf8Span(q);
            }
            return t;
        }

        private int TryReadSqlFields(ref Utf8Span inputorig, out int endposition, ref int s)
        {
            var offset = 0;
            var idx = 0;
            endposition = 0;
            var input = inputorig;
            while (true)
            {
                if (input.IsEmpty) break;


                if (Utf8Utils.IsWhiteSpace(input.CharAt(0)))
                {
                    input = input.SubstringRaw(1);
                    offset++;
                    continue;
                }

                int len;
                int nextFieldStartOffset = -1;

                // if (/*Utf8Utils.IsLetter(input.CharAt(0))*/)
                {

                    /*
                    if (input.StartsWithRaw(SQL_NULL, StringComparison.OrdinalIgnoreCase) && 0 == Utf8Utils.IsLetterOrDigitOrUnderscore(input, SQL_NULL.Length()))
                    {

                    }
                    */


                    var depth = 0;
                    var j = 0;
                    while (true)
                    {
                        if (j == input.Length())
                        {
                            AppendString(ref inputorig, ref s);
                            input = inputorig.SubstringRaw(offset);
                        }
                        var ch = input.CharAt(j);
                        if (IsOpenParens(ch))
                        {
                            depth++;
                            j++;
                            continue;
                        }
                        else if (IsCloseParens(ch))
                        {
                            if (depth == 0) { nextFieldStartOffset = -1; break; }
                            depth--;
                            j++;
                            continue;
                        }
                        else if (IsQuote(ch))
                        {
                            var z = EndOfQuotedString(offset + j, ref inputorig, ref s);
                            input = inputorig.SubstringRaw(offset);
                            j = z - offset;
                        }
                        else if (depth == 0 && ch == (byte)',')
                        {
                            nextFieldStartOffset = offset + j + 1;
                            break;
                        }
                        else
                        {
                            j++;
                        }
                    }

                    len = j;
                }
           
                
                if (idx == fields.Length)
                {
                    var newsize = fields.Length * 2;
                    var newarr = new StringSection[newsize];

                    fields.AsSpan().CopyTo(newarr);
                    fields = newarr;
                }

                while (len != 0 && Utf8Utils.IsWhiteSpace(input.CharAt(len - 1)))
                {
                    len--;
                }
                
                fields[idx] = new StringSection(offset, len);
                idx++;
                if (nextFieldStartOffset == -1) break;
                offset = nextFieldStartOffset;
                input = inputorig.SubstringRaw(offset);
            }
            return idx;
        }

        
        private int EndOfQuotedString(int offset, ref Utf8Span inputorig, ref int s)
        {
            var quoteType = inputorig.CharAt(offset);
            var searchStart = offset + 1;
            while (true)
            {
                //var end = inputorig.IndexOfRar((byte)'\'', offset + pos);
                var end = AppendUntilFind(ref inputorig, ref s, quoteType, searchStart);
                if (end == -1) return -1;
                end++;

                var precedingBackslashes = 0;
                for (int i = end - 2; i >= 0; i--)
                {
                    var ch = inputorig.CharAt(i);
                    if (ch == (byte)'\\')
                    {
                        precedingBackslashes++;
                    }
                    else
                    {
                        break;
                    }
                }


                if (precedingBackslashes % 2 == 1) { searchStart = end; continue; }
                else if (inputorig.TryCharAt(end) == quoteType) { searchStart = end + 1; continue; }
                return end;

            }
        }

        public Scratchpad Scratchpad => scratchpad;

        private readonly static Utf8String SQL_NULL = (Utf8String)"NULL";
        private (int start, int length) UnescapeSql(Utf8Span str)
        {
            if (str.EqualsRaw(SQL_NULL, StringComparison.OrdinalIgnoreCase)) return (0, 0);
            if (str.Length() == 0) return (0, 0);
            var reusable = true;
            byte quoteType;
            if (str.CharAt(0) == (byte)'N' && str.Length() >= 2 && IsQuote(str.CharAt(1))) { str = str.SubstringRaw(1); reusable = false; quoteType = str.CharAt(1); }
            if (IsQuote(str.CharAt(0)))
            {
                quoteType = str.CharAt(0);
                var slash = str.IndexOfRaw((byte)'\\');
                var quote = str.SubstringRaw(1, str.Length() - 2).IndexOfRaw((str.CharAt(0)));
                if (slash == -1 && quote == -1 && reusable) return (1, str.Length() - 2);
                
                else
                {
                    var output = scratchpad.Use(str.Length() - 2);
                    //str.SubstringRaw(1, slash - 1).Bytes.CopyTo(output);
                    //var len = slash - 1;
                    var len = 0;
                    for (int i = 1; i < str.Length() - 1; i++)
                    {
                        var ch = str.CharAt(i);
                        if (ch == (byte)'\\')
                        {
                            i++;
                            var next = str.CharAt(i);
                            ch = UnescapeSqlSpecialChar(next);
                            output[len++] = ch;
                        }
                        else if (ch == quoteType && str.CharAt(i + 1) == quoteType)
                        {
                            i++;
                            output[len++] = ch;
                        }
                        else
                        {
                            output[len++] = ch;
                        }
                    }
                    var offset = ms.Length;
                    ms.Write(output.Slice(0, len));
                    return (offset, -len);
                }
            }
            else return (0, str.Length());
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
