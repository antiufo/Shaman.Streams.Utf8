using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;

namespace Shaman.Runtime
{
    public static class Utf8StringExtensions
    {

        public static Utf8SpanArray Split(this Utf8Span this_, byte ch)
        {
            return Split(this_, ch, StringSplitOptions.None);
        }

        public static Utf8SpanArray Split(this Utf8Span this_, byte ch, StringSplitOptions options)
        {
            var result = default(Utf8SpanArray);
            Split(this_, ch, options, ref result);
            return result;
        }

        public static void Split(this Utf8Span this_, byte ch, StringSplitOptions options, ref Utf8SpanArray arr)
        {
            arr.data1 = this_;
            var removeEmpty = (options & StringSplitOptions.RemoveEmptyEntries) != 0;

            var index = 0;
            var pos = 0;
            arr.count = 0;
            while (true)
            {
                var idx = this_.SubstringRaw(pos).IndexOfRaw(ch);
                if (idx == -1) break;
                else idx += pos;
                var length = idx != -1 ? idx - pos : this_.Length() - pos;
                if (!removeEmpty || length != 0)
                {
                    arr.Add(pos, length);
                    index++;
                }
                pos = idx + 1;
            }
            var last = this_.SubstringRaw(pos);
            if (!removeEmpty || last.Length() != 0)
            {
                arr.Add(pos, last.Length());
            }
        }



        private readonly static Utf8String[] EmptyUtf8StringArray = new Utf8String[] { };

        public static int IndexOfRaw(this Utf8Span str, Utf8Span value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var k = str.SubstringRaw(start).IndexOfRaw(value, comparisonType);
            return k != -1 ? start + k : -1;
        }

        public static int IndexOfRaw(this Utf8Span str, byte value, int start)
        {
            var k = str.SubstringRaw(start).IndexOfRaw(value);
            return k != -1 ? start + k : -1;
        }

        public static int IndexOfRaw(this Utf8Span str, byte value)
        {
            return str.Bytes.IndexOf(value);
        }
        public static int LastIndexOfRaw(this Utf8Span str, byte value)
        {
            for (int i = str.Length() - 1; i >= 0; i--)
            {
                if (str.CharAt(i) == value) return i;
            }
            return -1;
        }
        private const StringComparison IgnoreCaseMask = (StringComparison)1;

        public static int IndexOfRaw(this Utf8Span str, Utf8Span value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if ((comparisonType & IgnoreCaseMask) == 0) return str.Bytes.IndexOf(value.Bytes);
            return IndexOfRawCaseInsensitive(str, value);
        }

        private static int IndexOfRawCaseInsensitive(Utf8Span str, Utf8Span value)
        {
            var maxToCheck = str.Length() - value.Length();
            for (int i = 0; i <= maxToCheck; i++)
            {
                if (EqualsIgnoreCaseRaw(str.SubstringRaw(i, value.Length()), value)) return i;
            }

            return -1;
        }

        private static bool EqualsIgnoreCaseRaw(Utf8Span str, Utf8Span str2)
        {
            if (str.Length() != str2.Length()) return false;
            var a = str.Bytes;
            var b = str2.Bytes;
            for (int i = 0; i < a.Length; i++)
            {
                if (!EqualsIgnoreCaseRaw(a[i], b[i])) return false;
            }
            return true;
        }
        public static bool StartsWithRaw(this Utf8Span str, Utf8Span value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (str.Length() < value.Length()) return false;
            return str.SubstringRaw(0, value.Length()).EqualsRaw(value, comparisonType);
        }

        public static bool EndsWithRaw(this Utf8Span str, Utf8Span value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (str.Length() < value.Length()) return false;
            return str.SubstringRaw(str.Length() - value.Length()).EqualsRaw(value, comparisonType);
        }

        public static bool EqualsRaw(this Utf8Span str, Utf8Span str2, StringComparison comparisonType)
        {
            if ((comparisonType & IgnoreCaseMask) == 0) return str.Bytes.SequenceEqual(str2.Bytes);
            else return EqualsIgnoreCaseRaw(str, str2);
        }

