using System;
using System.IO;
using HarmonyLib;

// Observation only: raw installed CraftingData.Read span from the SAME owner
// file read. No native live queue or deserialized ItemValue is replaced.
internal static class RebirthRemoteResourceCraftingRead
{
    internal sealed class Scope:IDisposable
    {
        internal CraftingData Value;internal string Image;internal bool Failed;
        internal Scope(){if(current!=null)throw new InvalidOperationException("Nested original queue read.");current=this;}
        public void Dispose(){if(ReferenceEquals(current,this))current=null;}
    }
    [ThreadStatic]private static Scope current;
    internal static long Begin(PooledBinaryReader reader)
    {
        if(current==null)return -1;
        try{return reader.BaseStream.CanSeek?reader.BaseStream.Position:-1;}catch{current.Failed=true;return -1;}
    }
    internal static void End(PooledBinaryReader reader,long start,CraftingData value)
    {
        var scope=current;if(scope==null)return;
        try
        {
            var stream=reader.BaseStream;long end=stream.Position;
            if(start<0||end-start<3||end-start>4*1024*1024||scope.Value!=null||value?.RecipeQueueItems==null||value.RecipeQueueItems.Length>255)throw new InvalidDataException();
            var bytes=new byte[checked((int)(end-start))];
            try
            {
                stream.Position=start;int offset=0;
                while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            }
            finally{stream.Position=end;}
            if(bytes[0]!=2||bytes[1]!=0||bytes[2]!=value.RecipeQueueItems.Length)throw new InvalidDataException();
            scope.Value=value;scope.Image=Convert.ToBase64String(bytes);
        }
        catch{scope.Failed=true;}
    }
    internal static bool TryRead(RebirthStablePlayerIdentity owner,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved,out string exactQueue)
    {
        actor=null;saved=null;exactQueue=null;
        try
        {
            using(var scope=new Scope())
            {
                if(!RebirthCamperEnergySavedOwner.TryRead(owner,out actor,out saved)||scope.Failed||!ReferenceEquals(scope.Value,saved.craftingData)||scope.Image==null)return false;
                exactQueue=scope.Image;return true;
            }
        }
        catch{actor=null;saved=null;exactQueue=null;return false;}
    }

    internal static bool MatchesSavedQueue(string expected,string saved)
    {
        if(expected==null||saved==null)return false;
        if(expected==saved)return true;
        try
        {
            byte[] proposed=Convert.FromBase64String(expected);
            byte[] bytes=Convert.FromBase64String(saved);
            if(bytes.Length<3||proposed.Length<=bytes.Length||proposed.Length>4*1024*1024||
                bytes[0]!=2||bytes[1]!=0||!bytes.AsSpan(0,3).SequenceEqual(proposed.AsSpan(0,3)))return false;
            using var input=new MemoryStream(bytes,false);
            using var reader=MemoryPools.poolBinaryReader.AllocSync(true);
            reader.SetBaseStream(input);
            input.Position=3;
            int offset=3;
            for(int slot=0;slot<bytes[2];slot++)
            {
                int start=(int)input.Position;
                if(reader.ReadUInt16()!=RecipeQueueItem.Version)return false;
                input.Position=start;
                var row=new RecipeQueueItem();
                row.Read(reader);
                int end=(int)input.Position;
                int length=end-start;
                if(offset>proposed.Length-length)return false;
                if(bytes.AsSpan(start,length).SequenceEqual(proposed.AsSpan(offset,length)))
                {
                    offset+=length;
                    continue;
                }
                const int repairFlag=9; // Native v2 fields preceding the optional repair item.
                int repair=start+repairFlag;
                // ItemValue.ReadOrNull discards a serialized None on upload.
                // Only an idle row's explicit None and zero repair amount may differ.
                if(row.Recipe!=null||row.Multiplier!=0||row.IsCrafting||row.RepairItem!=null||row.AmountToRepair!=0||
                    end<=repair||bytes[repair]!=0||offset>proposed.Length-length-3)return false;
                int proposedRepair=offset+repairFlag;
                if(!bytes.AsSpan(start,repairFlag).SequenceEqual(proposed.AsSpan(offset,repairFlag))||
                    proposed[proposedRepair]!=1||proposed[proposedRepair+1]!=0||proposed[proposedRepair+2]!=0||proposed[proposedRepair+3]!=0||
                    !bytes.AsSpan(repair+1,end-repair-1).SequenceEqual(proposed.AsSpan(proposedRepair+4,end-repair-1)))return false;
                offset+=length+3;
            }
            return input.Position==input.Length&&offset==proposed.Length;
        }
        catch{return false;}
    }
}
[HarmonyPatch(typeof(CraftingData),nameof(CraftingData.Read))]
internal static class RebirthRemoteResourceCraftingReadObserver
{
    private static void Prefix(PooledBinaryReader _br,out long __state)=>__state=RebirthRemoteResourceCraftingRead.Begin(_br);
    private static void Postfix(CraftingData __instance,PooledBinaryReader _br,long __state)=>RebirthRemoteResourceCraftingRead.End(_br,__state,__instance);
}
