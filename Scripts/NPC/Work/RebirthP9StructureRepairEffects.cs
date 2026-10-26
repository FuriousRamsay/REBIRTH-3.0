using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// B08 native-equivalent post-repair effects and cold continuation.
//
// Native ItemActionRepair (player branch) after DamageBlock: arm animation and
// repair sound, bUseStarted/blockDamagePerc (HUD overlay only), MinEvent
// onSelfRepairBlock with the pre-repair block, then Progression.AddLevelExp of
// material.Experience * count. This file reproduces the observable NPC subset
// once, after the block publication proof, with a write-ahead marker:
//   nativeEffects=attempted  -> saved before any effect call
//   nativeEffects=done       -> saved after all calls returned
//   attempted on re-entry    -> a prior process entered the calls; they are never
//                               replayed (lost), because a MinEvent cannot be
//                               reconciled against any postimage.
// Experience goes through the idempotent progression event bus with an EventId
// derived from the operation, and only when the owner approves the award rule.
internal static partial class RebirthNpcStructureRepairOriginal
{
    // Release decision: RepairCompleted currently awards a fixed amount in
    // RebirthNpcProgressionService, not the native material experience.
    internal static bool ProgressionAwardApproved=false;

    private static readonly string[] VolatileActionState={"lastUse","useStarted","damagePercent","upgradePercent",
        "hitX","hitY","hitZ","hitRaw","hitDamage","hasRepairItems","repairType"};

    // A cold NativeInventory/InventoryDataRepair is rebuilt by the engine, so the
    // process-local action fields cannot equal the pre-restart image. Everything
    // that identifies the tool, action, amount, cost and wear still has to match.
    private static bool ColdPolicyEquals(XElement saved,XElement live)
    {
        var a=new XElement(saved);var b=new XElement(live);
        foreach(var name in VolatileActionState){a.Attribute(name)?.Remove();b.Attribute(name)?.Remove();}
        return XNode.DeepEquals(a,b);
    }

    private static bool ExpectedBlockState(RebirthNpcConcreteWorkContext context,XElement row,EntityRebirthHumanoidNPC npc)
    {
        var block=npc.world.GetBlock(NativePosition(row));
        if(block.rawData!=(uint)row.Attribute("blockRaw"))return false;
        string phase=(string)row.Attribute("phase");
        return phase=="native-tool-published"?block.damage==(int)row.Attribute("damage"):
            block.damage==(int)row.Attribute("damage")||block.damage==0;
    }

    // Accept a new NPC incarnation (restart, despawn/respawn) only after the
    // saved effects are proved against the live reconstructed body: the typed
    // material and tool receipts plus the full physical bag image. Phases whose
    // effect prefix is unknown (debit/tool attempted) stay held.
    private static bool TryColdRebind(RebirthNpcConcreteWorkContext context,XElement row,out XElement next)
    {
        next=row;
        try
        {
            var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
            if(intent==null||row.Attribute("nativeBinding")==null||(uint?)row.Attribute("nativeEmbodiment")==intent.Generation)return true;
            string phase=(string)row.Attribute("phase");
            if(phase!="native-tool-published"&&phase!="native-block-attempted"&&phase!="native-block-published")return false;
            var id=context.Assignment.NpcId;
            var probe=new XElement(row);probe.SetAttributeValue("nativeCold","1");
            if(!NativeContext(context,probe,out var npc,out _,true)||!PublishedTool(row)||!BagMatches(npc,row,true)||
                phase!="native-block-published"&&!SavedDebit(id,row)||!ExpectedBlockState(context,row,npc)||
                !NativeBirthCurrent(npc.world,row,context.OperationId))return false;
            var rebound=new XElement(row);
            rebound.SetAttributeValue("nativeEmbodiment",intent.Generation);rebound.SetAttributeValue("nativeCold","1");
            rebound.SetAttributeValue("nativeColdCount",((int?)row.Attribute("nativeColdCount")??0)+1);
            if(!NativeReplace(id,row,rebound))return false;
            next=rebound;return true;
        }
        catch{return false;}
    }

    private static int NativeExperience(XElement row)
    {
        var item=ItemClass.GetItem((string)row.Attribute("material"));
        float xp=item.ItemClass.MadeOfMaterial.Experience*(int)row.Attribute("quantity");
        return float.IsNaN(xp)||float.IsInfinity(xp)||xp<0f||xp>=int.MaxValue?0:(int)xp;
    }

