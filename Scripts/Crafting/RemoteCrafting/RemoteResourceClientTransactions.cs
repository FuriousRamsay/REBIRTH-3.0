using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RemoteResourceClientOperation : byte
{
    Craft = 1,
    ItemRepair = 2,
    BlockRepair = 3,
    BlockUpgrade = 4,
    CookingPull = 5
}

public enum RemoteResourceTransactionRequestMode : byte
{
    ExecuteOrReplay = 1,
    QueryOnly = 2
}

public enum RemoteResourceTransactionOutcomeState : byte
{
    Unknown = 0,
    Success = 1,
    Failed = 2
}

/// <summary>
/// Dedicated-client preflight for UI actions whose vanilla code queues the action before
/// inventory removal. The client sends only the remote shortfall; the authoritative
/// server re-discovers/revalidates current sources and consumes that shortfall before the
/// client is allowed to run the vanilla queue insertion path.
/// </summary>
public static class RemoteResourceClientTransactionCoordinator
{
    // A client process restart must not reuse the previous process's server journal keys.
    private static ulong clientSessionEpoch = CreateSessionEpoch();

    private static long nextRequestId;
    [ThreadStatic] private static ItemActionEntryCraft resumeCraft;
    [ThreadStatic] private static ItemActionEntryRepair resumeRepair;
    [ThreadStatic] private static ItemActionData resumeBlockRepair;
    [ThreadStatic] private static ItemActionData resumeBlockUpgrade;
    internal static bool InvokeOriginalBlock(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(resumeBlockRepair!=null||resumeBlockUpgrade!=null||!RebirthRemoteResourceBlockNativeCapture.ConsumeInvocation(action,data,original))return false;
        try
        {
            if(original.Kind==RemoteResourceClientOperation.BlockRepair){resumeBlockRepair=data;action.ExecuteAction(data,false);}
            else if(original.Kind==RemoteResourceClientOperation.BlockUpgrade){resumeBlockUpgrade=data;action.OnHoldingUpdate(data);}
            else return false;
            return true; // Invocation only; payment requires captured command and native saved fullAfter.
        }
        finally{resumeBlockRepair=null;resumeBlockUpgrade=null;}
    }

    public static bool HasPendingInventoryOperation(EntityPlayerLocal player)
        => player?.world!=null&&GameManager.Instance!=null&&
            ReferenceEquals(GameManager.Instance.World,player.world)&&
            ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            (RebirthCamperStationContentsDelivery.HoldsOwner(player)||RebirthConsumptionDurableDelivery.HoldsInventoryOwner(player)||RemoteResourceClientGrantContext.Active||RebirthRemoteResourceOwnerAction.IsHeld(player)||RebirthHydrationOwnerHold.IsHeld(player));

    public static bool IsDedicatedClient
    {
        get
        {
            ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return connection != null && !connection.IsServer;
        }
    }

    // Fix38: a requirements refresh must not clamp the requested count after the server
    // reserves remote ingredients but before its matching grant arrives. This is read-only;
    // the existing receipt/selection checks still decide whether the request may commit.
    internal static bool HasPendingCraftForUi(XUi ui)
    {
        if (ui == null || !IsDedicatedClient) return false;
        if (ReferenceEquals(resumeCraft?.ItemController?.xui, ui)) return true;
        if (RebirthRemoteResourceCraftProtocol.HasPendingForUi(ui)) return true;
        return false;
    }

    public static void Clear()
    {
        unchecked { clientSessionEpoch++; }
        if (clientSessionEpoch == 0) clientSessionEpoch = 1;
        resumeCraft = null;
        resumeRepair = null;
        resumeBlockRepair = null;
        resumeBlockUpgrade = null;
        RemoteResourceClientGrantContext.End();
    }