        private const byte UpperToLowerCaseIncrement = ('a' - 'A');

        private static bool EqualsIgnoreCaseRaw(byte a, byte b)
        {
            if (a >= 'A' && a <= 'Z') a += UpperToLowerCaseIncrement;
            if (b >= 'A' && b <= 'Z') b += UpperToLowerCaseIncrement;
            return a == b;
        }

        public static Utf8Span SubstringRaw(this Utf8Span str, int start)
        {
            return str.Bytes.Slice(start).AsUtf8Span();
        }
        public static Utf8Span SubstringRaw(this Utf8Span str, int start, int length)
        {
            return str.Bytes.Slice(start, length).AsUtf8Span();
        }
        public static Utf8Span AsUtf8Span(this ReadOnlySpan<byte> str)
        {
            return new Utf8Span(str);
        }
        public static Utf8Span CaptureBetween(this Utf8Span str, Utf8Span before, Utf8Span after, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var start = str.IndexOfRaw(before, comparisonType);
            if (start == -1) throw new InvalidDataException();
            str = str.SubstringRaw(start + before.Length());
            var end = str.IndexOfRaw(after, comparisonType);
            if (end == -1) throw new InvalidDataException();
            return str.SubstringRaw(0, end);
        }

        public static Span<T> Slice<T>(this T[] arr, int start)
        {
            return new Span<T>(arr, start, arr.Length - start);
        }

        public static Span<T> Slice<T>(this T[] arr, int start, int length)
        {
            return new Span<T>(arr, start, length);
        }

        public static byte? TryCharAt(this Utf8Span str, int index)
        {
            if (index < 0 || index >= str.Length()) return null;
            return str.CharAt(index);
        }
        public static byte CharAt(this Utf8Span str, int index) => str.Bytes[index];
        public static int Length(this Utf8Span str) => str.Bytes.Length;
        public static byte CharAt(this Utf8String str, int index) => str.Bytes[index];
        public static int Length(this Utf8String str) => str.Bytes.Length;

        public static Utf8Span TryCaptureBetween(this Utf8Span str, Utf8Span before, Utf8Span after, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var start = str.IndexOfRaw(before, comparisonType);
            if (start == -1) return Utf8Span.Empty;
            str = str.SubstringRaw(start + before.Length());
            var end = str.IndexOfRaw(after, comparisonType);
            if (end == -1) return Utf8Span.Empty;
            return str.SubstringRaw(0, end);
        }





        public static Utf8Span CaptureBetween(this Utf8Span str, byte before, byte after)
        {
            var start = str.IndexOfRaw(before);
            if (start == -1) throw new InvalidDataException();
            str = str.SubstringRaw(start + 1);
            var end = str.IndexOfRaw(after);
            if (end == -1) throw new InvalidDataException();
            return str.SubstringRaw(0, end);
        }


        public static Utf8Span TryCaptureBetween(this Utf8Span str, byte before, byte after)
        {
            var start = str.IndexOfRaw(before);
            if (start == -1) return Utf8Span.Empty;
            str = str.SubstringRaw(start + 1);
            var end = str.IndexOfRaw(after);
            if (end == -1) return Utf8Span.Empty;
            return str.SubstringRaw(0, end);
        }






        public static Utf8Span CaptureAfter(this Utf8Span str, Utf8Span prefix, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var start = str.IndexOfRaw(prefix, comparisonType);
            if (start == -1) throw new InvalidDataException();
            return str.SubstringRaw(start + prefix.Length());
        }

        public static Utf8Span CaptureBefore(this Utf8Span str, Utf8Span suffix, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var end = str.IndexOfRaw(suffix, comparisonType);
            if (end == -1) throw new InvalidDataException();
            return str.SubstringRaw(0, end);
        }




        public static Utf8Span CaptureAfter(this Utf8Span str, byte prefix)
        {
            var start = str.IndexOfRaw(prefix);
            if (start == -1) throw new InvalidDataException();
            return str.SubstringRaw(start + 1);
        }

