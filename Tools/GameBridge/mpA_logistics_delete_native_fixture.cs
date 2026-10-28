using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// PREPARED, NOT RUN. Target-native harness, actual production codec/projection.
// No native save/upload, callback invocation, file writes or inventory mutation.
internal static class MpALogisticsDeleteNativeFixture
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static PlayerDataFile Marker(string digest,int state,bool omitZeros)
    {
        var words=new Dictionary<string,float>{{"rbLogisticsDeleteState",state}};
        for(int i=0;i<16;i++){ushort value=Convert.ToUInt16(digest.Substring(4*i,4),16);if(!omitZeros||value!=0)words.Add("rbLogisticsDelete"+i,value);}
        var data=new PlayerDataFile();data.buffData=StreamUtils.ToBlob(writer=>
        {writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)words.Count);foreach(var pair in words){writer.Write(pair.Key);writer.Write(pair.Value);}});
        return data;
    }
    internal static void VerifyMarker()
    {
        string digest="00000000a123000000000000000000000000000000000000000000000000b456";
        var data=Marker(digest,1,true);
        Check(RebirthLogisticsDeleteWitness.TryRead(data,out var read,out int state)&&read==digest&&state==1,"actual native omitted zero words preserve Started1 digest");
        data=Marker(digest,2,true);
        Check(RebirthLogisticsDeleteWitness.TryRead(data,out read,out state)&&read==digest&&state==2,"Completed2 remains distinct");
        Check(!RebirthLogisticsDeleteWitness.TryRead(Marker(digest,0,false),out _,out _),"missing positive native stage refuses");
        Check(!RebirthLogisticsDeleteWitness.TryRead(Marker(digest,3,false),out _,out _),"unknown stage is not a completed native clear");
        Check(!RebirthLogisticsDeleteWitness.TryRead(Marker(new string('0',64),2,true),out _,out _),"empty original-bound digest refuses");
    }
    internal static void VerifyEquipment(byte[] actualBefore,byte[] actualAfter,int type,float amount)
    {
        Check(RebirthLogisticsDeleteWitness.TryEquipmentProjection(actualBefore,out var projected,type,amount),"actual native equipment roundtrip supported");
        Check(projected.SequenceEqual(actualAfter),"KeepOnDeath survivors include exact native configured degradation");
        Check(!ReferenceEquals(projected,actualBefore),"projection detached from original native owner blob");
        Check(!RebirthLogisticsDeleteWitness.TryEquipmentProjection(actualBefore,out _,4,amount),"unknown native policy refuses");
        Check(!RebirthLogisticsDeleteWitness.TryEquipmentProjection(actualBefore,out _,type,float.NaN),"malformed native policy refuses");
    }
    internal static void VerifyMixedInput(RebirthLogisticsTransferJournal.Record original,int nativePhase)
    {
        Check(nativePhase==1||nativePhase==2,"fixture only covers actual original setter transitions");
        Check(RebirthLogisticsDeathPartition.TryExpected(original,nativePhase,out var before)&&
            RebirthLogisticsDeathPartition.TryExpected(original,nativePhase+1,out var alternate),"actual canonical original stages");
        int changed=-1;
        for(int i=0;i<before.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[i],alternate[i])){changed=i;break;}
        Check(changed>=0,"fixture original needs a real changed owner cell");
        var capture=typeof(RebirthLogisticsNativeDeathReceipt).GetMethod("TryCaptureDeleteInput",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var restore=typeof(RebirthLogisticsNativeDeathReceipt).GetMethod("TryDeleteInput",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var actual=ItemStack.Clone(before);actual[changed]=alternate[changed].Clone();
        var args=new object[]{original,nativePhase,actual,null};Check((bool)capture.Invoke(null,args),"exact original partially applied cell accepted as native before-clear input");
        var mask=(byte[])args[3];var rebuilt=new object[]{original,nativePhase,mask,null};Check((bool)restore.Invoke(null,rebuilt)&&
            RebirthLogisticsDeathPartition.Same(actual,(ItemStack[])rebuilt[3]),"bounded mask reconstructs original native quantities/metadata only");
        actual[changed]=actual[changed].Clone();actual[changed].count=int.MaxValue;
        Check(!(bool)capture.Invoke(null,new object[]{original,nativePhase,actual,null}),"arbitrary mixed quantity refuses");
        Check(!(bool)restore.Invoke(null,new object[]{original,nativePhase,new byte[mask.Length+1],null}),"oversized mask refuses");
        // Original phase1 requires all endpoint/NPC attempts zero and no saved
        // owner debit; phase2 final alternate requires an actual paid outcome.
        // Those canonical originals are supplied by the native scenario.
    }
    internal static void VerifyUndeliveredMask(RebirthLogisticsTransferJournal.Record original,int phase,byte[] actualMask,ItemStack[] expected)
    {
        Check(RebirthLogisticsNativeDeathReceipt.TryUndeliveredDelete(original,phase,actualMask,out var owed),"actual original arithmetic accepts exact physical mask");
        Check(RebirthLogisticsDeathPartition.Same(owed,expected),"delivered physical credits are not invented as owed after loss");
        if(actualMask!=null)
            Check(!RebirthLogisticsNativeDeathReceipt.TryUndeliveredDelete(original,phase,new byte[actualMask.Length+1],out _),"malformed native input mask cannot invent escrow");
        Check(!RebirthLogisticsNativeDeathReceipt.TryUndeliveredDelete(original,5,actualMask,out _),"native no-effect ACK has no loss arithmetic interpretation");
        // Call with phase1 mixed debit and phase2 mixed paid/refund originals.
        // This executes no native callback and proves no native receipt.
    }
    internal static void VerifyPreservedEscrow(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record actualPrepared,ItemStack[] expectedUndelivered)
    {
        Check(RebirthLogisticsNativeDeathReceipt.TryDeleteOwed(original,out var actual),"actual positive native delete receipt available");
        Check(RebirthLogisticsDeathPartition.Same(actual,expectedUndelivered),"only not-yet-physical original positives/refunds survive native deletion");
        Check(RebirthLogisticsDeathPartition.Same(actualPrepared.PreservedOwed,actual),"physical plan retains exact typed original owed vector");
        Check(RebirthLogisticsDeathPartition.TryOwed(original,actualPrepared,out var packedOwed)&&RebirthLogisticsDeathPartition.Same(actual,packedOwed),"packer consumes preserved original custody unchanged");
        Check(RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,actualPrepared),"all original owed quantities conserved across frozen native bodies");
        if(original.Outcome==3&&actual.Any(item=>!item.IsEmpty()))Check(original.OwnerEscrowRecovery,"unpaid partial-refund cannot use generic no-effect terminal shortcut");
        // The supplied native scenario must explicitly mark cells whose native
        // Final was witnessed before deletion as EMPTY expectedUndelivered.
        // Refunding those destroyed delivered credits would fail this equality.
    }
    // Full native scenario obligations remain: original held operation, actual
    // native death, before-remove/degrade Started1 upload, successful nested
    // DeleteAll callback, Completed2 primary + original fatal, KeepOnDeath,
    // interrupted first/second saves, absent first publication, reconnect and
    // exact source outcome/idempotent terminal. This fixture proves none of
    // those lifecycle/authentication paths by itself.
}
