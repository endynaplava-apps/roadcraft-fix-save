using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RoadCraftSaveTool;

internal sealed record SsfDecodeResult(
    int HeaderLength,
    int ExpectedTotalCompressed,
    int ExpectedTotalUncompressed,
    byte[] HeaderBytes,
    byte[] DecompressedPayload
);

internal static class SsfCodec
{
    private static readonly byte[] MagicSSF1 = Encoding.ASCII.GetBytes("SSF1");

    public const int DefaultChunkSize = 1_048_576;

    public static SsfDecodeResult Decode(string path)
    {
        var file = File.ReadAllBytes(path);
        if (!StartsWith(file, MagicSSF1))
            throw new InvalidDataException("Not SSF1.");

        int totalComp = BitConverter.ToInt32(file, 4);
        int totalUnc = BitConverter.ToInt32(file, 12);

        int headerLen = file.Length - totalComp;
        if (headerLen < 16 || headerLen > 1024 * 1024)
            headerLen = FindFirstBlockOffset(file, maxScan: 256 * 1024);

        var header = file.AsSpan(0, headerLen).ToArray();
        var payload = DecompressBlocks(file, headerLen, totalComp);

        // non-fatal warning
        if (totalUnc > 0 && payload.Length != totalUnc)
            Console.WriteLine($"WARN: uncompressed size mismatch (expected {totalUnc}, got {payload.Length}).");

        return new SsfDecodeResult(headerLen, totalComp, totalUnc, header, payload);
    }

    public static void EncodeLike(string inputFileWithHeader, byte[] decompressedPayload, string outputFile, int chunkSize = DefaultChunkSize)
    {
        var original = File.ReadAllBytes(inputFileWithHeader);
        if (!StartsWith(original, MagicSSF1))
            throw new InvalidDataException("Not SSF1.");

        int oldTotalComp = BitConverter.ToInt32(original, 4);
        int headerLen = original.Length - oldTotalComp;
        if (headerLen < 16 || headerLen > 1024 * 1024)
            headerLen = FindFirstBlockOffset(original, maxScan: 256 * 1024);

        var header = original.AsSpan(0, headerLen).ToArray();
        EncodeWithHeaderBytes(header, decompressedPayload, outputFile, chunkSize);
    }

    public static void EncodeWithHeaderBytes(byte[] headerBytes, byte[] decompressedPayload, string outputFile, int chunkSize = DefaultChunkSize)
    {
        if (headerBytes.Length < 16)
            throw new InvalidDataException("Header bytes too short.");
        if (!StartsWith(headerBytes, MagicSSF1))
            throw new InvalidDataException("Header is not SSF1.");

        var header = headerBytes.ToArray();
        var blocks = CompressToBlocks(decompressedPayload, chunkSize);

        // Update standard header fields
        WriteI32LE(header, 4, blocks.Length);
        WriteI32LE(header, 12, decompressedPayload.Length);

        var md5Hex = MD5Hex(blocks);
        var md5Bytes = Encoding.ASCII.GetBytes(md5Hex);
        Buffer.BlockCopy(md5Bytes, 0, header, 20, 32);

        using var fs = File.Create(outputFile);
        fs.Write(header, 0, header.Length);
        fs.Write(blocks, 0, blocks.Length);
    }

    private static byte[] DecompressBlocks(byte[] file, int startOffset, int totalComp)
    {
        int offset = startOffset;
        int processed = 0;

        using var msOut = new MemoryStream();

        while (processed < totalComp)
        {
            if (offset + 8 > file.Length) throw new InvalidDataException("Unexpected EOF while reading block header.");

            int uSize = ReadI32LE(file, offset);
            int cSize = ReadI32LE(file, offset + 4);
            offset += 8;

            if (cSize <= 0 || offset + cSize > file.Length)
                throw new InvalidDataException($"Invalid compressed block size at offset {offset - 8}.");

            var comp = new ReadOnlySpan<byte>(file, offset, cSize);
            offset += cSize;
            processed += 8 + cSize;

            var dec = TryInflate(comp, zlibWrapper: true) ?? TryInflate(comp, zlibWrapper: false);
            if (dec == null)
                throw new InvalidDataException($"Could not decompress block at offset {offset - cSize}.");

            msOut.Write(dec, 0, dec.Length);
        }

        return msOut.ToArray();
    }

    private static byte[] CompressToBlocks(byte[] decompressed, int chunkSize)
    {
        using var ms = new MemoryStream();
        int off = 0;

        while (off < decompressed.Length)
        {
            int uSize = Math.Min(chunkSize, decompressed.Length - off);
            var span = new ReadOnlySpan<byte>(decompressed, off, uSize);

            byte[] comp;
            using (var cms = new MemoryStream())
            {
                using (var zs = new ZLibStream(cms, CompressionLevel.Optimal, leaveOpen: true))
                    zs.Write(span);
                comp = cms.ToArray();
            }

            WriteI32LE(ms, uSize);
            WriteI32LE(ms, comp.Length);
            ms.Write(comp, 0, comp.Length);

            off += uSize;
        }

        return ms.ToArray();
    }

    private static byte[]? TryInflate(ReadOnlySpan<byte> data, bool zlibWrapper)
    {
        try
        {
            using var msIn = new MemoryStream(data.ToArray());
            using Stream ds = zlibWrapper
                ? new ZLibStream(msIn, CompressionMode.Decompress)
                : new DeflateStream(msIn, CompressionMode.Decompress);

            using var msOut = new MemoryStream();
            ds.CopyTo(msOut);
            var result = msOut.ToArray();
            return result.Length > 0 ? result : null;
        }
        catch { return null; }
    }

    private static int FindFirstBlockOffset(byte[] file, int maxScan)
    {
        int limit = Math.Min(maxScan, file.Length - 16);
        for (int off = 0; off < limit; off++)
        {
            if (off + 8 > file.Length) break;
            int u = ReadI32LE(file, off);
            int c = ReadI32LE(file, off + 4);
            if (c <= 0) continue;
            if (off + 8 + c > file.Length) continue;
            if (u < 0 || u > 500_000_000) continue;

            var comp = new ReadOnlySpan<byte>(file, off + 8, c);
            if (TryInflate(comp, true) != null || TryInflate(comp, false) != null)
                return off;
        }
        throw new InvalidDataException("Could not locate first compressed block. Header length may differ or format is different.");
    }

    private static bool StartsWith(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
            if (data[i] != prefix[i]) return false;
        return true;
    }

    private static int ReadI32LE(byte[] data, int offset) => BitConverter.ToInt32(data, offset);

    private static void WriteI32LE(byte[] data, int offset, int value)
    {
        var b = BitConverter.GetBytes(value);
        Buffer.BlockCopy(b, 0, data, offset, 4);
    }

    private static void WriteI32LE(Stream s, int value)
    {
        Span<byte> b = stackalloc byte[4];
        b[0] = (byte)(value & 0xFF);
        b[1] = (byte)((value >> 8) & 0xFF);
        b[2] = (byte)((value >> 16) & 0xFF);
        b[3] = (byte)((value >> 24) & 0xFF);
        s.Write(b);
    }

    private static string MD5Hex(byte[] data)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
