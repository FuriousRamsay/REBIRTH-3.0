using System;
using System.IO;
using HarmonyLib;

// Trusted bounded spans from this native serializer only, never network input.
public static class RebirthStationSerializedContents
{
    // Validate the native material suffix using current physical block definitions.
    // Publication retains the full serialized span; material totals are not ingredients.
    private static bool TryPhysicalInput(ItemStack[] input,RebirthStationGridAdmission admission,string blockName,out ItemStack[] physical)
    {
        physical=null;
        var block=Block.GetBlockByName(blockName);
        if(input==null||block==null)return false;
        if(!block.Properties.Contains("Workstation","InputMaterials"))
        {if(input.Length>9)return false;physical=input;return true;}
        string configured=block.Properties.GetString("Workstation","InputMaterials");
        if(string.IsNullOrEmpty(configured)||!admission.TryGetPhysicalSlotCount(out int count)||count!=3&&count!=9)return false;
        var names=configured.Replace(" ","").Split(',');
        if(names.Length>byte.MaxValue-count||input.Length!=count+names.Length)return false;
        for(int i=0;i<names.Length;i++)
        {
            var definition=ItemClass.GetItemClass("unit_"+names[i]);var stack=input[count+i];
            if(definition==null||stack?.itemValue==null||stack.count<0||
                stack.itemValue.type!=definition.Id&&!(stack.count==0&&stack.itemValue.type==0))return false;
        }
        physical=new ItemStack[count];Array.Copy(input,physical,count);return true;
    }
    public static bool Matches(byte[] inputBytes,byte[] queueBytes,RebirthStationGridAdmission admission,
        string creation,int x,int y,int z,string block,int owner)
    {
        if(admission==null||inputBytes==null||queueBytes==null||inputBytes.Length<1||queueBytes.Length<1||
            inputBytes.Length>256*1024||queueBytes.Length>256*1024)return false;
        try{
            ItemStack[] input;
            using(var stream=new MemoryStream(inputBytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){
                reader.SetBaseStream(stream);int count=reader.ReadByte();input=new ItemStack[count];
                for(int i=0;i<count;i++)input[i]=new ItemStack().Read(reader);
                if(stream.Position!=stream.Length)return false;
            }
            if(!TryPhysicalInput(input,admission,block,out var physical))return false;
            using(var stream=new MemoryStream(queueBytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){
                reader.SetBaseStream(stream);int count=reader.ReadByte();if(count<1||count>16)return false;int matches=0;
                for(int i=0;i<count;i++){
                    long start=stream.Position;if(reader.ReadUInt16()!=2)return false;stream.Position=start;
                    var entry=new RecipeQueueItem();entry.Read(reader);
                    if(entry.Recipe==null||!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job)||job!=admission.JobId)continue;
                    if(entry.StartingEntityId!=owner||entry.AmountToRepair!=0||entry.RepairItem!=null||
                        !admission.MatchesQueuedObservation(creation,x,y,z,block,physical,entry.Recipe,entry.Multiplier))return false;
                    matches++;
                }
                return matches==1&&stream.Position==stream.Length;
            }
        }catch{return false;}
    }
}
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.writeItemStackArray))]
public static class RebirthStationInputSerializedPatch
{
    [HarmonyPrefix] public static void Prefix(TileEntityWorkstation __instance,PooledBinaryWriter bw,ItemStack[] stack,out long __state)
    {__state=ReferenceEquals(stack,__instance.Input)?RebirthStationSnapshotEvidence.StartStationSpan(__instance,bw.BaseStream):-1;}
    [HarmonyPostfix] public static void Postfix(TileEntityWorkstation __instance,PooledBinaryWriter bw,long __state)
    {RebirthStationSnapshotEvidence.CaptureStationSpan(__instance,bw.BaseStream,__state,true);}
}
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.writeRecipeStackArray))]
public static class RebirthStationQueueSerializedPatch
{
    [HarmonyPrefix] public static void Prefix(TileEntityWorkstation __instance,PooledBinaryWriter _bw,out long __state)
    {__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);}
    [HarmonyPostfix] public static void Postfix(TileEntityWorkstation __instance,PooledBinaryWriter _bw,long __state)
    {RebirthStationSnapshotEvidence.CaptureStationSpan(__instance,_bw.BaseStream,__state,false);}
}
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.writeItemStackArray))]
public static class RebirthStationOutputSerializedPatch
{
    [HarmonyPrefix] public static void Prefix(TileEntityWorkstation __instance,System.IO.BinaryWriter bw,ItemStack[] stack,out long __state)
    {__state=ReferenceEquals(stack,__instance.Output)?RebirthStationSnapshotEvidence.StartTerminalSpan(__instance,bw.BaseStream):-1;}
    [HarmonyPostfix] public static void Postfix(TileEntityWorkstation __instance,System.IO.BinaryWriter bw,long __state)
    {RebirthStationSnapshotEvidence.CaptureTerminalSpan(__instance,bw.BaseStream,__state,true);}
}
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.writeCraftCompleteData))]
public static class RebirthStationCompletionSerializedPatch
{
    [HarmonyPrefix] public static void Prefix(TileEntityWorkstation __instance,System.IO.BinaryWriter _bw,out long __state)
    {__state=RebirthStationSnapshotEvidence.StartTerminalSpan(__instance,_bw.BaseStream);}
    [HarmonyPostfix] public static void Postfix(TileEntityWorkstation __instance,System.IO.BinaryWriter _bw,long __state)
    {RebirthStationSnapshotEvidence.CaptureTerminalSpan(__instance,_bw.BaseStream,__state,false);}
}