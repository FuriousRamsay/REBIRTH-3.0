using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Native producers for the release-held Structure preimage. No caller may use
// a bag/block lookalike to manufacture a returned invocation or a saved effect.
internal static partial class RebirthNpcStructureRepairOriginal
{
    private sealed class RepairPublication
    {
        internal World World;internal Chunk Chunk;internal Guid Operation;
        internal RebirthNpcStableId Npc;internal string Binding;internal Func<bool> Current;
        internal RebirthStationSnapshotEvidence.Request Request;
    }
    private static RepairPublication publication;
    private static RebirthOriginalNativeBlockIdentity.Admission repairBirth;
    private const string DebitElement="structureRepairMaterialDebit";

    internal static bool HasNativePending(RebirthNpcStableId id)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return false;
            var row=Read(person);
            return row?.Attribute("nativeBinding")!=null&&(string)row.Attribute("phase")!="native-completed";
        }
        catch{return true;}
    }
    private static bool NativeRead(RebirthNpcStableId id,Guid operation,out XElement row)
    {
        row=null;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return ReadArchived(id,operation,out row);
            row=Read(person);
            if(row==null||(string)row.Attribute("operation")!=operation.ToString("N"))return ReadArchived(id,operation,out row);
            return row!=null&&(string)row.Attribute("version")=="1"&&
                (string)row.Attribute("npc")==id.ToString()&&
                (string)row.Attribute("operation")==operation.ToString("N");
        }
        catch{row=null;return false;}
    }
    private static bool NativeReplace(RebirthNpcStableId id,XElement old,XElement next)
    {
        try
        {
            string before=old.ToString(SaveOptions.DisableFormatting),after=next.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
            {
                if(!XNode.DeepEquals(Read(person),old))throw new InvalidOperationException("Structure repair original changed.");
                person.UnknownOptionalElements=person.UnknownOptionalElements.Select(x=>x==before?after:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return Guid.TryParseExact((string)next.Attribute("operation"),"N",out var op)&&
                NativeRead(id,op,out var saved)&&XNode.DeepEquals(saved,next);
        }
        catch{return false;}
    }
    private static Vector3i NativePosition(XElement row)=>new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"));
    private static bool NativeContext(RebirthNpcConcreteWorkContext context,XElement row,out EntityRebirthHumanoidNPC npc,out EntityPlayer owner)=>
        NativeContext(context,row,out npc,out owner,false);
    // coldRebind: the saved NPC embodiment may differ (restart/respawn); only
    // TryColdRebind passes true and then proves the live body against the saved receipts.
    private static bool NativeContext(RebirthNpcConcreteWorkContext context,XElement row,out EntityRebirthHumanoidNPC npc,out EntityPlayer owner,bool coldRebind)
    {
        npc=null;owner=null;
        var a=context?.Assignment;var world=GameManager.Instance?.World;
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||a==null||row==null||
            !RebirthNpcSingleRepairWork.IsDefinition(context.Definition?.DefinitionId)||
            (string)row.Attribute("definition")!=context.Definition.DefinitionId||
            intent==null||!RebirthNpcSingleRepairWork.Allows(intent)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||intent.PrimaryId!=a.AssignmentId||
            intent.Actor is not EntityRebirthHumanoidNPC human||human.RebirthRuntimeState?.StableId!=a.NpcId||
            human.IsDead()||human.IsPreparedRestorationPending||!ReferenceEquals(world.GetEntity(human.entityId),human)||
            !ReferenceEquals(human.world,world)||human.bag?.ItemGrid==null||human.inventory?.ItemGrid?.items==null||human.equipment?.ItemGrid?.items==null||
            human.Hand==null||human.Hand.IsBusy||human.GetAttackTarget()!=null||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(a.NpcId)||
            RebirthCompanionInventoryLockService.IsNpcItemLocked(a.NpcId,(string)row.Attribute("material"))||
            (string)row.Attribute("assignment")!=a.AssignmentId.ToString()||(string)row.Attribute("target")!=a.TargetKey||
            (string)row.Attribute("world")!=world.worldState?.Guid||
            !RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveStructureVolume(a,out var volume,out long revision)||
            (long?)row.Attribute("revision")!=revision||
            !RebirthNpcDeskVolumeWorkTargetAdapter.ContainsStructurePoint(a,
                NativePosition(row).ToVector3()+new UnityEngine.Vector3(.5f,.5f,.5f),out var pointRevision)||
            pointRevision!=revision||
            (human.position-NativePosition(row).ToVector3()).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)return false;
        owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        var persistent=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        if(owner==null||owner.IsDead()||persistent==null||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
            human.RebirthRuntimeState.OwnershipKind!=RebirthNpcOwnershipKind.Player||
            !string.Equals(identity.CanonicalId,human.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)||
            world.IsWithinTraderArea(NativePosition(row)))return false;
        var claim=world.GetLandClaimOwner(NativePosition(row),persistent);
        if(claim!=EnumLandClaimOwner.None&&claim!=EnumLandClaimOwner.Self)return false;
        var admittedBlock=new BlockValue{rawData=(uint)row.Attribute("blockRaw"),damage=(ushort)(int)row.Attribute("damage")};
        bool toolReturned=(int?)row.Attribute("nativeToolReturned")==1;
        var expectedPolicy=row.Element(toolReturned?"nativeToolPolicyAfter":"nativeRepairPolicy");
        if(expectedPolicy==null||
            !RebirthNpcSingleRepairPolicy.TryObserve(human,NativePosition(row),admittedBlock,true,out var observedPolicy,!toolReturned))return false;
        observedPolicy.Name=expectedPolicy.Name;
        if(!XNode.DeepEquals(expectedPolicy,observedPolicy)&&
            !(toolReturned&&(string)row.Attribute("nativeCold")=="1"&&ColdPolicyEquals(expectedPolicy,observedPolicy)))return false;
        if(row.Attribute("nativeBinding")!=null&&((string)row.Attribute("nativeOwner")!=identity.StorageKey||
            (int?)row.Attribute("nativeDeaths")!=owner.Died||(!coldRebind&&(uint?)row.Attribute("nativeEmbodiment")!=intent.Generation)||
            !RebirthOriginalActionScope.Matches(owner,identity.StorageKey,(string)row.Attribute("nativeCreation"))))return false;
        npc=human;return true;
    }
    private static XElement BagImage(EntityRebirthHumanoidNPC npc)=>new XElement("bag",
        Enumerable.Range(0,npc.bag.SlotCount).Select(i=>new XElement("slot",new XAttribute("index",i),
            new XAttribute("frame",npc.bag.ItemGrid[i]==null||npc.bag.ItemGrid[i].IsEmpty()?string.Empty:RebirthHumanEquipment.Frame(npc.bag.ItemGrid[i])))),
        new XElement("toolbelt",npc.inventory.ItemGrid.items.Select(x=>new XElement("slot",new XAttribute("frame",x==null||x.IsEmpty()?string.Empty:RebirthHumanEquipment.Frame(x))))),
        new XElement("equipment",npc.equipment.ItemGrid.items.Select(x=>new XElement("slot",new XAttribute("frame",x==null||x.IsEmpty()?string.Empty:RebirthHumanEquipment.Frame(x))))));
    private static bool BagMatches(EntityRebirthHumanoidNPC npc,XElement row,bool after)
    {
        var expected=new XElement(row.Element("nativeBagBefore"));expected.Name="bag";
        if(after)expected.Elements("slot").Single(x=>(int)x.Attribute("index")== (int)row.Attribute("bagSlot"))
            .SetAttributeValue("frame",(string)row.Attribute("after"));
        if(after&&(int?)row.Attribute("nativeToolReturned")==1)expected.Element("toolbelt").Elements("slot")
            .ElementAt((int)row.Attribute("nativeToolSlot")).SetAttributeValue("frame",(string)row.Attribute("nativeToolAfter"));
        return XNode.DeepEquals(expected,BagImage(npc));
    }
    private static XElement DebitReceipt(XElement row)=>new XElement(DebitElement,
        new XAttribute("operation",(string)row.Attribute("operation")),new XAttribute("binding",(string)row.Attribute("nativeBinding")),
        new XAttribute("stack",(string)row.Attribute("nativeStack")),new XAttribute("slot",(string)row.Attribute("bagSlot")),
        new XAttribute("after",(string)row.Attribute("after")));
    private static bool SavedDebit(RebirthNpcStableId id,XElement row)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null)return false;
            var equipment=person.UnknownOptionalElements.Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var receipt=equipment?.Elements(DebitElement).SingleOrDefault(x=>(string)x.Attribute("operation")== (string)row.Attribute("operation"));
            if(receipt==null||!XNode.DeepEquals(receipt,DebitReceipt(row)))return false;
            Guid stack=Guid.ParseExact((string)row.Attribute("nativeStack"),"N");
            var expected=new XElement(row.Element("nativeInventoryBefore").Elements().Single());
            var expectedStack=expected.Elements().Single(x=>(string)x.Attribute("id")==stack.ToString("N"));
            string after=(string)row.Attribute("after");
            if(string.IsNullOrEmpty(after))expectedStack.Remove();
            else expectedStack.SetAttributeValue("count",RebirthHumanEquipment.Decode(after).count);
            uint beforeRevision=(uint)row.Attribute("nativeRevision");
            if(beforeRevision==uint.MaxValue)return false;
            expected.SetAttributeValue("revision",beforeRevision+1);
            var published=row.Element("materialInventoryPublished")?.Elements().Single()??inventory.NativeStacks.Write();
            if(!XNode.DeepEquals(expected,published))return false;
            var actual=published.Elements().Select(x=>RebirthNpcNativeStackRecord.TryRead(x,out var value)?value:null)
                .SingleOrDefault(x=>x?.StackId==stack);
            if(string.IsNullOrEmpty(after))return actual==null;
            var intended=RebirthHumanEquipment.Decode(after);
            return actual!=null&&actual.Count==intended.count&&actual.Payload==RebirthNativeItemCodec.Encode(intended.itemValue);
        }
        catch{return false;}
    }
    // The native capture observer calls this only to retain/removal-account the
    // already bound stack; it confers no mutation or debit authority.
    internal static bool TryReadDebitManagedStack(RebirthNpcStableId id,XElement receipt,out Guid stack)
    {
        stack=Guid.Empty;
        return Guid.TryParseExact((string)receipt.Attribute("operation"),"N",out var op)&&NativeRead(id,op,out var row)&&
            row.Attribute("nativeBinding")!=null&&XNode.DeepEquals(receipt,DebitReceipt(row))&&
            Guid.TryParseExact((string)row.Attribute("nativeStack"),"N",out stack);
    }
    internal static bool TryAdmitNativeDebit(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||!NativeContext(context,row,out var npc,out var owner)||
                (string)row.Attribute("phase")!="prepared")return false;
            if(row.Attribute("nativeBinding")!=null)return BagMatches(npc,row,false);
            if(RemoteResourceTransactions.ActionBlocksNpc(id)||RebirthNpcMetabolismCustody.Blocks(id)||
                RebirthNpcWorkNativeTransferOriginal.HasPending(id)||RebirthSmartLootingHumanOriginal.HasPending(id))return false;
            var equipment=RebirthHumanEquipment.Read(npc);
            if(equipment.Element("swap")!=null||equipment.Element("bookMove")!=null)return false;
            RebirthHumanEquipment.Save(npc,equipment);
            var binding=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&
                (int)x.Attribute("index")== (int)row.Attribute("bagSlot"));
            if(binding==null||!Guid.TryParseExact((string)binding.Attribute("id"),"N",out var stack)||stack==Guid.Empty||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null||inventory.Revision>=uint.MaxValue-1||
                !inventory.NativeStacks.FitsObservationBytes(2*1024*1024))return false;
            var physical=npc.bag.ItemGrid[(int)row.Attribute("bagSlot")];
            if(physical==null||physical.IsEmpty()||RebirthHumanEquipment.Frame(physical)!=(string)row.Attribute("before")||
                RebirthCompanionInventoryLockService.IsNpcItemLocked(id,physical.itemValue.ItemClass.GetItemName()))return false;
            var next=new XElement(row);
            next.SetAttributeValue("nativeStack",stack.ToString("N"));next.SetAttributeValue("nativeRevision",inventory.Revision);
            next.SetAttributeValue("nativeOwner",RebirthStablePlayerIdentity.ComputeStorageKey(context.Assignment.ActorId));
            next.SetAttributeValue("nativeCreation",RebirthCamperCharacterAccess.CurrentCreation(owner));next.SetAttributeValue("nativeDeaths",owner.Died);
            next.SetAttributeValue("nativeEmbodiment",RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent.Generation);
            var before=BagImage(npc);before.Name="nativeBagBefore";next.Add(before);
            if(Encoding.UTF8.GetByteCount(before.ToString(SaveOptions.DisableFormatting))>2*1024*1024)return false;
            next.Add(new XElement("nativeInventoryBefore",inventory.NativeStacks.Write()));
            // Reserve the material/tool published snapshots and returned
            // policy within the existing immutable archive limit up front.
            if(Encoding.UTF8.GetByteCount(next.ToString(SaveOptions.DisableFormatting))+3L*Encoding.UTF8.GetByteCount(inventory.NativeStacks.Write().ToString(SaveOptions.DisableFormatting))+262144L>6*1024*1024)return false;
            next.SetAttributeValue("nativeBinding",RebirthRemoteResourceDurableJournal.Digest(next.ToString(SaveOptions.DisableFormatting)));
            return NativeReplace(id,row,next);
        }
        catch{return false;}
    }
    internal static bool TryDebitNativeMaterial(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||row.Attribute("nativeBinding")==null||
                !NativeContext(context,row,out var npc,out _))return false;
            if(SavedDebit(id,row))return true;
            // Only the first positively checkpointed attempt enters SetSlot.
            // An attempted original without its saved receipt is never retried.
            if((string)row.Attribute("phase")!="prepared"||!NativeBirthCurrent(npc.world,row,context.OperationId)||
                string.IsNullOrEmpty((string)row.Attribute("nativeBirthProof"))||!BagMatches(npc,row,false)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var beforeInventory)||
                beforeInventory.Revision!=(uint)row.Attribute("nativeRevision")||beforeInventory.NativeStacks==null||
                !XNode.DeepEquals(row.Element("nativeInventoryBefore").Elements().Single(),beforeInventory.NativeStacks.Write()))return false;
            var block=npc.world.GetBlock(NativePosition(row));
            if(block.rawData!=(uint)row.Attribute("blockRaw")||block.damage!=(int)row.Attribute("damage"))return false;
            var attempted=new XElement(row);attempted.SetAttributeValue("phase","native-debit-attempted");
            if(!NativeReplace(id,row,attempted)||!NativeContext(context,attempted,out npc,out _)||!BagMatches(npc,attempted,false))return false;
            var equipment=RebirthHumanEquipment.Read(npc);
            npc.bag.SetSlot((int)row.Attribute("bagSlot"),string.IsNullOrEmpty((string)row.Attribute("after"))?
                ItemStack.Empty.Clone():RebirthHumanEquipment.Decode((string)row.Attribute("after")));
            if(!NativeContext(context,attempted,out npc,out _)||!BagMatches(npc,attempted,true))return false;
            equipment.Add(DebitReceipt(attempted));
            if(string.IsNullOrEmpty((string)row.Attribute("after")))equipment.Element("bindings")?.Elements("binding")
                .Where(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")== (int)row.Attribute("bagSlot")).Remove();
            RebirthHumanEquipment.Save(npc,equipment);
            return SavedDebit(id,attempted);
        }
        catch{return false;}
    }
    private static string RepairKey(XElement row)=>"rebirth.npc.structure.repair.v1:"+(string)row.Attribute("operation");
    private static byte[] RepairStamp(XElement row)=>Encoding.UTF8.GetBytes("native-returned|"+(string)row.Attribute("nativeBinding"));
    private static string DebitProof(XElement row)=>RebirthRemoteResourceDurableJournal.Digest(
        DebitReceipt(row).ToString(SaveOptions.DisableFormatting)+"|"+row.Element("nativeInventoryBefore").ToString(SaveOptions.DisableFormatting)+"|"+(string)row.Attribute("nativeRevision"));
    // Retirement requires this exact positively archived terminal. An active
    // or uncertain preimage cannot release any native target identity claim.
    private static bool NativeRepairTerminal(World world,Guid operation,string digest,Vector3i position,string birth)=>
        RebirthNpcSingleRepairWork.ExecutionQualified&&ArchivedNativeTerminal(world,operation,digest,position,birth);
    private static bool NativeBirthCurrent(World world,XElement row,Guid operation)=>
        row.Attribute("nativeBirth")!=null&&RebirthOriginalNativeBlockIdentity.TryReadClaim(world,NativePosition(row),
            RebirthOriginalNativeBlockIdentity.StructureRepairPurpose,operation,(string)row.Attribute("nativeBinding"),out var before,out var after)&&
        before==(string)row.Attribute("nativeBirth")&&after==before;
    internal static bool TryAdmitNativeBlock(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||row.Attribute("nativeBinding")==null||
                (string)row.Attribute("phase")!="prepared"||!NativeContext(context,row,out var npc,out _)||!BagMatches(npc,row,false))return false;
            if(!RebirthOriginalNativeBlockIdentity.RegisterStructureRepairTerminalReader(NativeRepairTerminal))return false;
            RebirthOriginalNativeBlockIdentity.Install();
            if(row.Attribute("nativeBirthProof")!=null)return NativeBirthCurrent(npc.world,row,context.OperationId);
            bool Authorized()=>NativeRead(id,context.OperationId,out var saved)&&
                (string)saved.Attribute("nativeBinding")== (string)row.Attribute("nativeBinding")&&
                (string)saved.Attribute("phase")=="prepared"&&NativeContext(context,saved,out var live,out _)&&BagMatches(live,saved,false);
            if(repairBirth!=null&&(!repairBirth.Current()||repairBirth.Operation!=context.OperationId||repairBirth.Digest!=(string)row.Attribute("nativeBinding")))
            {repairBirth.Dispose();repairBirth=null;}
            if(repairBirth==null)repairBirth=row.Attribute("nativeBirth")==null?
                RebirthOriginalNativeBlockIdentity.Begin(npc.world,NativePosition(row),RebirthOriginalNativeBlockIdentity.StructureRepairPurpose,
                    context.OperationId,(string)row.Attribute("nativeBinding"),Authorized):
                RebirthOriginalNativeBlockIdentity.Rebind(npc.world,NativePosition(row),RebirthOriginalNativeBlockIdentity.StructureRepairPurpose,
                    context.OperationId,(string)row.Attribute("nativeBinding"),(string)row.Attribute("nativeBirth"),Authorized);
            if(repairBirth==null)return false;
            byte[] birthImage=RebirthOriginalNativeBlockIdentity.CopyMarker(npc.world,NativePosition(row));
            if(birthImage==null)return false;
            var sourceRow=new XElement(row);var nativePosition=NativePosition(row);
            repairBirth.SnapshotProof=bytes=>RebirthOriginalNativeBlockSnapshot.TryProve(bytes,
                World.toChunkXZ(nativePosition.x),World.toChunkXZ(nativePosition.z),nativePosition,
                (uint)sourceRow.Attribute("blockRaw"),(ushort)(int)sourceRow.Attribute("damage"),
                RebirthOriginalNativeBlockIdentity.NativeKey(nativePosition),birthImage,out _);
            // Retain the actual native identity before waiting for publication;
            // cold reentry can only rebind this identity, never mint a new one.
            if(row.Attribute("nativeBirth")==null)
            {
                var bound=new XElement(row);bound.SetAttributeValue("nativeBirth",repairBirth.Id);
                if(!NativeReplace(id,row,bound))return false;row=bound;
            }
            if(!repairBirth.Saved())return false;
            if(string.IsNullOrEmpty(repairBirth.SavedProof))return false;
            var published=new XElement(row);published.SetAttributeValue("nativeBirthProof",repairBirth.SavedProof);
            if(!NativeReplace(id,row,published))return false;
            repairBirth.Dispose();repairBirth=null;return true;
        }
        catch{return false;}
    }
    internal static bool TryInvokeNativeRepair(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||!NativeContext(context,row,out var npc,out _)||
                (string)row.Attribute("phase")!="native-tool-published"||!SavedDebit(id,row)||!SavedTool(id,row)||!BagMatches(npc,row,true)||
                !NativeBirthCurrent(npc.world,row,context.OperationId))return false;
            var world=npc.world;var position=NativePosition(row);var block=world.GetBlock(position);
            if(block.rawData!=(uint)row.Attribute("blockRaw")||block.damage!=(int)row.Attribute("damage")||
                block.isair||block.ischild||!block.Block.CanRepair(block)||
                !RebirthRemoteResourceBlockHost.Ready(world,position,out var chunk,out _,out var current)||
                chunk.ChunkCustomData.dict.ContainsKey(RepairKey(row)))return false;
            var attempted=new XElement(row);attempted.SetAttributeValue("phase","native-block-attempted");
            attempted.SetAttributeValue("nativeDebitProof",DebitProof(row));
            if(!NativeReplace(id,row,attempted)||!current()||!NativeContext(context,attempted,out npc,out _)||!BagMatches(npc,attempted,true))return false;
            var immediate=world.GetBlock(position);
            if(immediate.rawData!=block.rawData||immediate.damage!=block.damage)return false;
            block.Block.DamageBlock(world,position,block,-block.damage,npc.entityId);
            var after=world.GetBlock(position);
            if(!current()||!NativeContext(context,attempted,out npc,out _)||after.rawData!=block.rawData||after.damage!=0||
                !BagMatches(npc,attempted,true)||!NativeBirthCurrent(world,row,context.OperationId)||chunk.ChunkCustomData.dict.ContainsKey(RepairKey(row)))return false;
            string key=RepairKey(row);
            chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=RepairStamp(row)});chunk.isModified=true;
            return true;
        }
        catch{return false;}
    }
    internal static bool TryPublishNativeRepair(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var id=context.Assignment.NpcId;
            if(!NativeRead(id,context.OperationId,out var row)||!NativeContext(context,row,out var npc,out _)||
                (string)row.Attribute("phase")!="native-block-attempted"||!SavedDebit(id,row)||!SavedTool(id,row))return false;
            var world=npc.world;var position=NativePosition(row);string key=RepairKey(row);byte[] stamp=RepairStamp(row);
            if(!RebirthRemoteResourceBlockHost.Ready(world,position,out var chunk,out var provider,out var providerCurrent))return false;
            byte[] birth=RebirthOriginalNativeBlockIdentity.CopyMarker(world,position);if(birth==null)return false;
            bool Current()=>providerCurrent()&&NativeBirthCurrent(world,row,context.OperationId)&&
                (RebirthOriginalNativeBlockIdentity.CopyMarker(world,position)??Array.Empty<byte>()).SequenceEqual(birth)&&
                world.GetBlock(position).rawData==(uint)row.Attribute("blockRaw")&&world.GetBlock(position).damage==0&&
                chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data!=null&&marker.data.SequenceEqual(stamp)&&
                marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null;
            if(!Current())return false;
            if(publication!=null&&(!ReferenceEquals(publication.World,world)||publication.Operation!=context.OperationId||
                publication.Npc!=id||publication.Binding!=(string)row.Attribute("nativeBinding")||publication.Current?.Invoke()!=true))
            {publication.Request?.Dispose();publication=null;}
            if(publication?.Request!=null&&RebirthStationSnapshotEvidence.HasPublished(publication.Request)&&
                RebirthStationSnapshotEvidence.TryCopy(publication.Request,out var bytes)&&
                RebirthOriginalNativeBlockSnapshot.TryProve(bytes,chunk.X,chunk.Z,position,(uint)row.Attribute("blockRaw"),0,key,stamp,out var proof,null,
                    RebirthOriginalNativeBlockIdentity.NativeKey(position),birth))
            {
                var saved=new XElement(row);saved.SetAttributeValue("phase","native-block-published");saved.SetAttributeValue("nativeBlockProof",proof);
                if(!NativeReplace(id,row,saved))return false;
                publication.Request.Dispose();publication=null;return true;
            }
            if(publication!=null)return false;
            var request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,Current);if(request==null)return false;
            publication=new RepairPublication{World=world,Chunk=chunk,Npc=id,Operation=context.OperationId,Binding=(string)row.Attribute("nativeBinding"),Current=Current,Request=request};
            lock(chunk)
            {
                if(!Current())return false;
                chunk.InProgressSaving=true;
                try{provider.m_RegionFileManager.SaveChunkSnapshot(chunk,true);}finally{chunk.InProgressSaving=false;}
            }
            return false;
        }
        catch{return false;}
    }
    private static RebirthNpcWorkOutcomeRecord NativeOutcome(RebirthNpcWorkAssignment assignment,XElement row)=>new RebirthNpcWorkOutcomeRecord{
        OutcomeId=Guid.ParseExact((string)row.Attribute("operation"),"N"),AssignmentId=assignment.AssignmentId,NpcId=assignment.NpcId,
        DefinitionId=RebirthNpcSingleRepairWork.DefinitionId,TargetId=RebirthNpcSingleRepairWork.TargetId(assignment),
        StartedUtcTicks=assignment.CreatedUtcTicks,CompletedUtcTicks=(long)row.Attribute("nativeCompletedTicks"),
        DurableCompletion=true,DurableWitness="native-structure-repair:"+(string)row.Attribute("nativeBinding")+"|debit:"+
            (string)row.Attribute("nativeDebitProof")+"|tool:"+(string)row.Attribute("nativeToolProof")+"|block:"+(string)row.Attribute("nativeBlockProof"),
        State=RebirthNpcWorkOutcomeState.Completed,FailureCategory=RebirthNpcWorkFailureCategory.None,
        Detail="One selected damaged block repaired with saved native material debit, tool wear and block publication.",
        Consumed=new System.Collections.Generic.Dictionary<string,int>{{(string)row.Attribute("material"),(int)row.Attribute("quantity")}}};
    internal static bool AllowsNativeOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        try
        {
            if(outcome==null||!RebirthNpcSingleRepairWork.IsDefinition(outcome.DefinitionId)||outcome.State!=RebirthNpcWorkOutcomeState.Completed||
                !outcome.DurableCompletion||!RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)||!RebirthNpcSingleRepairWork.Allows(a)||
                a.NpcId!=outcome.NpcId||outcome.OutcomeId!=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId)||
                !NativeRead(a.NpcId,outcome.OutcomeId,out var row)||(string)row.Attribute("definition")!=outcome.DefinitionId||
                ((string)row.Attribute("phase")!="native-block-published"&&(string)row.Attribute("phase")!="native-completed")||
                (string)row.Attribute("assignment")!=a.AssignmentId.ToString()||(string)row.Attribute("target")!=a.TargetKey||
                (long?)row.Attribute("startedTicks")!=a.CreatedUtcTicks||!NativePosition(row).Equals(RebirthNpcSingleRepairWork.Position(a))||
                (string)row.Attribute("nativeDebitProof")!=DebitProof(row)||!PublishedTool(row)||
                ((string)row.Attribute("nativeBlockProof"))?.Length!=64||
                ((long?)row.Attribute("nativeCompletedTicks")??0)<=0)return false;
            var expected=NativeOutcome(a,row);
            return outcome.DurableWitness==expected.DurableWitness&&outcome.StartedUtcTicks==expected.StartedUtcTicks&&
                outcome.CompletedUtcTicks==expected.CompletedUtcTicks&&outcome.TargetId==expected.TargetId&&outcome.Detail==expected.Detail&&
                outcome.Produced==null&&outcome.Consumed?.Count==1&&outcome.Consumed.TryGetValue((string)row.Attribute("material"),out int used)&&
                used==(int)row.Attribute("quantity");
        }
        catch{return false;}
    }
    internal static bool AdvanceNativeOperation(RebirthNpcConcreteWorkContext context,out RebirthNpcWorkMutationResult result)
    {
        result=null;
        try
        {
            var a=context.Assignment;
            if(!NativeRead(a.NpcId,context.OperationId,out var row))
            {
                if(!TryPrepare(context,out var detail))return false;
                result=RebirthNpcWorkMutationResult.Continue(0f,detail);return true;
            }
            if((string)row.Attribute("phase")=="native-completed"&&RebirthNpcSingleRepairWork.IsDefinition(context.Definition?.DefinitionId)&&
                AllowsNativeOutcome(NativeOutcome(a,row)))
            {
                // Terminal row already saved by an earlier incarnation: report it, no live body needed.
                RebirthP9RepairShortfall.Clear(a.NpcId,a.AssignmentId);
                var done=NativeOutcome(a,row);
                result=new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                    Detail=done.Detail,Consumed=done.Consumed};return true;
            }
            if(!TryColdRebind(context,row,out row))return false;
            if(!NativeContext(context,row,out _,out _))return false;
            string phase=(string)row.Attribute("phase");
            if(phase=="prepared")
            {
                if(!TryAdmitNativeDebit(context))return false;
                if(!TryAdmitNativeBlock(context))
                {result=RebirthNpcWorkMutationResult.Continue(0f,"Exact native block identity and damaged preimage publication are pending.");return true;}
                if(!TryDebitNativeMaterial(context))return false;
                result=RebirthNpcWorkMutationResult.Continue(0f,"Native material debit saved; repair invocation remains owed.");return true;
            }
            if(phase=="native-debit-attempted")
            {
                if(!SavedDebit(a.NpcId,row)||!TryWearNativeTool(context))return false;
                result=RebirthNpcWorkMutationResult.Continue(0f,"Exact native tool wear saved; the block repair remains owed.");return true;
            }
            if(phase=="native-tool-attempted")return false; // Unknown entered wear/action prefix never repeats.
            if(phase=="native-tool-published")
            {
                if(!SavedDebit(a.NpcId,row)||!SavedTool(a.NpcId,row)||!TryInvokeNativeRepair(context))return false;
                result=RebirthNpcWorkMutationResult.Continue(0f,"Genuine native repair returned; exact chunk publication remains pending.");return true;
            }
            if(phase=="native-block-attempted")
            {
                if(!TryPublishNativeRepair(context))
                {
                    // A native marker is the positive returned-call receipt;
                    // without it an entered invocation is never replayed.
                    var world=GameManager.Instance?.World;var pos=NativePosition(row);
                    var chunk=world?.GetChunkSync(World.toChunkXZ(pos.x),World.toChunkXZ(pos.z)) as Chunk;
                    if(chunk?.ChunkCustomData?.dict==null||!chunk.ChunkCustomData.dict.TryGetValue(RepairKey(row),out var marker)||marker?.data==null||
                        !marker.data.SequenceEqual(RepairStamp(row)))return false;
                    result=RebirthNpcWorkMutationResult.Continue(0f,"Returned native repair is awaiting positive saved chunk readback.");return true;
                }
                return NativeRead(a.NpcId,context.OperationId,out row)&&AdvanceNativeOperation(context,out result);
            }
            if(phase!="native-block-published"&&phase!="native-completed")return false;
            if(!TryApplyNativeEffects(context,row,out row))return false;
            if(row.Attribute("nativeCompletedTicks")==null)
            {
                var completed=new XElement(row);completed.SetAttributeValue("nativeCompletedTicks",DateTime.UtcNow.Ticks);
                if(!NativeReplace(a.NpcId,row,completed))return false;row=completed;
            }
            if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(NativeOutcome(a,row)))return false;
            if((string)row.Attribute("phase")!="native-completed")
            {
                var terminal=new XElement(row);terminal.SetAttributeValue("phase","native-completed");
                if(!NativeReplace(a.NpcId,row,terminal))return false;
            }
            RebirthP9RepairShortfall.Clear(a.NpcId,a.AssignmentId);
            result=new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                Detail=NativeOutcome(a,row).Detail,Consumed=NativeOutcome(a,row).Consumed};return true;
        }
        catch{return false;}
    }
}
