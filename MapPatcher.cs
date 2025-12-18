using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RoadCraftSaveTool;

internal static class MapPatcher
{
    private const string BuildCraneReplacementProperty = """
"Establish_Task_Build_Crane":{
  "$type":"RequestEstablishSaveDesc",
  "isFinished":false,
  "issuedRewardCount":0,
  "routeSaveDesc":{
    "nodes":[{"x": 712.76129150390625,"y": 16.101736068725586,"z": -152.00442504882812}],
    "isNew":true,
    "isConnected":false
  },
  "trafficSaveDesc":{
    "regularConvoySaveDesc":{
      "isRunning":false,
      "isPioneer":false,
      "isStucked":false,
      "isMalfunction":false,
      "activeLifeControllerDescs":[],
      "preterminatedLifeControllerDescs":[],
      "timeToSendNext":481.0
    },
    "objectiveConvoySaveDesc":{
      "$type":"ObjectiveConvoySaveDesc",
      "isRunning":false,
      "isPioneer":false,
      "isStucked":false,
      "isMalfunction":false,
      "activeLifeControllerDescs":[],
      "preterminatedLifeControllerDescs":[],
      "timeToSendNext":0.0,
      "isValid":false,
      "lastAiIndex":-1,
      "passedTrucksCount":-1
    },
    "wasObjectiveAttached":true,
    "retrySaveDesc":{
      "borderIndex":2147483647,
      "stateBorderIndex":2147483647,
      "passedPoses":[],
      "passedRotations":[]
    },
    "stuckPos":null,
    "stuckReason":2,
    "stuckWayTrail":[]
  },
  "firstBuildingMalfunction":false,
  "secondBuildingMalfunction":false,
  "isProgressed":false,
  "isClientSave":false
}
""";

    public static void PatchBuildCraneEstablish(string inputMapFile, string outputMapFile)
    {
        PatchJsonProperty(
            inputMapFile: inputMapFile,
            outputMapFile: outputMapFile,
            blockSelector: "request-system",
            propName: "Establish_Task_Build_Crane",
            valueJson: ExtractReplacementValueJson(BuildCraneReplacementProperty)
        );

        Console.WriteLine("OK: patched Establish_Task_Build_Crane (request-system).");
    }

    public static void PatchJsonProperty(string inputMapFile, string outputMapFile, string blockSelector, string propName, string valueJson)
    {
        var ssf = SsfCodec.Decode(inputMapFile);
        var parsed = SmbhParser.Parse(ssf.DecompressedPayload);

        var selectorNorm = Normalize(blockSelector);

        int patchedBlocks = 0;
        int patchedPropsTotal = 0;

        var newBlocks = new List<SmbhBlock>(parsed.Blocks.Count);

        JsonNode? newValueNode;
        try
        {
            newValueNode = JsonNode.Parse(valueJson);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Provided valueJson is not valid JSON: {ex.Message}");
        }

        if (newValueNode == null)
            throw new InvalidDataException("valueJson parsed to null.");

        foreach (var b in parsed.Blocks)
        {
            if (!IsMatchSelector(b.Name, selectorNorm) || !JsonExtractor.LooksLikeJson(b.PayloadBytes))
            {
                newBlocks.Add(b);
                continue;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(Encoding.UTF8.GetString(b.PayloadBytes));
            }
            catch
            {
                newBlocks.Add(b);
                continue;
            }

            if (root == null)
            {
                newBlocks.Add(b);
                continue;
            }

            int patchedHere = ReplacePropertyRecursive(root, propName, newValueNode);

            if (patchedHere > 0)
            {
                var newJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                var newBytes = Encoding.UTF8.GetBytes(newJson);

                newBlocks.Add(new SmbhBlock(b.Name, b.NameBytes, newBytes));

                patchedBlocks++;
                patchedPropsTotal += patchedHere;
            }
            else
            {
                newBlocks.Add(b);
            }
        }

        if (patchedBlocks == 0)
            throw new InvalidDataException($"No JSON blocks matched selector '{blockSelector}' with replaceable property '{propName}'.");

        var rebuilt = SmbhParser.Build(parsed.PrefixBytes, newBlocks, parsed.SuffixBytes);

        SsfCodec.EncodeWithHeaderBytes(ssf.HeaderBytes, rebuilt, outputMapFile, chunkSize: SsfCodec.DefaultChunkSize);

        Console.WriteLine($"OK: patchedBlocks={patchedBlocks}, patchedProps={patchedPropsTotal}");
        Console.WriteLine($"OK: wrote {outputMapFile}");
    }

    private static int ReplacePropertyRecursive(JsonNode node, string propName, JsonNode newValue)
    {
        int patched = 0;

        if (node is JsonObject obj)
        {
            if (obj.ContainsKey(propName))
            {
                obj[propName] = newValue.DeepClone();
                patched++;
            }

            foreach (var kv in obj)
                if (kv.Value != null)
                    patched += ReplacePropertyRecursive(kv.Value, propName, newValue);
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
                if (child != null)
                    patched += ReplacePropertyRecursive(child, propName, newValue);
        }

        return patched;
    }

    private static string ExtractReplacementValueJson(string propertySnippet)
    {
        var wrapped = "{\n" + propertySnippet.Trim() + "\n}";
        var root = JsonNode.Parse(wrapped) as JsonObject
                   ?? throw new InvalidDataException("Replacement snippet did not parse as an object.");

        var val = root["Establish_Task_Build_Crane"];
        if (val == null) throw new InvalidDataException("Replacement snippet missing Establish_Task_Build_Crane.");
        return val.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static bool IsMatchSelector(string blockName, string selectorNorm)
    {
        if (string.IsNullOrWhiteSpace(selectorNorm)) return false;
        var bn = Normalize(blockName);
        return bn.Contains(selectorNorm, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string s)
        => (s ?? "").Trim().ToLowerInvariant().Replace("-", "_");
}
