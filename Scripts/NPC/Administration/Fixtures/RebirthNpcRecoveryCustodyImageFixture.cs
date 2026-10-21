#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;

// PREPARED, NOT RUN. Offline codec invariant only; this does not certify
// positive primary-file/current-world observation or whole recovery convergence.
internal static class RebirthNpcRecoveryCustodyImageFixture
{
    internal static void Verify()
    {
        var method=typeof(RebirthNpcInventoryPersistenceStore).GetMethod("RecoveryBackingImage",BindingFlags.Static|BindingFlags.NonPublic);
        Require(method!=null);
        var first=Row(1,7,4,Guid.NewGuid());var second=Row(2,3,2,Guid.NewGuid());
        string baseline=Image(method,first,second);
        Require(baseline==Image(method,second,first)); // record enumeration is not custody
        first.Quantities["test"]=5;Require(baseline!=Image(method,first,second));first.Quantities["test"]=4;
        first.Reservations["test"]=1;Require(baseline!=Image(method,first,second));first.Reservations.Clear();
        first.Revision++;Require(baseline!=Image(method,first,second));first.Revision--;
        var replay=first.ReplayJournal[0];first.ReplayJournal[0]=Guid.NewGuid();Require(baseline!=Image(method,first,second));first.ReplayJournal[0]=replay;
        Require(baseline!=Image(method,first));Require(baseline==Image(method,first,second));
        string custody=RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first,second});
        first.Revision=1;
        Require(RebirthNpcInventoryTransactionService.ValidatePersistentRecord(first,out _));
        Require(custody==RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first,second}));
        first.Quantities["test"]++;
        Require(custody!=RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first,second}));first.Quantities["test"]--;
        first.ReplayJournal[0]=Guid.NewGuid();
        Require(custody!=RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first,second}));first.ReplayJournal[0]=replay;
        first.Revision=7;
        Require(RebirthNpcNativeStackSet.TryCreate(first.NpcId,7,new RebirthNpcNativeStackRecord[0],out var native));first.NativeStacks=native;
        string physical=RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first});
        first.Revision=1;
        Require(!RebirthNpcInventoryTransactionService.ValidatePersistentRecord(first,out _)); // nested native custody revision cannot be rewritten
        Require(physical==RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first}));
        first.NativeStacks=native.WithRevision(1);
        Require(physical!=RebirthNpcInventoryPersistenceStore.RecoveryCustodyImage(new[]{first})); // ONLY the outer transport revision is omitted
    }
    private static string Image(MethodInfo method,params RebirthNpcInventoryPersistentRecord[] records)=>(string)method.Invoke(null,new object[]{records});
    private static RebirthNpcInventoryPersistentRecord Row(ulong id,uint revision,int count,Guid replay)=>new RebirthNpcInventoryPersistentRecord
    {NpcId=new RebirthNpcStableId(1,id),Revision=revision,Quantities=new Dictionary<string,int>{{"test",count}},Reservations=new Dictionary<string,int>(),ReplayJournal=new[]{replay},LogisticsEffects=new Dictionary<Guid,string>()};
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Recovery custody canonical image invariant failed");}
}

#endif
