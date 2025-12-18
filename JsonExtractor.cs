using System.Text;
using System.Text.Json;

namespace RoadCraftSaveTool;

internal static class JsonExtractor
{
    public static bool LooksLikeJson(byte[] bytes)
    {
        var span = bytes.AsSpan();
        int i = 0;
        while (i < span.Length && IsWs(span[i])) i++;
        if (i >= span.Length) return false;
        return span[i] == (byte)'{' || span[i] == (byte)'[';
    }

    public static JsonElement ParseJson(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes));
        return doc.RootElement.Clone();
    }

    public static List<JsonFragment> ExtractJsonFragments(byte[] bytes, string? contains, int maxFragments)
    {
        var results = new List<JsonFragment>(Math.Min(maxFragments, 64));

        byte[]? needle = null;
        if (!string.IsNullOrEmpty(contains))
            needle = Encoding.ASCII.GetBytes(contains);

        int i = 0;
        while (i < bytes.Length && results.Count < maxFragments)
        {
            while (i < bytes.Length && bytes[i] != (byte)'{' && bytes[i] != (byte)'[') i++;
            if (i >= bytes.Length) break;

            if (needle != null)
            {
                int wStart = Math.Max(0, i - 4096);
                int wLen = Math.Min(bytes.Length - wStart, 8192);
                if (IndexOf(bytes, needle, wStart, wLen) < 0)
                {
                    i++;
                    continue;
                }
            }

            if (TryParseAt(bytes, i, out var consumed, out var element))
            {
                results.Add(new JsonFragment { Offset = i, Length = consumed, Data = element });
                i += consumed;
            }
            else
            {
                i++;
            }
        }

        return results;
    }

    public static bool TryExtractLargestJson(byte[] bytes, out int bestOffset, out int bestLen)
    {
        bestOffset = 0;
        bestLen = 0;

        int i = 0;
        while (i < bytes.Length)
        {
            while (i < bytes.Length && bytes[i] != (byte)'{' && bytes[i] != (byte)'[') i++;
            if (i >= bytes.Length) break;

            if (TryParseAt(bytes, i, out var consumed, out _))
            {
                if (consumed > bestLen)
                {
                    bestLen = consumed;
                    bestOffset = i;
                }
                i += consumed;
            }
            else i++;
        }

        return bestLen > 0;
    }

    public static List<string> ExtractPrintableStrings(byte[] bytes, int minLen, int maxItems)
    {
        var results = new List<string>(Math.Min(maxItems, 128));
        var sb = new StringBuilder();

        void Flush()
        {
            if (sb.Length >= minLen)
            {
                var s = sb.ToString();
                if (!results.Contains(s))
                    results.Add(s);
            }
            sb.Clear();
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b >= 32 && b <= 126)
            {
                sb.Append((char)b);
                if (sb.Length > 4096) Flush();
            }
            else
            {
                Flush();
                if (results.Count >= maxItems) break;
            }
        }

        Flush();
        return results;
    }

    public static void WriteReport(string path, DecodeReport report)
    {
        var opts = new JsonWriterOptions { Indented = true };

        using var fs = File.Create(path);
        using var w = new Utf8JsonWriter(fs, opts);

        w.WriteStartObject();
        w.WriteString("sourceFile", report.SourceFile);
        w.WriteString("format", report.Format);
        w.WriteNumber("headerLength", report.HeaderLength);
        w.WriteNumber("expectedTotalCompressed", report.ExpectedTotalCompressed);
        w.WriteNumber("expectedTotalUncompressed", report.ExpectedTotalUncompressed);
        w.WriteNumber("actualDecompressedBytes", report.ActualDecompressedBytes);
        w.WriteString("payloadKind", report.PayloadKind);

        if (report.Notes.Count > 0)
        {
            w.WritePropertyName("notes");
            w.WriteStartArray();
            foreach (var n in report.Notes) w.WriteStringValue(n);
            w.WriteEndArray();
        }

        if (report.Payload is JsonElement el)
        {
            w.WritePropertyName("payload");
            el.WriteTo(w);
        }

        if (report.JsonFragments != null)
        {
            w.WritePropertyName("jsonFragments");
            w.WriteStartArray();
            foreach (var f in report.JsonFragments)
            {
                w.WriteStartObject();
                w.WriteNumber("offset", f.Offset);
                w.WriteNumber("length", f.Length);
                w.WritePropertyName("data");
                f.Data.WriteTo(w);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        if (report.Strings != null)
        {
            w.WritePropertyName("strings");
            w.WriteStartArray();
            foreach (var s in report.Strings) w.WriteStringValue(s);
            w.WriteEndArray();
        }

        w.WriteEndObject();
        w.Flush();
    }

    private static bool TryParseAt(byte[] bytes, int offset, out int consumed, out JsonElement element)
    {
        consumed = 0;
        element = default;

        try
        {
            var mem = new ReadOnlyMemory<byte>(bytes, offset, bytes.Length - offset);
            var reader = new Utf8JsonReader(mem.Span, isFinalBlock: true, state: default);
            if (!reader.Read())
                return false;

            int depth = 0;
            var start = reader.TokenType;
            if (start == JsonTokenType.StartObject || start == JsonTokenType.StartArray) depth = 1;
            else return false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray) depth++;
                if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray) depth--;

                if (depth == 0)
                {
                    consumed = checked((int)reader.BytesConsumed);
                    var slice = new ReadOnlyMemory<byte>(bytes, offset, consumed);
                    using var doc = JsonDocument.Parse(slice);
                    element = doc.RootElement.Clone();
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    private static bool IsWs(byte b) => b == (byte)' ' || b == (byte)'\n' || b == (byte)'\r' || b == (byte)'\t';

    private static int IndexOf(byte[] hay, byte[] needle, int start, int count)
    {
        int end = start + count - needle.Length;
        for (int i = start; i <= end; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (hay[i + j] != needle[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }
}
