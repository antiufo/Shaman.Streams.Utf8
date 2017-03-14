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
    public static class Utf8StringExtensions
    {

        public static Utf8String[] Split(this Utf8String this_, byte ch)
        {
            return Split(this_, ch, StringSplitOptions.None);
        }

        public static Utf8String[] Split(this Utf8String this_, byte ch, StringSplitOptions options)
        {
            Utf8String[] result = null;
            Split(this_, ch, options, ref result);
            return result;
        }

        public static void Split(this Utf8String this_, byte ch, StringSplitOptions options, ref Utf8String[] arr)
        {
            var removeEmpty = (options & StringSplitOptions.RemoveEmptyEntries) != 0;
            var count = 0;
            if (removeEmpty)
            {
                var prevWasSplit = true;
                for (int i = 0; i < this_.Length; i++)
                {
                    if (this_[i] == ch)
                    {
                        prevWasSplit = true;
                    }
                    else
                    {
                        if (prevWasSplit) count++;
                        prevWasSplit = false;
                    }

                }
            }
            else
            {
                count++;
                for (int i = 0; i < this_.Length; i++)
                {
                    if (this_[i] == ch)
                    {
                        count++;
                    }
                }
            }
            if (count == 0) { arr = EmptyUtf8StringArray; return; }
            if (arr == null || arr.Length != count) arr = new Utf8String[count];
            var index = 0;
            var pos = 0;
            while (index < count)
            {
                var idx = this_.Substring(pos).IndexOf(ch);
                if (idx == -1) Debug.Assert(index == count - 1);
                else idx += pos;
                var length = idx != -1 ? idx - pos : this_.Length - pos;
                if (!removeEmpty || length != 0)
                {
                    arr[index] = this_.Substring(pos, length);
                    index++;
                }
                pos = idx + 1;
            }
        }

   


        private readonly static Utf8String[] EmptyUtf8StringArray = new Utf8String[] { };

        public static int IndexOf(this Utf8String str, Utf8String value, int start)
        {
            var k = str.Substring(start).IndexOf(value);
            return k != -1 ? start + k : -1;
        }

        public static int IndexOf(this Utf8String str, byte value, int start)
        {
            var k = str.Substring(start).IndexOf(value);
            return k != -1 ? start + k : -1;
        }

        public static Utf8String CaptureBetween(this Utf8String str, Utf8String before, Utf8String after)
        {
            var start = str.IndexOf(before);
            if (start == -1) throw new InvalidDataException();
            str = str.Substring(start + before.Length);
            var end = str.IndexOf(after);
            if (end == -1) throw new InvalidDataException();
            return str.Substring(0, end);
        }


        public static Utf8String? TryCaptureBetween(this Utf8String str, Utf8String before, Utf8String after)
        {
            var start = str.IndexOf(before);
            if (start == -1) return null;
            str = str.Substring(start + before.Length);
            var end = str.IndexOf(after);
            if (end == -1) return null;
            return str.Substring(0, end);
        }





        public static Utf8String CaptureBetween(this Utf8String str, byte before, byte after)
        {
            var start = str.IndexOf(before);
            if (start == -1) throw new InvalidDataException();
            str = str.Substring(start + 1);
            var end = str.IndexOf(after);
            if (end == -1) throw new InvalidDataException();
            return str.Substring(0, end);
        }


        public static Utf8String? TryCaptureBetween(this Utf8String str, byte before, byte after)
        {
            var start = str.IndexOf(before);
            if (start == -1) return null;
            str = str.Substring(start + 1);
            var end = str.IndexOf(after);
            if (end == -1) return null;
            return str.Substring(0, end);
        }






        public static Utf8String CaptureAfter(this Utf8String str, Utf8String prefix)
        {
            var start = str.IndexOf(prefix);
            if (start == -1) throw new InvalidDataException();
            return str.Substring(start + prefix.Length);
        }

        public static Utf8String CaptureBefore(this Utf8String str, Utf8String suffix)
        {
            var end = str.IndexOf(suffix);
            if (end == -1) throw new InvalidDataException();
            return str.Substring(0, end);
        }




        public static Utf8String CaptureAfter(this Utf8String str, byte prefix)
        {
            var start = str.IndexOf(prefix);
            if (start == -1) throw new InvalidDataException();
            return str.Substring(start + 1);
        }

        public static Utf8String CaptureBefore(this Utf8String str, byte suffix)
        {
            var end = str.IndexOf(suffix);
            if (end == -1) throw new InvalidDataException();
            return str.Substring(0, end);
        }



        public static Utf8String? TryCaptureAfter(this Utf8String str, Utf8String prefix)
        {
            var start = str.IndexOf(prefix);
            if (start == -1) return null;
            return str.Substring(start + prefix.Length);
        }

        public static Utf8String? TryCaptureBefore(this Utf8String str, Utf8String suffix)
        {
            var end = str.IndexOf(suffix);
            if (end == -1) return null;
            return str.Substring(0, end);
        }


        public static Utf8String? TryCaptureAfter(this Utf8String str, byte prefix)
        {
            var start = str.IndexOf(prefix);
            if (start == -1) return null;
            return str.Substring(start + 1);
        }

        public static Utf8String? TryCaptureBefore(this Utf8String str, byte suffix)
        {
            var end = str.IndexOf(suffix);
            if (end == -1) return null;
            return str.Substring(0, end);
        }

    }
    
    public static class Utf8Utils
    {
        public static int ParseInt32(Utf8String str)
        {
            if (!PrimitiveParser.TryParseInt32(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length) throw new FormatException();
            return value;
        }
        public static long ParseInt64(Utf8String str)
        {
            if (!PrimitiveParser.TryParseInt64(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length) throw new FormatException();
            return value;
        }
        
        

        public static bool TryParseInt32(Utf8String str, out int val)
        {
            if (!PrimitiveParser.TryParseInt32(str, out val, out var consumed) || consumed != str.Length) return false;
            return true;
        }
        
        public static bool TryParseInt64(Utf8String str, out long val)
        {
            if (!PrimitiveParser.TryParseInt64(str, out val, out var consumed) || consumed != str.Length) return false;
            return true;
        }
        
        
        
        

        public static uint ParseUInt32(Utf8String str)
        {
            if (!PrimitiveParser.TryParseUInt32(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length) throw new FormatException();
            return value;
        }
        public static ulong ParseUInt64(Utf8String str)
        {
            if (!PrimitiveParser.TryParseUInt64(str, out var value, out var consumed)) throw new FormatException();
            if (consumed != str.Length) throw new FormatException();
            return value;
        }
        
        

        public static bool TryParseUInt32(Utf8String str, out uint val)
        {
            if (!PrimitiveParser.TryParseUInt32(str, out val, out var consumed) || consumed != str.Length) return false;
            return true;
        }
        
        public static bool TryParseUInt64(Utf8String str, out ulong val)
        {
            if (!PrimitiveParser.TryParseUInt64(str, out val, out var consumed) || consumed != str.Length) return false;
            return true;
        }
        
        
        
        

        public static DateTime ParseDateConcatenated(Utf8String date)
        {
            if(date.Length != 14) throw new FormatException();
            return new DateTime(
                ParseInt32(date.Substring(0, 4)),
                ParseInt32(date.Substring(4, 2)),
                ParseInt32(date.Substring(6, 2)),
                ParseInt32(date.Substring(8, 2)),
                ParseInt32(date.Substring(10, 2)),
                ParseInt32(date.Substring(12, 2)), 
                DateTimeKind.Utc
                );
        }
        
        public static DateTime ParseDateSeparated(Utf8String date)
        {
            if(date.Length != 19) throw new FormatException();
            return new DateTime(
                ParseInt32(date.Substring(0, 4)),
                ParseInt32(date.Substring(5, 2)),
                ParseInt32(date.Substring(8, 2)),
                ParseInt32(date.Substring(11, 2)),
                ParseInt32(date.Substring(14, 2)),
                ParseInt32(date.Substring(17, 2)), 
                DateTimeKind.Utc
                );
        }
        
        public static Utf8String ReadTo(ref Utf8String str, byte end)
        {
            var idx = str.IndexOf(end);
            if (idx == -1) throw new FormatException();

            var v = str.Substring(0, idx);
            str = str.Substring(idx + 1);
            return v;
        }
        
        public static int ParseInt32To(ref Utf8String str, byte end)
        {
            return ParseInt32(ReadTo(ref str, end));
        }


        
        

    }

    

}
