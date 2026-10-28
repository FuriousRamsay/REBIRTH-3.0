using System;
using System.Linq;
using System.Collections.Generic;

// Explicit native typed metadata/container doubles. Actual storage helper linked;
// this is not a native ItemValue binary/vehicle final-file test.
class TypedMetadataValue
{public object Value;public object GetValue()=>Value;public TypedMetadataValue Clone()=>new(){Value=Value};}
class ItemValue
{
    public Dictionary<string,TypedMetadataValue> Metadata=new();public object TextureFullArray=new();public int UntouchedNativeField=42;
    public ItemValue Clone()=>new(){Metadata=Metadata.ToDictionary(p=>p.Key,p=>p.Value?.Clone()),TextureFullArray=TextureFullArray,UntouchedNativeField=UntouchedNativeField};
    public void SetMetadata(string key,object value)=>Metadata[key]=new(){Value=value};
    public bool TryGetMetadata<T>(string key,out T value){if(Metadata.TryGetValue(key,out var v)&&v.Value is T typed){value=typed;return true;}value=default;return false;}
}
static class RebirthCamperRemoteTransport {public const int MaximumFrame=8*1024*1024;}
class Program
{
    static int count;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
    static void Refused(Action action,string name){try{action();throw new Exception("Unexpected admission: "+name);}catch(InvalidOperationException){Check(true,name);}catch(FormatException){Check(true,name);}}
    static void Main()
    {
        var item=new ItemValue();item.SetMetadata("foreign","preserve");var originalMap=item.Metadata;var texture=item.TextureFullArray;
        byte[] data=Enumerable.Range(0,160000).Select(n=>(byte)(n*31)).ToArray();
        RebirthCamperEnergyDeviceLeaseStorage.Write(item,data);
        Check(RebirthCamperEnergyDeviceLeaseStorage.Read(item).SequenceEqual(data),"over64KiB complete payload roundtrip");
        Check(!ReferenceEquals(originalMap,item.Metadata)&&item.Metadata["foreign"].GetValue().Equals("preserve")&&ReferenceEquals(texture,item.TextureFullArray)&&item.UntouchedNativeField==42,"detached preflight preserves unrelated native fields/metadata");
        Check(item.Metadata.Where(p=>p.Key.StartsWith("RebirthCamperEnergyDeviceLeasePart_")).All(p=>((string)p.Value.GetValue()).Length<=65532),"each native typed string below per-string safety bound");
        var shortData=data.Take(20).ToArray();RebirthCamperEnergyDeviceLeaseStorage.Write(item,shortData);
        Check(RebirthCamperEnergyDeviceLeaseStorage.Read(item).SequenceEqual(shortData)&&item.Metadata.Count(p=>p.Key.StartsWith("RebirthCamperEnergyDeviceLeasePart_"))==1,"replacement removes only stale owned chunks");
        var legacy=new ItemValue();legacy.SetMetadata(RebirthCamperEnergyDeviceLeaseStorage.Key,Convert.ToBase64String(data));
        Check(RebirthCamperEnergyDeviceLeaseStorage.Read(legacy).SequenceEqual(data),"legacy single Base64 payload remains readable");
        item.SetMetadata("RebirthCamperEnergyDeviceLeasePart_0",Convert.ToBase64String(new byte[20]));
        Refused(()=>RebirthCamperEnergyDeviceLeaseStorage.Read(item),"same-shaped corrupt chunk digest refuses");
        RebirthCamperEnergyDeviceLeaseStorage.Write(item,data);item.Metadata.Remove("RebirthCamperEnergyDeviceLeasePart_1");
        Refused(()=>RebirthCamperEnergyDeviceLeaseStorage.Read(item),"missing indexed chunk refuses");
        var full=new ItemValue();for(int n=0;n<255;n++)full.SetMetadata("foreign"+n,"value");var heldMap=full.Metadata;
        Refused(()=>RebirthCamperEnergyDeviceLeaseStorage.Write(full,data),"native byte key budget refuses safely");
        Check(ReferenceEquals(heldMap,full.Metadata)&&full.Metadata.Count==255,"key-budget refusal never publishes partial metadata");
        var invalid=new ItemValue();invalid.Metadata["invalid"]=null;heldMap=invalid.Metadata;
        Refused(()=>RebirthCamperEnergyDeviceLeaseStorage.Write(invalid,data),"foreign malformed typed metadata refuses before write");
        Check(ReferenceEquals(heldMap,invalid.Metadata),"malformed foreign metadata preserved on refusal");
        var empty=new ItemValue();Check(RebirthCamperEnergyDeviceLeaseStorage.Read(empty).Length==0,"missing native lease is empty without mutation");
        Console.WriteLine($"PASS {count} actual storage-helper assertions with explicit native doubles; no native binary/save/network proof.");
    }
}
