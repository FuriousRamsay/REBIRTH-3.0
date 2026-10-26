using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Held B08 source/custody original. A Desk Structure is a saved volume, not a
// single block at its center. This only selects and checkpoints one physical
// damaged block and one exact native NPC bag debit; it never repairs a block.
internal static partial class RebirthNpcStructureRepairOriginal
{
    private const string Element="structureRepairOriginal";

    internal static bool TryPrepare(RebirthNpcConcreteWorkContext context,out string detail)
    {
        detail="No authorized damaged structure and physical repair material are available.";
        try
        {
            var world=GameManager.Instance?.World;
            var assignment=context?.Assignment;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||
               string.IsNullOrEmpty(world.worldState?.Guid)||assignment==null||context.OperationId==Guid.Empty||
               (context.Definition?.DefinitionId!=RebirthNpcConstructionWorkPackageService.RepairDefinitionId&&
                !RebirthNpcSingleRepairWork.IsDefinition(context.Definition?.DefinitionId))||
               !RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveStructureVolume(assignment,out var volume,out var revision)||
               context.Target?.TargetRevision!=revision.ToString(System.Globalization.CultureInfo.InvariantCulture)||
               !RebirthNpcWorkNavigationService.TryResolveNpc(assignment.NpcId,out var candidate)||
               candidate is not EntityRebirthHumanoidNPC npc||npc.IsDead()||npc.IsPreparedRestorationPending||
               !ReferenceEquals(npc.world,world)||!ReferenceEquals(world.GetEntity(npc.entityId),npc)||
               npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null||
               npc.bag?.ItemGrid==null||volume.SqrDistance(npc.position)>36f)return false;
            var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
            var persistent=owner==null?null:GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
            if(owner==null||owner.IsDead()||!ReferenceEquals(owner.world,world)||
               !ReferenceEquals(world.GetEntity(owner.entityId),owner)||persistent==null||
               npc.RebirthRuntimeState?.OwnershipKind!=RebirthNpcOwnershipKind.Player||
               !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||
               !string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase))return false;
            if(RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(assignment.NpcId,out var saved)&&
               Read(saved) is XElement retained)
            {
                if(RebirthNpcSingleRepairWork.IsDefinition(context.Definition.DefinitionId)&&
                    (string)retained.Attribute("operation")!=context.OperationId.ToString("N")&&
                    (string)retained.Attribute("phase")=="native-completed")
                {
                    if(!TryArchiveCompleted(assignment.NpcId,context.OperationId))return false;
                }
                else
                {
                if((string)retained.Attribute("operation")!=context.OperationId.ToString("N")||
                   (string)retained.Attribute("assignment")!=assignment.AssignmentId.ToString()||
                   (string)retained.Attribute("target")!=assignment.TargetKey||
                   (long?)retained.Attribute("revision")!=revision||
                   (string)retained.Attribute("world")!=world.worldState.Guid)return false;
                detail="Saved structure repair original remains held for native settlement.";return true;
                }
            }
            var equipment=RebirthHumanEquipment.Read(npc);
            if(RebirthNpcInteractionSessionService.IsInventoryAccessing(assignment.NpcId)||
               RebirthNpcWorkNativeTransferOriginal.HasPending(assignment.NpcId)||
               RebirthSmartLootingHumanOriginal.HasPending(assignment.NpcId)||
               equipment.Element("bookMove")!=null||equipment.Element("swap")!=null)return false;
            var center=new Vector3i(Mathf.FloorToInt(npc.position.x),Mathf.FloorToInt(npc.position.y),
                Mathf.FloorToInt(npc.position.z));
            for(int y=center.y-3;y<=center.y+3;y++)
            for(int x=center.x-6;x<=center.x+6;x++)
            for(int z=center.z-6;z<=center.z+6;z++)
            {
                var position=new Vector3i(x,y,z);
                if(RebirthNpcSingleRepairWork.IsDefinition(context.Definition.DefinitionId)&&
                    !position.Equals(RebirthNpcSingleRepairWork.Position(assignment)))continue;
                // Native ItemActionRepair checks a repairable current block and
                // trader protection, not placement collision at an occupied cell.
                // Keep the stricter same-owner claim rule for delegated NPC work.
                if(!volume.Contains(new Vector3(x+.5f,y+.5f,z+.5f))||
                   World.SandboxUseTraderArea==TraderAreaStates.Default&&world.IsWithinTraderArea(position))continue;
                var claim=world.GetLandClaimOwner(position,persistent);
                if(claim!=EnumLandClaimOwner.None&&claim!=EnumLandClaimOwner.Self)continue;
                var block=world.GetBlock(position);
                if(block.isair||block.ischild||block.damage<=0||block.damage>=block.Block.MaxDamage||
                   !block.Block.CanRepair(block)||block.Block.MaxDamage<=0||
                   block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block)||
                   block.Block.RepairItems==null||block.Block.RepairItems.Count!=1||
                   block.Block.ResourceScale<=0f||float.IsNaN(block.Block.ResourceScale)||
                   float.IsInfinity(block.Block.ResourceScale))continue;
                if(!RebirthNpcDeskVolumeWorkTargetAdapter.ContainsStructurePoint(assignment,
                    new Vector3(x+.5f,y+.5f,z+.5f),out var pointRevision)||pointRevision!=revision)continue;
                var cost=block.Block.RepairItems[0];
                if(cost.Count<=0)continue;
                var material=ItemClass.GetItem(cost.ItemName);
                if(material==null||material.IsEmpty()||string.IsNullOrEmpty(material.ItemClass?.Name)||
                    RebirthCompanionInventoryLockService.IsNpcItemLocked(assignment.NpcId,material.ItemClass.GetItemName()))continue;
                // Match native float/truncation semantics only inside the admitted
                // finite range. Casting NaN/overflow and then clamping to one would
                // checkpoint a false material obligation for a malformed recipe.
                float materialCost=cost.Count*((float)block.damage/block.Block.MaxDamage)*
                    block.Block.ResourceScale;
                if(float.IsNaN(materialCost)||float.IsInfinity(materialCost)||materialCost<=0f||
                   materialCost>=65536f)continue;
                int needed=Utils.FastMax((int)materialCost,1);
                XElement nativePolicy=null;
                if(RebirthNpcSingleRepairWork.IsDefinition(context.Definition.DefinitionId)&&
                    (!RebirthNpcSingleRepairPolicy.TryObserve(npc,position,block,true,out nativePolicy)||
                     (int)nativePolicy.Attribute("cost")!=needed))continue;
                // B13 provisional Management discount on the debited quantity only (after the native-policy cost check).
                needed=RebirthCompanionLeadership.ManagementAdjustedRepairMaterial(needed,assignment.NpcId);
                for(int slot=0;slot<npc.bag.SlotCount;slot++)
                {
                    var stack=npc.bag.ItemGrid[slot];
                    if(stack==null||stack.IsEmpty()||stack.count<needed||stack.itemValue.type!=material.type||
                       stack.itemValue.HasModSlots&&stack.itemValue.HasMods())continue;
                    var after=stack.Clone();after.count-=needed;
                    var image=new XElement(Element,new XAttribute("version",1),
                        new XAttribute("npc",assignment.NpcId.ToString()),
                        new XAttribute("assignment",assignment.AssignmentId),
                        new XAttribute("operation",context.OperationId.ToString("N")),
                        new XAttribute("world",world.worldState.Guid),
                        new XAttribute("target",assignment.TargetKey),new XAttribute("revision",revision),
                        new XAttribute("x",x),new XAttribute("y",y),new XAttribute("z",z),
                        new XAttribute("blockRaw",block.rawData),new XAttribute("damage",block.damage),
                        new XAttribute("material",material.ItemClass.Name),new XAttribute("quantity",needed),
                        new XAttribute("bagSlot",slot),new XAttribute("before",RebirthHumanEquipment.Frame(stack)),
                        new XAttribute("after",after.count>0?RebirthHumanEquipment.Frame(after):string.Empty),
                        new XAttribute("phase","prepared"));
                    if(RebirthNpcSingleRepairWork.IsDefinition(context.Definition.DefinitionId))
                    {
                        image.SetAttributeValue("definition",context.Definition.DefinitionId);
                        image.SetAttributeValue("startedTicks",assignment.CreatedUtcTicks);
                        image.Add(new XElement(nativePolicy));
                    }
                    string raw=image.ToString(SaveOptions.DisableFormatting);
                    if(!RebirthNpcAggregatePersistenceStore.Mutate(assignment.NpcId,person=>
                    {
                        if(Read(person)!=null)throw new InvalidDataException("Another structure repair original is pending.");
                        person.UnknownOptionalElements=(person.UnknownOptionalElements??Array.Empty<string>()).Concat(new[]{raw}).ToArray();
                    }))return false;
                    RebirthNpcAggregatePersistenceStore.Save();
                    if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(assignment.NpcId,out var proof)||
                       !XNode.DeepEquals(Read(proof),image))return false;
                    detail="Damaged block and exact physical repair material saved; native debit and block settlement remain held.";
                    return true;
                }
            }
            return false;
        }
        catch{return false;}
    }

    private static XElement Read(RebirthNpcPersistentRecord person)
    {
        XElement found=null;
        foreach(var raw in person.UnknownOptionalElements??Array.Empty<string>())
        {
            var row=RebirthCompanionDeskStore.Parse(raw);
            if(row.Name!=Element)continue;
            if(found!=null)throw new InvalidDataException("Duplicate structure repair originals.");
            found=row;
        }
        return found;
    }
}
