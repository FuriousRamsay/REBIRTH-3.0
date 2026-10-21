using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Detached original native Block owner intent. No callback, source setter,
// native target command or payment permission is created by this planner.
internal static class RebirthRemoteResourceBlockPlan
{
    private static bool Kind(RemoteResourceClientOperation kind)=>kind==RemoteResourceClientOperation.BlockRepair||kind==RemoteResourceClientOperation.BlockUpgrade;
    private static bool Native(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data)
    {
        int index=data?.indexInEntityOfAction??-1;
        return player!=null&&action!=null&&data?.invData!=null&&ReferenceEquals(player.inventory?.holdingItemData,data.invData)&&ReferenceEquals(data.invData.holdingEntity,player)&&
            index>=0&&data.invData.actionData!=null&&index<data.invData.actionData.Count&&ReferenceEquals(data.invData.actionData[index],data)&&
            data.invData.item?.Actions!=null&&index<data.invData.item.Actions.Length&&ReferenceEquals(data.invData.item.Actions[index],action)&&
            data.invData.slotIdx>=0&&data.invData.slotIdx<player.inventory.Length&&player.inventory.holdingItemIdx==data.invData.slotIdx;
    }
    private static string Progress(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target)
        =>RebirthRemoteResourceBlockProgress.Capture(action,data,target);
    internal static bool Capture(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target,RemoteResourceClientOperation kind,
        ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            if(!Kind(kind)||!Native(player,action,data)||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||
                float.IsNaN(action.repairAmount)||float.IsInfinity(action.repairAmount)||full==null||remote==null||remote.Count==0)return false;
            var block=player.world.GetBlock(target);if(block.isair||block.ischild||block.Block==null||target.y<0||target.y>=255)return false;
            if(kind==RemoteResourceClientOperation.BlockRepair&&(!block.Block.CanRepair(block)||
                !(block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block))&&Utils.FastMin((int)action.repairAmount,block.damage)<=0))return false;
            var before=RebirthCamperEnergyActorImage.Capture(player);int slot=data.invData.slotIdx;var tool=before.Belt[slot];
            if(tool==null||tool.IsEmpty()||RebirthNativeItemCodec.Encode(tool.itemValue)!=RebirthNativeItemCodec.Encode(data.invData.itemValue)||
                !RebirthRemoteResourceCraftPlan.IsExactShortfall(before,full,remote,player.inventory.Length)||
                !RebirthRemoteResourceCraftPlan.TryDebit(before,full,remote,player.inventory.Length,out var preview,out _)||
                !RebirthStationGridIngredients.IsSameStackSnapshot(tool,preview.Belt[slot]))return false;
            var afterTool=tool.itemValue.Clone();if(kind==RemoteResourceClientOperation.BlockRepair&&afterTool.MaxUseTimes>0)afterTool.UseTimes++;
            preview.Belt[slot]=new ItemStack(afterTool.Clone(),tool.count);RebirthRemoteResourceClientOriginal.ActorImage(preview);
            string progress=Progress(action,data,target);
            var body=new XElement("action",new XAttribute("x",target.x),new XAttribute("y",target.y),new XAttribute("z",target.z),new XAttribute("blockRaw",Convert.ToUInt64(block.rawData)),
                new XAttribute("blockDamage",block.damage),new XAttribute("toolSlot",slot),new XAttribute("toolCount",tool.count),new XAttribute("beltCount",player.inventory.Length),new XAttribute("actionIndex",data.indexInEntityOfAction),
                new XAttribute("repairAmount",action.repairAmount.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("upgradeHits",action.blockUpgradeCount),new XAttribute("upgradeChanged",action.bUpgradeCountChanged?1:0),
                new XElement("toolInput",RebirthNativeItemCodec.Encode(tool.itemValue)),new XElement("toolAfter",RebirthNativeItemCodec.Encode(afterTool)),new XElement("progress",progress));
            return RebirthRemoteResourceClientOriginal.TryPrepare(player,epoch,request,kind,full,remote,body,out original)&&Progress(action,data,target)==progress;
        }
        catch{original=null;return false;}
    }
    internal static bool Expected(RebirthRemoteResourceClientOriginal.Intent original,ItemStack[] paid,out RebirthCamperEnergyActorImage before,
        out RebirthCamperEnergyActorImage after,out ItemStack[] localRemoved)
    {
        before=after=null;localRemoved=null;
        try
        {
            if(!Validate(original))return false;var root=original.Copy();var action=root.Element("action");int slot=(int)action.Attribute("toolSlot");
            before=RebirthRemoteResourceClientOriginal.ReadActor(root.Element("actorBefore").Value);
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("requirements").Value));
            if(!RebirthRemoteResourceCraftPlan.TryDebit(before,needs,paid,(int)action.Attribute("beltCount"),out after,out var removed)||slot>=before.Belt.Length||
                before.Belt[slot].count!=(int)action.Attribute("toolCount")||RebirthNativeItemCodec.Encode(before.Belt[slot].itemValue)!=action.Element("toolInput").Value||
                !RebirthStationGridIngredients.IsSameStackSnapshot(before.Belt[slot],after.Belt[slot])||!RebirthNativeItemCodec.TryDecode(action.Element("toolAfter").Value,out var tool))return false;
            // Native attempted tool wear is preserved; no refund planner may
            // silently roll it back as an ordinary material debit.
            after.Belt[slot]=new ItemStack(tool,before.Belt[slot].count);
            localRemoved=removed.Take(removed.Count-paid.Length).Select(stack=>stack.Clone()).ToArray();
            RebirthRemoteResourceClientOriginal.ActorImage(after);return true;
        }
        catch{before=after=null;localRemoved=null;return false;}
    }
    internal static bool Validate(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(original==null||!Kind(original.Kind))return false;var action=original.Copy().Element("action");
            if(action?.Attributes().Count()!=12||action.Elements().Count()!=3||action.Nodes().Any(node=>node is not XElement)||
                action.Elements().Any(node=>!new[]{"toolInput","toolAfter","progress"}.Contains(node.Name.LocalName)||node.Name.NamespaceName.Length!=0||node.HasAttributes||node.Nodes().Any(child=>child is not XText))||
                action.Elements().GroupBy(node=>node.Name).Any(group=>group.Count()!=1))return false;
            foreach(string name in new[]{"x","y","z","blockDamage","toolSlot","toolCount","beltCount","actionIndex","upgradeHits","upgradeChanged"})
                if((string)action.Attribute(name)!=((int)action.Attribute(name)).ToString(CultureInfo.InvariantCulture))return false;
            if((string)action.Attribute("blockRaw")!=((ulong)action.Attribute("blockRaw")).ToString(CultureInfo.InvariantCulture)||
                (int)action.Attribute("y")<0||(int)action.Attribute("y")>=255||(int)action.Attribute("blockDamage")<0||(int)action.Attribute("blockDamage")>ushort.MaxValue||(int)action.Attribute("toolSlot")<0||(int)action.Attribute("toolSlot")>=(int)action.Attribute("beltCount")||(int)action.Attribute("beltCount")>20||
                (int)action.Attribute("toolCount")<1||(int)action.Attribute("actionIndex")<0||(int)action.Attribute("upgradeChanged")<0||(int)action.Attribute("upgradeChanged")>1)return false;
            float amount=float.Parse((string)action.Attribute("repairAmount"),CultureInfo.InvariantCulture);
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount.ToString("R",CultureInfo.InvariantCulture)!=(string)action.Attribute("repairAmount")||
                !RebirthNativeItemCodec.TryDecode(action.Element("toolInput").Value,out var tool)||!RebirthNativeItemCodec.TryDecode(action.Element("toolAfter").Value,out var after)||
                tool.ItemClass?.Actions==null||(int)action.Attribute("actionIndex")>=tool.ItemClass.Actions.Length||tool.ItemClass.Actions[(int)action.Attribute("actionIndex")] is not ItemActionRepair)return false;
            var nativeAction=(ItemActionRepair)tool.ItemClass.Actions[(int)action.Attribute("actionIndex")];
            if(!RebirthRemoteResourceBlockProgress.TryRead(action.Element("progress").Value,out var progress)||progress.RepairAmount!=amount||
                progress.RepairAmount!=nativeAction.repairAmount||progress.HitOffset!=nativeAction.hitCountOffset||progress.UpgradeHits!=(int)action.Attribute("upgradeHits")||
                progress.UpgradeChanged!=((int)action.Attribute("upgradeChanged")==1)||!BuildRequirements(original,nativeAction,progress,out var requirements))return false;
            var frozen=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(frozen.Length!=requirements.Count||frozen.Where((stack,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(stack,requirements[index])).Any())return false;
            var expected=tool.Clone();if(original.Kind==RemoteResourceClientOperation.BlockRepair&&expected.MaxUseTimes>0)expected.UseTimes++;
            if(RebirthNativeItemCodec.Encode(expected)!=RebirthNativeItemCodec.Encode(after))return false;
            return true;
        }
        catch{return false;}
    }
    private static bool BuildRequirements(RebirthRemoteResourceClientOriginal.Intent original,ItemActionRepair nativeAction,RebirthRemoteResourceBlockProgress progress,out List<ItemStack> requirements)
    {
        requirements=new List<ItemStack>();var action=original.Copy().Element("action");
        ulong raw=(ulong)action.Attribute("blockRaw");if(raw>uint.MaxValue)return false;
        var block=new BlockValue((uint)raw);block.damage=(ushort)(int)action.Attribute("blockDamage");
        var position=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));var definition=block.Block;
        if(definition==null||block.isair||block.ischild)return false;
        if(original.Kind==RemoteResourceClientOperation.BlockUpgrade)
        {
            if(definition.CanRepair(block)||!nativeAction.isUpgradeItem||!progress.UseStarted||!progress.UpgradeChanged||progress.Target!=position||
                !int.TryParse(definition.Properties.GetString("UpgradeBlock","UpgradeHitCount"),out int hits)||
                progress.UpgradeHits<(hits+progress.HitOffset<1f?1:(int)(hits+progress.HitOffset))||
                !RemoteResourceActionConsumerPatchInstaller.TryBuildUpgradeRequirement(nativeAction,block,out var need))return false;
            requirements.Add(need);return true;
        }
        if(!definition.CanRepair(block)||definition.MaxDamage<=0)return false;
        int amount=Utils.FastMin((int)progress.RepairAmount,block.damage);float fraction=(float)amount/definition.MaxDamage;var items=definition.RepairItems;
        if(definition.RepairItemsMeshDamage!=null&&definition.shape.UseRepairDamageState(block)){amount=1;fraction=1f;items=definition.RepairItemsMeshDamage;}
        if(amount<=0||items==null||float.IsNaN(definition.ResourceScale)||float.IsInfinity(definition.ResourceScale))return false;
        if(progress.SameRepairList&&(progress.LastHit!=position||new BlockValue((uint)progress.LastRaw).type!=block.type||progress.Fractions?.Length!=items.Count||progress.Names?.Length!=items.Count||
            items.Where((item,index)=>item.ItemName!=progress.Names[index]||item.Count!=progress.Counts[index]).Any()))return false;
        for(int index=0;index<items.Count;index++)
        {
            if(progress.SameRepairList&&progress.Fractions[index]>0f)continue;
            var value=ItemClass.GetItem(items[index].ItemName);if(value==null||value.IsEmpty())return false;
            int count=Utils.FastMax((int)(items[index].Count*fraction*definition.ResourceScale),1);requirements.Add(new ItemStack(value,count));
        }
        return requirements.Count>0;
    }
    internal static bool MatchesLive(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!Validate(original)||!Native(player,action,data)||!RebirthRemoteResourceClientOriginal.Current(player,original))return false;
            var body=original.Copy().Element("action");var position=new Vector3i((int)body.Attribute("x"),(int)body.Attribute("y"),(int)body.Attribute("z"));var block=player.world.GetBlock(position);
            return data.invData.slotIdx==(int)body.Attribute("toolSlot")&&data.indexInEntityOfAction==(int)body.Attribute("actionIndex")&&
                RebirthNativeItemCodec.Encode(data.invData.itemValue)==body.Element("toolInput").Value&&Convert.ToUInt64(block.rawData)==(ulong)body.Attribute("blockRaw")&&block.damage==(int)body.Attribute("blockDamage")&&
                Progress(action,data,position)==body.Element("progress").Value;
        }
        catch{return false;}
    }
    internal static bool RestorePrepared(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,
        RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        try
        {
            if(!Validate(original)||action?.GetType()!=typeof(ItemActionRepair)||!Native(player,action,data)||
                !RebirthRemoteResourceOwnerAction.CanRestorePreparedBlock(player,original,actualSavedHostPermission)||
                LocalPlayerUI.GetUIForPlayer(player)?.windowManager?.IsModalWindowOpen()!=false)return false;
            var body=original.Copy().Element("action");var target=new Vector3i((int)body.Attribute("x"),(int)body.Attribute("y"),(int)body.Attribute("z"));
            var hit=player.HitInfo;
            if(!hit.bHitValid||!GameUtils.IsBlockOrTerrain(hit.tag)||hit.hit.distanceSq>Constants.cDigAndBuildDistance*Constants.cDigAndBuildDistance||
                World.SandboxUseTraderArea==TraderAreaStates.Default&&player.world.IsWithinTraderArea(hit.hit.blockPos))return false;
            var position=hit.hit.blockPos;var block=player.world.GetBlock(position);
            if(block.ischild){position=block.Block.multiBlockPos.GetParentPos(position,block);block=player.world.GetBlock(position);}
            if(position!=target||Convert.ToUInt64(block.rawData)!=(ulong)body.Attribute("blockRaw")||block.damage!=(int)body.Attribute("blockDamage")||
                data.invData.slotIdx!=(int)body.Attribute("toolSlot")||data.indexInEntityOfAction!=(int)body.Attribute("actionIndex")||
                RebirthNativeItemCodec.Encode(data.invData.itemValue)!=body.Element("toolInput").Value||
                !RebirthRemoteResourceBlockProgress.TryRead(body.Element("progress").Value,out var progress))return false;
            var tool=data.invData.itemValue;
            if(tool.MaxUseTimes>0&&tool.UseTimes>=tool.MaxUseTimes||tool.UseTimes==0f&&tool.MaxUseTimes==0)return false;
            if(original.Kind==RemoteResourceClientOperation.BlockRepair&&
                (UnityEngine.Time.time-data.lastUseTime<action.Delay||EffectManager.GetValue(PassiveEffects.DisableItem,player.inventory.holdingItemItemValue,0f,player,null,data.invData.item.ItemTags)>0f))return false;
            return progress.RestoreNative(action,data,target)&&MatchesLive(player,action,data,original)&&
                RebirthRemoteResourceOwnerAction.CanRestorePreparedBlock(player,original,actualSavedHostPermission);
        }
        catch{return false;}
    }
}
