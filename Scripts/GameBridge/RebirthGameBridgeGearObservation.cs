using System;
using Newtonsoft.Json.Linq;

// Read-only owner evidence. Never pumps a transfer, saves, uploads or releases custody.
public static class RebirthGameBridgeGearObservation
{
    public static void Handle(BridgeRequest req)
    {
        if(req.Method!="GET"){req.Fail("Gear observation is GET only.",405);return;}
        string trace=req.QueryString("trace");
        if(trace!=null&&trace!="1"&&trace!="0")
        {req.Fail("trace must be explicit 1 or 0.",400);return;}
        var player=RebirthGameBridgeCombat.PlayerFor(req);
        if(player==null)return;
        try
        {
            var world=player.world;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(GameManager.Instance?.World,world)||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(player.entityId),player))
            {req.Fail("Current native owner required.",409);return;}
            if(trace!=null)RebirthGearTrace.Configure(trace=="1",req.QueryBool("clearTrace",false));
            var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;
            if(belt==null||!RebirthGearInventorySnapshot.TryCapture(bag,belt,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out var inventory))
            {req.Fail("Exact native inventory capture refused.",409);return;}
            var cursor=player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack;
            if(cursor==null){req.Fail("Native cursor unavailable.",409);return;}
            var locks=new JArray();var slotLocks=player.bag.LockedSlots;
            if(slotLocks!=null)for(int i=0;i<slotLocks.Length;i++)locks.Add(slotLocks[i]);
            var receipts=new JObject();var intents=new JArray();
            if(player.Buffs?.CVars!=null)foreach(var cvar in player.Buffs.CVars)
            {
                if(cvar.Key.StartsWith("rbGear_",StringComparison.OrdinalIgnoreCase))receipts[cvar.Key]=cvar.Value;
                if(cvar.Key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))
                    intents.Add(new JObject{["key"]=cvar.Key,["value"]=cvar.Value});
            }
            var result=new JObject{
                ["schema"]=1,["entityId"]=player.entityId,["worldKey"]=GamePrefs.GetString(EnumGamePrefs.GameGuidClient),
                ["gameName"]=GamePrefs.GetString(EnumGamePrefs.GameName),["remote"]=world.IsRemote(),
                ["connected"]=manager!=null&&!manager.IsServer&&manager.connectionToServer!=null&&
                    manager.connectionToServer.Length>0&&manager.connectionToServer[0]!=null&&!manager.connectionToServer[0].IsDisconnected(),
                ["creationId"]=RebirthSurvivorClientState.GetProjectedCreationId(player),
                ["gearHeld"]=RebirthGearOwnerReservation.IsHeld(player),
                ["libraryHeld"]=RebirthBackpackLibraryReservation.IsHeld(player),
                ["resourcePending"]=RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player),
                ["creationHeld"]=RebirthCharacterCreationHoldService.IsHeld(player),
                ["bag"]=Stacks(inventory.Bag),["belt"]=Stacks(inventory.Belt),
                ["ownedBeltSlots"]=inventory.OwnedBeltSlots,["bagLocks"]=locks,
                ["cursor"]=CursorStack(cursor),
                ["rawNativeCursorCount"]=cursor.count,["rawNativeCursorType"]=cursor.itemValue?.type,
                ["rawNativeCursorIsEmpty"]=cursor.IsEmpty(),
                ["cursorObservationNormalized"]=cursor.count>0&&cursor.itemValue?.type==0,
                ["receipts"]=receipts,["intentMarkers"]=intents,
                ["projectedBackpack"]=RebirthSurvivorClientState.GetProjectedGearItem(player,"backpack"),
                ["projectedWalkman"]=RebirthSurvivorClientState.GetProjectedGearItem(player,"walkman")};
            RebirthGearTransferState offer=null;
            bool pending=RebirthGearOfferClient.TryGetCurrentOffer(world,player.entityId,out offer);
            bool hasTerminal=RebirthGearTerminalClient.TryGetCurrent(player,out _,out var terminal);
            if(!pending&&hasTerminal)RebirthGearOfferClient.TryGetSettledOriginal(world,player.entityId,terminal.TransactionId,out offer);
            result["offerPending"]=pending;
            result["terminal"]=hasTerminal?JObject.FromObject(terminal):null;
            result["offer"]=offer==null?null:Offer(offer);
            result["savedRetirement"]=offer!=null&&hasTerminal&&
                RebirthGearTerminalOwnerCheckpoint.HasSavedRetirement(player,offer,terminal.Applied);
            // Live exact publication evidence only: unloaded/missing recovery is unknown.
            var recovery=new JArray();
            if(offer!=null)foreach(var entity in world.Entities.list)
            {
                var pack=entity as EntityRebirthGearRecoveryBackpack;
                var item=entity as EntityRebirthGearRecoveryItem;
                var identity=pack?.RecoveryIdentity??item?.RecoveryIdentity;
                if(identity==null||identity.CreationId!=offer.CreationId||identity.TransactionId.ToString("N")!=offer.TransactionId)continue;
                var stacks=new JArray();
                if(pack!=null&&pack.bag?.ItemGrid?.items!=null)foreach(var stack in pack.bag.ItemGrid.items)stacks.Add(Stack(stack));
                if(item!=null)stacks.Add(Stack(item.itemStack));
                recovery.Add(new JObject{["entityId"]=entity.entityId,["publicationId"]=identity.PublicationId.ToString("N"),
                    ["entryIndex"]=identity.EntryIndex,["stacks"]=stacks});
            }
            result["recovery"]=recovery;result["trace"]=RebirthGearTrace.Snapshot();req.Complete(result);
        }
        catch(Exception error){req.Fail("Gear observation refused: "+error.GetType().Name,409);}
    }
    private static JArray Stacks(RebirthGearInventoryPlan.Stack[] slots)
    {var result=new JArray();foreach(var slot in slots)result.Add(new JObject{["count"]=slot.Count,["itemData"]=slot.ItemData});return result;}
    private static JObject Stack(ItemStack stack)
    {
        if(stack==null||stack.count==0)return new JObject{["count"]=0,["itemData"]=""};
        if(stack.count<0||stack.itemValue==null||stack.itemValue.IsEmpty())throw new InvalidOperationException("Invalid stack.");
        string data=RebirthNativeItemCodec.Encode(stack.itemValue);
        if(string.IsNullOrEmpty(data))throw new InvalidOperationException("Item encoding refused.");
        return new JObject{["count"]=stack.count,["itemData"]=data};
    }
    private static JObject CursorStack(ItemStack stack)
    {
        // Native cosmetic empty targets are ItemStack(ItemValue.None,1).
        // BasePartStack.SwapItem transfers that exact shape into the cursor;
        // ItemStackGrid.SetItem retains its positive count, while native
        // ItemStack.IsEmpty tests type==0. Normalize the observation only.
        if(stack==null||stack.count<0||stack.itemValue==null)
            throw new InvalidOperationException("Invalid native cursor.");
        if(stack.IsEmpty())return new JObject{["count"]=0,["itemData"]=""};
        return Stack(stack);
    }
    private static JObject Offer(RebirthGearTransferState offer)
    {
        if(!offer.TryGetPlan(out var plan))throw new InvalidOperationException("Invalid plan.");
        return new JObject{["transactionId"]=offer.TransactionId,["creationId"]=offer.CreationId,["slotId"]=offer.SlotId,
            ["expectedRevision"]=offer.ExpectedRevision,["receiptKey"]="rbGear_"+offer.TransactionId,
            ["plan"]=JObject.FromObject(plan)};
    }
}