    // The first-priority mobile native action owns OnActivated and therefore
    // skips the ordinary terrain preflight prefix. Its hasItems prefix also
    // does not populate vanilla tempIngredientList. Resolve the same exact
    // modified requirements here before selecting the fetched original.
    internal static bool TryOwnMobileResourceActivation(EntityPlayerLocal player,ItemActionEntryCraft entry)
    {
        if(!RemoteResourcesRuntimePolicy.Enabled||!IsDedicatedClient||!RebirthRemoteResourceFetchedStationOriginal.ProtocolActive||
            !RebirthRemoteResourceFetchedStationOriginal.IsEntryCandidate(player,entry))return false;
        if(RebirthRemoteResourceFetchedStationOriginal.TryAttachOwningEntry(player,entry))return true;
        var recipe=(entry.ItemController as XUiC_RecipeEntry)?.Recipe;int count=entry.craftCountControl?.Count??0;
        if(recipe?.ingredients==null||count<1||count>9999)return true;
        var requirements=new List<ItemStack>();
        foreach(var ingredient in recipe.ingredients)
        {
            if(!RebirthCraftingIngredientQuantity.TryResolveTotal(player,recipe,ingredient,entry.craftingTier,count,out int total)||total<1)return true;
            requirements.Add(new ItemStack(ingredient.itemValue.Clone(),total));
        }
        if(requirements.Count==0||HasAllLocal(player,requirements))return false;
        if(!TryBuildRemoteShortfall(player,requirements,out var shortfall))
        {RemoteResourceClientAvailability.InvalidateAndRequest(player);return true;}
        if(shortfall.Count==0)return false;
        RebirthRemoteResourceFetchedStationOriginal.BeginClient(player,entry,clientSessionEpoch,NextRequestId(),requirements,shortfall);
        return true;
    }
    public static bool BeforeCraftActivated(ItemActionEntryCraft entry)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || entry == null) return true;
        if (ReferenceEquals(resumeCraft, entry)) return true;
        if (!IsDedicatedClient)
        {
            EntityPlayerLocal local = entry.ItemController != null && entry.ItemController.xui != null &&
                entry.ItemController.xui.playerUI != null ? entry.ItemController.xui.playerUI.entityPlayer : null;
            if (local != null) RemoteResourceSnapshotCache.ForceRebuild(local);
            // Only personal crafting shares this native-owner original. Other
            // stations retain their distinct payment/queue authority.
            if(local==null||!RebirthRemoteResourceCraftOriginal.IsPersonalEntry(entry))return true;
        }
        if (RebirthRemoteResourceCraftProtocol.TryAttach(entry)) return false;

        XUiC_RecipeEntry recipeEntry = entry.ItemController as XUiC_RecipeEntry;
        Recipe recipe = recipeEntry != null ? recipeEntry.Recipe : null;
        XUi xui = entry.ItemController != null ? entry.ItemController.xui : null;
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (recipe == null || xui == null || player == null || entry.craftCountControl == null) return true;

        // Let the vanilla helper build the exact modified ingredient list. If the current
        // replicated availability does not satisfy it, vanilla handles the disabled action.
        if (!entry.hasItems(xui, recipe)) return true;
        int craftCount = Math.Max(1, entry.craftCountControl.Count);
        List<ItemStack> requirements = CloneRequirements(entry.tempIngredientList, craftCount);
        if (RebirthRemoteResourcePlacedStationOriginal.ProtocolActive &&
            RebirthRemoteResourcePlacedStationOriginal.IsEntryCandidate(player, entry))
        {
            // Select the actual native payment pool before the legacy bag-first
            // shortcut. An input-funded station must not spend the owner's bag.
            List<ItemStack> placedRemote;
            if (!RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player, entry, out _, out var payment)) return false;
            if (payment == RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput)
            {
                if (!RebirthRemoteResourcePlacedStationProjection.TryCurrentShortfall(entry, out requirements, out placedRemote))
                { RemoteResourceClientAvailability.InvalidateAndRequest(player); return false; }
            }
            else
            {
                if (requirements.Count == 0 || HasAllLocal(player, requirements)) return true;
                if (!TryBuildRemoteShortfall(player, requirements, out placedRemote))
                { RemoteResourceClientAvailability.InvalidateAndRequest(player); return false; }
            }
            if (placedRemote.Count == 0) return true;
            RebirthRemoteResourcePlacedStationOriginal.BeginClient(player, entry, clientSessionEpoch, NextRequestId(), requirements, placedRemote);
            return false; // A retained original never falls back to legacy payment.
        }
        if (requirements.Count == 0 || HasAllLocal(player, requirements)) return true;

        XUiC_WorkstationInputGrid workstationInput = entry.craftCountControl.WindowGroup != null
            ? entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>()
            : null;
        if (workstationInput != null && workstationInput.HasItems(entry.tempIngredientList, craftCount)) return true;

        List<ItemStack> remoteShortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out remoteShortfall))
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }
        if (remoteShortfall.Count == 0) return true;

        ulong requestId = NextRequestId();
        if(workstationInput==null&&RebirthRemoteResourceFetchedStationOriginal.ProtocolActive&&
            RebirthRemoteResourceFetchedStationOriginal.IsEntryCandidate(player,entry))
        {
            // Own mobile native queue adapter, selected only after its complete
            // protocol is activated. Other station architectures keep their
            // existing path; no personal queue witness is substituted here.
            RebirthRemoteResourceFetchedStationOriginal.BeginClient(player,entry,clientSessionEpoch,requestId,requirements,remoteShortfall);
            return false;
        }
        if (workstationInput == null&&RebirthRemoteResourceCraftOriginal.IsPersonalEntry(entry))
        {
            // The ordinary personal queue uses the saved original protocol.
            // Station payment retains its existing native lease path until
            // its distinct queue/publication adapter is migrated.
            RebirthRemoteResourceCraftProtocol.Begin(player,entry,clientSessionEpoch,requestId,requirements,remoteShortfall);
            return false;
        }
        // Architecture recognition must survive a temporarily missing native
        // lock/readiness witness. The genuine placed Source4 producer above
        // owns this ordinary workstation; it may wait for custody but cannot
        // silently become a legacy bag-first grant/refund transaction.
        var classifiedWindow=entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>()??
            entry.craftCountControl.WindowGroup?.Controller as XUiC_WorkstationWindowGroup;
        if(classifiedWindow!=null&&classifiedWindow.WorkstationData?.TileEntity is not RebirthCamperStationState&&
            !recipe.IsScrap&&!RebirthCookingHeat.Managed(recipe)&&!RebirthRecyclerRuntime.IsMarked(recipe)&&!RebirthStationGridQueue.IsMarked(recipe))
        {RemoteResourceClientAvailability.InvalidateAndRequest(player);return false;}
        // Managed cooking/recycler/grid jobs have genuine custom workspace
        // and command producers, not this generic native entry. A stale
        // personal/mobile or unclassified native context likewise has no
        // authenticated owning adapter here. Preserve its inputs while its
        // real producer becomes available; never mint a legacy grant lease.
        RemoteResourceClientAvailability.InvalidateAndRequest(player);
        return false;
    }

    // Called only by the purpose-scoped durable craft coordinator after its
    // host saved the exact original action permission. Not a legacy result API.
    internal static bool ResumeOriginalCraft(ItemActionEntryCraft entry,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,
        ItemStack[] paid,Recipe expected,int count,RebirthCamperEnergyActorImage after,Func<bool> authenticatedPermission,out string markedRecipe)
    {
        markedRecipe=null;
        try
        {
            var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;
            if(!RebirthRemoteResourceCraftOriginal.IsPersonalEntry(entry)||!RebirthRemoteResourceClientOriginal.Current(player,original)||original.Kind!=RemoteResourceClientOperation.Craft||authenticatedPermission?.Invoke()!=true||
                resumeCraft!=null||RemoteResourceClientGrantContext.Active||expected==null||after==null||paid==null||count<1||count>short.MaxValue||entry.craftCountControl.Count!=count)return false;
            if(!RebirthRemoteResourceCraftPlan.TryCreate(player,entry,paid,out _,out var currentAfter,out var currentRecipe,out int currentCount)||
                currentCount!=count||currentRecipe.craftingTier!=expected.craftingTier||!after.Matches(currentAfter)||
                RebirthRemoteResourceOwnerAction.RecipeImage(currentRecipe)!=RebirthRemoteResourceOwnerAction.RecipeImage(expected))return false;
            if(!RebirthRemoteResourceCraftRecipe.TryMarked(expected,source,binding,out var expectedMarked))return false;
            var data=player.PlayerUI.xui.GetCraftingData();if(data?.RecipeQueueItems==null||RebirthRemoteResourceClientOriginal.QueueImage(data)!=original.Copy().Element("queueBefore").Value)return false;
            var existing=data.RecipeQueueItems.Where(row=>row?.Recipe!=null).Select(row=>row.Recipe).ToArray();
            if(!RebirthRemoteResourceCraftOriginal.TryRead(original,out var retainedAction,out var actionPhase)||actionPhase!=RebirthCamperTransferJournal.Phase.Prepared||
                (string)retainedAction.Attribute("source")!=source.ToString("N")||(string)retainedAction.Attribute("binding")!=binding||
                retainedAction.Element("markedRecipe").Value!=RebirthRemoteResourceOwnerAction.RecipeImage(expectedMarked)||
                retainedAction.Element("afterActor").Value!=RebirthRemoteResourceClientOriginal.ActorImage(after)||
                !RebirthRemoteResourceCraftOriginal.BeginExecution(original))return false;
            RemoteResourceClientGrantContext.Begin(paid.ToList());
            try
            {
                using(var scope=RebirthRemoteResourceOwnerAction.BeginCraftOriginalScope(player,entry,original,source,binding,authenticatedPermission))
                {
                    if(scope==null)return false;
                    resumeCraft=entry;try{entry.OnActivated();}finally{resumeCraft=null;}
                }
            }
            finally{RemoteResourceClientGrantContext.End();}
            var rows=player.PlayerUI.xui.GetCraftingData()?.RecipeQueueItems?.Where(row=>row?.Recipe!=null&&!existing.Any(recipe=>ReferenceEquals(recipe,row.Recipe))).ToArray();
            if(rows==null||rows.Length!=1||rows[0].Multiplier!=count||rows[0].Quality!=expected.craftingTier||
                !RebirthRemoteResourceOwnerAction.StampActualRecipe(rows[0].Recipe,source,binding))return false;
            markedRecipe=RebirthRemoteResourceOwnerAction.RecipeImage(rows[0].Recipe);
            if(markedRecipe!=RebirthRemoteResourceOwnerAction.RecipeImage(expectedMarked)||!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),2f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{markedRecipe=null;return false;}
    }
    public static bool BeforeItemRepairActivated(ItemActionEntryRepair entry)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || entry == null) return true;
        if (!IsDedicatedClient)
        {
            EntityPlayerLocal local = entry.ItemController != null && entry.ItemController.xui != null &&
                entry.ItemController.xui.playerUI != null ? entry.ItemController.xui.playerUI.entityPlayer : null;
            if (local != null) RemoteResourceSnapshotCache.ForceRebuild(local);
            return true;
        }
        if (ReferenceEquals(resumeRepair, entry)) return true;
        if (RebirthRemoteResourceRepairProtocol.TryAttach(entry)) return false;

        XUi xui = entry.ItemController != null ? entry.ItemController.xui : null;
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null) return true;
        ItemStack itemStack = ItemStack.Empty.Clone();
        XUiC_EquipmentStack equipment = entry.ItemController as XUiC_EquipmentStack;
        XUiC_ItemStack grid = entry.ItemController as XUiC_ItemStack;
        if (equipment != null) itemStack = equipment.ItemStack;
        else if (grid != null) itemStack = grid.ItemStack;
        if (itemStack == null || itemStack.IsEmpty()) return true;
        ItemClass itemClass = ItemClass.GetForId(itemStack.itemValue.type);
        if (itemClass == null || itemClass.RepairTools == null || itemClass.RepairTools.Length == 0) return true;
        ItemClass repairTool = ItemClass.GetItemClass(itemClass.RepairTools[0].Value);
        if (repairTool == null) return true;
        int maxRepairTools = Convert.ToInt32(Math.Ceiling((double)Mathf.CeilToInt(itemStack.itemValue.UseTimes) / repairTool.RepairAmount.Value));
        int availableRepairTools = xui.PlayerInventory.GetItemCount(new ItemValue(repairTool.Id));
        int count = Mathf.Min(Math.Max(0, maxRepairTools), Math.Max(0, availableRepairTools));
        // Vanilla repairs as much as the currently available tool count allows rather than
        // requiring enough material for a full repair. Preserve that behavior here.
        if (count == 0) return true;
        ItemStack requirement = new ItemStack(new ItemValue(repairTool.Id), count);
        List<ItemStack> full = new List<ItemStack> { requirement };
        if (HasAllLocal(player, full)) return true;
        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, full, out shortfall) || shortfall.Count == 0)
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }

        ulong requestId = NextRequestId();
        RebirthRemoteResourceRepairProtocol.Begin(player,entry,clientSessionEpoch,requestId,full,shortfall);
        return false;
    }

    // These two edges are called only by the authenticated durable-original
    // response path. They never route a volatile legacy outcome into a paid
    // original and do not infer non-use from a callback result.
    internal static bool PrepareOriginalRepair(ItemActionEntryRepair entry,RebirthRemoteResourceClientOriginal.Intent original,
        string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,Func<bool> authenticatedSource,out XElement action)
    {
        action=null;
        try
        {
            var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedSource?.Invoke()!=true||RemoteResourceClientGrantContext.Active)return false;
            var input=GetControllerStack(entry.ItemController);if(input==null||input.count!=1)return false;
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(needs.Length!=1||needs[0].count<1)return false;
            string nativeInput=RebirthNativeItemCodec.Encode(input.itemValue);
            if(!RebirthRemoteResourceOwnerAction.TryCreateRepairPaymentIntent(player,original,sourceWorld,sourceOperation,binding,paid,needs[0].itemValue.type,needs[0].count,nativeInput,out var payment)||
                !RebirthRemoteResourceOwnerAction.TryPlanRepairOwner(entry,payment,nativeInput,out var before,out var after))return false;
            if(!RebirthRemoteResourceClientOriginal.SaveRepairSource(player,original,sourceWorld,sourceOperation,binding,paid,authenticatedSource))return false;
            string queued;int amount;
            RemoteResourceClientGrantContext.Begin(paid.ToList());
            try
            {
                if(!RebirthRepairOriginalCorrection.TryCalculateRemoteOriginalAmount(entry,payment.Encode(),out amount)||
                    !RebirthRepairOriginalCorrection.TryPrepareRemoteOriginal(entry,payment.Encode(),nativeInput,amount,out _,out queued))return false;
            }
            finally{RemoteResourceClientGrantContext.End();}
            if(!RebirthRemoteResourceClientOriginal.PrepareRepairAction(player,original,payment.Encode(),queued,amount,before,after,out action)||
                !RebirthRemoteResourceOwnerAction.TryHold(player,original,sourceOperation,binding,before,after,authenticatedSource))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),1f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{action=null;return false;}
    }
    internal static bool ResumeOriginalRepair(ItemActionEntryRepair entry,RebirthRemoteResourceClientOriginal.Intent original,ItemStack[] paid,Func<bool> authenticatedPermission)
    {
        var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedPermission?.Invoke()!=true||RemoteResourceClientGrantContext.Active||resumeRepair!=null||
                !RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out var action)||
                !RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||
                !RebirthRemoteResourceClientOriginal.TryReadRepairSource(original,out var sourceWorld,out var sourceOperation,out var sourceBinding,out var savedPaid)||
                sourceWorld!=payment.SourceWorld||sourceOperation!=payment.SourceOperation||sourceBinding!=payment.Binding||
                paid==null||!RebirthCamperInventorySnapshot.Capture(savedPaid).SequenceEqual(RebirthCamperInventorySnapshot.Capture(paid)))return false;
            var input=GetControllerStack(entry.ItemController);if(input==null||input.count!=1)return false;
            string paidInput=RebirthNativeItemCodec.Encode(input.itemValue);
            RemoteResourceClientGrantContext.Begin(paid.ToList());
            try
            {
                if(!RebirthRepairOriginalCorrection.TryRestorePreparedRemoteOriginal(entry,payment.Encode(),paidInput,action.Element("queuedInput").Value,(int)action.Element("amount"),out _))return false;
                using(var scope=RebirthRemoteResourceOwnerAction.BeginRepairOriginalScope(player,entry,original,payment.SourceWorld,payment.SourceOperation,payment.Binding,paid,payment.KitType,payment.TotalKits,paidInput,authenticatedPermission))
                {
                    if(scope==null||!RebirthRemoteResourceClientOriginal.BeginRepairOriginalExecution(player,original,authenticatedPermission))return false;
                    resumeRepair=entry;
                    try{entry.OnActivated();}
                    finally{resumeRepair=null;}
                }
            }
            finally{RemoteResourceClientGrantContext.End();}
            // The actual accepted paid queue and exact original native delta,
            // not return/Consumed, authorize the owner phase2 native upload.
            return RebirthRemoteResourceClientOriginal.TrySaveRepairOwnerUsage(player,original,entry);
        }
        catch{return false;}
    }

    public static bool BeforeBlockExecute(ItemActionRepair action, ItemActionData actionData, bool released)
    {
        if (action == null || actionData == null || released) return true;
        if (ReferenceEquals(resumeBlockRepair, actionData)) return true;
        if (RebirthRemoteResourceBlockProtocol.BlocksNative(action,actionData)) return false;
        if (!RemoteResourcesRuntimePolicy.Enabled || !IsDedicatedClient) return true;

        EntityPlayerLocal player = actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || player.HitInfo == null || !player.HitInfo.bHitValid) return true;
        if (Time.time - actionData.lastUseTime < action.Delay) return true;
        if (actionData.invData.itemValue.MaxUseTimes > 0 && actionData.invData.itemValue.UseTimes >= actionData.invData.itemValue.MaxUseTimes)
            return true;
        if (actionData.invData.itemValue.UseTimes == 0f && actionData.invData.itemValue.MaxUseTimes == 0) return true;
        if (player.HitInfo.hit.distanceSq > Constants.cDigAndBuildDistance * Constants.cDigAndBuildDistance) return true;
        if (World.SandboxUseTraderArea == TraderAreaStates.Default && actionData.invData.world.IsWithinTraderArea(player.HitInfo.hit.blockPos)) return true;
        if (!GameUtils.IsBlockOrTerrain(player.HitInfo.tag)) return true;

        Vector3i target;
        List<ItemStack> requirements;
        if (!TryBuildBlockRepairRequirements(action, actionData, out target, out requirements) || requirements.Count == 0)
            return true;
        if (HasAllLocal(player, requirements)) return true;

        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out shortfall) || shortfall.Count == 0)
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            RemoteResourceLiveSync.DispatchLocalUi(player);
            return false;
        }

        ulong requestId = NextRequestId();
        if (RebirthRemoteResourceBlockProtocol.IsActive)
            RebirthRemoteResourceBlockProtocol.Begin(player,action,actionData as ItemActionRepair.InventoryDataRepair,target,RemoteResourceClientOperation.BlockRepair,clientSessionEpoch,requestId,requirements,shortfall);
        return false;
    }

    public static bool BeforeBlockHoldingUpdate(ItemActionRepair action, ItemActionData actionData)
    {
        if (action == null || actionData == null) return true;
        if (ReferenceEquals(resumeBlockUpgrade, actionData)) return true;
        if (RebirthRemoteResourceBlockProtocol.BlocksNative(action,actionData)) return false;
        if (!RemoteResourcesRuntimePolicy.Enabled || !IsDedicatedClient) return true;
        if (!action.bUpgradeCountChanged) return true;

        EntityPlayerLocal player = actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || actionData.invData.world == null) return true;
        BlockValue blockValue = actionData.invData.world.GetBlock(action.blockTargetPos);
        int requiredHits;
        if (blockValue.Block == null || !int.TryParse(blockValue.Block.Properties.GetString("UpgradeBlock", "UpgradeHitCount"), out requiredHits))
            return true;
        requiredHits = requiredHits + action.hitCountOffset < 1f ? 1 : (int)(requiredHits + action.hitCountOffset);
        if (action.blockUpgradeCount < requiredHits) return true;

        ItemStack requirement;
        if (!RemoteResourceActionConsumerPatchInstaller.TryBuildUpgradeRequirement(action, blockValue, out requirement))
            return true;
        List<ItemStack> requirements = new List<ItemStack> { requirement };
        if (HasAllLocal(player, requirements)) return true;

        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out shortfall) || shortfall.Count == 0)
        {
            action.blockUpgradeCount = 0;
            action.bUpgradeCountChanged = false;
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            RemoteResourceLiveSync.DispatchLocalUi(player);
            return false;
        }

        ulong requestId = NextRequestId();
        if (RebirthRemoteResourceBlockProtocol.IsActive)
            RebirthRemoteResourceBlockProtocol.Begin(player,action,actionData as ItemActionRepair.InventoryDataRepair,action.blockTargetPos,RemoteResourceClientOperation.BlockUpgrade,clientSessionEpoch,requestId,requirements,shortfall);
        return false;
    }

    private static bool TryBuildBlockRepairRequirements(
        ItemActionRepair action, ItemActionData actionData, out Vector3i target, out List<ItemStack> requirements)
    {
        target = Vector3i.zero;
        requirements = new List<ItemStack>();
        EntityPlayerLocal player = actionData != null && actionData.invData != null
            ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || !player.HitInfo.bHitValid) return false;
        target = player.HitInfo.hit.blockPos;
        BlockValue blockValue = actionData.invData.world.GetBlock(target);
        if (blockValue.ischild)
        {
            target = blockValue.Block.multiBlockPos.GetParentPos(target, blockValue);
            blockValue = actionData.invData.world.GetBlock(target);
        }
        Block block = blockValue.Block;
        if (block == null || !block.CanRepair(blockValue)) return false;
        int repairedDamage = Utils.FastMin((int)action.repairAmount, blockValue.damage);
        float repairFraction = (float)repairedDamage / block.MaxDamage;
        List<Block.SItemNameCount> repairItems = block.RepairItems;
        if (block.RepairItemsMeshDamage != null && block.shape.UseRepairDamageState(blockValue))
        {
            repairFraction = 1f;
            repairItems = block.RepairItemsMeshDamage;
        }
        if (repairItems == null) return false;
        ItemActionRepair.InventoryDataRepair data = actionData as ItemActionRepair.InventoryDataRepair;
        bool sameProgress = data != null && data.lastHitPosition == target && data.lastHitBlockValue.type == blockValue.type &&
            ReferenceEquals(data.lastRepairItems, repairItems) && data.lastRepairItemsPercents != null &&
            data.lastRepairItemsPercents.Length == repairItems.Count;
        float resourceScale = block.ResourceScale;
        for (int i = 0; i < repairItems.Count; i++)
        {
            float pendingFraction = sameProgress ? data.lastRepairItemsPercents[i] : 0f;
            if (pendingFraction > 0f) continue;
            float scaled = repairItems[i].Count * repairFraction * resourceScale;
            int count = Utils.FastMax((int)scaled, 1);
            ItemValue value = ItemClass.GetItem(repairItems[i].ItemName);
            if (value == null || value.IsEmpty()) continue;
            requirements.Add(new ItemStack(value, count));
        }
        return true;
    }


    private static ItemStack GetControllerStack(XUiController controller)
    {
        XUiC_EquipmentStack equipment = controller as XUiC_EquipmentStack;
        if (equipment != null) return equipment.ItemStack;
        XUiC_ItemStack grid = controller as XUiC_ItemStack;
        return grid != null ? grid.ItemStack : null;
    }

    private static ulong CreateSessionEpoch()
    {
        ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        return value == 0 ? 1UL : value;
    }

    private static ulong NextRequestId()
    {
        return unchecked((ulong)Interlocked.Increment(ref nextRequestId));
    }

    private static List<ItemStack> CloneRequirements(IList<ItemStack> source, int multiplier)
    {
        Dictionary<int, ItemStack> aggregate = new Dictionary<int, ItemStack>();
        for (int i = 0; source != null && i < source.Count; i++)
        {
            ItemStack stack = source[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int count = stack.count * multiplier;
            ItemStack current;
            if (aggregate.TryGetValue(stack.itemValue.type, out current)) current.count += count;
            else aggregate[stack.itemValue.type] = new ItemStack(stack.itemValue.Clone(), count);
        }
        return new List<ItemStack>(aggregate.Values);
    }

    private static bool HasAllLocal(EntityPlayerLocal player, IList<ItemStack> requirements)
    {
        for (int i = 0; requirements != null && i < requirements.Count; i++)
            if (CountConsumableLocal(player, requirements[i].itemValue) < requirements[i].count) return false;
        return true;
    }

    private static bool TryBuildRemoteShortfall(EntityPlayerLocal player, IList<ItemStack> requirements, out List<ItemStack> shortfall)
    {
        shortfall = new List<ItemStack>();
        for (int i = 0; requirements != null && i < requirements.Count; i++)
        {
            ItemStack required = requirements[i];
            int local = CountConsumableLocal(player, required.itemValue);
            int needed = Math.Max(0, required.count - local);
            if (needed == 0) continue;
            if ((player.world.IsRemote()?RemoteResourceClientAvailability.GetCount(player, required.itemValue):RemoteResourceTransactions.CountRemote(player,required.itemValue)) < needed) return false;
            shortfall.Add(new ItemStack(required.itemValue.Clone(), needed));
        }
        return true;
    }

    public static int CountConsumableLocal(EntityPlayer player, ItemValue itemValue)
    {
        if (player == null || itemValue == null || itemValue.IsEmpty() || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return 0;
        int total = 0;
        ItemStack[] bag = player.bag != null ? player.bag.ItemGrid.items : null;
        // Backpack slot locks are organization/Quick Stack locks, not vanilla crafting
        // exclusion locks. Base 3.1 Bag.GetItemCount/DecItem consume from these slots, so
        // the remote-resource preflight must count them too or dedicated clients are
        // incorrectly told they cannot craft recipes that vanilla can satisfy locally.
        for (int i = 0; bag != null && i < bag.Length; i++)
        {
            ItemStack stack = bag[i];
            if (Matches(stack, itemValue)) total += stack.count;
        }
        int slots = player.inventory != null ? player.inventory.Length : 0;
        for (int i = 0; i < slots; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (Matches(stack, itemValue)) total += stack.count;
        }
        return total;
    }

    private static bool Matches(ItemStack stack, ItemValue itemValue)
    {
        return stack != null && !stack.IsEmpty() && stack.count > 0 && stack.itemValue.type == itemValue.type &&
               (!stack.itemValue.HasModSlots || !stack.itemValue.HasMods());
    }
}

public static class RemoteResourceClientGrantContext
{
    [ThreadStatic] private static List<ItemStack> granted;
    [ThreadStatic] private static Dictionary<int, int> counts;
    [ThreadStatic] private static bool consumed;

    public static bool Active { get { return granted != null; } }
    public static bool Consumed { get { return consumed; } }

    public static void Begin(List<ItemStack> removedRemote)
    {
        granted = removedRemote != null ? new List<ItemStack>() : new List<ItemStack>();
        counts = new Dictionary<int, int>();
        consumed = false;
        for (int i = 0; removedRemote != null && i < removedRemote.Count; i++)
        {
            ItemStack stack = removedRemote[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            granted.Add(stack.Clone());
            int current;
            counts.TryGetValue(stack.itemValue.type, out current);
            counts[stack.itemValue.type] = current + stack.count;
        }
    }

    public static void End()
    {
        granted = null;
        counts = null;
        consumed = false;
    }

    public static int GetCount(ItemValue itemValue)
    {
        if (!Active || itemValue == null || itemValue.IsEmpty()) return 0;
        int value;
        return counts.TryGetValue(itemValue.type, out value) ? value : 0;
    }

    public static void AppendStacks(List<ItemStack> destination)
    {
        if (!Active || destination == null) return;
        for (int i = 0; i < granted.Count; i++) destination.Add(granted[i].Clone());
    }

    public static bool CanCover(EntityPlayerLocal player, ItemStack requirement)
    {
        if (!Active || player == null || requirement == null || requirement.IsEmpty() || requirement.count <= 0 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        int remote;
        counts.TryGetValue(requirement.itemValue.type, out remote);
        return RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, requirement.itemValue) + remote >= requirement.count;
    }

    public static bool HasItems(EntityPlayerLocal player, IList<ItemStack> requirements, int multiplier)
    {
        if (!Active || player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, int> requiredByType = new Dictionary<int, int>();
        Dictionary<int, ItemValue> values = new Dictionary<int, ItemValue>();
        for (int i = 0; i < requirements.Count; i++)
        {
            ItemStack requirement = requirements[i];
            if (requirement == null || requirement.IsEmpty() || requirement.count <= 0) continue;
            int current;
            requiredByType.TryGetValue(requirement.itemValue.type, out current);
            requiredByType[requirement.itemValue.type] = current + requirement.count * multiplier;
            values[requirement.itemValue.type] = requirement.itemValue;
        }
        foreach (KeyValuePair<int, int> pair in requiredByType)
        {
            int remote;
            counts.TryGetValue(pair.Key, out remote);
            if (RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, values[pair.Key]) + remote < pair.Value)
                return false;
        }
        return true;
    }

    public static bool ConsumeLocalRemainder(EntityPlayerLocal player, IList<ItemStack> requirements, int multiplier, IList<ItemStack> removedItems)
    {
        if (!Active || player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, int> remoteUseByType = new Dictionary<int, int>();
        Dictionary<int, int> localNeedByType = new Dictionary<int, int>();
        Dictionary<int, ItemValue> values = new Dictionary<int, ItemValue>();
        for (int i = 0; i < requirements.Count; i++)
        {
            ItemStack requirement = requirements[i];
            if (requirement == null || requirement.IsEmpty() || requirement.count <= 0) continue;
            int total = requirement.count * multiplier;
            int alreadyRemote;
            remoteUseByType.TryGetValue(requirement.itemValue.type, out alreadyRemote);
            int availableRemote;
            counts.TryGetValue(requirement.itemValue.type, out availableRemote);
            int useRemote = Math.Min(total, Math.Max(0, availableRemote - alreadyRemote));
            remoteUseByType[requirement.itemValue.type] = alreadyRemote + useRemote;
            int currentLocal;
            localNeedByType.TryGetValue(requirement.itemValue.type, out currentLocal);
            localNeedByType[requirement.itemValue.type] = currentLocal + total - useRemote;
            values[requirement.itemValue.type] = requirement.itemValue;
        }
        foreach (KeyValuePair<int, int> pair in localNeedByType)
            if (pair.Value > 0 && RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, values[pair.Key]) < pair.Value)
                return false;

        foreach (KeyValuePair<int, int> pair in localNeedByType)
            if (pair.Value > 0 && !ConsumeLocal(player, values[pair.Key], pair.Value, removedItems)) return false;
        foreach (KeyValuePair<int, int> pair in remoteUseByType)
        {
            int available;
            counts.TryGetValue(pair.Key, out available);
            counts[pair.Key] = Math.Max(0, available - pair.Value);
        }
        if (removedItems != null)
            for (int i = 0; i < granted.Count; i++) removedItems.Add(granted[i].Clone());
        if (player.PlayerUI != null && player.PlayerUI.xui != null && player.PlayerUI.xui.PlayerInventory != null)
        {
            player.PlayerUI.xui.PlayerInventory.dispatchBackpackItemsChanged();
            player.PlayerUI.xui.PlayerInventory.dispatchToolbeltItemsChanged();
        }
        consumed = true;
        return true;
    }

    private static bool ConsumeLocal(EntityPlayerLocal player, ItemValue itemValue, int count, IList<ItemStack> removedItems)
    {
        int left = count;
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; bag != null && i < bag.Length && left > 0; i++)
        {
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemValue.type ||
                (stack.itemValue.HasModSlots && stack.itemValue.HasMods())) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.bag.SetSlot(i, after);
            if (removedItems != null) removedItems.Add(new ItemStack(stack.itemValue.Clone(), take));
            left -= take;
        }
        int slots = player.inventory.Length;
        for (int i = 0; i < slots && left > 0; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemValue.type ||
                (stack.itemValue.HasModSlots && stack.itemValue.HasMods())) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.inventory.SetItem(i, after);
            if (removedItems != null) removedItems.Add(new ItemStack(stack.itemValue.Clone(), take));
            left -= take;
        }
        return left == 0;
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
internal static class RemoteResourceCraftPreflightPatch
{
    private static bool Prefix(ItemActionEntryCraft __instance)
    { return RemoteResourceClientTransactionCoordinator.BeforeCraftActivated(__instance); }
}

[HarmonyPatch(typeof(ItemActionEntryRepair), nameof(ItemActionEntryRepair.OnActivated))]
internal static class RemoteResourceItemRepairPreflightPatch
{
    private static bool Prefix(ItemActionEntryRepair __instance)
    { return RemoteResourceClientTransactionCoordinator.BeforeItemRepairActivated(__instance); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.ExecuteAction))]