    private static Guid ExperienceEventId(Guid operation)
    {
        using(var md5=MD5.Create())return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("rebirth.npc.structure.repair.xp.v1|"+operation.ToString("N"))));
    }

    private static bool TryEmitRepairExperience(RebirthNpcConcreteWorkContext context,XElement row,out XElement next)
    {
        next=row;
        try
        {
            if(!ProgressionAwardApproved||(string)row.Attribute("nativeXp")=="emitted"||row.Attribute("nativeEffectsXp")==null)return true;
            int xp=(int)row.Attribute("nativeEffectsXp");
            bool sent=xp<=0;
            if(xp>0)sent=RebirthNpcWorkOutcomeService.Emit(new RebirthNpcProgressionEvent{EventId=ExperienceEventId(context.OperationId),
                OutcomeId=context.OperationId,Kind=RebirthNpcProgressionEventKind.RepairCompleted,NpcId=context.Assignment.NpcId,
                AssignmentId=context.Assignment.AssignmentId,DefinitionId=RebirthNpcSingleRepairWork.DefinitionId,
                ResourceKey=(string)row.Attribute("material"),Quantity=xp,SettlementId=string.Empty,CreatedUtcTicks=DateTime.UtcNow.Ticks});
            // Emit is keyed by EventId in the bus and its sinks; one call per
            // saved marker is enough and a duplicate is harmless.
            // false means duplicate or a pending sink retry; give up marking only after 3 tries.
            int tries=((int?)row.Attribute("nativeXpTries")??0)+1;
            var marked=new XElement(row);
            if(sent||tries>=3)marked.SetAttributeValue("nativeXp","emitted");else marked.SetAttributeValue("nativeXpTries",tries);
            if(!NativeReplace(context.Assignment.NpcId,row,marked))return false;
            next=marked;return true;
        }
        catch{return false;}
    }

    private static bool TryApplyNativeEffects(RebirthNpcConcreteWorkContext context,XElement row,out XElement next)
    {
        next=row;
        try
        {
            if((string)row.Attribute("phase")!="native-block-published")return true;
            var id=context.Assignment.NpcId;string state=(string)row.Attribute("nativeEffects");
            if(state=="done"||state=="lost")return TryEmitRepairExperience(context,row,out next);
            if(state=="attempted")
            {
                // Never gated on live body state: a busy hand left by the entered calls must not strand it.
                var lost=new XElement(row);lost.SetAttributeValue("nativeEffects","lost");
                if(!NativeReplace(id,row,lost))return false;
                return TryEmitRepairExperience(context,lost,out next);
            }
            if(!NativeContext(context,row,out var npc,out _)||!PublishedTool(row))return false;
            var attempted=new XElement(row);attempted.SetAttributeValue("nativeEffects","attempted");
            attempted.SetAttributeValue("nativeEffectsXp",NativeExperience(row).ToString(CultureInfo.InvariantCulture));
            if(!NativeReplace(id,row,attempted))return false;
            int actionIndex=(int)row.Element("nativeRepairPolicy").Attribute("action");
            var action=npc.inventory.holdingItemItemValue.ItemClass.Actions[actionIndex] as ItemActionRepair;
            var data=npc.inventory.holdingItemData.actionData[actionIndex] as ItemActionRepair.InventoryDataRepair;
            if(action!=null&&data!=null)
            {
                npc.RightArmAnimationUse=true;
                string sound=!string.IsNullOrEmpty(action.repairActionSound)?action.repairActionSound:action.soundStart;
                if(!string.IsNullOrEmpty(sound))npc.PlayOneShot(sound);
                var priorAction=npc.MinEventContext.ItemActionData;var priorBlock=npc.MinEventContext.BlockValue;var priorPosition=npc.MinEventContext.Position;
                try
                {
                    npc.MinEventContext.ItemActionData=data;
                    npc.MinEventContext.BlockValue=new BlockValue{rawData=(uint)row.Attribute("blockRaw"),damage=(ushort)(int)row.Attribute("damage")};
                    npc.MinEventContext.Position=NativePosition(row).ToVector3();
                    npc.FireEvent(MinEventTypes.onSelfRepairBlock);
                }
                finally{npc.MinEventContext.ItemActionData=priorAction;npc.MinEventContext.BlockValue=priorBlock;npc.MinEventContext.Position=priorPosition;}
            }
            var done=new XElement(attempted);done.SetAttributeValue("nativeEffects","done");
            if(!NativeReplace(id,attempted,done))return false;
            return TryEmitRepairExperience(context,done,out next);
        }
        catch{return false;}
    }
}
