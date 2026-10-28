using System.Xml.Linq;
class Program
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static void Main(string[] args)
    {
        string root=Directory.GetCurrentDirectory();int expectedCount=200;
        for(int i=0;i<args.Length;i++)
        {
            if(args[i]=="--root")root=args[++i];
            else if(args[i]=="--expected-count")expectedCount=int.Parse(args[++i]);
            else throw new Exception("Unknown argument "+args[i]);
        }
        var mods=XDocument.Load(Path.Combine(root,"Config/_Vehicles/camper_wrap_collection_item_modifiers.xml")).Descendants("item_modifier").ToArray();
        Check(mods.Length==expectedCount,"Expected item modifier count");
        Check(XDocument.Load(Path.Combine(root,"Config/item_modifiers.xml")).Descendants("include").Any(i=>(string)i.Attribute("filename")=="_Vehicles/camper_pattern_item_modifiers.xml"),"Native root includes existing camper entry point");
        string collectionPath=Path.GetFullPath(Path.Combine(root,"Config/_Vehicles/camper_wrap_collection_item_modifiers.xml"));
        Check(XDocument.Load(Path.Combine(root,"Config/_Vehicles/camper_pattern_item_modifiers.xml")).Descendants("include").Any(i=>Path.GetFullPath(Path.Combine(root,"Config/_Vehicles",(string)i.Attribute("filename")))==collectionPath),"Existing camper include resolves to actual collection file");
        var value=new NativeItem();var vehicle=new EntityVehicle{vehicle=new NativeVehicle{Value=value}};
        var names=new HashSet<string>(StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var mod in mods)
        {
            string name=(string)mod.Attribute("name");
            string id=mod.Elements("property").Single(p=>(string)p.Attribute("name")=="RebirthCamperWrapId").Attribute("value").Value;
            Check(names.Add(name)&&ids.Add(id),"Unique native name and selected ID");
            Check(((string)mod.Attribute("modifier_tags")).Split(',').Contains("dye"),"Native cosmetic classification");
            value.Slots=new[]{new NativeItem{Class=new ItemClass{Name=name}}};
            Check(RebirthCamperPattern.Current(vehicle)==id,"Actual selector resolves XML item "+name);
            Check(ReferenceEquals(RebirthCamperPattern.Current(vehicle),RebirthCamperPattern.Current(vehicle)),"Canonical stable ID, no fresh string");
        }
        value.Slots=new[]{new NativeItem{Class=new ItemClass{Name="modVehiclePatternDisco"}}};
        Check(RebirthCamperPattern.Current(vehicle)=="disco","Legacy Disco unchanged");
        value.Slots=new[]{new NativeItem{Class=new ItemClass{Name="modDyeRed"}}};
        Check(RebirthCamperPattern.Current(vehicle)=="none","Normal dye is not a custom wrap");
        value.Slots=new[]{new NativeItem{Class=new ItemClass{Name="modVehicleWrapForgedUnknown"}}};
        Check(RebirthCamperPattern.Current(vehicle)=="none","Unknown IDs rejected");
        value.Slots=new[]{new NativeItem{Empty=true,Class=new ItemClass{Name="modVehicleWrapMidnightCartographer"}},null};
        Check(RebirthCamperPattern.Current(vehicle)=="none","Empty/null cosmetics ignored");
        value.Slots=new[]{new NativeItem{Class=new ItemClass{Name="modVehicleWrapMidnightCartographer"}}};
        RebirthCamperRadial.Camper=false;Check(RebirthCamperPattern.Current(vehicle)=="none","Other vehicles not selected");RebirthCamperRadial.Camper=true;
        for(int i=0;i<1000;i++)RebirthCamperPattern.Current(vehicle);
        long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)RebirthCamperPattern.Current(vehicle);
        Check(GC.GetAllocatedBytesForCurrentThread()-start==0,"Stable selector produces zero managed allocations in 10000 observations");
        Check(RebirthCamperPattern.Current(null)=="none","Null vehicle safe");
        Console.WriteLine("PASS "+checks+" linked-production selector/XML checks; native save/network gameplay remains separate acceptance.");
    }
}
public class EntityVehicle{public NativeVehicle vehicle;}
public class NativeVehicle{public NativeItem Value;public NativeItem GetUpdatedItemValue()=>Value;}
public class NativeItem{public NativeItem[] Slots=Array.Empty<NativeItem>();public ItemClass Class;public bool Empty;public int CosmeticModCount=>Slots.Length;public NativeItem GetCosmeticMod(int slot)=>Slots[slot];public bool IsEmpty()=>Empty;public ItemClass ItemClass=>Class;}
public class ItemClass{public string Name;public string GetItemName()=>Name;}
public static class RebirthCamperRadial{public static bool Camper=true;public static bool IsCamper(EntityVehicle e)=>Camper;}
namespace UnityEngine{internal struct Color{public Color(float r,float g,float b,float a){}}}
