using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// B07 saved native substrate. A desk Farm target is a volume, never the single block
// at its center. The selected native crop/preimage is checkpointed before an
// executor may attempt a block or inventory mutation. This class does not
// release the generic Work gate or credit a semantic inventory quantity.
internal static class RebirthNpcFarmOriginal
{
    private const string Element="farmWorkOriginal";
    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;
    private static bool AuthorizedSource(World world,RebirthNpcWorkAssignment assignment,
        EntityRebirthNPC npc,Vector3i position)
    {
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
        if(owner==null||owner.IsDead()||!ReferenceEquals(owner.world,world)||
           !ReferenceEquals(world.GetEntity(owner.entityId),owner)||
           npc?.RebirthRuntimeState?.OwnershipKind!=RebirthNpcOwnershipKind.Player||
           !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
           !string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase))return false;
        var persistent=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        if(persistent==null||world.IsWithinTraderArea(position))return false;
        var claim=world.GetLandClaimOwner(position,persistent);
        return (claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self)&&
            world.CanPickupBlockAt(position,persistent);
    }
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(world,recoveryWorld)){recoveryWorld=world;nextRecovery=0;recoveryCursor=0;}
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||
           RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||Time.unscaledTime<nextRecovery)return;
        nextRecovery=Time.unscaledTime+5f;
        var actors=RebirthNpcRuntimeRegistry.GetSnapshot();
        if(actors.Length==0)return;
        int count=Math.Min(4,actors.Length);
        for(int i=0;i<count;i++)
        {
            var id=actors[(recoveryCursor+i)%actors.Length].StableId;
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||
               world.GetEntity(entityId) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
               !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))continue;
            RebirthP8NpcWaterRefillWork.Recover(npc,id);
            RebirthP8NpcWaterMaintainer.Tick(world,npc,id);
            XElement row;
            try{row=Read(person);}catch{continue;}
            if(row==null||!Guid.TryParseExact((string)row.Attribute("operation"),"N",out var operation))continue;
            var phase=(string)row.Attribute("phase");
            if(phase!="prepared"&&Guid.TryParseExact((string)row.Attribute("operation"),"N",out var mgmtOp))
                TryApplyManagementIntent(id,mgmtOp); // B13 recovery, bounded retries every Tick interval
            if(phase=="block-attempted")TryPublishBlock(id,operation);
            else if(phase=="block-published"||phase=="bag-attempted")TryCreditBag(npc,operation);
            else if(phase=="bag-published"||phase=="completed")TryComplete(id,operation);
        }
        recoveryCursor=(recoveryCursor+count)%actors.Length;
    }
    internal static bool TryPrepare(RebirthNpcConcreteWorkContext context,out Vector3i cropPosition,out string detail)
    {
        cropPosition=default(Vector3i);detail="Saved Farm target or mature crop is unavailable.";
        var world=GameManager.Instance?.World;
        var assignment=context?.Assignment;
        if(world==null||world.IsRemote()||string.IsNullOrEmpty(world.worldState?.Guid)||
           !ThreadManager.IsMainThread()||assignment==null||
           context.OperationId==Guid.Empty||context.Target==null||
           !RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveFarmVolume(assignment,out var bounds,out var revision)||
           context.Target.TargetRevision!=revision.ToString(System.Globalization.CultureInfo.InvariantCulture)||
           !RebirthNpcWorkNavigationService.TryResolveNpc(assignment.NpcId,out var npc)||
           npc==null||npc.IsDead()||bounds.SqrDistance(npc.position)>36f)return false;
        var owned=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)&&
            (owned==null||!RebirthNpcSingleCropWork.Allows(owned)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(owned)||
             !ReferenceEquals(owned.Actor,npc)))return false;
        if(TryReadSaved(assignment.NpcId,context.OperationId,out var retained))
        {
            if((string)retained.Attribute("assignment")!=assignment.AssignmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)||
               (string)retained.Attribute("target")!=assignment.TargetKey||
               (string)retained.Attribute("revision")!=revision.ToString(System.Globalization.CultureInfo.InvariantCulture)||
               (string)retained.Attribute("world")!=world.worldState.Guid||
               RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)&&
               ((string)retained.Attribute("definition")!=assignment.DefinitionId||(long?)retained.Attribute("startedTicks")!=assignment.CreatedUtcTicks))return false;
            cropPosition=new Vector3i((int)retained.Attribute("x"),(int)retained.Attribute("y"),(int)retained.Attribute("z"));
            detail="Saved Farm original remains held for native settlement.";return true;
        }
        if(!TryRetirePrior(assignment.NpcId,context.OperationId))return false;
        // Only nearby, loaded crop blocks are considered; the scan is bounded
        // independently of the owner's maximum 256-block saved volume.
        var center=new Vector3i(Mathf.FloorToInt(npc.position.x),Mathf.FloorToInt(npc.position.y),Mathf.FloorToInt(npc.position.z));
        BlockValue selected=BlockValue.Air,replant=BlockValue.Air;string itemKey=null;
        bool found=false;
        for(int y=center.y-3;y<=center.y+3&&!found;y++)
        for(int x=center.x-6;x<=center.x+6&&!found;x++)
        for(int z=center.z-6;z<=center.z+6;z++)
        {
            var p=new Vector3i(x,y,z);
            if(RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)&&
                p!=new Vector3i(Mathf.FloorToInt(assignment.TargetPosition.x),Mathf.FloorToInt(assignment.TargetPosition.y),Mathf.FloorToInt(assignment.TargetPosition.z)))continue;
            if(!bounds.Contains(new Vector3(x+.5f,y+.5f,z+.5f)))continue;
            if(!AuthorizedSource(world,assignment,npc,p))continue;
            var value=world.GetBlock(p);var crop=value.Block as BlockPlantGrowingRebirth;
            if(crop==null||!crop.IsFullyGrownCrop(value)||!RebirthUtilities.IsPlayerGrownHarvestCrop(value))continue;
            if(!RebirthNpcAdvancedFarmingHarvestAdapter.TryResolveHarvestItem(value,out var key))continue;
            string seed=RebirthUtilities.GetSeedItemNameFromPlantBlock(value);
            var seedBlock=string.IsNullOrEmpty(seed)?BlockValue.Air:Block.GetBlockValue(seed);
            if(seedBlock.isair)continue;
            seedBlock.rotation=value.rotation;seedBlock.meta=value.meta;seedBlock.meta2=0;
            if(seedBlock.rawData==value.rawData)continue;
            if(!RebirthNpcDeskVolumeWorkTargetAdapter.ContainsFarmPoint(assignment,
                new Vector3(x+.5f,y+.5f,z+.5f),out var pointRevision)||pointRevision!=revision)continue;
            replant=seedBlock;
            cropPosition=p;selected=value;itemKey=key;found=true;break;
        }
        if(!found)return false;
        if(RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)&&!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(assignment))return false;
        // The native chunk marker is an incarnation witness, not a completion
        // receipt. A later block publication must prove the exact postimage.
        string source=AdvancedFarmingHarvestSettlement.EnsureSource(world,cropPosition);
        if(!Guid.TryParseExact(source,"N",out var sourceId)||sourceId==Guid.Empty||
           !AdvancedFarmingHarvestSettlement.MatchesSource(world,cropPosition,source))return false;
        if(npc.bag?.ItemGrid==null||npc.IsPreparedRestorationPending||npc.Hand==null||npc.Hand.IsBusy||
           RebirthNpcWorkNativeTransferOriginal.HasPending(assignment.NpcId))return false;
        var valueToCredit=ItemClass.GetItem(itemKey);
        var stackToCredit=new ItemStack(valueToCredit,1);
        if(valueToCredit.IsEmpty()||!RebirthHumanEquipment.Accept(10,stackToCredit))return false;
        int creditSlot=-1;
        for(int i=0;i<npc.bag.SlotCount;i++)
            if((npc.bag.ItemGrid[i]==null||npc.bag.ItemGrid[i].IsEmpty())&&
               RebirthHumanEquipment.Accept(10+i,stackToCredit)){creditSlot=i;break;}
        if(creditSlot<0)return false;
        string creditId=Guid.NewGuid().ToString("N");
        var image=new XElement(Element,new XAttribute("version",1),new XAttribute("npc",assignment.NpcId.ToString()),
            RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)?new XAttribute("definition",assignment.DefinitionId):null,
            RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)?new XAttribute("startedTicks",assignment.CreatedUtcTicks):null,
            RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)?new XAttribute("embodiment",owned.Generation):null,
            new XAttribute("assignment",assignment.AssignmentId),new XAttribute("operation",context.OperationId.ToString("N")),
            new XAttribute("target",assignment.TargetKey),new XAttribute("revision",revision),
            new XAttribute("world",world.worldState?.Guid??string.Empty),new XAttribute("source",source),
            new XAttribute("x",cropPosition.x),new XAttribute("y",cropPosition.y),new XAttribute("z",cropPosition.z),
            new XAttribute("raw",selected.rawData),new XAttribute("replantRaw",replant.rawData),
            new XAttribute("type",selected.type),new XAttribute("meta",selected.meta),new XAttribute("meta2",selected.meta2),
            new XAttribute("rotation",selected.rotation),new XAttribute("damage",selected.damage),
            new XAttribute("item",itemKey),new XAttribute("creditSlot",creditSlot),
            new XAttribute("creditId",creditId),new XAttribute("creditFrame",RebirthHumanEquipment.Frame(stackToCredit)),
            new XAttribute("phase","prepared"));
        // B13 journaled intent: persisted with the prepared row, before any block commit.
        if(RebirthNpcManagementGrowth.TryCaptureIntent(assignment.NpcId,out var mgmtOwner,out var mgmtLevel))
        {
            image.SetAttributeValue("mgmtOwner",mgmtOwner);
            image.SetAttributeValue("mgmtLevel",mgmtLevel.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        string raw=image.ToString(SaveOptions.DisableFormatting);
        if(!RebirthNpcAggregatePersistenceStore.TryGet(assignment.NpcId,out var person))return false;
        var old=Read(person);
        if(old!=null)
        {
            if(!XNode.DeepEquals(old,image)) { detail="Another Farm original remains held for reconciliation.";return false; }
        }
        else
        {
            if(!RebirthNpcAggregatePersistenceStore.Mutate(assignment.NpcId,record=>
            {
                if(Read(record)!=null)throw new InvalidDataException("Farm original changed before publication.");
                record.UnknownOptionalElements=(record.UnknownOptionalElements??Array.Empty<string>()).Concat(new[]{raw}).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
        }
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(assignment.NpcId,out var saved)||
           !XNode.DeepEquals(Read(saved),image))return false;
        detail="Mature crop selected with saved original; native custody and outcome remain held.";
        return true;
    }
    private static XElement Read(RebirthNpcPersistentRecord person)
    {
        XElement result=null;
        foreach(var raw in person.UnknownOptionalElements??Array.Empty<string>())
        {
            var row=RebirthCompanionDeskStore.Parse(raw);
            if(row.Name!=Element)continue;
            if(result!=null)throw new InvalidDataException("Duplicate Farm original.");
            result=row;
        }
        return result;
    }
    private static bool TryRetirePrior(RebirthNpcStableId npc,Guid nextOperation)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var old=Read(person);
            if(old==null)return true;
            if(!Guid.TryParseExact((string)old.Attribute("operation"),"N",out var prior)||
               prior==nextOperation||(string)old.Attribute("phase")!="completed"||
               !TryComplete(npc,prior))return false;
            string image=old.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,record=>
            {
                var rows=record.UnknownOptionalElements??Array.Empty<string>();
                if(rows.Count(x=>XElement.Parse(x).Name==Element)!=1||!rows.Contains(image))
                    throw new InvalidDataException("Completed Farm original changed before retirement.");
                record.UnknownOptionalElements=rows.Where(x=>x!=image).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var saved)&&Read(saved)==null;
        }
        catch{return false;}
    }

    // A phase is only authoritative after the exact aggregate primary is read
    // back. Callers may never infer block or bag settlement from an in-memory row.
    internal static bool TryReadSaved(RebirthNpcStableId npc,Guid operation,out XElement original)
    {
        original=null;
        try
        {
            if(operation==Guid.Empty||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var row=Read(person);
            if(row==null||(string)row.Attribute("operation")!=operation.ToString("N")||
               (string)row.Attribute("npc")!=npc.ToString()||
               row.Attribute("version")?.Value!="1")return false;
            original=row;return true;
        }
        catch{return false;}
    }

    internal static bool TryCheckpointPhase(RebirthNpcStableId npc,Guid operation,string expected,
        string next,string proof)
    {
        if(string.IsNullOrEmpty(expected)||string.IsNullOrEmpty(next)||proof==null||
           !TryReadSaved(npc,operation,out var previous)||
           (string)previous.Attribute("phase")!=expected)return false;
        try
        {
            var updated=new XElement(previous);
            updated.SetAttributeValue("phase",next);
            updated.SetAttributeValue("proof",proof);
            string oldImage=previous.ToString(SaveOptions.DisableFormatting);
            string newImage=updated.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,record=>
            {
                var rows=record.UnknownOptionalElements??Array.Empty<string>();
                if(!rows.Contains(oldImage)||rows.Count(x=>XElement.Parse(x).Name==Element)!=1)
                    throw new InvalidDataException("Farm original changed before phase publication.");
                record.UnknownOptionalElements=rows.Select(x=>x==oldImage?newImage:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return TryReadSaved(npc,operation,out var saved)&&XNode.DeepEquals(saved,updated);
        }
        catch{return false;}
    }

    private static string CreditBinding(XElement row,uint revision,string nativeImage)
        =>RebirthRemoteResourceDurableJournal.Digest(string.Join("|",new[]{
            (string)row.Attribute("npc"),(string)row.Attribute("assignment"),
            (string)row.Attribute("operation"),(string)row.Attribute("world"),
            (string)row.Attribute("source"),(string)row.Attribute("raw"),
            (string)row.Attribute("replantRaw"),(string)row.Attribute("creditId"),
            (string)row.Attribute("creditSlot"),(string)row.Attribute("creditFrame"),
            revision.ToString(System.Globalization.CultureInfo.InvariantCulture),nativeImage})+
            (RebirthNpcSingleCropWork.IsDefinition((string)row.Attribute("definition"))?
                "|"+string.Join("|",new[]{(string)row.Attribute("definition"),(string)row.Attribute("target"),(string)row.Attribute("revision"),
                    (string)row.Attribute("x"),(string)row.Attribute("y"),(string)row.Attribute("z"),(string)row.Attribute("startedTicks"),(string)row.Attribute("embodiment")}):string.Empty));

    // Positive native credit intent is checkpointed while the crop still
    // exists. A changed native bag revision requires a new action, never a
    // silent rebasing of this pending operation.
    internal static bool TryAdmitNativeCredit(EntityRebirthHumanoidNPC npc,Guid operation)
    {
        try
        {
            var id=npc?.RebirthRuntimeState?.StableId??default(RebirthNpcStableId);
            if(!ThreadManager.IsMainThread()||id.IsEmpty||npc.IsDead()||
               npc.IsPreparedRestorationPending||
               !ReferenceEquals(npc.world,GameManager.Instance?.World)||npc.world.IsRemote()||
               !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||
               !TryReadSaved(id,operation,out var row)||
               (string)row.Attribute("phase")!="prepared"||
               !int.TryParse((string)row.Attribute("creditSlot"),out int slot)||
               slot<0||slot>=npc.bag.SlotCount||
               npc.bag.ItemGrid[slot]!=null&&!npc.bag.ItemGrid[slot].IsEmpty()||
               npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null||
               RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||
               RebirthNpcWorkNativeTransferOriginal.HasPending(id))return false;
            if(row.Attribute("nativeCreditOriginal")!=null)return ValidCreditOriginal(id,row);
            var equipment=RebirthHumanEquipment.Read(npc);
            RebirthHumanEquipment.Save(npc,equipment);
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var saved)||saved.Revision==uint.MaxValue)return false;
            string nativeImage=saved.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty;
            var candidate=new XElement(row);
            candidate.SetAttributeValue("nativeCreditRevision",saved.Revision);
            candidate.SetAttributeValue("nativeCreditOriginal",CreditBinding(candidate,saved.Revision,nativeImage));
            string oldImage=row.ToString(SaveOptions.DisableFormatting);
            string newImage=candidate.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(id,record=>
            {
                var rows=record.UnknownOptionalElements??Array.Empty<string>();
                if(!rows.Contains(oldImage)||rows.Count(x=>XElement.Parse(x).Name==Element)!=1)
                    throw new InvalidDataException("Farm credit intent changed before publication.");
                record.UnknownOptionalElements=rows.Select(x=>x==oldImage?newImage:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return TryReadSaved(id,operation,out var current)&&XNode.DeepEquals(current,candidate)&&
                ValidCreditOriginal(id,current);
        }
        catch{return false;}
    }

    private static bool ValidCreditOriginal(RebirthNpcStableId npc,XElement row)
    {
        try
        {
            if(!uint.TryParse((string)row.Attribute("nativeCreditRevision"),out var revision)||
               !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc,out var saved)||
               saved.Revision!=revision)return false;
            string image=saved.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty;
            return (string)row.Attribute("nativeCreditOriginal")==CreditBinding(row,revision,image);
        }
        catch{return false;}
    }

    // Observe a native saved-chunk postimage only after the source marker and
    // exact block mutation commit were recorded. The observer's digest and
    // generation are bound into the persisted phase before any bag credit.
    internal static bool TryPublishBlock(RebirthNpcStableId npc,Guid operation)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!TryReadSaved(npc,operation,out var row)||
               (string)row.Attribute("phase")!="block-attempted"||
               !Guid.TryParseExact((string)row.Attribute("source"),"N",out var source)||
               source==Guid.Empty||!uint.TryParse((string)row.Attribute("raw"),out var before)||
               !uint.TryParse((string)row.Attribute("replantRaw"),out var after))return false;
            var world=GameManager.Instance?.World;
            if(world==null||world.IsRemote()||world.worldState?.Guid!=(string)row.Attribute("world"))return false;
            var position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"));
            bool Postimage()=>world.GetBlock(position).rawData==after&&
                AdvancedFarmingHarvestSettlement.HasCommit(world,position,operation,source,before,after);
            if(!Postimage()||!AdvancedFarmingHarvestPublication.Observe(operation,source,world,position,
                Postimage,out var proof)||proof==null||proof.World!=(string)row.Attribute("world")||
                proof.Operation!=operation||proof.Source!=source||proof.X!=position.x||
                proof.Y!=position.y||proof.Z!=position.z||proof.Digest?.Length!=64||
                proof.Generation<=0)return false;
            string witness=proof.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+proof.Digest;
            if(!TryCheckpointPhase(npc,operation,"block-attempted","block-published",witness))return false;
            AdvancedFarmingHarvestPublication.Complete(operation);
            return true;
        }
        catch{return false;}
    }

    // The narrow one-crop EAI calls this only after actual arrival and saved
    // native credit intent. General volume/plant/water work remains held.
    internal static bool TryCommitBlock(RebirthNpcConcreteWorkContext context)
    {
        try
        {
            var assignment=context?.Assignment;
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||assignment==null||
               context.OperationId==Guid.Empty||
               !TryReadSaved(assignment.NpcId,context.OperationId,out var row)||
               // No source mutation is admitted until an exact positive native
               // bag-credit intent has been saved and read back.
               row.Attribute("nativeCreditOriginal")==null||
               !ValidCreditOriginal(assignment.NpcId,row)||
               (string)row.Attribute("assignment")!=assignment.AssignmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)||
               (string)row.Attribute("target")!=assignment.TargetKey||
               !RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveFarmVolume(assignment,out var volume,out var revision)||
               (string)row.Attribute("revision")!=revision.ToString(System.Globalization.CultureInfo.InvariantCulture)||
               world.worldState?.Guid!=(string)row.Attribute("world")||
               !Guid.TryParseExact((string)row.Attribute("source"),"N",out var source)||source==Guid.Empty||
               !uint.TryParse((string)row.Attribute("raw"),out var before)||
               !uint.TryParse((string)row.Attribute("replantRaw"),out var after)||before==after||
               !RebirthNpcWorkNavigationService.TryResolveNpc(assignment.NpcId,out var npc)||
               npc is not EntityRebirthHumanoidNPC human||npc.IsDead()||npc.GetAttackTarget()!=null||
               human.IsPreparedRestorationPending||human.Hand==null||human.Hand.IsBusy||
               !int.TryParse((string)row.Attribute("creditSlot"),out int creditSlot)||
               creditSlot<0||creditSlot>=human.bag.SlotCount||
               human.bag.ItemGrid[creditSlot]!=null&&!human.bag.ItemGrid[creditSlot].IsEmpty()||
               RebirthNpcInteractionSessionService.IsInventoryAccessing(assignment.NpcId)||
               RebirthNpcWorkNativeTransferOriginal.HasPending(assignment.NpcId)||
               volume.SqrDistance(npc.position)>36f)return false;
            var position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"));
            if(RebirthNpcSingleCropWork.IsDefinition(assignment.DefinitionId)&&
                (RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
                 !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcSingleCropWork.Allows(intent)||
                 !ReferenceEquals(intent.Actor,human)||(uint?)row.Attribute("embodiment")!=intent.Generation||
                 (long?)row.Attribute("startedTicks")!=assignment.CreatedUtcTicks||
                 (npc.position-position.ToVector3()).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared))return false;
            if(!RebirthNpcDeskVolumeWorkTargetAdapter.ContainsFarmPoint(assignment,
                   new Vector3(position.x+.5f,position.y+.5f,position.z+.5f),out var pointRevision)||
               pointRevision!=revision||
               !AuthorizedSource(world,assignment,npc,position)||
               world.GetBlock(position).rawData!=before||
               !AdvancedFarmingHarvestSettlement.MatchesSource(world,position,source.ToString("N")))return false;
            if((string)row.Attribute("phase")=="prepared")
            {
                if(!TryCheckpointPhase(assignment.NpcId,context.OperationId,"prepared","block-attempted",string.Empty))return false;
            }
            else if((string)row.Attribute("phase")!="block-attempted")return false;
            var original=new BlockValue{rawData=before};
            var destination=RebirthUtilities.HarvestDestination(original,true);
            if(destination.rawData!=after||!RebirthUtilities.CommitHarvestSource(world,position,original,true,context.OperationId,source))return false;
            // B13: intent is already journaled; try now, recovery (Tick/TryComplete) retries until settled.
            TryApplyManagementIntent(assignment.NpcId,context.OperationId);
            return TryPublishBlock(assignment.NpcId,context.OperationId);
        }
        catch{return false;}
    }

    private static bool SavedNativeCredit(RebirthNpcStableId npc,Guid creditId,int slot,string frame)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person)||
               !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc,out var inventory)||
               inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
            if((string)binding?.Attribute("id")!=creditId.ToString("N"))return false;
            var native=inventory.NativeStacks.Write().Elements().Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null).ToArray();
            var credited=native.SingleOrDefault(x=>x?.StackId==creditId);
            var stack=RebirthHumanEquipment.Decode(frame);
            return credited!=null&&credited.Count==stack.count&&
                credited.Payload==RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch{return false;}
    }

    // The saved block proof is required before positive native bag custody.
    // Cold observation may resume this only after a durable block proof and
    // native credit identity; the new explicit purpose also pins embodiment.
    internal static bool TryCreditBag(EntityRebirthHumanoidNPC npc,Guid operation)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||npc==null||npc.IsDead()||
               npc.IsPreparedRestorationPending||!ReferenceEquals(world.GetEntity(npc.entityId),npc)||
               npc.RebirthRuntimeState==null||!TryReadSaved(npc.RebirthRuntimeState.StableId,operation,out var row)||
               (string)row.Attribute("phase")!="block-published"&&
               (string)row.Attribute("phase")!="bag-attempted"||
               (string)row.Attribute("world")!=world.worldState?.Guid||
               row.Attribute("nativeCreditOriginal")==null||
               !Guid.TryParseExact((string)row.Attribute("creditId"),"N",out var credit)||credit==Guid.Empty||
               !int.TryParse((string)row.Attribute("creditSlot"),out int slot)||
               slot<0||slot>=npc.bag.SlotCount)return false;
            if(npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null||
               RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId)||
               RebirthNpcWorkNativeTransferOriginal.HasPending(npc.RebirthRuntimeState.StableId))return false;
            if(RebirthNpcSingleCropWork.IsDefinition((string)row.Attribute("definition"))&&
                (!RebirthNpcAggregatePersistenceStore.TryGetView(npc.RebirthRuntimeState.StableId,out var person)||
                 person?.Presence==null||(uint?)row.Attribute("embodiment")!=person.Presence.EmbodimentGeneration))return false;
            string frame=(string)row.Attribute("creditFrame");
            var intended=RebirthHumanEquipment.Decode(frame);
            if(intended.IsEmpty()||intended.count!=1||!RebirthHumanEquipment.Accept(10+slot,intended))return false;
            bool alreadySaved=SavedNativeCredit(npc.RebirthRuntimeState.StableId,credit,slot,frame);
            if(!alreadySaved&&!ValidCreditOriginal(npc.RebirthRuntimeState.StableId,row))return false;
            bool firstCredit=(string)row.Attribute("phase")=="block-published";
            if(firstCredit&&
               !TryCheckpointPhase(npc.RebirthRuntimeState.StableId,operation,
                   "block-published","bag-attempted",(string)row.Attribute("proof")??string.Empty))return false;
            var bag=npc.bag.ItemGrid[slot];
            if(bag!=null&&!bag.IsEmpty()&&RebirthHumanEquipment.Frame(bag)!=frame)return false;
            // A live lookalike without the saved credit ID could have arrived
            // after a crash between SetSlot and Save, or from another action.
            // There is no durable owner witness to distinguish those cases.
            if(bag!=null&&!bag.IsEmpty()&&!alreadySaved)return false;
            // An entered setter is not an unspent permission. Empty physical
            // space after a saved/unknown credit cannot prove the write never
            // happened (the item may have moved or been consumed).
            if((bag==null||bag.IsEmpty())&&(!firstCredit||alreadySaved))return false;
            var equipment=RebirthHumanEquipment.Read(npc);
            var bindings=equipment.Element("bindings");
            if(bindings==null){bindings=new XElement("bindings");equipment.Add(bindings);}
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc.RebirthRuntimeState.StableId,
                   out var savedInventory))return false;
            var matching=(savedInventory.NativeStacks?.Write().Elements()??Enumerable.Empty<XElement>()).Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null)
                .Where(x=>x!=null&&x.Count==intended.count&&
                    x.Payload==RebirthNativeItemCodec.Encode(intended.itemValue)).ToArray();
            if(matching.Any(x=>x.StackId!=credit&&
               !bindings.Elements("binding").Any(b=>(string)b.Attribute("id")==x.StackId.ToString("N"))))return false;
            var binding=bindings.Elements("binding").SingleOrDefault(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
            if(binding!=null&&(string)binding.Attribute("id")!=credit.ToString("N")||
               bindings.Elements("binding").Any(x=>!ReferenceEquals(x,binding)&&
                   (string)x.Attribute("id")==credit.ToString("N")))return false;
            if(bag==null||bag.IsEmpty())npc.bag.SetSlot(slot,intended);
            if(binding==null){binding=new XElement("binding",new XAttribute("area","bag"),
                new XAttribute("index",slot));bindings.Add(binding);}
            binding.SetAttributeValue("id",credit.ToString("N"));
            RebirthHumanEquipment.Save(npc,equipment);
            if(!SavedNativeCredit(npc.RebirthRuntimeState.StableId,credit,slot,frame))return false;
            return TryCheckpointPhase(npc.RebirthRuntimeState.StableId,operation,
                "bag-attempted","bag-published",(string)row.Attribute("proof")??string.Empty);
        }
        catch{return false;}
    }

    private static string CompletionWitness(XElement row)
        =>"saved-farm-block:"+((string)row.Attribute("proof")??string.Empty)+
          "|source:"+((string)row.Attribute("source")??string.Empty)+
          "|credit:"+((string)row.Attribute("creditId")??string.Empty)+
          "|operation:"+((string)row.Attribute("operation")??string.Empty);

    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        try
        {
            if(outcome==null||outcome.DefinitionId!=RebirthNpcFarmingWorkPackageService.HarvestDefinitionId&&!RebirthNpcSingleCropWork.IsDefinition(outcome.DefinitionId)||
               outcome.State!=RebirthNpcWorkOutcomeState.Completed||!outcome.DurableCompletion||
               outcome.OutcomeId==Guid.Empty||!TryReadSaved(outcome.NpcId,outcome.OutcomeId,out var row)||
               (string)row.Attribute("phase")!="completed"||
               outcome.DefinitionId!=((string)row.Attribute("definition")??RebirthNpcFarmingWorkPackageService.HarvestDefinitionId)||
               outcome.AssignmentId.ToString(System.Globalization.CultureInfo.InvariantCulture)!=(string)row.Attribute("assignment")||
               outcome.TargetId!=(string)row.Attribute("target")||
               outcome.StartedUtcTicks!=((long?)row.Attribute("startedTicks")??0L)||
               outcome.DurableWitness!=CompletionWitness(row)||
               !long.TryParse((string)row.Attribute("completedTicks"),out var completed)||
               outcome.CompletedUtcTicks!=completed||outcome.Produced==null||outcome.Produced.Count!=1||
               !outcome.Produced.TryGetValue((string)row.Attribute("item"),out int count)||count!=1)return false;
            return Guid.TryParseExact((string)row.Attribute("creditId"),"N",out var credit)&&
                int.TryParse((string)row.Attribute("creditSlot"),out int slot)&&
                SavedNativeCredit(outcome.NpcId,credit,slot,(string)row.Attribute("creditFrame"));
        }
        catch{return false;}
    }

    // B13 recovery step. Returns true when no intent is pending (none, applied, skipped, or given up).
    // Only acts once the replanted block is observed at the position. Bounded: MaxAttempts, then gives up
    // (neutral native duration). Idempotent; never overwrites another bonus.
    internal static bool TryApplyManagementIntent(RebirthNpcStableId npc,Guid operation)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!TryReadSaved(npc,operation,out var row))return false;
            string state=(string)row.Attribute("mgmt");
            if(row.Attribute("mgmtOwner")==null||state=="done"||state=="gaveup")return true;
            var world=GameManager.Instance?.World;
            if(world==null||world.IsRemote()||world.worldState?.Guid!=(string)row.Attribute("world")||
               !uint.TryParse((string)row.Attribute("replantRaw"),out var after)||
               !float.TryParse((string)row.Attribute("mgmtLevel"),System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,out var level))return false;
            var position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"));
            if(world.GetBlock(position).rawData!=after)return false; // commit not observed yet
            int tries=(int?)row.Attribute("mgmtTries")??0;
            string next;
            if(RebirthNpcManagementGrowth.Stamp(world,position,(string)row.Attribute("mgmtOwner"),level))next="done";
            else if(tries+1>=RebirthNpcManagementGrowth.MaxAttempts)next="gaveup";
            else next=null;
            var updated=new XElement(row);
            if(next!=null)updated.SetAttributeValue("mgmt",next);
            else updated.SetAttributeValue("mgmtTries",tries+1);
            string oldImage=row.ToString(SaveOptions.DisableFormatting);
            string newImage=updated.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,record=>
            {
                var rows=record.UnknownOptionalElements??Array.Empty<string>();
                if(!rows.Contains(oldImage)||rows.Count(x=>XElement.Parse(x).Name==Element)!=1)
                    throw new InvalidDataException("Farm original changed before management intent update.");
                record.UnknownOptionalElements=rows.Select(x=>x==oldImage?newImage:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return next!=null;
        }
        catch{return false;}
    }

    internal static bool TryComplete(RebirthNpcStableId npc,Guid operation)
    {
        try
        {
            if(!TryApplyManagementIntent(npc,operation))return false; // hold completion until settled
            if(!ThreadManager.IsMainThread()||!TryReadSaved(npc,operation,out var row)||
               ((string)row.Attribute("phase")!="bag-published"&&
                (string)row.Attribute("phase")!="completed")||
               !Guid.TryParseExact((string)row.Attribute("creditId"),"N",out var credit)||
               !int.TryParse((string)row.Attribute("creditSlot"),out int slot)||
               !SavedNativeCredit(npc,credit,slot,(string)row.Attribute("creditFrame"))||
               string.IsNullOrEmpty((string)row.Attribute("proof")))return false;
            if((string)row.Attribute("phase")=="bag-published")
            {
                var completed=new XElement(row);
                completed.SetAttributeValue("phase","completed");
                completed.SetAttributeValue("completedTicks",DateTime.UtcNow.Ticks);
                string before=row.ToString(SaveOptions.DisableFormatting);
                string after=completed.ToString(SaveOptions.DisableFormatting);
                if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,person=>
                {
                    var rows=person.UnknownOptionalElements??Array.Empty<string>();
                    if(!rows.Contains(before)||rows.Count(x=>XElement.Parse(x).Name==Element)!=1)
                        throw new InvalidDataException("Farm completion original changed.");
                    person.UnknownOptionalElements=rows.Select(x=>x==before?after:x).ToArray();
                }))return false;
                RebirthNpcAggregatePersistenceStore.Save();
                if(!TryReadSaved(npc,operation,out row)||!XNode.DeepEquals(row,completed))return false;
            }
            if(!ulong.TryParse((string)row.Attribute("assignment"),out var assignment)||
               !long.TryParse((string)row.Attribute("completedTicks"),out var ticks)||
               ticks<=0)return false;
            string item=(string)row.Attribute("item");
            var outcome=new RebirthNpcWorkOutcomeRecord{
                OutcomeId=operation,AssignmentId=assignment,NpcId=npc,
                DefinitionId=(string)row.Attribute("definition")??RebirthNpcFarmingWorkPackageService.HarvestDefinitionId,
                TargetId=(string)row.Attribute("target"),DurableCompletion=true,
                DurableWitness=CompletionWitness(row),StartedUtcTicks=(long?)row.Attribute("startedTicks")??0L,CompletedUtcTicks=ticks,
                State=RebirthNpcWorkOutcomeState.Completed,
                FailureCategory=RebirthNpcWorkFailureCategory.None,
                Produced=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{item,1}}};
            if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome))return false;
            var crop=new BlockValue{rawData=(uint)row.Attribute("raw")};
            float raw=RebirthPhase8WorldOutputTrainingService.CalculateFarmingHarvestRaw(1);
            if(raw<=0f||crop.Block==null||
               !RebirthPhase8WorldOutputTrainingService.IsPlayerCropHarvest(crop.Block.GetBlockName()))return true;
            return RebirthHumanProgression.Award(npc,new RebirthSkillTrainingEvidence{
                SkillId="skill.farming",SourceKey="phase8:farming:npc:"+crop.Block.GetBlockName(),
                DurableReceiptId="npc:farming-original:"+operation.ToString("N"),
                ReferenceDescription="saved crop block publication and native NPC bag credit",
                AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,
                CreditedWork=1f,DiscreteRawAward=raw});
        }
        catch{return false;}
    }
}
