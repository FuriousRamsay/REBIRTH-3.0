using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace NativeCloneBoundary {
public class TypedMetadataValue{public enum TypeTag {None,Float,Integer,String}public readonly TypeTag typeTag;public object value;public TypedMetadataValue(object val,TypeTag tag){typeTag=tag;value=val;}public object GetValue()=>value;public TypedMetadataValue Clone(){_ = typeTag;return new TypedMetadataValue(value,typeTag);}}
public struct TextureFullArray {public long Bits;}
public static class Extensions {public static ItemValue[] CloneItemValueArray(this ItemValue[] values)=>values?.Select(v=>v.Clone()).ToArray();}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){} }
public partial class ItemValue {public struct Stat {public int type;public bool isBoosted;public short value;}public static int NonemptyConstructorCalls;public int type,Meta;public float UseTimes;public ushort Quality,Seed;public byte SelectedAmmoTypeIndex,Flags;public Stat[] Stats;public ItemValue[] modifications,cosmeticMods;public Dictionary<string,TypedMetadataValue> Metadata;public TextureFullArray TextureFullArray;public ItemValue()
	{
	}public ItemValue(int t){type=t;if(t!=0)NonemptyConstructorCalls++;}public static ItemValue None=>new ItemValue(0);public ItemValue Clone()
	{
		ItemValue itemValue = new ItemValue(type);
		itemValue.Meta = Meta;
		itemValue.UseTimes = UseTimes;
		itemValue.Quality = Quality;
		itemValue.SelectedAmmoTypeIndex = SelectedAmmoTypeIndex;
		if (Stats != null)
		{
			itemValue.Stats = new Stat[Stats.Length];
			Array.Copy(Stats, itemValue.Stats, Stats.Length);
		}
		itemValue.modifications = modifications.CloneItemValueArray();
		if (Metadata != null)
		{
			itemValue.Metadata = new Dictionary<string, TypedMetadataValue>();
			foreach (KeyValuePair<string, TypedMetadataValue> metadatum in Metadata)
			{
				itemValue.Metadata.Add(metadatum.Key, metadatum.Value?.Clone());
			}
		}
		itemValue.cosmeticMods = cosmeticMods.CloneItemValueArray();
		itemValue.Flags = Flags;
		if (itemValue.type == 0)
		{
			Seed = 0;
		}
		itemValue.Seed = Seed;
		itemValue.TextureFullArray = TextureFullArray;
		return itemValue;
	}
}
public partial class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack[] CreateArray(int n)=>Enumerable.Range(0,n).Select(_=>new ItemStack(new ItemValue(0),0)).ToArray();
public ItemStack Clone()
	{
		if (itemValue != null)
		{
			return new ItemStack(itemValue.Clone(), count);
		}
		return new ItemStack(ItemValue.None, count);
	}
}



// TOOLS-ONLY PROTOTYPE. No Harmony installer or production caller.
// The original owner must authenticate prior layout provenance before invoking
// this conversion. Current block names/counts are not saved-material evidence.
internal static class RebirthPlainStationNativeMigrationCandidate
{
    internal static bool TryExpandProvenPlainInput(
        bool priorLayoutProvenanceVerified, int priorPhysicalSlots,
        IList<string> priorMaterialNames, IList<string> currentMaterialNames,
        IList<ItemStack> input, IList<ItemStack> lastInput, IList<float> timers,
        out ItemStack[] expandedInput, out ItemStack[] expandedLastInput,
        out float[] expandedTimers)
    {
        expandedInput=null;expandedLastInput=null;expandedTimers=null;
        if(!priorLayoutProvenanceVerified||priorMaterialNames==null||currentMaterialNames==null||
            priorMaterialNames.Count!=0||currentMaterialNames.Count!=0||
            (priorPhysicalSlots!=3&&priorPhysicalSlots!=9)||
            input==null||lastInput==null||timers==null||
            input.Count!=priorPhysicalSlots||lastInput.Count!=priorPhysicalSlots||timers.Count!=priorPhysicalSlots)
            return false;
        for(int i=0;i<priorPhysicalSlots;i++)
            if(input[i]?.itemValue==null||input[i].count<0||lastInput[i]?.itemValue==null||lastInput[i].count<0||
                float.IsNaN(timers[i])||float.IsInfinity(timers[i]))return false;
        try
        {
            var nextInput=ItemStack.CreateArray(9);
            var nextPrevious=ItemStack.CreateArray(9);
            var nextTimers=new float[9];
            for(int i=0;i<priorPhysicalSlots;i++)
            {
                nextInput[i]=CopyStackWithoutMutation(input[i]);
                nextPrevious[i]=CopyStackWithoutMutation(lastInput[i]);
                nextTimers[i]=timers[i];
            }
            expandedInput=nextInput;expandedLastInput=nextPrevious;expandedTimers=nextTimers;
            return true;
        }
        catch
        {
            // All writes were to detached objects; retain original custody and
            // return no partially built result after unsupported copy failure.
            return false;
        }
    }

