using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

// UAssetAPI handles binary serialization. This tool only describes our menu edits.
internal static class Program
{
    const string GraphicsAsset = "DT_OP_GraphicSetting";
    const string ChoicesAsset = "DT_OP_GraphicSetting_Type";
    const string ChoicesPackage = "/Game/Datatable/UI/Option/Root/GraphicSetting/" + ChoicesAsset;
    const string SettingTag = "Delegate.UI.System.Option.Root.GraphicSetting.AAType";
    static readonly string[] Choices = ["Off", "FXAA", "TAA", "TSR"];

    static void Main(string[] args)
    {
        if (args.Length != 4 || (args[0] != "build" && args[0] != "verify"))
            throw new ArgumentException("Usage: MenuAssets build|verify <mappings> <source> <output>");
        var mappings = new Usmap(args[1]);
        if (args[0] == "build")
            Build(args[2], args[3], mappings);
        else
            VerifyConversion(args[2], args[3], mappings);
    }

    static void Build(string source, string output, Usmap mappings)
    {
        Require(!Directory.Exists(output), "Build folder must be fresh");
        var graphicsPath = FindAsset(source, GraphicsAsset);
        var choicesPath = FindAsset(source, "DT_OP_GraphicSetting_Rank");
        var graphicsOutput = Path.Combine(output, Path.GetRelativePath(source, graphicsPath));
        var choicesOutput = Path.Combine(Path.GetDirectoryName(graphicsOutput)!, ChoicesAsset + ".uasset");

        AddMethodRow(graphicsPath, graphicsOutput, mappings);
        BuildChoicesTable(choicesPath, choicesOutput, mappings);
        File.Copy(Path.Combine(source, "scriptobjects.bin"), Path.Combine(output, "scriptobjects.bin"));
        Console.WriteLine("Verified: all 10 original rows unchanged; Off / FXAA / TAA / TSR added.");
    }

    static void AddMethodRow(string source, string output, Usmap mappings)
    {
        var asset = Load(source, mappings);
        var table = Table(asset);
        var originalRows = (JArray)Contents(asset)["rows"]!;
        Require(originalRows.Count == 10, "Expected ten original graphics rows");
        var position = table.Table.Data.FindIndex(row => row.Name.ToString() == "NewRow_8");
        Require(position >= 0, "Original AA quality row not found");

        // Clone the existing row so the game supplies its normal menu behavior.
        var row = (StructPropertyData)table.Table.Data[position].Clone();
        row.Name = FName.FromString(asset, "NewRow_100");
        SetText(row, "DisplayText", "AA Method");
        Field<NamePropertyData>(Field<StructPropertyData>(row, "EntryTag"), "TagName").Value =
            FName.FromString(asset, SettingTag);
        SetText(row, "Command", "r.AntiAliasingMethod");
        Field<SoftObjectPropertyData>(row, "DT_RadioButton").Value = new FSoftObjectPath(
            FName.FromString(asset, ChoicesPackage), FName.FromString(asset, ChoicesAsset), null);
        Field<IntPropertyData>(row, "DefaultRadioButtonIndex").Value = 1;
        Field<BoolPropertyData>(row, "IsBranchInitValue").Value = false;
        SetText(row, "ItemDescription", "Choose the anti-aliasing method.");
        table.Table.Data.Insert(position + 1, row);

        WriteVerified(asset, output, mappings);
        var result = Load(output, mappings);
        var rows = (JArray)Contents(result)["rows"]!;
        Require(rows.Count == originalRows.Count + 1, "Unexpected graphics row count");
        foreach (var original in originalRows)
        {
            var unchanged = rows.Single(candidate => (string)candidate["Name"]! == (string)original["Name"]!);
            Require(JToken.DeepEquals(original, unchanged), $"Original row changed: {original["Name"]}");
        }
        var added = Table(result).Table.Data.Single(entry => entry.Name.ToString() == "NewRow_100");
        Require(Field<NamePropertyData>(Field<StructPropertyData>(added, "EntryTag"), "TagName")
            .Value.ToString() == SettingTag, "Incorrect option save tag");
        Require(Field<TextPropertyData>(added, "Command").CultureInvariantString.Value ==
            "r.AntiAliasingMethod", "Incorrect AA command");
        Require(Field<IntPropertyData>(added, "DefaultRadioButtonIndex").Value == 1, "Incorrect default choice");
        Require(!Field<BoolPropertyData>(added, "IsBranchInitValue").Value, "Native AA initialization enabled");
    }