        public static Utf8Span CaptureBefore(this Utf8Span str, byte suffix)
        {
            var end = str.IndexOfRaw(suffix);
            if (end == -1) throw new InvalidDataException();
            return str.SubstringRaw(0, end);
        }



        public static Utf8Span TryCaptureAfter(this Utf8Span str, Utf8Span prefix, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var start = str.IndexOfRaw(prefix, comparisonType);
            if (start == -1) return Utf8Span.Empty;
            return str.SubstringRaw(start + prefix.Length());
        }

        public static Utf8Span TryCaptureBefore(this Utf8Span str, Utf8Span suffix, StringComparison comparisonType = StringComparison.Ordinal)
        {
            var end = str.IndexOfRaw(suffix, comparisonType);
            if (end == -1) return Utf8Span.Empty;
            return str.SubstringRaw(0, end);
        }


        public static Utf8Span TryCaptureAfter(this Utf8Span str, byte prefix)
        {
            var start = str.IndexOfRaw(prefix);
            if (start == -1) return Utf8Span.Empty;
            return str.SubstringRaw(start + 1);
        }

        public static Utf8Span TryCaptureBefore(this Utf8Span str, byte suffix)
        {
            var end = str.IndexOfRaw(suffix);
            if (end == -1) return Utf8Span.Empty;
            return str.SubstringRaw(0, end);
        }

        public static Utf8Span TrimSimple(this Utf8Span span)
        {
            return TrimStartSimple(TrimEndSimple(span));
        }

        public static Utf8Span TrimStartSimple(this Utf8Span str)
        {
            while (str.Length() != 0 && Utf8Utils.IsWhiteSpace(str.CharAt(0)))
            {
                str = str.SubstringRaw(1);
            }
            return str;
        }
        public static Utf8Span TrimEndSimple(this Utf8Span str)
        {
            while (str.Length() != 0 && Utf8Utils.IsWhiteSpace(str.CharAt(str.Length() - 1)))
            {
                str = str.SubstringRaw(0, str.Length() - 1);
            }
            return str;
        }



    }

    public static class Utf8Utils
    {
        public static int IsLetterOrUnderscore(Utf8Span str, int offset)
        {
            if (str.CharAt(offset) == (byte)'_') return 1;
            return IsLetter(str, offset);
        }
        public static bool IsLetterOrUnderscore(byte ch)
        {
            if (ch == (byte)'_') return true;
            return IsLetter(ch);
        }
        public static bool IsDigit(byte ch)
        {
            return ch >= (byte)'0' && ch <= (byte)'9';
        }
        public static int IsDigit(Utf8Span str, int offset)
        {
            var ch = str.CharAt(offset);
            return ch >= (byte)'0' && ch <= (byte)'9' ? 1 : 0;
        }
        public static int IsLetterOrDigitOrUnderscore(Utf8Span str, int offset)
        {
            var ch = str.CharAt(offset);
            if (ch == (byte)'_' || ((byte)ch >= '0' && ch <= (byte)'9')) return 1;
            return IsLetter(str, offset);
        }
        public static bool IsLetterOrDigitOrUnderscore(byte ch)
        {
            if (ch == (byte)'_' || ((byte)ch >= '0' && ch <= (byte)'9')) return true;
            return IsLetter(ch);
        }
        public static int IsLetterOrDigit(Utf8Span str, int offset)
        {
            var ch = str.CharAt(offset);
            if ((byte)ch >= '0' && ch <= (byte)'9') return 1;
            return IsLetter(str, offset);
        }
        public static bool IsLetterOrDigit(byte ch)
        {
            if ((byte)ch >= '0' && ch <= (byte)'9') return true;
            return IsLetter(ch);
        }
        public static bool IsLetter(byte ch)
        {
            if (ch >= 'a' && ch <= 'z') return true;
            if (ch >= 'A' && ch <= 'Z') return true;
            return false;
        }
        public static int IsLetter(Utf8Span str, int offset)
        {
            str = str.SubstringRaw(offset);
            var ch = str.CharAt(0);
            if (ch >= 'a' && ch <= 'z') return 1;
            if (ch >= 'A' && ch <= 'Z') return 1;
            if (ch < 128) return 0;
            var enu = str.GetEnumerator();
            if (!enu.MoveNext()) return 0;
            var l = enu.Current;
            if (l > char.MaxValue) return 0;
            if (!char.IsLetter((char)l)) return 0;
            return new Utf8String(l.ToString()).Length();
        }