    // TOOLS ONLY. Owner authenticates saved/runtime prior and current ordered
    // mapping. This detached conversion does not authorize vanilla setup/read/UI.
    internal static bool TryExpandProvenMappedInput(
        bool priorLayoutProvenanceVerified,int priorPhysicalSlots,
        IList<string> priorMaterialNames,IList<string> currentMaterialNames,
        IList<ItemStack> input,IList<ItemStack> lastInput,IList<float> timers,
        out ItemStack[] expandedInput,out ItemStack[] expandedLastInput,
        out float[] expandedTimers)
    {
        expandedInput=null;expandedLastInput=null;expandedTimers=null;
        if(!priorLayoutProvenanceVerified||(priorPhysicalSlots!=3&&priorPhysicalSlots!=9)||
            priorMaterialNames==null||currentMaterialNames==null||
            priorMaterialNames.Count!=currentMaterialNames.Count||
            priorMaterialNames.Count>byte.MaxValue-9||input==null||lastInput==null||timers==null||
            input.Count!=priorPhysicalSlots+priorMaterialNames.Count||
            lastInput.Count!=priorPhysicalSlots||timers.Count!=priorPhysicalSlots)return false;
        var seenMaterialNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int j=0;j<priorMaterialNames.Count;j++)
            if(string.IsNullOrWhiteSpace(priorMaterialNames[j])||
                !string.Equals(priorMaterialNames[j],priorMaterialNames[j].Trim(),StringComparison.Ordinal)||
                !seenMaterialNames.Add(priorMaterialNames[j])||
                !string.Equals(priorMaterialNames[j],currentMaterialNames[j],StringComparison.Ordinal))return false;
        for(int i=0;i<input.Count;i++)
            if(input[i]?.itemValue==null||input[i].count<0)return false;
        for(int i=0;i<priorPhysicalSlots;i++)
            if(lastInput[i]?.itemValue==null||lastInput[i].count<0||
                float.IsNaN(timers[i])||float.IsInfinity(timers[i]))return false;
        try
        {
            var nextInput=ItemStack.CreateArray(9+priorMaterialNames.Count);
            var nextPrevious=ItemStack.CreateArray(9);
            var nextTimers=new float[9];
            for(int i=0;i<priorPhysicalSlots;i++)
            {
                nextInput[i]=CopyStackWithoutMutation(input[i]);
                nextPrevious[i]=CopyStackWithoutMutation(lastInput[i]);
                nextTimers[i]=timers[i];
            }
            for(int j=0;j<priorMaterialNames.Count;j++)
                nextInput[9+j]=CopyStackWithoutMutation(input[priorPhysicalSlots+j]);
            expandedInput=nextInput;expandedLastInput=nextPrevious;expandedTimers=nextTimers;
            return true;
        }
        catch{return false;} // Detached results only; no source normalization.
    }
    // Native ItemValue.Clone writes Seed=0 into the ORIGINAL empty ItemValue.
    // Native ItemStack/ItemValue wire codecs omit zero-count/type-zero payloads.
    // Copy the exact native Clone payload fields onto parameterless detached
    // values instead: no constructor RNG, no source setters or empty reset.
    private static ItemStack CopyStackWithoutMutation(ItemStack source)
    {
        int remainingNodes=4096;
        return new ItemStack(CopyValueWithoutMutation(source.itemValue,0,ref remainingNodes),source.count);
    }

    private static ItemValue CopyValueWithoutMutation(ItemValue source,int depth,ref int remainingNodes)
    {
        if(source==null)return null;
        if(depth>32||--remainingNodes<0)throw new InvalidOperationException("Unsupported cyclic/deep native item graph");
        // Exact native Read/Write container counts are bytes. Refuse malformed
        // over-representable containers BEFORE cloning/allocating any of them;
        // this includes all-null mod arrays whose nodes evade the graph budget.
        if((source.Stats?.Length??0)>byte.MaxValue||
            (source.modifications?.Length??0)>byte.MaxValue||
            (source.cosmeticMods?.Length??0)>byte.MaxValue||
            (source.Metadata?.Count??0)>byte.MaxValue)
            throw new InvalidOperationException("Native item container exceeds byte count framing");
        var result=new ItemValue();
        result.type=source.type;
        result.Meta=source.Meta;
        result.UseTimes=source.UseTimes;
        result.Quality=source.Quality;
        result.SelectedAmmoTypeIndex=source.SelectedAmmoTypeIndex;
        if(source.Stats!=null)result.Stats=(ItemValue.Stat[])source.Stats.Clone();
        result.modifications=CopyValueArrayWithoutMutation(source.modifications,depth+1,ref remainingNodes);
        result.cosmeticMods=CopyValueArrayWithoutMutation(source.cosmeticMods,depth+1,ref remainingNodes);
        if(source.Metadata!=null)
        {
            result.Metadata=new Dictionary<string,TypedMetadataValue>(source.Metadata.Comparer);
            foreach(var entry in source.Metadata)
            {
                object value=entry.Value?.GetValue();
                // Exact current native metadata payloads are immutable scalar
                // or string. Preserve null entries; refuse unknown reference
                // payloads instead of sharing them with original custody.
                if(value!=null&&!(value is int)&&!(value is float)&&!(value is string))
                    throw new InvalidOperationException("Unsupported native metadata payload");
                result.Metadata.Add(entry.Key,entry.Value?.Clone());
            }
        }
        result.Flags=source.Flags;
        result.Seed=source.Seed;
        result.TextureFullArray=source.TextureFullArray; // Native value struct.
        return result;
    }

    private static ItemValue[] CopyValueArrayWithoutMutation(ItemValue[] source,int depth,ref int remainingNodes)
    {
        if(source==null)return null;
        var result=new ItemValue[source.Length];
        for(int i=0;i<source.Length;i++)result[i]=CopyValueWithoutMutation(source[i],depth,ref remainingNodes);
        return result;
    }

    // Replacement for ONLY the three native read timer-assignment loops.
    // Preserve native prefix overwrite semantics and every old timer index;
    // allocating before assignment prevents fresh-constructor9stream overrun.
    // This is raw byte-count capacity handling, NOT layout authorization.
    internal static float[] ReadStoredMeltTimers(PooledBinaryReader reader,float[] previous)
    {
        int transmittedCount=reader.ReadByte();
        var result=new float[Math.Max(previous?.Length??0,transmittedCount)];
        if(previous!=null)Array.Copy(previous,result,previous.Length);
        for(int i=0;i<transmittedCount;i++)result[i]=reader.ReadSingle();
        return result;
    }

    // Source candidate for the read-only getter AFTER original owner supplies
    // an exact current tile/world and authenticated accepted layout witness.
    internal static int ResolvePhysicalSlots(bool currentLayoutProven,
        int provenPhysicalSlots,IList<string> provenMaterials,
        IList<ItemStack> input,IList<ItemStack> previous,IList<float> timers)
    {
        if(currentLayoutProven&&provenPhysicalSlots==9&&provenMaterials!=null&&provenMaterials.Count==0&&
            input?.Count==9&&previous?.Count==9&&timers?.Count==9)return 9;
        return 3; // Existing native fallback is presentation only; do NOT shrink arrays.
    }
}

public static class Probe {public static bool Run(){var input=ItemStack.CreateArray(3);var previous=ItemStack.CreateArray(3);input[0].itemValue.Seed=123;previous[1].itemValue.Seed=456;bool accepted=RebirthPlainStationNativeMigrationCandidate.TryExpandProvenPlainInput(true,3,Array.Empty<string>(),Array.Empty<string>(),input,previous,new float[3],out _,out _,out _);var control=new ItemValue(0){Seed=789};control.Clone();return accepted&&input[0].itemValue.Seed==123&&previous[1].itemValue.Seed==456&&control.Seed==0;}}
}