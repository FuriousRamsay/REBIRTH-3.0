using System;
using System.Collections.Generic;
namespace NativeLifecycle {



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

public partial class CandidateTileEntityWorkstation:TileEntityWorkstation {public CandidateTileEntityWorkstation(Chunk c):base(c){}
public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		ReadNativeBase(_br, _eStreamMode); // Exact base member alias; avoid invoking baseline TE.read twice.
		int version = _br.ReadByte();
		switch (_eStreamMode)
		{
		case StreamModeRead.Persistency:
			lastTickTime = _br.ReadUInt64();
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			if (!bUserAccessing)
			{
				isBurning = _br.ReadBoolean();
				currentBurnTimeLeft = _br.ReadSingle();
				currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			}
			else
			{
				_br.ReadBoolean();
				_br.ReadSingle();
				int num7 = _br.ReadByte();
				for (int m = 0; m < num7; m++)
				{
					_br.ReadSingle();
				}
			}
			isPlayerPlaced = _br.ReadBoolean();
			readItemStackArray(_br, ref lastInput);
			break;
		case StreamModeRead.FromClient:
		{
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			isBurning = _br.ReadBoolean();
			currentBurnTimeLeft = _br.ReadSingle();
			currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			isPlayerPlaced = _br.ReadBoolean();
			ulong num5 = _br.ReadUInt64();
			lastTickTime = GameTimer.Instance.ticks - num5;
			readItemStackArray(_br, ref lastInput);
			break;
		}
		case StreamModeRead.FromServer:
		{
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			if (!bUserAccessing)
			{
				isBurning = _br.ReadBoolean();
				currentBurnTimeLeft = _br.ReadSingle();
				currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			}
			else
			{
				_br.ReadBoolean();
				_br.ReadSingle();
				int num2 = _br.ReadByte();
				for (int j = 0; j < num2; j++)
				{
					_br.ReadSingle();
				}
			}
			isPlayerPlaced = _br.ReadBoolean();
			ulong num3 = _br.ReadUInt64();
			if (!bUserAccessing)
			{
				lastTickTime = GameTimer.Instance.ticks - num3;
			}
			readItemStackArray(_br, ref lastInput);
			break;
		}
		}
		OnSetLocalChunkPosition();
		SetDataFromNet();
	}
}
}
