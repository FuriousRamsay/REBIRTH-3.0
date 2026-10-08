using System;using System.IO;namespace NativeLifecycle {


// TOOLS ONLY. Original transport/read owner supplies exact authenticated layout
// and qualified current native header/tail readers; no live tile is read here.
internal static class RebirthPlainStationRemoteFrameGuardCandidate
{
    // Exact installed TileEntity.read remote branch consumes only this vector.
    // Compare detached header against authenticated current tile chunkPos;
    // original transport also validates world position/block/sender/lock.
    internal static bool TryReadCurrentRemoteBaseHeader(PooledBinaryReader reader,Vector3i expectedChunkPos)
        => StreamUtils.ReadVector3i(reader).Equals(expectedChunkPos);

    internal static bool TryPreflightBeforeLiveRead(
        PooledBinaryReader reader,StreamModeRead mode,bool currentNinePlainLayoutProven,
        int currentInputCount,int currentPreviousCount,int currentTimerCount,
        bool qualifiedCurrentNativeFrameAbi,
        Func<PooledBinaryReader,bool> skipQualifiedNativeBaseHeader,
        Func<PooledBinaryReader,bool> validateQualifiedNineInputTail,
        out bool refusedInputDowngrade)
    {
        return TryPreflightCore(reader,mode,currentNinePlainLayoutProven,9,3,
            currentInputCount,currentPreviousCount,currentTimerCount,qualifiedCurrentNativeFrameAbi,
            skipQualifiedNativeBaseHeader,validateQualifiedNineInputTail,out refusedInputDowngrade);
    }

    // Material names MUST originate from authenticated CURRENT tile/owner layout,
    // never saved/unknown mapping or payload shape. Owner qualifies exact order
    // and frame ABI. This overload is opt-in; unknown mapped layout fails closed.
    internal static bool TryPreflightMappedBeforeLiveRead(
        PooledBinaryReader reader,StreamModeRead mode,bool currentNineMappedLayoutProven,
        string[] authenticatedCurrentOrderedMaterialNames,bool qualifiedCurrentMappingAndOwnerAbi,
        int currentInputCount,int currentPreviousCount,int currentTimerCount,
        bool qualifiedCurrentNativeFrameAbi,
        Func<PooledBinaryReader,bool> skipQualifiedNativeBaseHeader,
        Func<PooledBinaryReader,int,int,int,bool> validateQualifiedMappedTail,
        out bool refusedInputDowngrade)
    {
        refusedInputDowngrade=false;
        if(!currentNineMappedLayoutProven||!qualifiedCurrentMappingAndOwnerAbi||
            authenticatedCurrentOrderedMaterialNames==null||validateQualifiedMappedTail==null)return false;
        // Freeze the ordered witness before callback-bearing parsing.
        if(authenticatedCurrentOrderedMaterialNames.Length>246)return false; // refuse before allocation
        var names=(string[])authenticatedCurrentOrderedMaterialNames.Clone();
        if(names.Length>246)return false; // detached witness bound
        var unique=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var name in names)
            if(string.IsNullOrWhiteSpace(name)||name!=name.Trim()||!unique.Add(name))return false;
        int full=9+names.Length,legacy=3+names.Length;
        return TryPreflightCore(reader,mode,true,full,legacy,currentInputCount,
            currentPreviousCount,currentTimerCount,qualifiedCurrentNativeFrameAbi,
            skipQualifiedNativeBaseHeader,
            tail=>{
                // Reconfirm the original supplied ordered witness, never adopt a replacement.
                if(authenticatedCurrentOrderedMaterialNames.Length!=names.Length)return false;
                for(int i=0;i<names.Length;i++)
                    if(!string.Equals(names[i],authenticatedCurrentOrderedMaterialNames[i],StringComparison.Ordinal))return false;
                bool valid=validateQualifiedMappedTail(tail,full,9,9);
                for(int i=0;i<names.Length;i++)
                    if(!string.Equals(names[i],authenticatedCurrentOrderedMaterialNames[i],StringComparison.Ordinal))return false;
                return valid;
            },out refusedInputDowngrade);
    }

    private static bool TryPreflightCore(
        PooledBinaryReader reader,StreamModeRead mode,bool currentLayoutProven,
        int expectedFullInputCount,int legacyFullInputCount,
        int currentInputCount,int currentPreviousCount,int currentTimerCount,
        bool qualifiedCurrentNativeFrameAbi,
        Func<PooledBinaryReader,bool> skipQualifiedNativeBaseHeader,
        Func<PooledBinaryReader,bool> validateQualifiedTail,
        out bool refusedInputDowngrade)
    {
        refusedInputDowngrade=false;
        if(!currentLayoutProven)return true; // preserves only legacy plain API behavior
        if((mode!=StreamModeRead.FromClient&&mode!=StreamModeRead.FromServer)||
            currentInputCount!=expectedFullInputCount||currentPreviousCount!=9||currentTimerCount!=9||
            !qualifiedCurrentNativeFrameAbi||reader==null||
            skipQualifiedNativeBaseHeader==null||validateQualifiedTail==null)return false;
        var stream=reader.BaseStream;
        if(stream==null||!stream.CanRead||!stream.CanSeek)return false;
        long start;
        try{start=stream.Position;}catch{return false;}
        try
        {
            // Ceiling65535 remains an original-owner admission policy proposal.
            // Caller supplies THIS exact frame, not an enclosing stream.
            if(stream.Length-start<1||stream.Length-start>65535)return false;
            if(!skipQualifiedNativeBaseHeader(reader)||reader.ReadByte()!=50)return false;
            int fuelCount=reader.ReadByte();
            for(int i=0;i<fuelCount;i++)new ItemStack().Read(reader);
            int inputCount=reader.ReadByte();
            if(inputCount!=expectedFullInputCount)
            {
                refusedInputDowngrade=inputCount==legacyFullInputCount;
                return false;
            }
            // Count consumed; detached ORIGINAL-owner tail qualification must read
            // full9+M inputs, nine previous inputs/timers, all remaining native
            // fields and exact frame end. No guessed codec/live tile mutation.
            return validateQualifiedTail(reader)&&stream.Position==stream.Length;
        }
        catch{return false;}
        finally{stream.Position=start;} // restoration fault => caller must reject/dispose
    }
}

}