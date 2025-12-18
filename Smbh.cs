using System.Text;

namespace RoadCraftSaveTool;

internal sealed record SmbhBlock(
    string Name,
    byte[] NameBytes,     // includes trailing 0
    byte[] PayloadBytes   // raw payload
);

internal sealed record SmbhParseResult(
    byte[] PrefixBytes,
    List<SmbhBlock> Blocks,
    byte[] SuffixBytes
);

internal static class SmbhParser
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SMBH");

    public static SmbhParseResult Parse(byte[] decompressedPayload)
    {
        // Find first valid SMBH block. Everything before it is preserved as prefix.
        int first = FindFirstValidBlockOffset(decompressedPayload, maxScan: Math.Min(2_000_000, decompressedPayload.Length));
        var prefix = decompressedPayload.AsSpan(0, first).ToArray();

        var blocks = new List<SmbhBlock>();
        int pos = first;

        while (pos + 12 <= decompressedPayload.Length && IsAt(decompressedPayload, pos, Magic))
        {
            uint nameLen = ReadU32LE(decompressedPayload, pos + 4);
            uint payloadLen = ReadU32LE(decompressedPayload, pos + 8);

            long headerEnd = (long)pos + 12;
            long nameStart = headerEnd;
            long payloadStart = nameStart + nameLen;
            long next = payloadStart + payloadLen;

            if (nameLen == 0 || nameLen > 1024) break;
            if (payloadLen > decompressedPayload.Length) break;
            if (next > decompressedPayload.Length) break;

            var nameBytes = decompressedPayload.AsSpan((int)nameStart, (int)nameLen).ToArray();
            if (nameBytes[^1] != 0) break;

            var name = Encoding.ASCII.GetString(nameBytes, 0, nameBytes.Length - 1);

            var payload = decompressedPayload.AsSpan((int)payloadStart, (int)payloadLen).ToArray();

            blocks.Add(new SmbhBlock(name, nameBytes, payload));
            pos = (int)next;
        }

        var suffix = decompressedPayload.AsSpan(pos, decompressedPayload.Length - pos).ToArray();

        return new SmbhParseResult(prefix, blocks, suffix);
    }

    public static byte[] Build(byte[] prefix, List<SmbhBlock> blocks, byte[] suffix)
    {
        using var ms = new MemoryStream(prefix.Length + suffix.Length + blocks.Count * 32);
        ms.Write(prefix, 0, prefix.Length);

        foreach (var b in blocks)
        {
            var nameBytes = b.NameBytes;
            if (nameBytes.Length == 0 || nameBytes[^1] != 0)
                nameBytes = Encoding.ASCII.GetBytes(b.Name + "\0");

            WriteAscii(ms, "SMBH");
            WriteU32LE(ms, (uint)nameBytes.Length);
            WriteU32LE(ms, (uint)b.PayloadBytes.Length);
            ms.Write(nameBytes, 0, nameBytes.Length);
            ms.Write(b.PayloadBytes, 0, b.PayloadBytes.Length);
        }

        ms.Write(suffix, 0, suffix.Length);
        return ms.ToArray();
    }

    private static int FindFirstValidBlockOffset(byte[] data, int maxScan)
    {
        int limit = Math.Min(maxScan, data.Length - 16);

        for (int i = 0; i < limit; i++)
        {
            if (!IsAt(data, i, Magic)) continue;

            uint nameLen = ReadU32LE(data, i + 4);
            uint payloadLen = ReadU32LE(data, i + 8);

            if (nameLen == 0 || nameLen > 1024) continue;

            long nameStart = (long)i + 12;
            long payloadStart = nameStart + nameLen;
            long next = payloadStart + payloadLen;

            if (next <= 0 || next > data.Length) continue;

            // name should be ASCII-ish and end with 0
            if (data[(int)nameStart + (int)nameLen - 1] != 0) continue;

            // Quick plausibility: name bytes printable or underscore/dot
            var ok = true;
            for (int k = 0; k < (int)nameLen - 1; k++)
            {
                byte c = data[(int)nameStart + k];
                if (c == 0) { ok = false; break; }
                if (!(c >= 32 && c <= 126)) { ok = false; break; }
            }
            if (!ok) continue;

            return i;
        }

        throw new InvalidDataException("Could not find SMBH blocks in decompressed payload (format differs).");
    }

    private static bool IsAt(byte[] data, int offset, byte[] magic)
    {
        if (offset + magic.Length > data.Length) return false;
        for (int i = 0; i < magic.Length; i++)
            if (data[offset + i] != magic[i]) return false;
        return true;
    }

    private static uint ReadU32LE(byte[] data, int offset) => BitConverter.ToUInt32(data, offset);

    private static void WriteAscii(Stream s, string text)
    {
        var b = Encoding.ASCII.GetBytes(text);
        s.Write(b, 0, b.Length);
    }

    private static void WriteU32LE(Stream s, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        b[0] = (byte)(value & 0xFF);
        b[1] = (byte)((value >> 8) & 0xFF);
        b[2] = (byte)((value >> 16) & 0xFF);
        b[3] = (byte)((value >> 24) & 0xFF);
        s.Write(b);
    }
}
