using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Unversioned;

static partial class Program
{
    const float CommandWidth = 318;
    const float PartyWidth = 400;
    const float PartyRowHeight = 82;
    const float ContentHeight = PartyRowHeight * 4;
    const float DockPadding = 8;
    const float ColumnGap = 8;
    const float DockWidth = CommandWidth + PartyWidth + ColumnGap + DockPadding * 2;
    const float HelpHeight = 128;
    const float DockHeight = ContentHeight + DockPadding * 2 + HelpHeight;
    const float ScreenInset = 20;

    static NormalExport Native(string name, string type, params PropertyData[] fields)
    {
        var widget = AddFrom(E("WidgetTree"), name, Id(E("WidgetTree")), fields.ToList());
        widget.ClassIndex = Import("/Script/UMG", type, "Class");
        widget.TemplateIndex = Import("/Script/UMG", "Default__" + type, type, "/Script/UMG");
        widget.SerializationBeforeCreateDependencies = [widget.ClassIndex, widget.TemplateIndex];
        return widget;
    }

    static ArrayPropertyData Slots(NormalExport parent)
    {
        var slots = parent.Data.OfType<ArrayPropertyData>().SingleOrDefault(p => p.Name.ToString() == "Slots");
        if (slots != null) return slots;
        slots = new ArrayPropertyData(N("Slots")) { ArrayType = N("ObjectProperty"), Value = [] };
        parent.Data.Add(slots);
        return slots;
    }

    static void Detach(NormalExport child)
    {
        var reference = child.Data.OfType<ObjectPropertyData>().SingleOrDefault(p => p.Name.ToString() == "Slot");
        if (reference != null && reference.Value.Index > 0)
        {
            var slot = (NormalExport)a.Exports[reference.Value.Index - 1];
            var parent = (NormalExport)a.Exports[F<ObjectPropertyData>(slot.Data, "Parent").Value.Index - 1];
            var slots = Slots(parent);
            slots.Value = slots.Value.Where(p => ((ObjectPropertyData)p).Value.Index != reference.Value.Index).ToArray();
            for (int i = 0; i < slots.Value.Length; i++) slots.Value[i].Name = N(i.ToString());
            F<ObjectPropertyData>(slot.Data, "Content").Value = new(0);
            child.Data.Remove(reference);
        }
    }

    static NormalExport Attach(NormalExport parent, NormalExport child, string slotType, bool first = false)
    {
        Detach(child);
        var slot = Native(child.ObjectName + "_DockSlot", slotType, Obj("Parent", Id(parent)), Obj("Content", Id(child)));
        slot.OuterIndex = Id(parent);
        slot.CreateBeforeCreateDependencies = [Id(parent)];
        Put(child.Data, Obj("Slot", Id(slot)));
        var slots = Slots(parent);
        slots.Value = first ? [Obj("0", Id(slot)), ..slots.Value] : [..slots.Value, Obj("0", Id(slot))];
        for (int i = 0; i < slots.Value.Length; i++) slots.Value[i].Name = N(i.ToString());
        parent.CreateBeforeSerializationDependencies.Add(Id(slot));
        slot.CreateBeforeSerializationDependencies = [Id(child)];
        return slot;
    }

