using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

static partial class Program
{
    static readonly Dictionary<string, string> FontOriginals = new() {
        ["CL_NarrationText"] = "ChapterTitleText",
        ["CL_OpeningNarrationText"] = "TelopText",
        ["CL_BattleNameText"] = "UnipueText",
        ["CL_BattleNumberStyle"] = "BP_SystemTextStyle_PramNUM",
    };

    static void VerifyFonts()
    {
        foreach (var path in Directory.GetFiles(baseline, "*.uasset", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Load(Find(source, FontOriginals.GetValueOrDefault(name, name)));
            var expected = Contents(Load(path));
            var actual = Contents(Load(Find(output, name)));
            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(expected, actual))
                throw new Exception("Font IoStore readback failed: " + name);
        }
        Console.WriteLine("All font-only assets match IoStore readback.");
    }

    // Start with the eleven font/role overrides, not the 79-asset smaller-UI test.
    // Keep font faces and narration routing; restore authored sizes exactly,
    // including omitted sizes that inherit their defaults.
    static void BuildDefaultSizeFonts()
    {
        foreach (var path in Directory.GetFiles(baseline, "*.uasset", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var originalName = FontOriginals.GetValueOrDefault(name, name);
            var original = Load(Find(source, originalName));
            var originalJson = Path.Combine(Path.GetDirectoryName(output)!, "original-font-json");
            Directory.CreateDirectory(originalJson);
            File.WriteAllText(Path.Combine(originalJson, originalName + ".json"), original.SerializeJson());
            a = Load(path);
            string ExportPath(UAssetAPI.UAsset asset, NormalExport export)
                => (export.OuterIndex.Index > 0 ? ExportPath(asset, (NormalExport)asset.Exports[export.OuterIndex.Index - 1]) + "." : "") + export.ObjectName;
            foreach (var export in a.Exports.OfType<NormalExport>())
            {
                var originalExport = original.Exports.OfType<NormalExport>().Single(e =>
                    ExportPath(original, e) == ExportPath(a, export).Replace(name, originalName));
                RestoreSizes(export.Data, originalExport.Data);
            }
            a.ResolveAncestries();
            var expected = Contents(a);
            var target = Path.Combine(output, Path.GetRelativePath(baseline, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            a.Write(target);
            var diagnostics = Path.Combine(Path.GetDirectoryName(output)!, "default-font-json");
            Directory.CreateDirectory(diagnostics);
            File.WriteAllText(Path.Combine(diagnostics, name + ".json"), a.SerializeJson());
            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(expected, Contents(Load(target))))
                throw new Exception("Font default-size readback failed: " + name);
            Console.WriteLine("Restored default sizes: " + name);
        }
        File.Copy(Path.Combine(source, "scriptobjects.bin"), Path.Combine(output, "scriptobjects.bin"), true);
    }

    static void RestoreSizes(List<PropertyData> edited, List<PropertyData> original)
    {
        foreach (var font in edited.OfType<StructPropertyData>().ToArray())
        {
            var old = original.OfType<StructPropertyData>().SingleOrDefault(p => p.Name.ToString() == font.Name.ToString() && p.ArrayIndex == font.ArrayIndex);
            if (old == null) continue;
            if (font.StructType.ToString() == "SlateFontInfo")
            {
                font.Value.RemoveAll(p => p.Name.ToString() == "Size");
                var size = old.Value.SingleOrDefault(p => p.Name.ToString() == "Size");
                if (size != null) font.Value.Add((PropertyData)size.Clone());
            }
            else RestoreSizes(font.Value, old.Value);
        }
    }
}
