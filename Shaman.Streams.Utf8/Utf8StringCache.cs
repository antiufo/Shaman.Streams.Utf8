using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Utf8;

namespace Shaman.Runtime
{
    public static partial class Utf8StringCache
    {
        private const int size = 6841;

        private static CacheSlot[] cache;
        private static byte[] scratchpad = new byte[1024];
        private static int usedScratchpadBytes;

        private static object lockObj = new object();

        public static void ClearForCurrentThread()
        {
            cache = null;
        }



        public static string ToStringCached(this Utf8String utf8)
        {
            if (utf8.Length == 0)
            {
                return string.Empty;
            }
            var utf8length = utf8.Length;
            int hash = CalculateHash(utf8[0], utf8[utf8length / 2], utf8[utf8length - 1], utf8length);
            if (cache == null)
            {
                cache = new CacheSlot[6841];
            }
            CacheSlot cacheSlot = cache[hash];
            var list = cacheSlot.List;
            if (list == null) cacheSlot.List = list = new CacheEntry[6];


            for (int i = 0; i < list.Length; i++)
            {
                var entry = list[i];
                if (entry.String == null) break;
                if (entry.Span.BlockEquals(utf8.Bytes)) return entry.String;
            }

            lock (lockObj)
            {
                var entry = new CacheEntry();
                entry.Length = utf8length;
                entry.String = utf8.ToString();

                if (usedScratchpadBytes + utf8length <= scratchpad.Length)
                {
                    utf8.CopyTo(scratchpad.Slice(usedScratchpadBytes));
                    entry.Bytes = scratchpad;
                    entry.Offset = usedScratchpadBytes;
                    usedScratchpadBytes += utf8length;
                }
                else
                {
                    scratchpad = new byte[Math.Max(scratchpad.Length, utf8length * 2)];
                    utf8.CopyTo(scratchpad);
                    entry.Bytes = scratchpad;
                    entry.Offset = 0;
                    usedScratchpadBytes = utf8length;
                }

                cacheSlot.List[cacheSlot.NextItemToReplace] = entry;
                cacheSlot.NextItemToReplace = (cacheSlot.NextItemToReplace + 1) % cacheSlot.List.Length;

                cache[hash] = cacheSlot;
                return entry.String;
            }
        }
        private static int CalculateHash(byte firstChar, byte middleChar, byte lastChar, int length)
        {
            return ((int)((firstChar * '\u0B9B') ^ (lastChar * '\u0F07') ^ (middleChar * '\u138B') ^ length) % 6841);
        }

        public static string Dump()
        {
            return string.Join("\n", cache.Where(x => x.List != null).SelectMany(x => x.List).Select(x => x.String).ToArray());
        }
    }

    internal struct CacheSlot
    {
        public CacheEntry[] List;
        public int NextItemToReplace;
    }

    internal struct CacheEntry
    {
        public string String;
        public byte[] Bytes;
        public int Offset;
        public int Length;

        public ReadOnlySpan<byte> Span => Bytes.Slice(Offset, Length);
    }
}
