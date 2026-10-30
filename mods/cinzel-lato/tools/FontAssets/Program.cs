using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

// Only two asset edits: serif subheadings and Latin coverage for the numeric font.
internal static class Program
{
    static readonly string[] Assets = ["WBP_SystemSubhead", "SystemTextNUM"];

    static void Main(string[] args)
    {
        if (args.Length != 4 || (args[0] != "build" && args[0] != "verify"))
            throw new ArgumentException("FontAssets build|verify <mappings> <source> <output>");
        var mappings = new Usmap(args[1]);
        foreach (var name in Assets)
        {
            var path = Find(args[2], name);
            var original = Load(path, mappings);
            if (args[0] == "verify")
            {
                Require(JToken.DeepEquals(Contents(original), Contents(Load(Find(args[3], name), mappings))),
                    $"IoStore conversion changed {name}");
                continue;
            }
            var edited = Load(path, mappings);
            if (name == "WBP_SystemSubhead") RetargetSubhead(edited);
            else ExtendNumericCoverage(edited);
            VerifyScope(name, Contents(original), Contents(edited));
            var output = Path.Combine(args[3], Path.GetRelativePath(args[2], path));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            foreach (var entry in edited.GetNameMapIndexList()) edited.OverrideNameMapHashes[entry] = 0;
            var expected = Contents(edited);
            edited.Write(output);
            Require(JToken.DeepEquals(expected, Contents(Load(output, mappings))), $"Serialization changed {name}");
        }
        if (args[0] == "build")
            File.Copy(Path.Combine(args[2], "scriptobjects.bin"), Path.Combine(args[3], "scriptobjects.bin"));
        Console.WriteLine($"Verified two font assets ({args[0]}).");
    }

    static void RetargetSubhead(UAsset asset)
    {
        var font = asset.Imports.Single(i => i.ClassName.ToString() == "Font" && i.ObjectName.ToString() == "SystemText");
        var package = asset.Imports.Single(i => i.ObjectName.ToString() == "/Game/UI/Font/SystemText");
        font.ObjectName = FName.FromString(asset, "TitleText_menu");
        package.ObjectName = FName.FromString(asset, "/Game/UI/Font/TitleText_menu");
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
        var expected = name == "WBP_SystemSubhead"
            ? new[] { ("/Game/UI/Font/SystemText.SystemText", "/Game/UI/Font/TitleText_menu.TitleText_menu") }
            : new[] { ("65", "32"), ("90", "126") };
        Require(changes.Select(c => (c.Before.ToString(), c.After.ToString())).SequenceEqual(expected),
            $"Unexpected changes outside the intended font edit: {name}");
    }

    static UAsset Load(string path, Usmap mappings)
    {
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
        Require(asset.Exports.All(e => e is NormalExport), $"Undecoded export: {path}");
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
