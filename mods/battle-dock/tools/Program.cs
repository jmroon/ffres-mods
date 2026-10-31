using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

// Experimental asset-only top dock. Existing battle logic and input widgets remain.
static partial class Program
{
    static string source, baseline, output;
    static Usmap mappings;
    static UAsset a;
    static readonly List<object> changes = new();
    static FName N(string name) => FName.FromString(a, name);
    static T F<T>(IEnumerable<PropertyData> fields, string name) where T:PropertyData => (T)fields.Single(p=>p.Name.ToString()==name);
    static NormalExport E(string name, string parent=null) => a.Exports.OfType<NormalExport>().Single(e=>e.ObjectName.ToString()==name && (parent==null || a.Exports[e.OuterIndex.Index-1].ObjectName.ToString()==parent));
    static FPackageIndex Id(Export e) => new(a.Exports.IndexOf(e)+1);
    static List<PropertyData> D(string name, string parent=null) => E(name,parent).Data;
    static StructPropertyData S(string name,string type,params PropertyData[] fields) => new(N(name),N(type)){Value=fields.ToList()};
    static FloatPropertyData Float(string name,float v) => new(N(name)){Value=v};
    static BoolPropertyData Bool(string name,bool v) => new(N(name)){Value=v};
    static ObjectPropertyData Obj(string name,FPackageIndex v) => new(N(name)){Value=v};
    static EnumPropertyData Enum(string name,string type,string v) => new(N(name)){EnumType=N(type),InnerType=N("ByteProperty"),Value=N(v)};
    static void Put(List<PropertyData> fields,PropertyData value) {int i=fields.FindIndex(p=>p.Name==value.Name);if(i<0)fields.Add(value);else fields[i]=value;}
    static void SetFloat(List<PropertyData> fields,string name,float value) => Put(fields,Float(name,value));
    static void Padding(List<PropertyData> fields,float l=0,float t=0,float r=0,float b=0) => Put(fields,S("Padding","Margin",Float("Left",l),Float("Top",t),Float("Right",r),Float("Bottom",b)));
    static StructPropertyData Vec(string name,double x,double y) => S(name,"Vector2D",new Vector2DPropertyData(N(name)){Value=new FVector2D(x,y)});
    static StructPropertyData Color(string name,float r,float g,float b,float alpha) => S(name,"LinearColor",new LinearColorPropertyData(N(name)){Value=new FLinearColor(r,g,b,alpha)});
    static string Find(string root,string name) => Directory.GetFiles(root,name+".uasset",SearchOption.AllDirectories).Single();
    static UAsset Load(string path) {
        var v=new UAsset(path,EngineVersion.VER_UE5_6,mappings);
        if(v.Exports.Any(e=>e is not NormalExport)||!v.VerifyBinaryEquality()) {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"failed-read.json"),v.SerializeJson());
            Console.WriteLine("Undecoded exports: "+string.Join(", ",v.Exports.Where(e=>e is not NormalExport).Select(e=>e.ObjectName)));
            throw new Exception("Not lossless: "+path);
        }
        return v;
    }
    static void Append(ArrayPropertyData array,PropertyData value) {value.Name=N(array.Value.Length.ToString());array.Value=[..array.Value,value];}
    static FPackageIndex Import(string package,string obj,string cls,string classPackage="/Script/CoreUObject")
    {
        int pi=a.Imports.FindIndex(i=>i.ObjectName.ToString()==package && i.OuterIndex.Index==0);
        if(pi<0){a.Imports.Add(new Import("/Script/CoreUObject","Package",new(0),package,false,a));pi=a.Imports.Count-1;}
        var outer=new FPackageIndex(-pi-1);
        int oi=a.Imports.FindIndex(i=>i.ObjectName.ToString()==obj && i.OuterIndex.Index==outer.Index);
        if(oi>=0)return new(-oi-1);
        a.Imports.Add(new Import(classPackage,cls,outer,obj,false,a));return new(-a.Imports.Count);
    }
    static NormalExport AddFrom(NormalExport template,string name,FPackageIndex outer,List<PropertyData> data)
    {
        var e=(NormalExport)template.Clone();e.ObjectName=N(name);e.OuterIndex=outer;e.Data=data;e.Extras=[];
        e.CreateBeforeSerializationDependencies=[];e.SerializationBeforeSerializationDependencies=[];
        e.SerializationBeforeCreateDependencies=[e.ClassIndex,e.TemplateIndex];e.CreateBeforeCreateDependencies=[outer];
        a.Exports.Add(e);return e;
    }
    static void Edit(string name,Action patch,string rename=null)
    {
        var original=Find(source,name);Load(original); // Prime nested Blueprint schemas.
        var basePath=Directory.GetFiles(baseline,name+".uasset",SearchOption.AllDirectories).SingleOrDefault()??original;
        a=Load(basePath);var before=Contents(a);
        JArray Functions() => new(JObject.Parse(a.SerializeJson())["Exports"]!.Where(e=>((string)e["$type"]!).Contains("FunctionExport")));
        var functions=Functions();
        patch();
        if(!JToken.DeepEquals(functions,Functions()))throw new Exception("Unexpected bytecode edit: "+name);
        if(rename!=null){foreach(var e in a.Exports)e.ObjectName=N(e.ObjectName.ToString().Replace(name,rename));a.FolderName=new FString(a.FolderName.ToString().Replace(name,rename));}
        a.ResolveAncestries();foreach(var n in a.GetNameMapIndexList())a.OverrideNameMapHashes[n]=0;
        string rel=Path.GetRelativePath(source,original);if(rename!=null)rel=Path.Combine(Path.GetDirectoryName(rel)!,rename+".uasset");
        string dest=Path.Combine(output,rel);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var expected=Contents(a);a.Write(dest);
        var diagnostics=Path.Combine(Path.GetDirectoryName(output)!,"edited-json");Directory.CreateDirectory(diagnostics);
        File.WriteAllText(Path.Combine(diagnostics,(rename??name)+".json"),a.SerializeJson());
        var read=Contents(Load(dest));
        if(!JToken.DeepEquals(expected,read)){File.WriteAllText(Path.Combine(output,"expected.json"),expected.ToString());File.WriteAllText(Path.Combine(output,"actual.json"),read.ToString());throw new Exception("Serialization changed "+name);}
        changes.Add(new {asset=rel,before,after=expected});Console.WriteLine("Checked "+name);
    }
    static void Main(string[] args)
    {
        mappings=new Usmap(args[1]);source=args[2];baseline=args[3];output=args[4];
        if(args[0]=="font-baseline") { BuildDefaultSizeFonts(); return; }
        if(args[0]=="verify-fonts") { VerifyFonts(); return; }
        if(args[0]=="verify")
        {
            foreach(var clone in StyleClones) {
                Load(Find(source,clone.Key));
                Load(Find(baseline,clone.Value));
            }
            foreach(var path in Directory.GetFiles(baseline,"*.uasset",SearchOption.AllDirectories))
            {
                string name=Path.GetFileNameWithoutExtension(path);
                var orig=Directory.GetFiles(source,name+".uasset",SearchOption.AllDirectories).SingleOrDefault();
                if(orig!=null)Load(orig);
                // Prime the custom portrait's original schema before reading the clone.
                if(name=="BD_Portrait")Load(Find(source,"WBP_UnitList_UnitIcon"));
                var expected=Contents(Load(path));var actual=Contents(Load(Find(output,name)));
                if(!JToken.DeepEquals(expected,actual)){File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"verify-expected.json"),expected.ToString());File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"verify-actual.json"),actual.ToString());throw new Exception("IoStore changed "+name);}
            }
            Console.WriteLine("All dock assets match IoStore readback.");return;
        }
        Directory.CreateDirectory(output);
        BuildDockStyles();
        Edit("WBP_UnitList_UnitIcon",()=>{
            SetFloat(D("SizeBox_2"),"WidthOverride",128);SetFloat(D("SizeBox_2"),"HeightOverride",72);
            Put(D("SizeBox_2"),Enum("Clipping","EWidgetClipping","ClipToBounds"));
            var tr=F<StructPropertyData>(D("Image"),"RenderTransform").Value;
            // Fill the crop vertically and centre the face within the portrait strip.
            Put(tr,Vec("Translation",-112,-18));Put(tr,Vec("Scale",0.72,0.72));
        },"BD_Portrait");
        Edit("WBP_Btl_UnitStatus",PartyCard);
        Edit("WBP_Btl_UnitStatusList",()=>{
            var grid=F<StructPropertyData>(D("WBP_DirectionLayoutMenu"),"GridProperty").Value;
            F<EnumPropertyData>(grid,"GridDirection").Value=N("Vertical");
            SetFloat(grid,"SpaceBetweenEntryInGridDirection",0);
            F<EnumPropertyData>(grid,"EntryVerticalAlignment").Value=N("VAlign_Top");
        });
        Edit("WBP_BattleLayout",DockFrame);
        Edit("WBP_BtlCmd_ListFrame",()=>{
            Padding(D("OverlaySlot_1","Overlay_3"));
            Padding(D("OverlaySlot_0","Overlay_3"));
            var root=E("Overlay_3");
            var box=FixedBox("BD_CommandBounds",CommandWidth,ContentHeight);
            AttachSingle(box,root,"SizeBoxSlot");
            F<ObjectPropertyData>(D("WidgetTree"),"RootWidget").Value=Id(box);
        });
        Edit("WBP_BtlCmd_PrimaryList",()=>FixedCommandMenu(true));
        Edit("WBP_BtlCmd_SecondaryList",()=>FixedCommandMenu(false));
        Edit("WBP_Btl_UnitState_Point",InlinePointRow);
        Edit("WBP_BtlCmd_ListHeader",()=>{
            SetFloat(D("SizeBox_0"),"HeightOverride",24);
            Put(D("WBP_BtlCmd_ListUnit"),Enum("Visibility","ESlateVisibility","Collapsed"));
            Put(D("CPP_WBP_HorizontalGradient_125"),Enum("Visibility","ESlateVisibility","Collapsed"));
            // The limit key guide and readiness effects remain in this shallow header.
        });
        Edit("WBP_Btl_TimeLine",()=>{
            F<Vector2DPropertyData>(F<StructPropertyData>(D("Spacer_127"),"Size").Value,"Size").Value=new FVector2D(110,1);
        });
        Edit("WBP_LimitGauge",()=>{
            // Preserve its readiness effects and gauge, reducing only empty padding.
            Padding(D("OverlaySlot_0","Overlay_1"),t:4,b:4);
        });
        // Smaller timeline step spacing moves portraits with their actual turn markers.
        Edit("DA_BtlTimeLine",Timeline);
        // Restore the shared window to its original appearance. Only dock instances
        // suppress their individual frames; other battle windows remain unaffected.
        Edit("WBP_BtlWindow",()=>{});
        File.Copy(Path.Combine(source,"scriptobjects.bin"),Path.Combine(output,"scriptobjects.bin"),true);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"asset-changes.json"),JsonConvert.SerializeObject(changes,Formatting.Indented));
    }
    static void Timeline()
    {
        SetFloat(D("DA_BtlTimeLine"),"EntrySpacing",52);
    }
    static void PrimaryMenu()
    {
        SetFloat(D("SizeBox_0"),"WidthOverride",310);
        // Frame only the action list. When it collapses for a submenu, its frame
        // also has zero height; the separate limit header remains available.
        var column=E("VerticalBox_0");
        var window=E("WBP_BtlWindow");
        var list=E("WBP_List");
        var rootSlot=E("SizeBoxSlot_0","SizeBox_0");
        var listSlot=E("PanelSlot_0","CPP_CommonContentSlot_120");
        F<ObjectPropertyData>(rootSlot.Data,"Content").Value=Id(column);
        Put(column.Data,Obj("Slot",Id(rootSlot)));
        F<ObjectPropertyData>(listSlot.Data,"Content").Value=Id(window);
        Put(window.Data,Obj("Slot",Id(listSlot)));
        var binding=(StructPropertyData)F<ArrayPropertyData>(window.Data,"NamedSlotBindings").Value.Single();
        F<ObjectPropertyData>(binding.Value,"Content").Value=Id(list);
        list.Data.RemoveAll(p=>p.Name.ToString()=="Slot");
    }
    static void PartyCard()
    {
        SetFloat(D("SizeBox_0"),"WidthOverride",PartyWidth);
        SetFloat(D("SizeBox_0"),"HeightOverride",PartyRowHeight);
        Put(D("SizeBox_0"),Bool("bOverride_HeightOverride",true));
        Put(D("Overlay_Unit"),Enum("Visibility","ESlateVisibility","Collapsed"));
        var content=E("VerticalBox_0");
        var bgBinding=F<StructPropertyData>(F<ArrayPropertyData>(D("WBP_BtlWindow"),"NamedSlotBindings").Value.Cast<PropertyData>(),"0");
        // Wrap current stats in a new overlay so the portrait is beside them.
        var tree=Id(E("WidgetTree"));
        var overlay=AddFrom(E("Overlay_Unit"),"BD_Content",tree,[]);
        var statsSlot=AddFrom(E("OverlaySlot_0","Overlay_1"),"BD_StatsSlot",Id(overlay),[Obj("Parent",Id(overlay)),Obj("Content",Id(content))]);
        Padding(statsSlot.Data,l:132,t:5,r:5,b:5);
        Put(statsSlot.Data,Enum("HorizontalAlignment","EHorizontalAlignment","HAlign_Fill"));
        Put(statsSlot.Data,Enum("VerticalAlignment","EVerticalAlignment","VAlign_Fill"));
        Put(content.Data,Obj("Slot",Id(statsSlot)));
        var portraitClass=Import("/Game/UI/Common/UnitList/Widget/Entry/Parts/Icon/BD_Portrait","BD_Portrait_C","WidgetBlueprintGeneratedClass","/Script/UMG");
        var portraitCDO=Import("/Game/UI/Common/UnitList/Widget/Entry/Parts/Icon/BD_Portrait","Default__BD_Portrait_C","BD_Portrait_C","/Game/UI/Common/UnitList/Widget/Entry/Parts/Icon/BD_Portrait");
        var portrait=AddFrom(E("WBP_Btl_StatusExStateIcon"),"BD_Portrait",tree,[Bool("bIsVariable",true)]);
        portrait.ClassIndex=portraitClass;portrait.TemplateIndex=portraitCDO;
        portrait.SerializationBeforeCreateDependencies=[portraitClass,portraitCDO];
        var portraitSlot=AddFrom(E("OverlaySlot_0","Overlay_1"),"BD_PortraitSlot",Id(overlay),[Obj("Parent",Id(overlay)),Obj("Content",Id(portrait))]);
        Padding(portraitSlot.Data,l:4,t:4,b:4);
        Put(portraitSlot.Data,Enum("HorizontalAlignment","EHorizontalAlignment","HAlign_Left"));
        Put(portraitSlot.Data,Enum("VerticalAlignment","EVerticalAlignment","VAlign_Center"));
        Put(portrait.Data,Obj("Slot",Id(portraitSlot)));
        Put(overlay.Data,new ArrayPropertyData(N("Slots")){ArrayType=N("ObjectProperty"),Value=[Obj("0",Id(portraitSlot)),Obj("1",Id(statsSlot))]});
        F<ObjectPropertyData>(bgBinding.Value,"Content").Value=Id(overlay);
        overlay.CreateBeforeSerializationDependencies=[Id(portraitSlot),Id(statsSlot)];
        portrait.CreateBeforeSerializationDependencies=[Id(portraitSlot)];statsSlot.CreateBeforeSerializationDependencies=[Id(content)];portraitSlot.CreateBeforeSerializationDependencies=[Id(portrait)];
        FinishFixedPartyCard(overlay);
        // The card's active animation used to lift it into its neighbour. Keep its
        // timing/bindings but remove only the translation; colour feedback remains.
        foreach(var prop in D("MovieScene2DTransformSection_0").OfType<StructPropertyData>().Where(p=>p.Name.ToString()=="Translation"))
            foreach(var c in prop.Value.OfType<MovieSceneFloatChannelPropertyData>())
                for(int i=0;i<c.Value.Values.Length;i++)c.Value.Values[i].Value=0;
        BindPortrait(portraitClass);
    }
    static void AddCardBackdrop(FPackageIndex tree)
    {
        // A local solid backing keeps the translucent stock selection frame readable
        // against bright scenery. It sits behind the original window and contents.
        var panel=E("Overlay_1");
        var imageClass=Import("/Script/UMG","Image","Class");
        var imageCDO=Import("/Script/UMG","Default__Image","Image","/Script/UMG");
        var image=AddFrom(E("WBP_Btl_StatusExStateIcon"),"BD_Backdrop",tree,[
            S("Brush","SlateBrush",Enum("DrawAs","ESlateBrushDrawType","Image")),
            Color("ColorAndOpacity",0.012f,0.065f,0.095f,0.78f),
            Enum("Visibility","ESlateVisibility","HitTestInvisible")]);
        image.ClassIndex=imageClass;image.TemplateIndex=imageCDO;
        image.SerializationBeforeCreateDependencies=[imageClass,imageCDO];
        var slot=AddFrom(E("OverlaySlot_0","Overlay_1"),"BD_BackdropSlot",Id(panel),[
            Obj("Parent",Id(panel)),Obj("Content",Id(image)),
            Enum("HorizontalAlignment","EHorizontalAlignment","HAlign_Fill"),
            Enum("VerticalAlignment","EVerticalAlignment","VAlign_Fill")]);
        Put(image.Data,Obj("Slot",Id(slot)));
        var slots=F<ArrayPropertyData>(panel.Data,"Slots");
        slots.Value=[Obj("0",Id(slot)),..slots.Value];
        for(int i=0;i<slots.Value.Length;i++)slots.Value[i].Name=N(i.ToString());
        panel.CreateBeforeSerializationDependencies.Add(Id(slot));
        image.CreateBeforeSerializationDependencies=[Id(slot)];
        slot.CreateBeforeSerializationDependencies=[Id(image)];
    }
    static void BindPortrait(FPackageIndex portraitClass)
    {
        var cls=a.Exports.OfType<ClassExport>().Single();
        var template=cls.LoadedProperties.OfType<FObjectProperty>().First(p=>p.Name.ToString()=="WBP_Btl_StatusExStateIcon");
        var field=new FObjectProperty {Name=N("BD_Portrait"),PropertyClass=portraitClass,SerializedType=template.SerializedType,Flags=template.Flags,
            ArrayDim=template.ArrayDim,ElementSize=template.ElementSize,PropertyFlags=template.PropertyFlags,RepNotifyFunc=template.RepNotifyFunc};
        cls.LoadedProperties=[..cls.LoadedProperties,field];
        // A new reflected property shifts inherited unversioned field indices.
        // Refresh the write schema before serializing this class's default object.
        var schema=Usmap.GetSchemaFromStructExport(cls,mappings.AreFNamesCaseInsensitive);
        foreach(var key in mappings.Schemas.Keys.Where(k=>k==cls.ObjectName.ToString()||k.EndsWith("."+cls.ObjectName)).ToArray())mappings.Schemas[key]=schema;
        cls.SerializationBeforeCreateDependencies.Add(portraitClass);
        var vm=D("MVVMViewClass_0");var library=F<StructPropertyData>(vm,"BindingLibrary").Value;
        var groups=F<ArrayPropertyData>(library,"CompiledFields");var names=F<ArrayPropertyData>(library,"CompiledFieldNames");
        var paths=F<ArrayPropertyData>(library,"FieldPaths");
        int propCount=groups.Value.Cast<StructPropertyData>().Sum(g=>F<Int16PropertyData>(g.Value,"NumberOfProperties").Value);
        int funcCount=groups.Value.Cast<StructPropertyData>().Sum(g=>F<Int16PropertyData>(g.Value,"NumberOfFunctions").Value);
        var unitBase=Import("/Script/mucha","CPP_VM_UnitBase","Class");
        void Group(FPackageIndex owner,string name,bool property)
        {
            var group=(StructPropertyData)groups.Value[0].Clone();
            F<ObjectPropertyData>(group.Value,"ClassOrScriptStruct").Value=owner;
            F<Int16PropertyData>(group.Value,"LibraryStartIndex").Value=(short)names.Value.Length;
            F<Int16PropertyData>(group.Value,"NumberOfProperties").Value=(short)(property?1:0);
            F<Int16PropertyData>(group.Value,"NumberOfFunctions").Value=(short)(property?0:1);
            Append(groups,group);Append(names,new NamePropertyData(N("0")){Value=N(name)});
        }
        Group(unitBase,"UnitId",true);Group(Id(cls),"BD_Portrait",true);Group(portraitClass,"SetUnitId",false);
        var sourceModel=F<ArrayPropertyData>(vm,"Sources").Value.Cast<StructPropertyData>().Single(s=>F<NamePropertyData>(s.Value,"PropertyName").Value.ToString()=="CPP_VM_BtlUnitParam");
        int sourceIndex=Array.IndexOf(F<ArrayPropertyData>(vm,"Sources").Value,sourceModel);
        void Path(int index,bool property,bool obj)
        {
            var p=(StructPropertyData)paths.Value[0].Clone();
            F<Int16PropertyData>(p.Value,"Index").Value=(short)index;
            F<BoolPropertyData>(p.Value,"bIsObjectProperty").Value=obj;
            F<BoolPropertyData>(p.Value,"bIsScriptStructProperty").Value=false;
            F<BoolPropertyData>(p.Value,"bIsProperty").Value=property;Append(paths,p);
        }
        int start=paths.Value.Length;
        Path(0,true,true);Path(propCount,true,false);Path(propCount+1,true,true);Path(funcCount,false,false);
        var bindings=F<ArrayPropertyData>(vm,"Bindings");
        var binding=(StructPropertyData)bindings.Value.Cast<StructPropertyData>().First(b=>F<StructPropertyData>(b.Value,"Binding").Value.Any()).Clone();
        var compiled=F<StructPropertyData>(binding.Value,"Binding").Value;
        void Range(string name,int offset,int length) {var d=F<StructPropertyData>(compiled,name).Value;F<Int16PropertyData>(d,"StartIndex").Value=(short)offset;F<Int16PropertyData>(d,"Num").Value=(short)length;}
        Range("SourceFieldPath",start,2);Range("DestinationFieldPath",start+2,2);Range("ConversionFunctionFieldPath",-1,-1);
        // Copy the direct assignment flags/types from the existing command-header portrait binding.
        F<BytePropertyData>(binding.Value,"Flags").Value=9;
        F<EnumPropertyData>(binding.Value,"ExecutionMode").Value=N("DelayedWhenSharedElseImmediate");
        F<BytePropertyData>(compiled,"Type").Value=0;
        var bit=binding.Value.Single(p=>p.Name.ToString()=="SourceBitField");
        if(bit is UInt64PropertyData u)u.Value=1UL<<sourceIndex;else throw new Exception("Unexpected source mask "+bit.GetType());
        int bindingIndex=bindings.Value.Length;Append(bindings,binding);
        var fields=F<ArrayPropertyData>(sourceModel.Value,"FieldToRegisterTo");var id=(StructPropertyData)fields.Value[0].Clone();F<NamePropertyData>(id.Value,"FieldName").Value=N("UnitId");Append(fields,id);
        var triggers=F<ArrayPropertyData>(sourceModel.Value,"Bindings");var trigger=(StructPropertyData)triggers.Value[0].Clone();
        F<NamePropertyData>(F<StructPropertyData>(trigger.Value,"FieldId").Value,"FieldName").Value=N("UnitId");
        F<IntPropertyData>(F<StructPropertyData>(trigger.Value,"BindingKey").Value,"Index").Value=bindingIndex;Append(triggers,trigger);
    }
    static JObject Contents(UAsset asset)
    {
        var json=JObject.Parse(asset.SerializeJson());
        string Resolve(int index){if(index==0)return "";var o=index<0?json["Imports"]![-index-1]!:json["Exports"]![index-1]!;var parent=Resolve((int)o["OuterIndex"]!);return (parent==""?"":parent+".")+(string)o["ObjectName"]!;}
        JToken Norm(JToken t){if(t is JArray arr){var items=arr.Select(Norm);if(arr.Count>0&&arr.All(x=>x is JObject&&x["Name"]!=null))items=items.OrderBy(x=>(string)x["Name"]).ThenBy(x=>(int?)x["ArrayIndex"]??0);return new JArray(items);}if(t is not JObject o)return t.DeepClone();var r=new JObject();foreach(var p in o.Properties()){if(p.Name=="IsZero")continue;bool reference=p.Name is "PropertyClass" or "ClassWithin" or "SuperStruct" or "ClassDefaultObject" || (p.Name=="Value"&&((string)o["$type"]??"").Contains("ObjectPropertyData"));r[p.Name]=reference&&p.Value.Type==JTokenType.Integer?new JValue(Resolve((int)p.Value)):Norm(p.Value);}return r;}
        var result=new JObject();for(int i=0;i<asset.Exports.Count;i++){var e=json["Exports"]![i]!;var item=new JObject{["class"]=Resolve((int)e["ClassIndex"]!),["data"]=Norm(e["Data"]!),["extras"]=e["Extras"]?.DeepClone()};if(e["LoadedProperties"]!=null)item["fields"]=Norm(e["LoadedProperties"]!);result[Resolve(i+1)]=item;}return result;
    }
}
