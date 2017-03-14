# Shaman.Streams.Utf8

Library for working with UTF8 streams (based on `Span<T>` and `Utf8String`).

## Utf8StreamWriter

It's like a `StreamWriter`, except it also supports writing raw UTF8 strings (plus other optimizations).

## CsvReader

Reads UTF8 encoded CSV files.

```csharp
using (var csv = new CsvReader(@"C:\temp\example.csv"))
{
    string[] header = csv.ReadHeader();
    int firstNameIdx = Array.IndexOf(header, "First name");
    int lastNameIdx = Array.IndexOf(header, "Last name");
    while (true)
    {
        var line = csv.ReadLine();
        if (line == null) break;
        Utf8String firstName = line[firstNameIdx];
        Utf8String lastName = line[lastNameIdx];

        // Process row.
        // Note: these strings are valid only until the next line is read, then they'll contain garbage.
        // If you need to store them for later, you can use Scratchpad.
    }
}
```

## Scratchpad
Provides a place where to allocate `Span`s and `Utf8String`s from. It is backed by a byte array.
You can use it as a memory arena: by calling `Clear()`, previous data will be marked as deleted (and old `Span`s will contain garbage), and the memory can be reused for more allocations.

## MemoryBuffer
It's like a `MemoryStream` for `Span`s. You can append data, and then retrieve a `Span<byte>` with all the contents written so far.

## SqlDumpReader
Efficiently reads and parses a SQL database dump.
```csharp
using (var stream = File.OpenRead(@"C:\temp\dbdump.sql"))
using (var dbdump = new SqlDumpReader(stream))
{
    while (!dbdump.IsCompleted)
    {
        Utf8String[] values = dbdump.TryReadRow(out int numfields);
        if (numfields == 0) continue;

        // Table name: dbdump.CurrentTableName
        // Read each field
        for (int i = 0; i < numfields; i++)
        {
            Utf8String val = dbdump.UnescapeSql(values[i]);
        }
        
    }
}
```

## Utf8StreamReader
It's like a `StreamReader`, but it produces `Utf8String`s (which are backed by the same byte buffer that was used by `Stream.Read`)

It also implements `Stream`, in case you want to parse some portions of your file in a binary way.

```csharp
using(var reader = new Utf8StreamReader(@"c:\temp\file"))
{
    while (!reader.IsCompleted)
    {
        Utf8String line = reader.ReadLine();
        // Process line.
        // Note: the string is guaranteeded to remain consistent until the next call to ReadLine (or ReadTo).
        // If you need to store them (or some portions) for later, you can use Scratchpad (see above).
    }
}

```
## Utf8StringCache
Provides an interning cache for `Utf8String` -> `System.String`.
```csharp
using Shaman.Runtime;

Utf8String a;
string b = a.ToStringCached();
```

## Utf8StringExtensions
Extension methods for `Utf8String`s:
* `Utf8String Split(byte separator)`
* `Utf8String Split(byte separator, StringSplitOptions options)`
* `void Split(byte separator, StringSplitOptions options, ref Utf8String array)`
* `int IndexOf(Utf8String value, int start)`
* `int IndexOf(byte value, int start)`
* `Utf8String CaptureBetween(Utf8String prefix, Utf8String suffix)` (+ `Try` version)
* `Utf8String CaptureAfter(Utf8String prefix)` (+ `Try` version)
* `Utf8String CaptureBefore(Utf8String suffix)` (+ `Try` version)

For similar methods for `string`s, see [https://github.com/antiufo/Shaman.CommonExtensions](Shaman.CommonExtensions). For similar methods for `ValueString`s, see [https://github.com/antiufo/Shaman.ValueString](Shaman.ValueString).

## Utf8Utils
* `ParseInt32`, `ParseUInt32`, `ParseInt64`, `ParseUInt64` (and their `Try` versions)
* `ParseDateConcatenated` (eg. `yyyyMMddHHmmss`)
* `ParseDateSeparated` (eg. `yyyy-MM-dd HH:mm:ss`)
* `Utf8String ReadTo(ref Utf8String str, byte end)`
