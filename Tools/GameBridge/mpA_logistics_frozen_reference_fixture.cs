using System;
using System.IO;
using System.Linq;

// PREPARED NOT RUN. Real harness supplies genuine frozen journal/native bodies.
// No helper creates a fatal, owner image, spawn, tracking entry or publication.
internal sealed class mpA_logistics_frozen_reference_fixture
{
    private readonly RebirthLogisticsTransferJournal.Record original;
    private readonly string frozenPlan;
    private readonly EntityBackpack[] bodies;
    private readonly ItemStack[][] items;
    internal mpA_logistics_frozen_reference_fixture(RebirthLogisticsTransferJournal.Record actual)
    {
        original=actual;
        if(!RebirthLogisticsDeathJournal.TryRead(actual,out var death)||death.State<2)throw new Exception("Actual frozen paid distribution required.");
        frozenPlan=RebirthLogisticsDeathJournal.PlanKey(death);
        var ids=new System.Collections.Generic.List<int>{death.EntityId};foreach(var parcel in death.Parcels)if(parcel.State>=2)ids.Add(parcel.EntityId);
        bodies=new EntityBackpack[ids.Count];items=new ItemStack[ids.Count][];
        for(int i=0;i<ids.Count;i++)
        {
            bodies[i]=GameManager.Instance.World.GetEntity(ids[i]) as EntityBackpack;if(bodies[i]?.bag?.ItemGrid?.items==null)throw new Exception("Existing native body required; no fixture spawn.");
            items[i]=ItemStack.Clone(bodies[i].bag.ItemGrid.items);
        }
    }
    internal void RequireReferenceOnly()
    {
        if(!RebirthLogisticsDeathJournal.TryRead(original,out var death)||RebirthLogisticsDeathJournal.PlanKey(death)!=frozenPlan)throw new Exception("Reference repair rewrote immutable paid plan/Spawn/proof.");
        for(int i=0;i<bodies.Length;i++)if(!ReferenceEquals(GameManager.Instance.World.GetEntity(bodies[i].entityId),bodies[i])||!RebirthLogisticsDeathPartition.Same(items[i],bodies[i].bag.ItemGrid.items))throw new Exception("Pointer repair replaced body or changed material.");
    }
    internal static void RequireState1Retained(RebirthLogisticsTransferJournal.Record actual,bool expectedReady)
    {
        if(!RebirthLogisticsDeathJournal.TryRead(actual,out var before)||before.State!=1||before.After!=null)throw new Exception("Actual State1 native-before-only original required.");
        string plan=RebirthLogisticsDeathJournal.PlanKey(before),proof=before.BeforeProof;
        if(RebirthLogisticsParcelReferenceJournal.ReadyForNoEffect(actual)!=expectedReady)throw new Exception("Unexpected metadata retirement readiness.");
        if(!RebirthLogisticsDeathJournal.TryRead(actual,out var after)||after.State!=1||after.After!=null||after.BeforeProof!=proof||RebirthLogisticsDeathJournal.PlanKey(after)!=plan)throw new Exception("Readiness synthesized an After/payment image or altered native Before.");
    }
    internal static void RequireIndependentNoEffectTerminal(RebirthLogisticsTransferJournal.Record actual,bool expectedItemReady,bool expectedMetadataReady)
    {
        if(!RebirthLogisticsDeathJournal.TryRead(actual,out var native)||native.State!=1||native.After!=null)throw new Exception("Actual positive State1 native-before-only original required.");
        string frozen=RebirthLogisticsTransferJournal.Encode(actual),plan=RebirthLogisticsDeathJournal.PlanKey(native);
        if(RebirthLogisticsParcelReferenceJournal.ReadyForNoEffectItemTerminal(actual)!=expectedItemReady||RebirthLogisticsParcelReferenceJournal.ReadyForNoEffect(actual)!=expectedMetadataReady)throw new Exception("No-effect item terminal and GUID readiness were conflated.");
        if(!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=frozen||!RebirthLogisticsDeathJournal.TryRead(actual,out var after)||after.State!=1||after.After!=null||RebirthLogisticsDeathJournal.PlanKey(after)!=plan)throw new Exception("Read-only terminal predicates altered original/After/native proof.");
    }
    // PREPARED NOT RUN: harness must use a genuine native removal, never
    // remove via fixture or fabricate a missing body as evidence.
    internal static void RequireState1RemovalReadiness(RebirthLogisticsTransferJournal.Record actual,bool actualRemovalReceiptExpected)
    {
        if(!RebirthLogisticsDeathJournal.TryRead(actual,out var before)||before.State!=1||GameManager.Instance.World.GetEntity(before.EntityId)!=null)throw new Exception("An absent positively observed State1 native body is required.");
        RequireState1Retained(actual,actualRemovalReceiptExpected);
    }
    internal static void RequireUntracked(EntityBackpack actual)
    {
        foreach(var row in GameManager.Instance.GetPersistentPlayerList().Players.Values)
            if(row.backpacksByID!=null)foreach(var cell in row.backpacksByID)
                if(cell.Key==actual.entityId||cell.Value.EntityID==actual.entityId)throw new Exception("Untracked variant inserted or aliased a native tracking entry.");
    }
    internal static void RequireEffectivePublished(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record frozen,RebirthLogisticsDeathJournal.Parcel parcel,EntityBackpack body,int actualOwner)
    {
        if(!RebirthLogisticsParcelReferenceJournal.TryEffective(original,frozen,parcel,body,out int owner)||owner!=actualOwner||body.RefPlayerId!=actualOwner)throw new Exception("Frozen body lacks completed original-bound live reference.");
    }
    internal static void RequireDetachedCapacity(byte[] actualBaseline,int x,int z,Guid operation,int id,int entityClass,int oldOwner,int newOwner,
        byte[] actualOriginalMarker,byte[] actualPreviousReference,byte[] proposedReference,bool expected)
    {
        var unchanged=(byte[])actualBaseline.Clone();
        bool fits=RebirthLogisticsParcelPreview.TryBuildReference(actualBaseline,x,z,operation,id,entityClass,oldOwner,newOwner,actualOriginalMarker,actualPreviousReference,proposedReference,out var candidate);
        if(fits!=expected||!unchanged.SequenceEqual(actualBaseline))throw new Exception("Detached reference preflight changed original bytes or accepted an unrepresentable native stage.");
        if(!fits)return;
        if(!RebirthLogisticsParcelPreview.TryReadOriginal(candidate,x,z,operation,id,entityClass,out var frame,out var marker)||!marker.SequenceEqual(actualOriginalMarker)||
            !RebirthLogisticsParcelPreview.TryReadMarker(candidate,x,z,"RebirthLogisticsReferenceV1:"+id+":"+operation.ToString("N"),out var reference)||!reference.SequenceEqual(proposedReference))throw new Exception("Reference preview rewrote original custody or lost exact secondary marker.");
        EntityCreationData native=null;
        try
        {
            using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)throw new Exception("Trailing native frame.");}
            var body=native.entityData.ToArray();if(BitConverter.ToInt32(body,body.Length-(int)native.bagData.Length-4)!=newOwner)throw new Exception("Detached native reference differs from planned pointer.");
        }
        finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
    }
    // Deferred real cases: positive saved baseline required; original-before vs
    // output-after; wrong GUID/marker/full native image; Prepared reconnect
    // tombstone; Attempted old/new/intermediate ref; cold original+extra marker
    // atomic snapshot; final temp promotion; 4+ and max607 bodies (native max3
    // tracked); a new tracker row/cell during Untracked watch invalidates;
    // unchanged tracked timestamp/no eviction; native publication pressure;
    // full/partial paid output delivery cannot run during metadata hold;
    // world unload clears transient arrays but durable original stays retained.
}

// Deferred State1 split: positive native-before publication + actual original
// saved NoEffect allows Phase2->3->4 with client body absent/nav pending; no
// false After, source effects, debt, refund or body spawn. Native GUID remains
// until exact actual ACK or existing positive native removal. Missing/corrupt
// Before/fatal/saved owner proof or any attempted source retains item terminal.
