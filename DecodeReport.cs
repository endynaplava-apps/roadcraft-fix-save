using System.Text.Json;

namespace RoadCraftSaveTool;

// Minimal model used by JsonExtractor.WriteReport().
// The GUI does not need this at runtime, but the type must exist to compile.
internal sealed class DecodeReport
{
    public string SourceFile { get; set; } = "";
    public string Format { get; set; } = "";
    public int HeaderLength { get; set; }
    public int ExpectedTotalCompressed { get; set; }
    public int ExpectedTotalUncompressed { get; set; }
    public int ActualDecompressedBytes { get; set; }
    public string PayloadKind { get; set; } = ""; // "json" | "binary"
    public JsonElement? Payload { get; set; }
    public List<JsonFragment>? JsonFragments { get; set; }
    public List<string>? Strings { get; set; }
    public List<string> Notes { get; set; } = new();
}
