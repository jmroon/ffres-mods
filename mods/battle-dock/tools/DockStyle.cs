using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

static partial class Program
{
    // Clone only the chain used by battle entries. Shared menu assets stay intact.
    static readonly Dictionary<string, string> StyleClones = new()
    {
        ["DA_LeftLineButton"] = "BD_ButtonStyle",
        ["WBP_LeftLineBG"] = "BD_ClearBackground",
        ["WBP_LeftLineButton"] = "BD_FlatButton",
        ["WBP_ActionEntryBase"] = "BD_ActionEntry",
        ["WBP_ItemEntry"] = "BD_ItemEntry",
        ["WBP_InventoryItemEntry"] = "BD_InventoryItemEntry",
    };

    static void ReplaceImports(string original, string replacement)
    {
        // Updating names in place also preserves references from compiled fields,
        // function operands, templates and dependency lists to these imports.
        foreach (var import in a.Imports)
        {
            if (import.ObjectName.ToString() is var name &&
                (name == original || name == original + "_C" || name == "Default__" + original + "_C" || name.EndsWith("/" + original)))
                import.ObjectName = N(name.Replace(original, replacement));
            if (import.ClassName.ToString() == original + "_C")
                import.ClassName = N(replacement + "_C");
            if (import.ClassPackage.ToString().EndsWith("/" + original))
                import.ClassPackage = N(import.ClassPackage.ToString().Replace(original, replacement));
        }
    }

    static NormalExport Edge(NormalExport parent, string name, bool vertical, bool far,
        float r, float g, float b, float alpha)
    {
        var box = FixedBox(name, 1, 1);
        Put(box.Data, Bool(vertical ? "bOverride_HeightOverride" : "bOverride_WidthOverride", false));
        Put(box.Data, Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
        AttachSingle(box, Image(name + "Ink", r, g, b, alpha), "SizeBoxSlot");
        var slot = OverlayChild(parent, box);
        Put(slot.Data, Enum(vertical ? "HorizontalAlignment" : "VerticalAlignment",
            vertical ? "EHorizontalAlignment" : "EVerticalAlignment",
            vertical ? (far ? "HAlign_Right" : "HAlign_Left") : (far ? "VAlign_Bottom" : "VAlign_Top")));
        return box;
    }

    static NormalExport Outline(string name, float r, float g, float b, float alpha)
    {
        var outline = Native(name, "Overlay", Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
        Edge(outline, name + "Left", true, false, r, g, b, alpha);
        Edge(outline, name + "Right", true, true, r, g, b, alpha);
        Edge(outline, name + "Top", false, false, r, g, b, alpha);
        Edge(outline, name + "Bottom", false, true, r, g, b, alpha);
        return outline;
    }

    static void BuildDockStyles()
    {
        Edit("DA_LeftLineButton", () => {
            var styles = F<MapPropertyData>(D("DA_LeftLineButton"), "StyleMap");
            foreach (var style in styles.Value.Values.Cast<StructPropertyData>())
            {
                Put(style.Value, Bool("bOverrideFocusBrush", true));
                var brush = MenuBrush("T_mainmenu_Listlabel_Select", 128, 64, 0.5f);
                brush.Name = N("FocusBrush");
                Put(style.Value, brush);
            }
        }, StyleClones["DA_LeftLineButton"]);
        Edit("WBP_LeftLineBG", () => {
            // Retain native style targets and callbacks, suppress only their ink.
            SetFloat(D("CPP_WBP_HorizontalGradient_573"), "RenderOpacity", 0);
            SetFloat(D("LineImage"), "RenderOpacity", 0);
        }, StyleClones["WBP_LeftLineBG"]);
        Edit("WBP_LeftLineButton", () => {
            ReplaceImports("WBP_LeftLineBG", StyleClones["WBP_LeftLineBG"]);
            ReplaceImports("DA_LeftLineButton", StyleClones["DA_LeftLineButton"]);
            Put(D("Image_Shadow"), Enum("Visibility", "ESlateVisibility", "Collapsed"));
            Padding(D("OverlaySlot_1", "Overlay_0"));
            var row = MenuArt("BD_RowFrame", "T_mainmenu_Listlabel", 128, 64, 0.3f, 0.68f);
            OverlayChild(E("Overlay_0"), row, first:true);
            // Keep the original focus/selection overlays and disabled-state logic.
        }, StyleClones["WBP_LeftLineButton"]);
        Edit("WBP_ActionEntryBase", () => ReplaceImports("WBP_LeftLineButton", StyleClones["WBP_LeftLineButton"]), StyleClones["WBP_ActionEntryBase"]);
        Edit("WBP_ItemEntry", () => ReplaceImports("WBP_ActionEntryBase", StyleClones["WBP_ActionEntryBase"]), StyleClones["WBP_ItemEntry"]);
        Edit("WBP_InventoryItemEntry", () => ReplaceImports("WBP_ItemEntry", StyleClones["WBP_ItemEntry"]), StyleClones["WBP_InventoryItemEntry"]);
        foreach (var entry in new[] { "WBP_BtlCmd_RootEntry", "WBP_BtlCmd_SkillEntry" })
            Edit(entry, () => ReplaceImports("WBP_ActionEntryBase", StyleClones["WBP_ActionEntryBase"]));
        Edit("WBP_BtlCmd_ItemEntry", () => ReplaceImports("WBP_InventoryItemEntry", StyleClones["WBP_InventoryItemEntry"]));
    }

    static StructPropertyData MenuBrush(string texture, float width, float height, float verticalMargin)
    {
        return S("Brush", "SlateBrush",
            Enum("DrawAs", "ESlateBrushDrawType", "Box"),
            Enum("ImageType", "ESlateBrushImageType", "FullColor"),
            Obj("ResourceObject", Import("/Game/UI/Textures/Menu/" + texture, texture, "Texture2D", "/Script/Engine")),
            S("Margin", "Margin", Float("Left", 0.5f), Float("Right", 0.5f), Float("Top", verticalMargin), Float("Bottom", verticalMargin)),
            S("ImageSize", "DeprecateSlateVector2D", new DeprecateSlateVector2DPropertyData(N("ImageSize")) { Value = new FVector2f(width, height) }));
    }

    static NormalExport MenuArt(string name, string texture, float width, float height, float margin, float opacity)
        => Native(name, "Image", MenuBrush(texture, width, height, margin),
            Color("ColorAndOpacity", 1, 1, 1, opacity), Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));

    static NormalExport DecoratedFrame(string name, float opacity)
    {
        // Small nine-slice corners retain the menu's ornament without its tall arch.
        return MenuArt(name, "T_CharaMenuListFrame", 24, 32, 0.5f, opacity);
    }
}