    static void BuildChoicesTable(string source, string output, Usmap mappings)
    {
        var asset = Load(source, mappings);
        var names = asset.GetNameMapIndexList();
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index].Value;
            if (name.Contains("DT_OP_GraphicSetting_Rank"))
                asset.SetNameReference(index, new FString(name.Replace("_Rank", "_Type")));
        }
        asset.FolderName = new FString(ChoicesPackage);
        var rows = Table(asset).Table.Data;
        Require(rows.Count == Choices.Length, "Expected four choices in the template");
        for (var index = 0; index < Choices.Length; index++)
            SetText(rows[index], "DisplayText", Choices[index]);

        WriteVerified(asset, output, mappings);
        var labels = Table(Load(output, mappings)).Table.Data.Select(
            row => Field<TextPropertyData>(row, "DisplayText").CultureInvariantString.Value);
        Require(labels.SequenceEqual(Choices), "Incorrect AA choices or order");
    }

    static void SetText(StructPropertyData row, string field, string label)
    {
        var text = Field<TextPropertyData>(row, field);
        text.Flags = 0;
        text.HistoryType = TextHistoryType.Base;
        text.TableId = null;
        text.Namespace = new FString("");
        text.Value = new FString("AAProbe_" + label); // Preserve the released localization keys.
        text.CultureInvariantString = new FString(label);
    }

    // UAssetAPI's string indexer adds field names to the asset's name map.
    // Looking up the existing property directly preserves unversioned assets.
    static T Field<T>(StructPropertyData row, string name) where T : PropertyData =>
        (T)row.Value.Single(property => property.Name.ToString() == name);

    static UAsset Load(string path, Usmap mappings)
    {
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
        Table(asset); // Reject an opaque/raw export even if its bytes were preserved.
        Require(asset.VerifyBinaryEquality(), $"UAssetAPI cannot round-trip {path} unchanged");
        return asset;
    }

    static DataTableExport Table(UAsset asset) =>
        asset.Exports.Single() as DataTableExport
        ?? throw new InvalidDataException($"DataTable was not decoded: {asset.FilePath}");

    static void WriteVerified(UAsset asset, string output, Usmap mappings)
    {
        // Retoc-extracted names have zero hashes. Keep the released representation.
        foreach (var name in asset.GetNameMapIndexList())
            asset.OverrideNameMapHashes[name] = 0;
        var expected = Contents(asset);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        asset.Write(output);
        Require(JToken.DeepEquals(expected, Contents(Load(output, mappings))),
            $"Menu contents changed during serialization: {output}");
    }

    static JObject Contents(UAsset asset)
    {
        // Compare decoded data, not header offsets that retoc may reorganize.
        // JSON stays in memory; no reports or manifests are generated.
        var json = JObject.Parse(asset.SerializeJson());
        var rowStruct = (ObjectPropertyData)Table(asset).Data.Single(
            property => property.Name.ToString() == "RowStruct");
        return new JObject
        {
            ["package"] = asset.FolderName.Value,
            ["rowType"] = rowStruct.ToImport(asset).ObjectName.ToString(),
            ["rows"] = json["Exports"]![0]!["Table"]!["Data"]!.DeepClone(),
        };
    }

    static void VerifyConversion(string source, string extracted, Usmap mappings)
    {
        var originals = Directory.GetFiles(source, "*.uasset", SearchOption.AllDirectories);
        var converted = Directory.GetFiles(extracted, "*.uasset", SearchOption.AllDirectories);
        Require(originals.Length == 2 && converted.Length == 2, "Expected exactly two menu assets");
        foreach (var original in originals)
        {
            var restored = Path.Combine(extracted, Path.GetRelativePath(source, original));
            Require(JToken.DeepEquals(Contents(Load(original, mappings)), Contents(Load(restored, mappings))),
                $"IoStore round-trip mismatch: {Path.GetFileName(original)}");
        }
        Console.WriteLine("Verified: both menu tables survived IoStore conversion unchanged.");
    }

    static string FindAsset(string root, string name) =>
        Directory.GetFiles(root, name + ".uasset", SearchOption.AllDirectories).Single();

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