        public static int ParseInt32(Utf8Span str)
        {
            if (!Utf8Parser.TryParseInt32(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length()) throw new FormatException();
            return value;
        }
        public static long ParseInt64(Utf8Span str)
        {
            if (!Utf8Parser.TryParseInt64(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length()) throw new FormatException();
            return value;
        }
        
        
        
        public static bool TryParseInt32(Utf8Span str, out int val)
        {
            if (!Utf8Parser.TryParseInt32(str, out val, out var consumed) || consumed != str.Length()) return false;
            return true;
        }
        
        public static bool TryParseInt64(Utf8Span str, out long val)
        {
            if (!Utf8Parser.TryParseInt64(str, out val, out var consumed) || consumed != str.Length()) return false;
            return true;
        }
        
        
        
        

        public static uint ParseUInt32(Utf8Span str)
        {
            if (!Utf8Parser.TryParseUInt32(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length()) throw new FormatException();
            return value;
        }
        public static ulong ParseUInt64(Utf8Span str)
        {
            if (!Utf8Parser.TryParseUInt64(str.Bytes, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length()) throw new FormatException();
            return value;
        }
        
        

        public static bool TryParseUInt32(Utf8Span str, out uint val)
        {
            if (!Utf8Parser.TryParseUInt32(str, out val, out var consumed) || consumed != str.Length()) return false;
            return true;
        }
        
        public static bool TryParseUInt64(Utf8Span str, out ulong val)
        {
            if (!Utf8Parser.TryParseUInt64(str, out val, out var consumed) || consumed != str.Length()) return false;
            return true;
        }
        
        
        
        

        public static DateTime ParseDateConcatenated(Utf8Span date)
        {
            if(date.Length() != 14) throw new FormatException();
            return new DateTime(
                ParseInt32(date.SubstringRaw(0, 4)),
                ParseInt32(date.SubstringRaw(4, 2)),
                ParseInt32(date.SubstringRaw(6, 2)),
                ParseInt32(date.SubstringRaw(8, 2)),
                ParseInt32(date.SubstringRaw(10, 2)),
                ParseInt32(date.SubstringRaw(12, 2)), 
                DateTimeKind.Utc
                );
        }
        
        public static DateTime ParseDateSeparated(Utf8Span date)
        {
            if(date.Length() != 19) throw new FormatException();
            return new DateTime(
                ParseInt32(date.SubstringRaw(0, 4)),
                ParseInt32(date.SubstringRaw(5, 2)),
                ParseInt32(date.SubstringRaw(8, 2)),
                ParseInt32(date.SubstringRaw(11, 2)),
                ParseInt32(date.SubstringRaw(14, 2)),
                ParseInt32(date.SubstringRaw(17, 2)), 
                DateTimeKind.Utc
                );
        }
        
        public static Utf8Span ReadTo(ref Utf8Span str, byte end)
        {
            var idx = str.IndexOfRaw(end);
            if (idx == -1) throw new FormatException();

            var v = str.SubstringRaw(0, idx);
            str = str.SubstringRaw(idx + 1);
            return v;
        }
        
        public static int ParseInt32To(ref Utf8Span str, byte end)
        {
            return ParseInt32(ReadTo(ref str, end));
        }

        public static bool IsWhiteSpace(byte b)
        {
            return
                b == (byte)' ' ||
                b == (byte)'\r' ||
                b == (byte)'\n' ||
                b == (byte)'\t';
        }
    }

    

}
