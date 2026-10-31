using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

// Keep narration separate from headings, even though the game shares their fonts.
internal static class Program
{
    static readonly Dictionary<string, string> FontCopies = new()
    {
        ["ChapterTitleText"] = "CL_NarrationText",
        ["TelopText"] = "CL_OpeningNarrationText",
        ["UnipueText"] = "CL_BattleNameText"
    };
    static readonly Dictionary<string, string> CaptionFonts = new()
    {
        ["WBP_TalkEvent_Caption_OP"] = "TelopText",
        ["WBP_TalkEvent_Caption_telop"] = "ChapterTitleText",
        ["WBP_TalkEvent_Caption_telopB"] = "ChapterTitleText",
        ["WBP_TalkEvent_Caption_telopC"] = "ChapterTitleText"
    };
    static readonly Dictionary<string, string> StyleCopies = new()
    {
        ["BP_SystemTextStyle_PramNUM"] = "CL_BattleNumberStyle"
    };
    static readonly string[] Assets = ["WBP_SystemSubhead", "SystemTextNUM", "WBP_Btl_UnitStatus", .. FontCopies.Keys, .. CaptionFonts.Keys, .. StyleCopies.Keys];

    static void Main(string[] args)
    {
        if (args.Length < 4 || (args[0] == "build" ? args.Length != 4 : args[0] != "verify" || args.Length != 5))
            throw new ArgumentException("FontAssets build|verify <mappings> <source> <output> [original-source for verify]");
        var mappings = new Usmap(args[1]);
        foreach (var name in Assets)
        {
            // Nested widget schemas come from neighbouring original Blueprints.
            // The mod contains only overrides, so prime those schemas from the full
            // source extraction before checking the isolated converted assets.
            if (args[0] == "verify") Load(Find(args[4], name), mappings);
            var outputName = FontCopies.GetValueOrDefault(name, StyleCopies.GetValueOrDefault(name, name));
            var path = Find(args[2], args[0] == "verify" ? outputName : name);
            var original = Load(path, mappings);
            if (args[0] == "verify")
            {
                Require(JToken.DeepEquals(Contents(original), Contents(Load(Find(args[3], outputName), mappings))),
                    $"IoStore conversion changed {name}");
                continue;
            }
            var edited = Load(path, mappings);
            if (name == "SystemTextNUM") ExtendNumericCoverage(edited);
            else if (name == "WBP_Btl_UnitStatus") StyleBattlePanel(edited);
            else if (StyleCopies.ContainsKey(name))
            {
                var style = edited.Exports.OfType<NormalExport>().Single(e => e.ObjectName.ToString().StartsWith("Default__"));
                Field<FloatPropertyData>(Field<StructPropertyData>(style.Data, "Font").Value, "Size").Value = 15;
            }
            else if (name == "WBP_SystemSubhead")
                RetargetImport(edited, "Font", "/Game/UI/Font/", "SystemText", "TitleText_menu");
            else if (CaptionFonts.TryGetValue(name, out var captionFont))
                RetargetImport(edited, "Font", "/Game/UI/Font/", captionFont, FontCopies[captionFont]);
            else
                foreach (var (from, to) in ReplacementFaces(name))
                    RetargetImport(edited, "FontFace", "/Game/UI/Font/FontFace/SE/", from, to);
            VerifyScope(name, Contents(original), Contents(edited));
            // Clone the original composite font so other languages keep their own faces,
            // ranges and scaling. Only the Western faces differ in these new assets.
            if (FontCopies.ContainsKey(name))
            {
                edited.Exports.Single().ObjectName = FName.FromString(edited, outputName);
                edited.FolderName = new FString("/Game/UI/Font/" + outputName);
            }
            else if (StyleCopies.ContainsKey(name))
            {
                // Keep the shared numeric style intact; only battle HP uses this copy.
                foreach (var export in edited.Exports)
                    export.ObjectName = FName.FromString(edited, export.ObjectName.ToString().Replace(name, outputName));
                edited.FolderName = new FString(edited.FolderName.ToString().Replace(name, outputName));
            }
            var relative = Path.Combine(Path.GetDirectoryName(Path.GetRelativePath(args[2], path))!, outputName + ".uasset");
            var output = Path.Combine(args[3], relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            foreach (var entry in edited.GetNameMapIndexList()) edited.OverrideNameMapHashes[entry] = 0;
            var expected = Contents(edited);
            edited.Write(output);
            Require(JToken.DeepEquals(expected, Contents(Load(output, mappings))), $"Serialization changed {name}");
        }
        if (args[0] == "build")
            File.Copy(Path.Combine(args[2], "scriptobjects.bin"), Path.Combine(args[3], "scriptobjects.bin"));
        Console.WriteLine($"Verified {Assets.Length} font and caption assets ({args[0]}).");
    }

    static (string From, string To)[] ReplacementFaces(string name) => name switch
    {
        "ChapterTitleText" => [("SE-MLSDS", "SE-ALPSN"), ("SE-MLSDT", "SE-ALPSI")],
        "TelopText" => [("SE-MLSDT", "SE-ALPSN")],
        "UnipueText" => [("SE-ALPSCB", "SE-GARRITHR")],
        _ => throw new ArgumentException($"Unknown font copy: {name}")
    };

    static void StyleBattlePanel(UAsset asset)
    {
        RetargetImport(asset, "Font", "/Game/UI/Font/", "UnipueText", "CL_BattleNameText");
        RetargetImport(asset, "BlueprintGeneratedClass", "/Game/UI/Common/Text/TextStyle/SystemText/",
            "BP_SystemTextStyle_PramNUM", "CL_BattleNumberStyle");
        foreach (var name in new[] { "Text_HP_Title", "Text_MP_Title", "Text_MP_Value", "Text_Name" })
        {
            var data = asset.Exports.OfType<NormalExport>().Single(e => e.ObjectName.ToString() == name).Data;
            Field<FloatPropertyData>(Field<StructPropertyData>(data, "Font").Value, "Size").Value = 15;
            // Retain the shadow offset and colour, but reduce its opacity.
            // HP numbers inherit their existing styling from the dedicated size-15 copy.
            var shadow = Field<StructPropertyData>(data, "ShadowColorAndOpacity");
            var property = Field<LinearColorPropertyData>(shadow.Value, "ShadowColorAndOpacity");
            var color = property.Value;
            color.A = 0.45f;
            property.Value = color;
        }
    }

    static void RetargetImport(UAsset asset, string className, string folder, string from, string to)
    {
        var suffix = className == "BlueprintGeneratedClass" ? "_C" : "";
        var font = asset.Imports.Single(i => i.ClassName.ToString() == className && i.ObjectName.ToString() == from + suffix);
        var package = asset.Imports.Single(i => i.ObjectName.ToString() == folder + from);
        font.ObjectName = FName.FromString(asset, to + suffix);
        package.ObjectName = FName.FromString(asset, folder + to);
    }

    static void ExtendNumericCoverage(UAsset asset)
    {
        var data = ((NormalExport)asset.Exports.Single()).Data;
        var composite = Field<StructPropertyData>(data, "CompositeFont");
        var subfonts = Field<ArrayPropertyData>(composite.Value, "SubTypefaces");
        var western = subfonts.Value.Cast<StructPropertyData>().Single(
            row => Field<StrPropertyData>(row.Value, "Cultures").Value.ToString() == "en;de;fr;es;");
        var ranges = Field<ArrayPropertyData>(western.Value, "CharacterRanges");
        var range = (StructPropertyData)ranges.Value.Single();
        var lower = Field<IntPropertyData>(Field<StructPropertyData>(range.Value, "LowerBound").Value, "Value");
        var upper = Field<IntPropertyData>(Field<StructPropertyData>(range.Value, "UpperBound").Value, "Value");
        Require(lower.Value == 65 && upper.Value == 90, "Unexpected numeric character range");
        // Include ASCII numbers, punctuation and lowercase; leave other cultures alone.
        lower.Value = 32;
        upper.Value = 126;
    }

    static T Field<T>(IEnumerable<PropertyData> fields, string name) where T : PropertyData =>
        (T)fields.Single(p => p.Name.ToString() == name);

    static void VerifyScope(string name, JToken before, JToken after)
    {
        if (name == "WBP_Btl_UnitStatus")
        {
            VerifyBattlePanel(before, after);
            return;
        }
        var changes = new List<(JToken Before, JToken After)>();
        void Compare(JToken a, JToken b)
        {
            if (JToken.DeepEquals(a, b)) return;
            if (a is JObject x && b is JObject y)
            {
                Require(x.Properties().Select(p => p.Name).Order().SequenceEqual(y.Properties().Select(p => p.Name).Order()), "Changed object structure");
                foreach (var property in x.Properties()) Compare(property.Value, y[property.Name]!);
            }
            else if (a is JArray xs && b is JArray ys)
            {
                Require(xs.Count == ys.Count, "Changed array length");
                for (int i = 0; i < xs.Count; i++) Compare(xs[i], ys[i]);
            }
            else changes.Add((a, b));
        }
        Compare(before, after);
        static string FontPath(string font) => $"/Game/UI/Font/{font}.{font}";
        static string FacePath(string face) => $"/Game/UI/Font/FontFace/SE/{face}.{face}";
        (string, string)[] expected;
        if (name == "WBP_SystemSubhead")
            expected = [(FontPath("SystemText"), FontPath("TitleText_menu"))];
        else if (name == "SystemTextNUM")
            expected = [("65", "32"), ("90", "126")];
        else if (StyleCopies.ContainsKey(name))
            expected = [("18", "15")];
        else if (CaptionFonts.TryGetValue(name, out var captionFont))
            expected = [(FontPath(captionFont), FontPath(FontCopies[captionFont]))];
        else
            expected = ReplacementFaces(name).Select(pair => (FacePath(pair.From), FacePath(pair.To))).ToArray();
        Require(changes.Select(c => (c.Before.ToString(), c.After.ToString())).SequenceEqual(expected),
            $"Unexpected changes outside the intended font edit: {name}");
    }

    static void VerifyBattlePanel(JToken before, JToken after)
    {
        // Describe the allowed edits on a copy of the original. Every other field,
        // including bindings, materials, layout and animation bytecode, must match.
        var expected = before.DeepClone();
        static JToken Value(JToken fields, string name) => fields.Single(p => (string)p["Name"]! == name)!["Value"]!;
        foreach (var name in new[] { "Text_HP_Title", "Text_MP_Title", "Text_MP_Value", "Text_Name" })
        {
            var fields = expected[$"WBP_Btl_UnitStatus_C.WidgetTree.{name}"]!["data"]!;
            var size = Value(Value(fields, "Font"), "Size");
            var originalSize = name == "Text_Name" ? 18 : name == "Text_MP_Value" ? 16 : 15;
            Require((float)size == originalSize, "Unexpected battle text size");
            size.Replace(new JValue(15.0f));
            var shadow = Value(Value(fields, "ShadowColorAndOpacity"), "ShadowColorAndOpacity");
            Require(Math.Abs((float)shadow["A"]! - 0.8f) < 0.0001f, "Unexpected battle shadow opacity");
            shadow["A"] = 0.45f;
            if (name == "Text_Name")
            {
                var font = Value(Value(fields, "Font"), "FontObject");
                Require((string)font! == "/Game/UI/Font/UnipueText.UnipueText", "Unexpected battle name font");
                font.Replace(new JValue("/Game/UI/Font/CL_BattleNameText.CL_BattleNameText"));
            }
        }
        var hpFields = expected["WBP_Btl_UnitStatus_C.WidgetTree.Text_HP_Value"]!["data"]!;
        var hpStyle = Value(hpFields, "Style");
        const string styleFolder = "/Game/UI/Common/Text/TextStyle/SystemText/";
        Require((string)hpStyle! == styleFolder + "BP_SystemTextStyle_PramNUM.BP_SystemTextStyle_PramNUM_C", "Unexpected HP style");
        hpStyle.Replace(new JValue(styleFolder + "CL_BattleNumberStyle.CL_BattleNumberStyle_C"));
        Require(JToken.DeepEquals(expected, after), "Unexpected changes outside battle-panel typography");
    }

    static UAsset Load(string path, Usmap mappings)
    {
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
        Require(asset.Exports.All(e => e is NormalExport), $"Undecoded export: {path}: " + string.Join("; ", asset.Exports.Where(e => e is not NormalExport).Select(e => e.ObjectName + " " + e.GetType().Name)));
        Require(asset.VerifyBinaryEquality(), $"Lossless round-trip failed: {path}");
        return asset;
    }

    // Retoc can reorder imports and exports; compare resolved object names and data.
    static JObject Contents(UAsset asset)
    {
        var json = JObject.Parse(asset.SerializeJson());
        string Resolve(int index)
        {
            if (index == 0) return "";
            var obj = index < 0 ? json["Imports"]![-index - 1]! : json["Exports"]![index - 1]!;
            var outer = Resolve((int)obj["OuterIndex"]!);
            return (outer == "" ? "" : outer + ".") + (string)obj["ObjectName"]!;
        }
        JToken Normalize(JToken token)
        {
            if (token is JArray array) return new JArray(array.Select(Normalize));
            if (token is not JObject obj) return token.DeepClone();
            var result = new JObject();
            foreach (var property in obj.Properties())
            {
                bool reference = property.Name is "LocalFontFaceAsset" or "FontObject" or "FontMaterial" or "OutlineMaterial"
                    || (property.Name == "Value" && ((string)obj["$type"] ?? "").Contains("ObjectPropertyData"));
                result[property.Name] = reference && property.Value.Type == JTokenType.Integer
                    ? new JValue(Resolve((int)property.Value)) : Normalize(property.Value);
            }
            return result;
        }
        var exports = new JObject();
        for (int index = 0; index < asset.Exports.Count; index++)
        {
            var export = json["Exports"]![index]!;
            exports[Resolve(index + 1)] = new JObject
            {
                ["class"] = Resolve((int)export["ClassIndex"]!),
                ["data"] = Normalize(export["Data"]!),
                ["extras"] = export["Extras"]?.DeepClone()
            };
        }
        return exports;
    }

    static string Find(string root, string name) => Directory.GetFiles(root, name + ".uasset", SearchOption.AllDirectories).Single();
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
