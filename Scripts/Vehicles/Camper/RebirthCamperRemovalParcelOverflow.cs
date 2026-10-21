using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

// Only a retained removal original with positively saved owner credit can
// enter here. The actual native Backpack spawn is one-use, then its exact
// EntityCreationData is qualified against an actual saved region snapshot.
internal static class RebirthCamperRemovalParcelOverflow
{
    private sealed class Budget:IRebirthStationSnapshotByteBudget
    {public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;public bool TryReserveAllocation(long bytes,out IDisposable reservation)=>RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out reservation);}
    private sealed class Runner:IDisposable
    {
        internal RebirthCamperRemovalParcelPlan Plan;internal EntityPlayer Actor;internal World World;internal EntityBackpack Body;
        internal ItemStack[] Expected;internal Vector3 Position;internal int Class,RefPlayer;internal Func<bool> Current;internal bool Ambiguous;
        internal RebirthCamperRemovalParcelObservation Observation;
        public void Dispose(){Observation?.Dispose();Observation=null;Body=null;Expected=null;Actor=null;World=null;Current=null;}
    }
    private static readonly Dictionary<Guid,Runner> runners=new Dictionary<Guid,Runner>();
    [ThreadStatic]private static Runner invoking;
    internal static void ReleaseStaleObservations()
    {
        if(!ThreadManager.IsMainThread()||runners.Count==0)return;
        foreach(var pair in runners.ToArray())
        {
            bool current=false;try{current=ReferenceEquals(pair.Value.World,GameManager.Instance?.World)&&pair.Value.Current?.Invoke()==true;}catch{}
            if(current)continue;pair.Value.Dispose();runners.Remove(pair.Key);
        }
    }
    private static bool installed;
    internal static bool ObserveLiveContents(RebirthCamperRemovalParcelPlan plan,out int entity,out string actual,out string expected)
    {
        entity=-1;actual=expected=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||plan==null||plan.World!=world.worldState?.Guid||plan.Overflow.Length==0)return false;
            if(!Accepted(plan,Read("Accepted",plan.Operation),out var accepted)||!NativeBag(plan,accepted,out var slots))return false;
            entity=(int)accepted.Attribute("entity");
            if(world.GetEntity(entity) is not EntityBackpack body||!ReferenceEquals(body.world,world)||body.entityClass!=(int)accepted.Attribute("class")||
                body.IsMarkedForUnload()||body.bag?.ItemGrid?.items==null||body.bag.ItemGrid.items.Length>256)return false;
            expected=RebirthStationPublicationRecord.Digest(RebirthCamperInventorySnapshot.Capture(slots));
            actual=RebirthStationPublicationRecord.Digest(RebirthCamperInventorySnapshot.Capture(body.bag.ItemGrid.items));
            return actual==expected;
        }
        catch{return false;}
    }
    internal static void Install()
    {if(installed)return;RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.removal.parcel.spawn"),typeof(Spawn));installed=true;}
    private static RebirthCamperTransferJournal Journal(string suffix)=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOverflow"+suffix));
    private static RebirthCamperTransferJournal.Record Read(string suffix,Guid operation)=>RebirthCamperTransferJournal.ReadExisting(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOverflow"+suffix),operation);
    private static byte[] Image(RebirthCamperRemovalParcelPlan plan)=>RebirthCamperRemovalParcelPlan.Bytes(new XElement("removalParcelOverflow",
        new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),
        new XElement("items",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(plan.Overflow)))));
    private static bool Exact(RebirthCamperTransferJournal.Record record,RebirthCamperRemovalParcelPlan plan,byte[] image)=>record!=null&&record.Operation==plan.Operation&&
        record.Vehicle==plan.Vehicle&&record.OwnerCreation==plan.Creation&&record.Revision==0&&record.Before.SequenceEqual(image)&&record.After.SequenceEqual(image);
    private static RebirthCamperTransferJournal.Record RetainedNonempty(RebirthCamperRemovalParcelPlan plan)
    {
        var record=Read("",plan.Operation);
        return plan.Overflow.Length>0&&Exact(record,plan,Image(plan))?record:null;
    }
    private static EntityBackpack Prototype(EntityPlayer actor,ItemStack[] outputs)
    {
        var body=EntityFactory.CreateEntity("Backpack".GetHashCode(),actor.position+Vector3.up*2f) as EntityBackpack;
        if(body?.bag?.ItemGrid==null){body?.OnEntityUnload();return null;}
        try
        {
            if(body.bag.ItemGrid.items.Length<outputs.Length)body.bag.Resize(new Vector2i(8,(outputs.Length+7)/8));
            for(int index=0;index<outputs.Length;index++)body.bag.SetSlot(index,outputs[index].Clone());
            var native=RebirthLogisticsBackpackPublication.CapturePlanImage(body);
            if(native?.Fits(body.bag.ItemGrid.items)!=true){body.OnEntityUnload();return null;}return body;
        }
        catch{body.OnEntityUnload();return null;}
    }
    internal static bool CanFit(EntityPlayer actor,ItemStack[] outputs)
    {
        if(outputs==null||outputs.Length>byte.MaxValue)return false;if(outputs.Length==0)return true;
        var body=Prototype(actor,outputs);if(body==null)return false;body.OnEntityUnload();return true;
    }
    internal static bool Terminal(RebirthCamperRemovalParcelPlan plan,out string proof)
    {
        proof=null;
        try
        {
            var original=Read("",plan.Operation);if(!Exact(original,plan,Image(plan))||original.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            var published=Read("Published",plan.Operation);
            if(published==null||published.Operation!=plan.Operation||published.Revision!=0||published.Vehicle!=plan.Vehicle||published.OwnerCreation!=plan.Creation||!published.Before.SequenceEqual(published.After))return false;
            var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new System.Text.UTF8Encoding(false,true).GetString(published.Before),8192);
            if(node.Name!="removalParcelOverflowPublished"||(string)node.Attribute("operation")!=plan.Operation.ToString("N")||
                (string)node.Attribute("binding")!=plan.Binding)return false;
            if(plan.Overflow.Length==0)
            {if(!XNode.DeepEquals(node,new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("empty",true))))return false;}
            else
            {
                var accepted=Read("Accepted",plan.Operation);
                if(!Accepted(plan,accepted,out _)||(string)node.Attribute("accepted")!=RebirthStationPublicationRecord.Digest(accepted.Before)||
                    node.Attributes().Count()!=8||node.Nodes().Any()||!System.IO.Path.IsPathRooted((string)node.Attribute("region"))||
                    ((string)node.Attribute("payload"))?.Length!=64||!((string)node.Attribute("payload")).All(Uri.IsHexDigit)||(long?)node.Attribute("generation")<=0||
                    !int.TryParse((string)node.Attribute("x"),out int x)||!int.TryParse((string)node.Attribute("z"),out int z)||
                    !XNode.DeepEquals(node,new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),
                        new XAttribute("accepted",RebirthStationPublicationRecord.Digest(accepted.Before)),new XAttribute("region",(string)node.Attribute("region")),
                        new XAttribute("payload",(string)node.Attribute("payload")),new XAttribute("x",x),new XAttribute("z",z),new XAttribute("generation",(long)node.Attribute("generation")))))return false;
            }
            proof=RebirthStationPublicationRecord.Digest(published.Before);return true;
        }
        catch{proof=null;return false;}
    }
    private static bool Accepted(RebirthCamperRemovalParcelPlan plan,RebirthCamperTransferJournal.Record record,out XElement node)
    {
        node=null;
        try
        {
            if(record==null||record.Operation!=plan.Operation||record.Revision!=0||record.Vehicle!=plan.Vehicle||record.OwnerCreation!=plan.Creation||!record.Before.SequenceEqual(record.After)||record.Before.Length>512*1024)return false;
            node=RebirthCookingRemoteAdmission.ReadProtocolXml(new System.Text.UTF8Encoding(false,true).GetString(record.Before),512*1024);
            byte[] frame=Convert.FromBase64String(node.Element("native").Value);
            return node.Name=="removalParcelOverflowAccepted"&&node.Attributes().Count()==5&&node.Elements().Count()==2&&
                (string)node.Attribute("operation")==plan.Operation.ToString("N")&&(string)node.Attribute("binding")==plan.Binding&&
                (int?)node.Attribute("version")==1&&(int)node.Attribute("entity")>=0&&(int)node.Attribute("class")>=0&&frame.Length>0&&frame.Length<=131072&&
                RebirthCookingDropSnapshot.Valid(node.Element("snapshot"),frame.Length)&&NativeBag(plan,node,out _);
        }
        catch{node=null;return false;}
    }
    private static bool NativeBag(RebirthCamperRemovalParcelPlan plan,XElement node,out ItemStack[] slots)
    {
        slots=null;
        try
        {
            byte[] frame=Convert.FromBase64String(node.Element("native").Value);var native=new EntityCreationData();
            try
            {
                using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
                if(native.id!=(int)node.Attribute("entity")||native.entityClass!=(int)node.Attribute("class")||native.bagData==null)return false;
                using(var stream=new MemoryStream(native.bagData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);var bag=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);if(stream.Position!=stream.Length||bag?.ItemGrid?.items==null)return false;slots=ItemStack.Clone(bag.ItemGrid.items);}
                if(slots.Length<plan.Overflow.Length||slots.Length>256)return false;
                for(int index=0;index<slots.Length;index++)if(index<plan.Overflow.Length?
                    !RebirthStationGridIngredients.IsSameStackSnapshot(slots[index],plan.Overflow[index]):!slots[index].IsEmpty())return false;
                return RebirthLogisticsBackpackPublication.ReadSavedPlanImage(frame,slots)!=null;
            }
            finally{native.entityData?.Dispose();native.bagData?.Dispose();}
        }
        catch{slots=null;return false;}
    }
    internal static bool RetainUnattempted(RebirthCamperRemovalParcelPlan plan)
    {
        if(plan.Overflow.Length==0)return true;
        byte[] bytes=Image(plan);var journal=Journal("");
        var record=journal.Prepare(plan.Operation,plan.Vehicle,plan.Creation,0,bytes,bytes);
        return Exact(record,plan,bytes)&&record.State==RebirthCamperTransferJournal.Phase.Prepared&&
            Exact(journal.Read(plan.Operation),plan,bytes);
    }
    private static bool SavedOwnerHandoff(EntityPlayer actor,RebirthCamperRemovalParcelPlan plan,bool allowRetired=true)
    {
        return actor?.world!=null&&actor.world.worldState.Guid==plan.World&&actor.Died==plan.Deaths&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==plan.Owner&&
            RebirthOriginalActionScope.Matches(actor,plan.Owner,plan.Creation)&&
            RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var saved)&&saved.id==actor.entityId&&saved.deaths==plan.Deaths&&
            RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out bool present)&&present&&
            (phase==500f?plan.Credited.Matches(actual):allowRetired&&phase==1000f&&plan.Retired.Matches(actual));
    }
    internal static bool Advance(EntityPlayer actor,RebirthCamperRemovalParcelPlan plan,Func<bool> current,out string proof)
    {
        proof=null;if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||plan==null||current?.Invoke()!=true)return false;
        try
        {
            if(Terminal(plan,out proof))return true;
            foreach(var stale in runners.Where(pair=>!ReferenceEquals(pair.Value.World,GameManager.Instance?.World)||pair.Value.Current?.Invoke()!=true).Select(pair=>pair.Key).ToArray())
            {runners[stale].Dispose();runners.Remove(stale);}
            byte[] bytes=Image(plan);var journal=Journal("");
            var original=plan.Overflow.Length==0?journal.Prepare(plan.Operation,plan.Vehicle,plan.Creation,0,bytes,bytes):RetainedNonempty(plan);
            if(!Exact(original,plan,bytes))return false;
            if(plan.Overflow.Length==0)
            {
                var empty=new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),new XAttribute("empty",true));
                byte[] image=RebirthCamperRemovalParcelPlan.Bytes(empty);Journal("Published").Prepare(plan.Operation,plan.Vehicle,plan.Creation,0,image,image);
                if(original.State==RebirthCamperTransferJournal.Phase.Prepared)original=journal.Advance(plan.Operation,original.State,RebirthCamperTransferJournal.Phase.Uncertain);
                if(original.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(plan.Operation,original.State,RebirthCamperTransferJournal.Phase.Applied);
                return Terminal(plan,out proof);
            }
            var accepted=Journal("Accepted").Read(plan.Operation);
            if(accepted!=null)
            {
                if(!Accepted(plan,accepted,out var saved))return false;
                if(RebirthCookingDropSnapshot.TryRead(GameIO.GetSaveGameDir(),saved.Element("snapshot"),Convert.FromBase64String(saved.Element("native").Value),current,new Budget(),out var recovered))
                {Publish(plan,accepted,recovered);if(original.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(plan.Operation,original.State,RebirthCamperTransferJournal.Phase.Applied);return Terminal(plan,out proof);}
            }
            if(!runners.TryGetValue(plan.Operation,out var runner))
            {
                if(runners.Count>=4||!SavedOwnerHandoff(actor,plan))return false;
                runner=new Runner{Plan=plan,Actor=actor,World=actor.world,Current=current};runners.Add(plan.Operation,runner);
            }
            if(original.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(invoking!=null||runner.Body!=null||!SavedOwnerHandoff(actor,plan,false))return false;
                var prototype=Prototype(actor,plan.Overflow);if(prototype==null)return false;
                try
                {
                    runner.Expected=ItemStack.Clone(prototype.bag.ItemGrid.items);runner.Class=prototype.entityClass;runner.Position=prototype.position;runner.RefPlayer=prototype.RefPlayerId;
                    var creation=new EntityCreationData(prototype){entityName=Localization.Get("xuiRebirthCamperOverflowBag"),id=-1};
                    if(!current())return false;journal.Advance(plan.Operation,original.State,RebirthCamperTransferJournal.Phase.Uncertain);
                    invoking=runner;try{GameManager.Instance.RequestToSpawnEntityServer(creation);}finally{invoking=null;}
                }
                finally{prototype.OnEntityUnload();}
            }
            if(runner.Ambiguous)return false;
            if(runner.Body==null&&accepted!=null&&Accepted(plan,accepted,out var prior))
            {runner.Body=actor.world.GetEntity((int)prior.Attribute("entity")) as EntityBackpack;if(runner.Body?.entityClass!=(int)prior.Attribute("class")||!NativeBag(plan,prior,out runner.Expected))return false;}
            // An entered native spawn without its actual accepted entity is
            // unknown. A matching later body never authorizes a second spawn.
            if(runner.Body==null)return false;
            void Capture(RebirthStationSnapshotEvidence.EntityImage image,string path,byte[] native)
            {
                if(!current())throw new InvalidDataException();
                var node=new XElement("removalParcelOverflowAccepted",new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),
                    new XAttribute("binding",plan.Binding),new XAttribute("entity",runner.Body.entityId),new XAttribute("class",runner.Body.entityClass),
                    new XElement("native",Convert.ToBase64String(native)),RebirthCookingDropSnapshot.Describe(GameIO.GetSaveGameDir(),path,image));
                byte[] captured=RebirthCamperRemovalParcelPlan.Bytes(node);
                if(accepted==null)accepted=Journal("Accepted").Prepare(plan.Operation,plan.Vehicle,plan.Creation,0,captured,captured);
                else if(!Accepted(plan,accepted,out var old)||(int)old.Attribute("entity")!=runner.Body.entityId||
                    !Convert.FromBase64String(old.Element("native").Value).SequenceEqual(native))throw new InvalidDataException();
            }
            if(runner.Observation==null)runner.Observation=RebirthCamperRemovalParcelObservation.Begin(runner.Body,runner.Expected,current,new Budget(),Capture);
            if(runner.Observation==null||accepted==null||!runner.Observation.Saved(out var published)||!current())return false;
            Publish(plan,accepted,published);
            var retained=journal.Read(plan.Operation);if(retained?.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(plan.Operation,retained.State,RebirthCamperTransferJournal.Phase.Applied);
            runner.Dispose();runners.Remove(plan.Operation);return Terminal(plan,out proof);
        }
        catch{proof=null;return false;}
    }
    private static void Publish(RebirthCamperRemovalParcelPlan plan,RebirthCamperTransferJournal.Record accepted,RebirthStationSnapshotEvidence.PublishedSnapshot proof)
    {
        byte[] bytes=RebirthCamperRemovalParcelPlan.Bytes(new XElement("removalParcelOverflowPublished",new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("binding",plan.Binding),
            new XAttribute("accepted",RebirthStationPublicationRecord.Digest(accepted.Before)),new XAttribute("region",proof.Path),new XAttribute("payload",proof.PayloadDigest),
            new XAttribute("x",proof.ChunkX),new XAttribute("z",proof.ChunkZ),new XAttribute("generation",proof.Generation)));
        Journal("Published").Prepare(plan.Operation,plan.Vehicle,plan.Creation,0,bytes,bytes);
    }
    [HarmonyPatch(typeof(World),nameof(World.SpawnEntityInWorld),typeof(Entity))]
    private static class Spawn
    {
        private static void Postfix(World __instance,Entity __0,bool __runOriginal)
        {
            var scope=invoking;if(!__runOriginal||scope==null||!ThreadManager.IsMainThread()||scope.Current?.Invoke()!=true||!ReferenceEquals(scope.World,__instance)||
                __0 is not EntityBackpack body||body.entityClass!=scope.Class||body.position!=scope.Position||body.RefPlayerId!=scope.RefPlayer||
                !ReferenceEquals(__instance.GetEntity(body.entityId),body)||!RebirthCamperEnergyItems.Same(body.bag?.ItemGrid?.items,scope.Expected))return;
            if(scope.Body!=null&&!ReferenceEquals(scope.Body,body))scope.Ambiguous=true;else scope.Body=body;
        }
    }
}
