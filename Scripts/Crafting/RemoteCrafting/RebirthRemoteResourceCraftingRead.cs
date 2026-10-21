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
}
[HarmonyPatch(typeof(CraftingData),nameof(CraftingData.Read))]
internal static class RebirthRemoteResourceCraftingReadObserver
{
    private static void Prefix(PooledBinaryReader _br,out long __state)=>__state=RebirthRemoteResourceCraftingRead.Begin(_br);
    private static void Postfix(CraftingData __instance,PooledBinaryReader _br,long __state)=>RebirthRemoteResourceCraftingRead.End(_br,__state,__instance);
}