internal static class RemoteResourceBlockRepairPreflightPatch
{
    private static bool Prefix(ItemActionRepair __instance, ItemActionData _actionData, bool _bReleased)
    { return RemoteResourceClientTransactionCoordinator.BeforeBlockExecute(__instance, _actionData, _bReleased); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.OnHoldingUpdate))]
internal static class RemoteResourceBlockUpgradePreflightPatch
{
    private static bool Prefix(ItemActionRepair __instance, ItemActionData _actionData)
    { return RemoteResourceClientTransactionCoordinator.BeforeBlockHoldingUpdate(__instance, _actionData); }
}

[HarmonyPatch(typeof(XUiC_WorkstationInputGrid), nameof(XUiC_WorkstationInputGrid.RemoveItems))]
internal static class RemoteResourceWorkstationInputGrantPatch
{
    private static bool Prefix(XUiC_WorkstationInputGrid __instance, IList<ItemStack> _itemStacks, int _multiplier, IList<ItemStack> _removedItems)
    {
        if (!RemoteResourceClientGrantContext.Active || __instance == null || __instance.xui == null || __instance.xui.playerUI == null) return true;
        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        RemoteResourceClientGrantContext.ConsumeLocalRemainder(player, _itemStacks, _multiplier, _removedItems);
        return false;
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceConsumeRequest : NetPackage
{
    private const int MaxRequirementStacks = 64;
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private ulong sessionEpoch;
    private ulong requestId;
    private RemoteResourceClientOperation operation;
    private RemoteResourceTransactionRequestMode mode;
    private List<ItemStack> requirements = new List<ItemStack>();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRemoteResourceConsumeRequest Setup(int id, PlatformUserIdentifierAbs uid, ulong epoch, ulong request,
        RemoteResourceClientOperation op, RemoteResourceTransactionRequestMode requestMode, List<ItemStack> items)
    {
        playerId = id; userId = uid; sessionEpoch = epoch; requestId = request; operation = op; mode = requestMode;
        if (items != null && items.Count > MaxRequirementStacks)
            throw new InvalidDataException("Remote Resources requirement count exceeds packet bound.");
        requirements = CloneStacks(items);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerId = binary.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(binary);
        sessionEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64();
        operation = (RemoteResourceClientOperation)binary.ReadByte();
        mode = (RemoteResourceTransactionRequestMode)binary.ReadByte();
        ItemStack[] values = GameUtils.ReadItemStack(binary);
        if (values != null && values.Length > MaxRequirementStacks)
            throw new InvalidDataException("Remote Resources requirement count exceeds packet bound.");
        requirements = values != null ? new List<ItemStack>(values) : new List<ItemStack>();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); PooledBinaryWriter binary = writer;
        binary.Write(playerId); userId.ToStream(binary); binary.Write(sessionEpoch); binary.Write(requestId);
        binary.Write((byte)operation); binary.Write((byte)mode);
        GameUtils.WriteItemStack(binary, requirements ?? new List<ItemStack>());
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread()||world == null || world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||
            manager?.IsServer!=true||Sender?.loginDone!=true||Sender.disconnecting||
            !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||Sender.netConnection==null||Channel>=Sender.netConnection.Length||!ReferenceEquals(Sender.netConnection[Channel],received)||
            sessionEpoch == 0 || requestId == 0 || requirements == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId) ||
            operation < RemoteResourceClientOperation.Craft || operation > RemoteResourceClientOperation.BlockUpgrade ||
            mode < RemoteResourceTransactionRequestMode.ExecuteOrReplay || mode > RemoteResourceTransactionRequestMode.QueryOnly) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        if (player == null||!ReferenceEquals(player.world,world)||player.IsDead() || persistent == null || persistent.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;
        if(operation==RemoteResourceClientOperation.ItemRepair)
        {Log.Warning("[RemoteResources] Legacy repair request refused; durable original repair protocol required.");return;}
        int originalDeaths=player.Died;string originalCreation=RebirthCamperCharacterAccess.CurrentCreation(player),originalWorldGuid=world.worldState?.Guid;

        RemoteResourceTransactionOutcome outcome = RemoteResourceTransactionOutcomeJournal.Resolve(
            world, player, userId, sessionEpoch, requestId, operation, requirements,
            mode == RemoteResourceTransactionRequestMode.QueryOnly,Sender,received);

        if(!ReferenceEquals(GameManager.Instance,callbacks)||!ReferenceEquals(callbacks.World,world)||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(playerId),player)||player.IsDead()||player.Died!=originalDeaths||
            world.worldState?.Guid!=originalWorldGuid||RebirthCamperCharacterAccess.CurrentCreation(player)!=originalCreation||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)||
            !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||Sender.disconnecting||!Sender.loginDone||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var confirmed)||!ReferenceEquals(confirmed,received)||confirmed.IsDisconnected())return;

        RemoteResourceDiagnostics.Write("preflight op=" + operation + " request=" + requestId + " epoch=" + sessionEpoch +
            " outcome=" + outcome.State + " removed=" + outcome.Removed.Count +
            " reason='" + (outcome.Reason ?? string.Empty) + "'");
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceConsumeResult>()
                .Setup(sessionEpoch, requestId, operation, outcome.State, outcome.Reason, outcome.Removed),
            _attachedToEntityId: playerId);
        if (outcome.State == RemoteResourceTransactionOutcomeState.Failed)
            RemoteResourceLiveSync.PushSnapshot(player, "failed transaction preflight");
    }
    public int GetLength() { return 0; }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceConsumeResult : NetPackage
{
    private const int MaxReasonLength = 512;
    private const int MaxRemovedStacks = 64;
    private ulong sessionEpoch;
    private ulong requestId;
    private RemoteResourceClientOperation operation;
    private RemoteResourceTransactionOutcomeState outcome;
    private string reason = string.Empty;
    private List<ItemStack> removed = new List<ItemStack>();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRemoteResourceConsumeResult Setup(ulong epoch, ulong request, RemoteResourceClientOperation op,
        RemoteResourceTransactionOutcomeState state, string why, List<ItemStack> items)
    {
        if (items != null && items.Count > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        sessionEpoch = epoch; requestId = request; operation = op; outcome = state; reason = ClampReason(why); removed = CloneStacks(items); return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        sessionEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64(); operation = (RemoteResourceClientOperation)binary.ReadByte();
        outcome = (RemoteResourceTransactionOutcomeState)binary.ReadByte(); reason = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxReasonLength);
        ItemStack[] values = GameUtils.ReadItemStack(binary);
        if (values != null && values.Length > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        removed = values != null ? new List<ItemStack>(values) : new List<ItemStack>();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); PooledBinaryWriter binary = writer;
        if (removed != null && removed.Count > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        binary.Write(sessionEpoch); binary.Write(requestId); binary.Write((byte)operation); binary.Write((byte)outcome);
        RebirthSurvivorNetworkCodec.WriteString(binary, reason, MaxReasonLength);
        GameUtils.WriteItemStack(binary, removed ?? new List<ItemStack>());
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        // Legacy result packet retained for wire compatibility. No current
        // client action creates its old transaction lease or accepts a grant.
    }
    public int GetLength() { return 0; }

    private static string ClampReason(string value)
    {
        value = value ?? string.Empty;
        return value.Length <= MaxReasonLength ? value : value.Substring(0, MaxReasonLength);
    }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

internal sealed class RemoteResourceTransactionOutcome
{
    public RemoteResourceTransactionOutcomeState State;
    public string Reason = string.Empty;
    public List<ItemStack> Removed = new List<ItemStack>();

    public RemoteResourceTransactionOutcome Clone()
    {
        RemoteResourceTransactionOutcome copy = new RemoteResourceTransactionOutcome { State = State, Reason = Reason ?? string.Empty };
        for (int i = 0; Removed != null && i < Removed.Count; i++) if (Removed[i] != null) copy.Removed.Add(Removed[i].Clone());
        return copy;
    }
}

internal static class RemoteResourceTransactionOutcomeJournal
{
    private const int MaxRequirementBytes=6*1024*1024;
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, RemoteResourceTransactionOutcome> Outcomes =
        new Dictionary<string, RemoteResourceTransactionOutcome>(StringComparer.Ordinal);
    private static World boundWorld;
    private static readonly Dictionary<string,RemoteResourceLegacyOutcomeStore.Entry> Originals=new Dictionary<string,RemoteResourceLegacyOutcomeStore.Entry>(StringComparer.Ordinal);

    public static RemoteResourceTransactionOutcome Resolve(World world, EntityPlayer player, PlatformUserIdentifierAbs userId,
        ulong sessionEpoch, ulong requestId, RemoteResourceClientOperation operation, IList<ItemStack> requirements, bool queryOnly,
        ClientInfo nativePeer=null,INetConnection nativeConnection=null)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||player==null||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead()||userId==null||string.IsNullOrEmpty(userId.CombinedString)||
            sessionEpoch==0||requestId==0||operation<RemoteResourceClientOperation.Craft||operation>RemoteResourceClientOperation.CookingPull)
            return new RemoteResourceTransactionOutcome{State=RemoteResourceTransactionOutcomeState.Unknown,Reason="Original resource authority scope is no longer current."};
        string key = BuildKey(userId, sessionEpoch, requestId, operation);
        if(!queryOnly)
        {
            try
            {
                if(requirements==null||requirements.Count==0||requirements.Count>4096)throw new InvalidDataException();
                var stacks=new ItemStack[requirements.Count];
                for(int i=0;i<stacks.Length;i++)stacks[i]=requirements[i];
                byte[] payload=RebirthCamperInventorySnapshot.Capture(stacks);
                if(payload.Length>MaxRequirementBytes)throw new InvalidDataException();
            }
            // Malformed replay payloads do not prove that this numerical
            // original never removed resources. Never turn them into a
            // refundable failure before checking retained custody.
            catch{return new RemoteResourceTransactionOutcome{State=RemoteResourceTransactionOutcomeState.Unknown,Reason="Original resource requirements could not be qualified; custody is retained."};}
        }
        lock (Gate)
        {
            BindWorld(world);
            if(Outcomes.TryGetValue(key,out var existing))
                return Originals.TryGetValue(key,out var source)&&source.TryReadWarm(player,out var warm)?warm:Unknown();
            // Current genuine producers now use their purpose-specific saved
            // originals. A legacy execute packet cannot create a destructive
            // source admission. Retained warm outcomes and pending metadata
            // remain queryable/pumped; absence is Unknown, never unused/ACK.
            return Unknown();
        }
    }
    private static RemoteResourceTransactionOutcome Unknown()=>new RemoteResourceTransactionOutcome
    {State=RemoteResourceTransactionOutcomeState.Unknown,Reason="Original resource outcome is awaiting durable reconciliation."};
    // Opaque, server-local handle. Detached snapshots cannot mutate the retained
    // removal outcome; the handle expires on world reset. Capacity pressure
    // never retires an original that may still own undelivered items.
    private sealed class RetainedHandle
    {
        internal World World;
        internal string Key;
        internal RemoteResourceTransactionOutcome Outcome;
    }

    internal static bool TryGetRetained(World world, PlatformUserIdentifierAbs authenticatedUserId,
        ulong epoch, ulong request, RemoteResourceClientOperation operation,
        out RemoteResourceTransactionOutcome snapshot, out object handle)
    {
        snapshot = null; handle = null;
        if (world == null || authenticatedUserId == null || string.IsNullOrEmpty(authenticatedUserId.CombinedString) ||
            epoch == 0 || request == 0 || operation < RemoteResourceClientOperation.Craft ||
            operation > RemoteResourceClientOperation.CookingPull) return false;
        lock (Gate)
        {
            // Read-only lookup must never bind/reset a journal for a stale caller.
            if (!ReferenceEquals(boundWorld, world)) return false;
            string key = BuildKey(authenticatedUserId, epoch, request, operation);
            RemoteResourceTransactionOutcome retained;
            if (!Outcomes.TryGetValue(key, out retained) || retained == null ||
                retained.State == RemoteResourceTransactionOutcomeState.Unknown ||
                !Originals.TryGetValue(key,out var source)||!source.TryReadCurrent(out var full)||full.Removed.Count==0) return false;
            snapshot = full;
            handle = new RetainedHandle { World = world, Key = key, Outcome = retained };
            return true;
        }
    }

    internal static bool MatchesRetained(World world, PlatformUserIdentifierAbs authenticatedUserId,
        ulong epoch, ulong request, RemoteResourceClientOperation operation, object handle)
    {
        RetainedHandle retainedHandle = handle as RetainedHandle;
        if (retainedHandle == null || world == null || authenticatedUserId == null ||
            epoch == 0 || request == 0 || operation < RemoteResourceClientOperation.Craft ||
            operation > RemoteResourceClientOperation.CookingPull) return false;
        lock (Gate)
        {
            RemoteResourceTransactionOutcome retained;
            return ReferenceEquals(boundWorld, world) && ReferenceEquals(retainedHandle.World, world) &&
                retainedHandle.Key == BuildKey(authenticatedUserId, epoch, request, operation) &&
                Outcomes.TryGetValue(retainedHandle.Key, out retained) && ReferenceEquals(retained, retainedHandle.Outcome) &&
                Originals.TryGetValue(retainedHandle.Key,out var source) && source.TryReadCurrent(out var observed) && observed.Removed.Count>0;
        }
    }
    public static void ResetForWorldUnload(World retiringWorld)
    {
        if(!ThreadManager.IsMainThread()||retiringWorld==null)return;
        lock (Gate)
        {
            if(!ReferenceEquals(boundWorld,retiringWorld))return;
            RemoteResourceLegacyOutcomeStore.RetireWorld(retiringWorld);
            Outcomes.Clear(); Originals.Clear(); boundWorld = null;
        }
    }

    private static void BindWorld(World world)
    {
        if (ReferenceEquals(boundWorld, world)) return;
        RemoteResourceLegacyOutcomeStore.RetireWorld(boundWorld);
        Outcomes.Clear(); Originals.Clear(); boundWorld = world;
    }

    private static string BuildKey(PlatformUserIdentifierAbs userId, ulong epoch, ulong request, RemoteResourceClientOperation operation)
    {
        string player = userId != null ? userId.CombinedString : string.Empty;
        return player + "|" + epoch + "|" + request + "|" + (byte)operation;
    }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
internal static class RemoteResourceClientTransactionUpdatePatch
{
    private static void Postfix()
    { RemoteResourceLegacyOutcomeStore.PumpArchive(); }
}

[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))]
internal static class RebirthRemoteResourcePersonalQueueHoldPatch
{
    [HarmonyPriority(Priority.First+1)]
    private static bool Prefix(XUiC_RecipeStack __instance)=>!RebirthRemoteResourceOwnerAction.BlocksPersonalQueue(__instance);
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"HandleOnPress")]
internal static class RebirthRemoteResourcePersonalQueueCancelHoldPatch
{
    [HarmonyPriority(Priority.First+1)]
    private static bool Prefix(XUiC_RecipeStack __instance)=>!RebirthRemoteResourceOwnerAction.BlocksPersonalQueue(__instance);
}

public static class RemoteResourceClientTransactionPatchInstaller
{
    private static bool installed;
    internal static bool IsInstalled=>installed;
    public static void Install()
    {
        if (installed) return;
        RebirthRemoteResourceRepairProtocol.Install();
        RebirthRemoteResourceCraftProtocol.Install();
        RebirthRemoteResourceBlockProtocol.Install();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        Harmony harmony = new Harmony("rebirth.remote.resources.client.transactions.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceCraftPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceItemRepairPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceBlockRepairPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceBlockUpgradePreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceWorkstationInputGrantPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceClientTransactionUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRemoteResourcePersonalQueueHoldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRemoteResourcePersonalQueueCancelHoldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRemoteResourceCraftOriginalQueueCapture));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRemoteResourceCraftOriginalIngredientsSeal));
        RebirthRemoteResourcePreAdmissionRefusal.Install(harmony);
        installed = true;
    }
}