    static void Fill(NormalExport slot)
    {
        Put(slot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Fill"));
        Put(slot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Fill"));
    }

    static NormalExport OverlayChild(NormalExport parent, NormalExport child, bool first = false)
    {
        var slot = Attach(parent, child, "OverlaySlot", first);
        Fill(slot);
        return slot;
    }

    static void AttachSingle(NormalExport parent, NormalExport child, string slotType)
    {
        var slot = Attach(parent, child, slotType);
        Fill(slot);
    }

    static void SetBounds(NormalExport box, float width, float height)
    {
        SetFloat(box.Data, "WidthOverride", width);
        SetFloat(box.Data, "HeightOverride", height);
        Put(box.Data, Bool("bOverride_WidthOverride", true));
        Put(box.Data, Bool("bOverride_HeightOverride", true));
        Put(box.Data, Bool("bOverride_MaxDesiredHeight", false));
    }

    static NormalExport FixedBox(string name, float width, float height)
    {
        var box = Native(name, "SizeBox");
        SetBounds(box, width, height);
        return box;
    }

    static NormalExport Image(string name, float r, float g, float b, float alpha, string texture = null)
    {
        var brush = S("Brush", "SlateBrush", Enum("DrawAs", "ESlateBrushDrawType", texture == null ? "Image" : "Box"));
        if (texture != null)
        {
            var resource = Import("/Game/UI/Textures/Menu/" + texture, texture, "Texture2D", "/Script/Engine");
            brush.Value.Add(Obj("ResourceObject", resource));
            brush.Value.Add(S("Margin", "Margin", Float("Left", 0.5f), Float("Top", 0.5f), Float("Right", 0.5f), Float("Bottom", 0.5f)));
            brush.Value.Add(S("ImageSize", "DeprecateSlateVector2D",
                new DeprecateSlateVector2DPropertyData(N("ImageSize")) { Value = new FVector2f(32, 32) }));
        }
        return Native(name, "Image", brush, Color("ColorAndOpacity", r, g, b, alpha),
            Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
    }

    static void DockFrame()
    {
        var panel = E("Overlay_Padding");
        var bounds = FixedBox("BD_DockBounds", DockWidth, DockHeight);
        var layers = Native("BD_DockLayers", "Overlay");
        AttachSingle(bounds, layers, "SizeBoxSlot");
        OverlayChild(layers, Image("BD_DockBackground", 0.012f, 0.065f, 0.095f, 0.88f));
        var wash = MenuArt("BD_MenuWash", "T_CharaMenuListBg", 300, 128, 0.5f, 0.24f);
        OverlayChild(layers, wash);
        var divider = FixedBox("BD_ColumnDivider", 1, ContentHeight);
        AttachSingle(divider, Image("BD_DividerInk", 0.5f, 0.8f, 0.85f, 0.55f), "SizeBoxSlot");
        var dividerSlot = OverlayChild(layers, divider);
        Padding(dividerSlot.Data, l:DockPadding + CommandWidth + ColumnGap / 2, t:DockPadding);
        Put(dividerSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));
        Put(dividerSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Top"));
        var frameSlot = OverlayChild(panel, bounds, first:true);
        Padding(frameSlot.Data, t:ScreenInset, r:ScreenInset);
        Put(frameSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Right"));
        Put(frameSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Top"));

        // Draw the fine edge above the contents, outside their eight-unit inset.
        var borderBounds = FixedBox("BD_BorderBounds", DockWidth, DockHeight);
        Put(borderBounds.Data, Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
        AttachSingle(borderBounds, DecoratedFrame("BD_DockBorder", 0.7f), "SizeBoxSlot");
        var borderSlot = OverlayChild(panel, borderBounds);
        Padding(borderSlot.Data, t:ScreenInset, r:ScreenInset);
        Put(borderSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Right"));
        Put(borderSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Top"));

        var partySlot = D("OverlaySlot_0", "Overlay_Padding");
        Padding(partySlot, t:ScreenInset + DockPadding, r:ScreenInset + DockPadding);
        F<EnumPropertyData>(partySlot, "VerticalAlignment").Value = N("VAlign_Top");
        Padding(D("OverlaySlot_9", "Overlay_Padding"), t:ScreenInset + DockPadding,
            r:ScreenInset + DockPadding + PartyWidth + ColumnGap);
        Put(D("RU_BG"), Enum("Visibility", "ESlateVisibility", "Collapsed"));

        // A real scrollable footer hosts the existing tooltip content at native
        // font size. The small runtime adapter moves it here only during battle.
        var helpBounds = FixedBox("BD_HelpBounds", DockWidth - DockPadding * 2, HelpHeight - DockPadding);
        var helpScroll = Native("BD_HelpScroll", "ScrollBox", Bool("bIsVariable", true),
            Enum("Clipping", "EWidgetClipping", "ClipToBounds"));
        AttachSingle(helpBounds, helpScroll, "SizeBoxSlot");
        var helpSlot = OverlayChild(panel, helpBounds);
        Padding(helpSlot.Data, t:ScreenInset + ContentHeight + DockPadding * 2, r:ScreenInset + DockPadding);
        Put(helpSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Right"));
        Put(helpSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Top"));
        RegisterWidgetField(helpScroll);
        var helpRule = FixedBox("BD_HelpRule", DockWidth - 32, 1);
        AttachSingle(helpRule, Image("BD_HelpRuleInk", 0.6f, 0.8f, 0.85f, 0.5f), "SizeBoxSlot");
        var ruleSlot = OverlayChild(layers, helpRule);
        Padding(ruleSlot.Data, t:ContentHeight + DockPadding * 2, l:16);
        Put(ruleSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));
        Put(ruleSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Top"));

        // Keep the decorative edge last in draw order and non-interactive.
        var borderSlots = Slots(panel);
        var edge = borderSlots.Value.Single(p => ((ObjectPropertyData)p).Value.Index == Id(borderSlot).Index);
        borderSlots.Value = [..borderSlots.Value.Where(p => p != edge), edge];
        for (int i = 0; i < borderSlots.Value.Length; i++) borderSlots.Value[i].Name = N(i.ToString());
    }

    static void SeparateWindowContent()
    {
        // Keep the original object in the tree for its existing Blueprint calls,
        // but move its content alongside it so opacity affects only the old frame.
        var window = E("WBP_BtlWindow");
        var bindings = F<ArrayPropertyData>(window.Data, "NamedSlotBindings");
        var binding = (StructPropertyData)bindings.Value.Single();
        var content = (NormalExport)a.Exports[F<ObjectPropertyData>(binding.Value, "Content").Value.Index - 1];
        var oldSlot = (NormalExport)a.Exports[F<ObjectPropertyData>(window.Data, "Slot").Value.Index - 1];
        var layer = Native("BD_WindowContents", "Overlay");
        F<ObjectPropertyData>(oldSlot.Data, "Content").Value = Id(layer);
        Put(layer.Data, Obj("Slot", Id(oldSlot)));
        window.Data.RemoveAll(p => p.Name.ToString() == "Slot");
        bindings.Value = [];
        OverlayChild(layer, window);
        SetFloat(window.Data, "RenderOpacity", 0);
        OverlayChild(layer, content);
    }

    static void FixedCommandMenu(bool primary)
    {
        SetBounds(E("SizeBox_0"), CommandWidth, primary ? ContentHeight : ContentHeight - 24);
        SeparateWindowContent();
        if (!primary) return;
        // The limit header gets a stable footer instead of an empty strip on top.
        var slots = Slots(E("VerticalBox_0"));
        slots.Value = slots.Value.Reverse().ToArray();
        for (int i = 0; i < slots.Value.Length; i++) slots.Value[i].Name = N(i.ToString());
        Padding(D("VerticalBoxSlot_1", "VerticalBox_0"));
    }

    static void InlinePointRow()
    {
        // This widget is referenced only by the battle party cards. Preserve the
        // original named slots and value/gauge logic; change their arrangement.
        var row = Native("BD_PointLine", "Overlay");
        var bounds = FixedBox("BD_PointBounds", 232, 24);
        // Let the enclosing card provide width; only row height is fixed here.
        Put(bounds.Data, Bool("bOverride_WidthOverride", false));
        AttachSingle(bounds, row, "SizeBoxSlot");
        var gauge = OverlayChild(row, E("NamedSlot_Gauge"));
        Padding(gauge.Data, l:40, r:70);
        Put(gauge.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Center"));
        var title = OverlayChild(row, E("NamedSlot_Title"));
        Put(title.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));
        Put(title.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Center"));
        var value = OverlayChild(row, E("NamedSlot_Value"));
        Put(value.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Right"));
        Put(value.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Center"));
        F<ObjectPropertyData>(D("WidgetTree"), "RootWidget").Value = Id(bounds);
    }

    static void FinishFixedPartyCard(NormalExport content)
    {
        SeparateWindowContent();
        // Reuse the existing bIsInAction -> SetIsAction -> WBP_Subhead visibility
        // path. The highlight used to be inside the hidden character-name strip.
        var highlight = E("WBP_Subhead");
        var panel = E("Overlay_1");
        ReplaceImports("WBP_LeftLineBG", "BD_ClearBackground");
        var highlightSlot = OverlayChild(panel, highlight);
        Padding(highlightSlot.Data, l:1, t:1, r:1, b:1);
        Put(highlight.Data, Enum("Visibility", "ESlateVisibility", "Collapsed"));
        var glow = Native("BD_ActiveOutline", "Overlay", Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
        OverlayChild(glow, MenuArt("BD_ActiveShape", "T_mainmenu_Listlabel_Select", 128, 64, 0.5f, 0.65f));
        OverlayChild(glow, DecoratedFrame("BD_ActiveFrame", 0.95f));
        Put(highlight.Data, new ArrayPropertyData(N("NamedSlotBindings")) {
            ArrayType = N("StructProperty"), Value = [S("0", "NamedSlotBinding",
                new NamePropertyData(N("Name")) { Value = N("NamedSlot_63") }, Obj("Content", Id(glow)))]
        });

        // Status conditions get reserved space on the portrait, never another row.
        var conditions = E("WBP_UnitStatusConditionList");
        var conditionBounds = FixedBox("BD_ConditionBounds", 128, 16);
        var conditionScale = Native("BD_ConditionScale", "ScaleBox",
            Enum("Stretch", "EStretch", "ScaleToFit"), Enum("StretchDirection", "EStretchDirection", "DownOnly"));
        AttachSingle(conditionBounds, conditionScale, "SizeBoxSlot");
        AttachSingle(conditionScale, conditions, "ScaleBoxSlot");
        var conditionSlot = OverlayChild(content, conditionBounds);
        Padding(conditionSlot.Data, l:4, b:2);
        Put(conditionSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));
        Put(conditionSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Bottom"));
        var extraState = D("OverlaySlot_3", "Overlay_1");
        Padding(extraState, l:100, t:2);
        Put(extraState, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));

        var portraitFrame = FixedBox("BD_PortraitFrame", 128, 72);
        AttachSingle(portraitFrame, DecoratedFrame("BD_PortraitOrnament", 0.45f), "SizeBoxSlot");
        var portraitFrameSlot = OverlayChild(content, portraitFrame);
        Padding(portraitFrameSlot.Data, l:4, t:4, b:4);
        Put(portraitFrameSlot.Data, Enum("HorizontalAlignment", "EHorizontalAlignment", "HAlign_Left"));
        Put(portraitFrameSlot.Data, Enum("VerticalAlignment", "EVerticalAlignment", "VAlign_Center"));
        Put(portraitFrame.Data, Enum("Visibility", "ESlateVisibility", "HitTestInvisible"));
    }

    static void RegisterWidgetField(NormalExport widget)
    {
        var cls = a.Exports.OfType<ClassExport>().Single();
        var template = cls.LoadedProperties.OfType<FObjectProperty>().First();
        cls.LoadedProperties = [..cls.LoadedProperties, new FObjectProperty {
            Name = widget.ObjectName, PropertyClass = widget.ClassIndex,
            SerializedType = template.SerializedType, Flags = template.Flags,
            ArrayDim = template.ArrayDim, ElementSize = template.ElementSize,
            PropertyFlags = template.PropertyFlags, RepNotifyFunc = template.RepNotifyFunc
        }];
        var schema = Usmap.GetSchemaFromStructExport(cls, mappings.AreFNamesCaseInsensitive);
        foreach (var key in mappings.Schemas.Keys.Where(k => k == cls.ObjectName.ToString() || k.EndsWith("." + cls.ObjectName)).ToArray())
            mappings.Schemas[key] = schema;
        cls.SerializationBeforeCreateDependencies.Add(widget.ClassIndex);
    }
}
