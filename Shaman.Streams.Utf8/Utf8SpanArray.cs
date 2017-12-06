using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Utf8;
using System.Threading.Tasks;


namespace Shaman.Runtime
{
    public ref struct Utf8SpanArray
    {
        internal Utf8Span data1;
        internal Utf8Span data2;
        internal StringSection[] boundaries;
        internal int count;
        public int Length => count;
        public Utf8Span this[int index]
        {
            get
            {
                var b = boundaries[index];
                if (b.Length < 0) return data2.SubstringRaw(b.Start, -b.Length);
                return data1.SubstringRaw(b.Start, b.Length);
            }
        }

        public string[] ToStringArray()
        {
            var arr = new string[this.count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = this[i].ToString();
            }
            return arr;
        }
        public Utf8String[] ToUtf8StringArray()
        {
            var arr = new Utf8String[this.count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = new Utf8String(this[i]);
            }
            return arr;
        }

        public string[] AsStringArray => ToStringArray();

        internal void Add(int pos, int length)
        {
            if (boundaries == null)
            {
                boundaries = new StringSection[8];
            }
            else if (boundaries.Length == count)
            {
                var b = new StringSection[boundaries.Length * 2];

                boundaries.AsSpan().CopyTo(b);
                boundaries = b;
            }
            boundaries[count] = new StringSection(pos, length);
            count++;
        }

        
    }

    internal struct StringSection
    {
        public StringSection(int start, int length)
        {
            this.Start = start;
            this.Length = length;
        }
        public int Start;
        public int Length;
    }

    internal ref struct Utf8SpanWithIndex
    {
        internal Utf8Span Span;
        internal int Index;

        public Utf8SpanWithIndex(Utf8Span utf8Span, int index)
        {
            Span = utf8Span;
            Index = index;
        }


        public Utf8SpanWithIndex SubstringRaw(int start, int length)
        {
            return new Utf8SpanWithIndex(Span.SubstringRaw(start, length), this.Index + start);
        }

        public Utf8SpanWithIndex SubstringRaw(int start)
        {
            return new Utf8SpanWithIndex(Span.SubstringRaw(start), this.Index + start);
        }

        public bool IsEmpty => Span.IsEmpty;
        public int Length => Span.Length();
    }
}
