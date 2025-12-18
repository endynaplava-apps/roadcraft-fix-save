using System.Text.Json;

namespace RoadCraftSaveTool;

// Needed because JsonExtractor exposes ExtractJsonFragments() which returns JsonFragment.
// The GUI only uses LooksLikeJson(), but the type must exist for compilation.
internal sealed class JsonFragment
{
    public int Offset { get; set; }
    public int Length { get; set; }
    public JsonElement Data { get; set; }
}