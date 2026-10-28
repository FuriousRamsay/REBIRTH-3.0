using System;
using System.Collections.Generic;
using System.Linq;

// PREPARED, NOT RUN. Actual production native-file syntax reader only.
// This grants no original permission and invokes no death/save/native setter.
internal static class MpALogisticsPhysicalLossMarkerFixture
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static PlayerDataFile Data(int stage,int bag,int belt,byte[] selected,string digest,bool omitZero,Dictionary<string,float> extra=null)
    {
        var words=new Dictionary<string,float>{{"rbLogisticsLossState",stage},{"rbLogisticsLossBagWidth",bag},{"rbLogisticsLossBeltWidth",belt}};
        for(int i=0;i<34;i++)
        {int n=i*2;int word=(n<selected.Length?selected[n]:0)|((n+1<selected.Length?selected[n+1]:0)<<8);if(!omitZero||word!=0)words.Add("rbLogisticsLossSelect"+i,word);}
        for(int i=0;i<16;i++){int word=Convert.ToUInt16(digest.Substring(i*4,4),16);if(!omitZero||word!=0)words.Add("rbLogisticsLoss"+i,word);}
        if(extra!=null)foreach(var row in extra)words.Add(row.Key,row.Value);
        var file=new PlayerDataFile();file.buffData=StreamUtils.ToBlob(writer=>
        {writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)words.Count);foreach(var row in words){writer.Write(row.Key);writer.Write(row.Value);}});return file;
    }
    internal static void VerifyProjection(ItemStack[] beforeBag,ItemStack[] beforeBelt,byte[] beforeEquipment,byte[] actualSelected,
        int mode,int lossMode,int minimum,int maximum,int degradeType,float amount,
        ItemStack[] actualRemovedBag,ItemStack[] actualRemovedBelt,byte[] actualRemovedEquipment,
        ItemStack[] actualDegradedBag,ItemStack[] actualDegradedBelt,byte[] actualDegradedEquipment,byte[] actualOldLifeFinalEquipment)
    {
        Check(RebirthLogisticsPhysicalLossWitness.TryProject(beforeBag,beforeBelt,beforeEquipment,actualSelected,mode,lossMode,minimum,maximum,degradeType,amount,out var projection),"actual native selected indices fit authored eligibility/count bounds");
        Check(RebirthLogisticsDeathPartition.Same(actualRemovedBag,projection.RemovedBag)&&RebirthLogisticsDeathPartition.Same(actualRemovedBelt,projection.RemovedBelt)&&actualRemovedEquipment.SequenceEqual(projection.RemovedEquipment),"exact native removed material postimages");
        Check(RebirthLogisticsDeathPartition.Same(actualDegradedBag,projection.DegradedBag)&&RebirthLogisticsDeathPartition.Same(actualDegradedBelt,projection.DegradedBelt)&&actualDegradedEquipment.SequenceEqual(projection.DegradedEquipment),"exact native degradation material postimages");
        Check(actualOldLifeFinalEquipment.SequenceEqual(projection.FinalEquipment),"equipment clear belongs to old-life respawn boundary");
        Check(!RebirthLogisticsPhysicalLossWitness.TryProject(beforeBag,beforeBelt,beforeEquipment,new byte[actualSelected.Length+1],mode,lossMode,minimum,maximum,degradeType,amount,out _),"extra selector bits cannot invent a native loss");
        Check(!RebirthLogisticsPhysicalLossWitness.TryProject(beforeBag,beforeBelt,beforeEquipment,actualSelected,mode,lossMode,minimum,maximum,4,amount,out _),"unsupported authored degradation refuses");
        // Inputs must be captured by the real native scenario. Pure projection
        // grants no fatal/owner publication, source credit, or overflow proof.
    }
    internal static void VerifyTypedReceipt(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record retained)
    {
        Check(retained.Physical?.Stage==5&&retained.Phase==retained.Physical.OwnerPhase,"fixture supplies full real native-stage chain");
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var validate=typeof(RebirthLogisticsNativeDeathReceipt).GetMethod("ValidatePhysical",flags);
        bool Valid(RebirthLogisticsNativeDeathReceipt.Record value)=>(bool)validate.Invoke(null,new object[]{original,value,null});
        Check(Valid(retained),"actual production typed physical rederivation");
        var memberwise=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var changed=(RebirthLogisticsNativeDeathReceipt.Record)memberwise.Invoke(retained,null);
        changed.Physical=(RebirthLogisticsNativeDeathReceipt.PhysicalObservation)memberwise.Invoke(retained.Physical,null);
        changed.Physical.LatestDigest=new string('0',64);Check(!Valid(changed),"unbound final native marker refuses");
        changed.Physical.LatestDigest=retained.Physical.LatestDigest;changed.Physical.Stage=4;
        Check(!Valid(changed),"incomplete physical stage is not a saved final equipment receipt");
        changed.Physical.Stage=5;changed.Physical.BeforeDigest=new string('0',64);
        Check(!Valid(changed),"missing original before publication cannot be inferred from final cells");
        // A consistent detached object alone is NOT a receipt producer. The
        // actual transition requires authenticated detached native early
        // evidence + actual final native4/5 primary, exact next stage and original server fatal policy.
    }
    internal static void Verify()
    {
        const int bag=169,belt=10;var selection=new byte[(bag+belt+13+7)/8];
        string digest="000000000000000000000000000000000000000000000000abcd000000001234";
        Check(RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(1,bag,belt,selection,digest,true),out var read)&&read.Stage==1&&read.Digest==digest,"original native zero omission roundtrip");
        selection[4/8]|=(byte)(1<<(4%8));int equipment=bag+belt+12;selection[equipment/8]|=(byte)(1<<(equipment%8));
        Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(1,bag,belt,selection,digest,true),out _),"before-selection native stage cannot contain selected indices");
        foreach(int stage in new[]{2,3,4,5})
        {Check(RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(stage,bag,belt,selection,digest,true),out read)&&read.Selected.SequenceEqual(selection),"bounded exact selected indices persist across native stages");}
        Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(6,bag,belt,selection,digest,true),out _),"unknown stage refused");
        Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(2,0,belt,selection,digest,true),out _),"missing native width refused");
        Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(2,bag,belt,selection,new string('0',64),true),out _),"unbound digest refused");
        var refusal=new Dictionary<string,float>{{"rbLogisticsLossBodyByteRefused",1},{"rbLogisticsLossBuilders",1},{"rbLogisticsLossInputCount",1},{"rbLogisticsLossInput0",1},{"rbLogisticsLossInput1",1}};
        Check(RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(4,bag,belt,selection,digest,true,refusal),out read)&&read.BodyByteRefused&&read.Requests==0&&!read.Inputs[0].Failed,"typed constructor refusal is distinct from actual native AddItem failure");
        Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(3,bag,belt,selection,digest,true,refusal),out _),"pre-constructor stage cannot claim body-byte refusal");
        refusal["rbLogisticsLossBuilders"]=0;Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(4,bag,belt,selection,digest,true,refusal),out _),"byte refusal requires actual captured original builder");
        refusal["rbLogisticsLossBuilders"]=1;refusal["rbLogisticsLossRequests"]=1;Check(!RebirthLogisticsPhysicalLossWitness.TryReadMarker(Data(4,bag,belt,selection,digest,true,refusal),out _),"refused constructor cannot also claim native request");
        // This exact native-file syntax is never sufficient for settlement.
        // Actual original source images, server fatal policy, actual selected
        // native callback, accepted ECD and saved final equipment remain
        // separate scenario obligations, not assertions made by this fixture.
    }
}
