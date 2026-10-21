using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// One original owner operation in native saved Cvars. The server journal retains
// the immutable sparse plan; native owner slots remain the inventory authority.
internal static class RebirthLogisticsOwnerReceipt
{
    private const string Prefix="rbLogisticsOwner";
    private static bool installed,active,awaitingTerminal;
    private static EntityPlayerLocal heldPlayer;private static World heldWorld;private static Guid heldOperation;
    private sealed class Permit {internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal Func<bool> Current;internal bool Used;}
    [ThreadStatic] private static Permit permit;
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.native.owner.receipt.v1");
        try
        {
            foreach(var type in new[]{typeof(RebirthLogisticsDeliveryGridSlot),typeof(RebirthLogisticsDeliveryGridBulk),typeof(RebirthLogisticsDeliveryGridAdd),
                typeof(RebirthLogisticsDeliveryGridCount),typeof(RebirthLogisticsDeliveryBagAdd),typeof(RebirthLogisticsDeliveryBagPartial),
                typeof(RebirthLogisticsDeliveryHeldUse),typeof(RebirthLogisticsDeliveryActions),typeof(RebirthLogisticsDeliveryDecItem),
                typeof(RebirthLogisticsDeliveryCursorGestures),typeof(RebirthLogisticsStaleTerminalUpload)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
            installed=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    // Volatile references only. Native saved original markers and files remain
    // the recovery authority when a different world/player is later loaded.
    internal static void ResetWorld(World world)
    {
        if(!ThreadManager.IsMainThread()||ReferenceEquals(heldWorld,world))return;
        heldPlayer=null;heldWorld=null;heldOperation=Guid.Empty;awaitingTerminal=false;
        if(!active)permit=null;
    }
    internal static bool TryOperation(EntityBuffs buffs,out Guid operation,out int phase)
    {
        operation=Guid.Empty;phase=0;if(buffs==null)return false;var bytes=new byte[16];
        for(int i=0;i<8;i++)
        {
            float part=buffs.GetCustomVar(Prefix+i);if(float.IsNaN(part)||float.IsInfinity(part)||part<0||part>ushort.MaxValue||part!=(int)part)return false;
            ushort word=(ushort)part;bytes[i*2]=(byte)word;bytes[i*2+1]=(byte)(word>>8);
        }
        float value=buffs.GetCustomVar(Prefix+"Phase");if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>5||value!=(int)value)return false;
        operation=new Guid(bytes);phase=(int)value;return operation!=Guid.Empty;
    }
    internal static bool HasPendingOriginal(EntityPlayerLocal player)
        =>player?.Buffs!=null&&ReferenceEquals(player.world,GameManager.Instance?.World)&&ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&
            (player.Buffs.GetCustomVar(Prefix+"Held")==1f||awaitingTerminal&&ReferenceEquals(player,heldPlayer)&&ReferenceEquals(player.world,heldWorld));
    internal static bool TryOriginalDeaths(EntityPlayerLocal player,out int deaths)
    {
        deaths=0;if(player?.Buffs==null||player.Buffs.GetCustomVar(Prefix+"DeathScope")!=1f)return false;
        float low=player.Buffs.GetCustomVar(Prefix+"Deaths0"),high=player.Buffs.GetCustomVar(Prefix+"Deaths1");
        if(float.IsNaN(low)||float.IsInfinity(low)||float.IsNaN(high)||float.IsInfinity(high)||low<0||low>ushort.MaxValue||high<0||high>32767||low!=(int)low||high!=(int)high)return false;
        deaths=(int)((uint)(ushort)low|((uint)(ushort)high<<16));return true;
    }
    internal static bool IsHeld(EntityPlayerLocal player)
        =>HasPendingOriginal(player)&&player.IsSpawned()&&!player.IsDead()&&(!TryOriginalDeaths(player,out int deaths)||player.Died==deaths);
    internal static bool Blocks(ItemStackGrid grid)
    {var player=GameManager.Instance?.World?.GetPrimaryPlayer();return RebirthLogisticsDeathSettlement.Blocks(grid)||IsHeld(player)&&(ReferenceEquals(grid,player.bag?.ItemGrid)||ReferenceEquals(grid,player.inventory?.ItemGrid));}
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var entry=permit;if(entry==null||entry.Used||!ReferenceEquals(grid,entry.Grid)||entry.Slot!=slot||!ReferenceEquals(value,entry.Value)||entry.Current?.Invoke()!=true)return RebirthLogisticsDeathSettlement.Consume(grid,slot,value);
        entry.Used=true;return true;
    }
    private static void SaveAndUpload(EntityPlayerLocal player)
    {
        var game=GameManager.Instance;var world=player?.world;
        if(game==null||world==null||!RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection))throw new IOException("Original native owner connection is unavailable; receipt retained.");
        int actor=player.entityId,deaths=player.Died;bool dead=player.IsDead();string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&
            ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(actor),player)&&player.entityId==actor&&player.Died==deaths&&player.IsDead()==dead&&
            RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&RebirthLogisticsOriginalTransport.TryOwnerConnection(out var liveManager,out var liveConnection)&&
            ReferenceEquals(liveManager,manager)&&ReferenceEquals(liveConnection,connection);
        if(!Current())throw new IOException("Original native owner changed before save; receipt retained.");
        game.SaveLocalPlayerData();
        if(!Current()||!RebirthLogisticsOriginalTransport.CanSendNative(connection))throw new IOException("Original native owner changed or upload is pressured; receipt retained.");
        var packet=NetPackageManager.GetPackage<NetPackagePlayerData>();
        bool attempted=false;
        try
        {
            if(!Current()||!LogisticsTransferService.ClientChannelReady(manager,packet))throw new IOException("Original native owner upload is unavailable; receipt retained.");
            packet.Setup(player);
            if(!Current())throw new IOException("Original native owner changed during upload capture; receipt retained.");
            attempted=true;manager.SendToServer(packet,false);
        }
        finally{if(!attempted&&packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);}
    }
    internal static bool TryUploadOriginal(EntityPlayerLocal player,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,player.world)||
            !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!player.IsSpawned()||player.IsDead()||
            !TryOperation(player.Buffs,out var actual,out _)||actual!=operation)return false;
        try{SaveAndUpload(player);return true;}catch{return false;}
    }
    // Native dropBackpack has already cleared its selected source cells. This
    // observer uploads that actual dead owner state without blocking any clear
    // or writing an item into a respawned owner.
    internal static void UploadDeathBoundary(EntityPlayerLocal player)
    {
        if(!installed||!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||!player.IsDead()||player.Died<1||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            player.Buffs.GetCustomVar(Prefix+"Held")!=1f||!TryOperation(player.Buffs,out _,out int phase)||phase>4)return;
        try{SaveAndUpload(player);}catch(Exception error){Log.Warning("[QuickStack] Native death owner upload retained: "+error.Message);}
    }
    internal static bool CanBegin(EntityPlayerLocal player,Guid operation)=>!(!installed||active||!ThreadManager.IsMainThread()||operation==Guid.Empty||player==null||player.Died<0||player.world?.IsRemote()!=true||!player.IsSpawned()||player.IsDead()||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            LocalPlayerUI.GetUIForPlayer(player)?.xui?.DragAndDropWindow?.IsEmpty()!=true||
            RebirthDogLifecycleService.HasHireInventoryHold(player)||HasPendingOriginal(player)||RebirthPlacedOriginal.IsHeld(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthCookingOutputDelivery.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||
            RebirthBackpackLibraryReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||
            RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory));
    internal static bool Begin(EntityPlayerLocal player,Guid operation,string acceptedHeader)
    {
        if(!CanBegin(player,operation)||!Guid.TryParseExact(acceptedHeader,"N",out var header)||header==Guid.Empty||header.ToString("N")!=acceptedHeader)return false;
        active=true;
        try
        {
            var headerBytes=header.ToByteArray();for(int i=0;i<8;i++)player.Buffs.SetCustomVar(Prefix+"Header"+i,headerBytes[i*2]|headerBytes[i*2+1]<<8,true);
            player.Buffs.SetCustomVar(Prefix+"HeaderScope",1f,true);
            var bytes=operation.ToByteArray();for(int i=0;i<8;i++)player.Buffs.SetCustomVar(Prefix+i,bytes[i*2]|bytes[i*2+1]<<8,true);
            player.Buffs.SetCustomVar(Prefix+"Deaths0",(ushort)((uint)player.Died&ushort.MaxValue),true);
            player.Buffs.SetCustomVar(Prefix+"Deaths1",(ushort)((uint)player.Died>>16),true);
            player.Buffs.SetCustomVar(Prefix+"DeathScope",1f,true);
            player.Buffs.SetCustomVar(Prefix+"Phase",0f,true);player.Buffs.SetCustomVar(Prefix+"Held",1f,true);
            heldPlayer=player;heldWorld=player.world;heldOperation=operation;awaitingTerminal=true;
            SaveAndUpload(player);return true;
        }
        catch(Exception error){Log.Warning("[QuickStack] Original owner preparation retained: "+error.Message);return false;}
        finally{active=false;}
    }
    internal static bool Matches(ItemStack[] live,ItemStack[] before,ItemStack[] after,bool mixed,bool applied)
    {
        if(live==null||before==null||after==null||live.Length!=before.Length||before.Length!=after.Length)return false;
        for(int i=0;i<live.Length;i++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i]))continue;
            bool a=RebirthStationGridIngredients.IsSameStackSnapshot(live[i],before[i]),b=RebirthStationGridIngredients.IsSameStackSnapshot(live[i],after[i]);
            if(mixed?!a&&!b:applied?!b:!a)return false;
        }
        return true;
    }
    internal static bool MatchesOriginal(ItemStack[] live,ItemStack[] scopeBefore,ItemStack[] scopeAfter,ItemStack[] before,ItemStack[] after,bool mixed,bool applied)
    {
        if(live==null||scopeBefore==null||scopeAfter==null||before==null||after==null||live.Length!=scopeBefore.Length||live.Length!=scopeAfter.Length||live.Length!=before.Length||live.Length!=after.Length)return false;
        for(int i=0;i<live.Length;i++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(scopeBefore[i],scopeAfter[i]))continue;
            bool a=RebirthStationGridIngredients.IsSameStackSnapshot(live[i],before[i]),b=RebirthStationGridIngredients.IsSameStackSnapshot(live[i],after[i]);
            if(mixed?!a&&!b:applied?!b:!a)return false;
        }
        return true;
    }
    // Called only after original authenticated server-plan/result correlation.
    // Return true means owner terminal acknowledgement, not a new item grant.
    internal static bool Advance(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record record,int serverPhase,Func<bool> authenticatedCurrent)
    {
        if(!installed||active||!ThreadManager.IsMainThread()||record==null||player==null||authenticatedCurrent==null||serverPhase<0||serverPhase>4)return false;
        if(serverPhase==4||record.Phase==4)return serverPhase==4&&record.Phase==4&&ReleaseCompletedOriginal(player,record,authenticatedCurrent);
        active=true;
        try
        {
            var world=player.world;var grid=player.bag?.ItemGrid;if(grid==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            bool Current()=>authenticatedCurrent()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world?.GetPrimaryPlayer(),player)&&
                ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&player.Died==record.Deaths&&
                ReferenceEquals(player.bag.ItemGrid,grid)&&record.Owner==identity.StorageKey&&record.World==RebirthLogisticsTransferJournal.WorldKey(world)&&
                record.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&TryOperation(player.Buffs,out var operation,out _)&&operation==record.Operation;
            if(!Current()||!TryOperation(player.Buffs,out _,out int phase))return false;
            if(phase==5)return false; // Terminal4 was routed before every item Apply.
            var final=record.Outcome==2?record.BeforeOwner:record.AfterOwner;
            awaitingTerminal=true;heldPlayer=player;heldWorld=world;heldOperation=record.Operation;
            if(phase==0)
            {
                if(record.Rejecting||serverPhase<1)return false;
                if(!Matches(grid.items,record.BeforeOwner,record.AfterOwner,false,false))return false;
                player.Buffs.SetCustomVar(Prefix+"Phase",1f,true);SaveAndUpload(player);return false;
            }
            if(phase==1&&serverPhase>=1)
            {
                if(record.Rejecting)return false;
                // Debit original owner sources and save them BEFORE any server
                // destination credit. Native death can then drop only debited
                // contents, never both the deposited source and its destination.
                if(!Apply(player,grid,record.BeforeOwner,record.DebitedOwner,record.BeforeOwner,record.AfterOwner,Current))return false;
                player.Buffs.SetCustomVar(Prefix+"Phase",2f,true);SaveAndUpload(player);phase=2;
            }
            if(phase==2&&serverPhase>=2)
            {
                // Only authoritative native endpoint publication authorizes owner
                // credits. Restart resumes this original intermediate->final delta.
                if(record.Outcome==0||!Apply(player,grid,record.DebitedOwner,final,record.BeforeOwner,record.AfterOwner,Current))return false;
                player.Buffs.SetCustomVar(Prefix+"Phase",3f,true);SaveAndUpload(player);phase=3;
            }
            if(phase==3&&serverPhase>=3)
            {
                if(!Current()||!MatchesOriginal(grid.items,record.BeforeOwner,record.AfterOwner,record.BeforeOwner,final,false,true))return false;
                player.Buffs.SetCustomVar(Prefix+"Phase",4f,true);SaveAndUpload(player);phase=4;
            }
            if(phase==4&&serverPhase==4)
            {
                if(!Current())return false;player.Buffs.SetCustomVar(Prefix+"Phase",4f,true);player.Buffs.SetCustomVar(Prefix+"Held",0f,true);
                SaveAndUpload(player);awaitingTerminal=false;return true;
            }
            return false;
        }
        catch(Exception error){Log.Warning("[QuickStack] Original owner delta retained: "+error.Message);return false;}
        finally{active=false;}
    }
    // Only an authenticated original rejection asks for this freeze. Full native
    // Before + unchanged belt are required; no slot setter runs. Native phase5
    // makes any delayed older phase1 debit permission permanently ineligible.
    internal static bool FreezeNoEffect(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record original,Func<bool> authenticatedRejected)
    {
        if(!installed||active||!ThreadManager.IsMainThread()||player==null||original==null||authenticatedRejected==null)return false;
        active=true;
        try
        {
            if(authenticatedRejected()!=true||original.Phase>1||original.Outcome!=0||!original.Rejecting||!string.IsNullOrEmpty(original.OwnerDebitProof)||
                original.Legs.Any(leg=>leg.Attempted!=0)||original.Npcs.Any(leg=>leg.Attempted!=0)||
                player.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !player.IsSpawned()||player.IsDead()||player.Died!=original.Deaths||RebirthCamperCharacterAccess.CurrentCreation(player)!=original.Creation||
                RebirthLogisticsTransferJournal.WorldKey(player.world)!=original.World||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.StorageKey!=original.Owner||
                !TryOperation(player.Buffs,out var operation,out int phase)||operation!=original.Operation||(phase!=0&&phase!=1&&phase!=5)||
                LocalPlayerUI.GetUIForPlayer(player)?.xui?.DragAndDropWindow?.IsEmpty()!=true||
                !FullSame(player.bag?.ItemGrid?.items,original.OriginalOwnerBag)||!FullSame(player.inventory?.ItemGrid?.items,original.OriginalOwnerBelt))return false;
            if(authenticatedRejected()!=true)return false;
            player.Buffs.SetCustomVar(Prefix+"Phase",5f,true);player.Buffs.SetCustomVar(Prefix+"Held",1f,true);
            heldPlayer=player;heldWorld=player.world;heldOperation=operation;awaitingTerminal=true;SaveAndUpload(player);return true;
        }
        catch{return false;}
        finally{active=false;}
    }
    private static bool FullSame(ItemStack[] left,ItemStack[] right)
        =>left!=null&&right!=null&&left.Length==right.Length&&left.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,right[index])).Any()==false;
    private static bool Apply(EntityPlayerLocal player,ItemStackGrid grid,ItemStack[] before,ItemStack[] after,ItemStack[] scopeBefore,ItemStack[] scopeAfter,Func<bool> current)
    {
        if(!MatchesOriginal(grid.items,scopeBefore,scopeAfter,before,after,true,false))return false;
        for(int slot=0;slot<grid.Length;slot++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot])||RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[slot],after[slot]))continue;
            if(!current()||!MatchesOriginal(grid.items,scopeBefore,scopeAfter,before,after,true,false)||grid.IsLocked(slot))return false;
            var value=after[slot].Clone();var entry=new Permit{Grid=grid,Slot=slot,Value=value,Current=current};if(permit!=null)return false;permit=entry;
            try{player.bag.SetSlot(slot,value);}finally{permit=null;}
            if(!entry.Used||!current()||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[slot],after[slot]))return false;
        }
        return current()&&MatchesOriginal(grid.items,scopeBefore,scopeAfter,before,after,false,true);
    }
    // Terminal original-operation queries may release this domain's old hold
    // after native respawn. They never replay a delta into a new-life backpack.
    private static bool SameLifeTerminalPostimage(EntityPlayerLocal player,RebirthStablePlayerIdentity owner,RebirthLogisticsTransferJournal.Record terminal)
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localOwner)||localOwner.StorageKey!=owner.StorageKey||player.Died!=terminal.Deaths||!TryOperation(player.Buffs,out var operation,out int phase)||operation!=terminal.Operation||(phase!=3&&phase!=4&&(phase!=5||terminal.Outcome!=3))||
            !RebirthLogisticsHeaderAdmission.TryAcceptedNonce(player,terminal,out var accepted)||!TryLocalHeader(player,out var liveNonce)||liveNonce!=accepted||
            !TrySavedOperation(localOwner,out var savedOperation,out int savedPhase,out float savedHeld,out var saved)||savedOperation!=operation||savedPhase!=phase||saved.deaths!=terminal.Deaths||saved.id!=player.entityId||saved.bDead||
            !TryReadDetachedHeader(saved,out var savedNonce)||savedNonce!=accepted)return false;
        float liveHeld=player.Buffs.GetCustomVar(Prefix+"Held");if((liveHeld!=0f&&liveHeld!=1f)||savedHeld!=liveHeld||phase!=4&&liveHeld!=1f)return false;
        ItemStack[] expected;
        if(terminal.Outcome==3){if(string.IsNullOrEmpty(terminal.OwnerNoEffectProof)||terminal.OwnerEscrowRecovery)return false;expected=terminal.OriginalOwnerBag;}
        else if(!RebirthLogisticsDeathPartition.TryExpected(terminal,3,out expected))return false;
        var savedBag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var savedBelt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
        return RebirthLogisticsDeathPartition.Same(expected,savedBag)&&RebirthLogisticsDeathPartition.Same(expected,player.bag?.ItemGrid?.items)&&
            RebirthLogisticsDeathPartition.Same(terminal.OriginalOwnerBelt,savedBelt)&&RebirthLogisticsDeathPartition.Same(terminal.OriginalOwnerBelt,player.inventory?.ItemGrid?.items);
    }
    // A terminal server history never permits another inventory delta. Close
    // only exact already-published same-life After, or the named old-life
    // fatal/body/parcel assertion transported from the authoritative originals.
    internal static bool ReleaseCompletedOriginal(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record terminal,Func<bool> authenticatedOriginalTerminal)
    {
        if(!installed||active||!ThreadManager.IsMainThread()||player==null||terminal==null||terminal.Phase!=4||terminal.Outcome==0||authenticatedOriginalTerminal?.Invoke()!=true||
            player.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!player.IsSpawned()||player.IsDead()||player.Died<terminal.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.StorageKey!=terminal.Owner||
            RebirthLogisticsTransferJournal.WorldKey(player.world)!=terminal.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=terminal.Creation||
            !TryOperation(player.Buffs,out var operation,out _)||operation!=terminal.Operation||(player.Buffs.GetCustomVar(Prefix+"Held")!=0f&&player.Buffs.GetCustomVar(Prefix+"Held")!=1f))return false;
        bool Proof()=>authenticatedOriginalTerminal()==true&&(player.Died==terminal.Deaths?SameLifeTerminalPostimage(player,owner,terminal):RebirthLogisticsOriginalTransport.HasPhysicalTerminalEvidence(player,terminal));
        if(!Proof())return false;active=true;
        try
        {
            if(!Proof())return false;
            player.Buffs.SetCustomVar(Prefix+"Phase",4f,true);player.Buffs.SetCustomVar(Prefix+"Held",0f,true);SaveAndUpload(player);
            if(ReferenceEquals(heldPlayer,player)&&heldOperation==terminal.Operation){awaitingTerminal=false;heldPlayer=null;heldWorld=null;heldOperation=Guid.Empty;}
            return true;
        }
        catch(Exception error){Log.Warning("[QuickStack] Original terminal hold release retained: "+error.Message);return false;}
        finally{active=false;}
    }
    // Native ProcessPackage has not saved incoming data yet. Suppress ONLY a
    // positively classified stale same-life original, preserving the actual
    // already-published final native file. Unknown/unrelated uploads pass.
    internal static bool ShouldRefuseStaleTerminalUpload(NetPackagePlayerData packet,World world)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var sender=packet?.Sender;var incoming=packet?.playerDataFile;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||sender==null||incoming==null||
                !ReferenceEquals(manager?.Clients.ForEntityId(sender.entityId),sender)||incoming.id!=sender.entityId||incoming.bDead||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner))return false;
            var actor=world.GetEntity(sender.entityId) as EntityPlayer;
            if(!ReferenceEquals(actor?.world,world)||actor.IsDead()||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var nativeOwner)||nativeOwner.StorageKey!=owner.StorageKey||
                !TryReadDetachedOperation(incoming,out var operation,out int phase,out float held)||held!=1f||phase<0||phase>2||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var terminal)||terminal.Phase!=4||(terminal.Outcome!=1&&terminal.Outcome!=2)||
                terminal.World!=RebirthLogisticsTransferJournal.WorldKey(world)||terminal.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||actor.Died!=terminal.Deaths||incoming.deaths!=terminal.Deaths||
                !RebirthLogisticsHeaderAdmission.TryAdmittedTerminalNonce(actor,terminal,out var nonce)||!TryReadDetachedHeader(incoming,out var incomingNonce)||incomingNonce!=nonce||
                !TrySavedOperation(owner,out var savedOperation,out int savedPhase,out float savedHeld,out var saved)||savedOperation!=operation||(savedPhase!=3&&savedPhase!=4)||
                (savedHeld!=0f&&savedHeld!=1f)||savedPhase==3&&savedHeld!=1f||saved.id!=actor.entityId||saved.deaths!=terminal.Deaths||saved.bDead||
                !TryReadDetachedHeader(saved,out var savedNonce)||savedNonce!=nonce||!RebirthLogisticsDeathPartition.TryExpected(terminal,3,out var expected))return false;
            var savedBag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var savedBelt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            if(!FullSame(expected,savedBag)||!FullSame(terminal.OriginalOwnerBelt,savedBelt))return false;
            var incomingBag=RebirthGearPlayerDataInventory.ReadSlots(incoming,true);var incomingBelt=RebirthGearPlayerDataInventory.ReadSlots(incoming,false);
            return incomingBag!=null&&incomingBelt!=null&&incomingBag.Length==expected.Length&&incomingBelt.Length==terminal.OriginalOwnerBelt.Length&&
                (!FullSame(expected,incomingBag)||!FullSame(terminal.OriginalOwnerBelt,incomingBelt));
        }
        catch{return false;}
    }
    [HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.ProcessPackage))]
    private static class RebirthLogisticsStaleTerminalUpload
    {
        private static bool Prefix(NetPackagePlayerData __instance,World _world)=>!ShouldRefuseStaleTerminalUpload(__instance,_world);
    }
    internal static bool TrySaved(RebirthStablePlayerIdentity identity,Guid operation,int phase,out PlayerDataFile data)
    {
        data=null;
        if(operation==Guid.Empty||!TrySavedOperation(identity,out var actual,out int savedPhase,out float held,out var saved)||actual!=operation||savedPhase!=phase||held!=1f)return false;
        data=saved;return true;
    }
    // A hash supplied by a packet is never payment. Construct this witness only
    // from the existing native final player file while the original server
    // operation/incarnation remains current. No live server replica is written.
    internal static bool TrySavedDebit(RebirthStablePlayerIdentity identity,RebirthLogisticsTransferJournal.Record record,
        Func<bool> originalCurrent,out string digest,out PlayerDataFile data)
        =>TrySavedStage(identity,record,2,originalCurrent,out digest,out data);
    internal static bool TrySavedStage(RebirthStablePlayerIdentity identity,RebirthLogisticsTransferJournal.Record record,int nativePhase,
        Func<bool> originalCurrent,out string digest,out PlayerDataFile data)
    {
        digest=null;data=null;
        try
        {
            if(!ThreadManager.IsMainThread()||nativePhase<2||nativePhase>5||GameManager.Instance?.World?.IsRemote()!=false||record==null||originalCurrent?.Invoke()!=true||
                identity.StorageKey!=record.Owner||record.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World)||
                !TrySaved(identity,record.Operation,nativePhase,out var saved)||saved.deaths!=record.Deaths)return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            if(bag==null||belt==null||record.OriginalOwnerBag==null||record.OriginalOwnerBelt==null||record.BeforeOwner==null||record.DebitedOwner==null||record.AfterOwner==null||
                bag.Length!=record.OriginalOwnerBag.Length||bag.Length!=record.BeforeOwner.Length||bag.Length!=record.DebitedOwner.Length||bag.Length!=record.AfterOwner.Length||belt.Length!=record.OriginalOwnerBelt.Length)return false;
            for(int i=0;i<bag.Length;i++)
            {
                var expected=nativePhase==5?record.OriginalOwnerBag[i]:RebirthStationGridIngredients.IsSameStackSnapshot(record.BeforeOwner[i],record.AfterOwner[i])?record.OriginalOwnerBag[i]:(nativePhase==2?record.DebitedOwner[i]:(record.Outcome==2?record.BeforeOwner[i]:record.AfterOwner[i]));
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(bag[i],expected))return false;
            }
            for(int i=0;i<belt.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(belt[i],record.OriginalOwnerBelt[i]))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
            {
                writer.Write(record.World);writer.Write(record.Owner);writer.Write(record.Creation);writer.Write(record.Operation.ToByteArray());writer.Write(record.Deaths);writer.Write(nativePhase);
                foreach(var image in new[]{saved.bagData,saved.inventoryData,saved.buffData}){var bytes=image.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();
                using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
            if(originalCurrent?.Invoke()!=true){digest=null;return false;}data=saved;return true;
        }
        catch{digest=null;data=null;return false;}
    }
    internal static bool TrySavedOperation(RebirthStablePlayerIdentity identity,out Guid operation,out int phase,out float held,out PlayerDataFile data)
    {
        data=null;operation=Guid.Empty;phase=0;held=0;
        try
        {
            if(!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.buffData==null||saved.buffData.Length>1048576)return false;
            if(!TryReadDetachedOperation(saved,out operation,out phase,out held))return false;
            data=saved;return true;
        }
        catch{return false;}
    }
    // Syntax only; this detached codec does not read/write a primary file.
    internal static bool TryReadDetachedOperation(PlayerDataFile saved,out Guid operation,out int phase,out float held)
    {
        operation=Guid.Empty;phase=0;held=0;
        try
        {
            if(saved?.buffData==null||saved.buffData.Length>1048576)return false;
            if(!TryReadNativeValues(saved,out var values))return false;
            if(!RebirthLogisticsSavedOwnerMarker.TryRead(values,out operation,out phase,out held))return false;
            return true;
        }
        catch{return false;}
    }
    private static bool TryReadNativeValues(PlayerDataFile saved,out Dictionary<string,float> result)
    {
        result=null;
        try
        {
            if(saved?.buffData==null||saved.buffData.Length>1048576)return false;
            var values=new Dictionary<string,float>(StringComparer.Ordinal);
            using(var stream=new MemoryStream(saved.buffData.ToArray()))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
                int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
                count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(values.ContainsKey(key))return false;values.Add(key,value);}
                if(stream.Position!=stream.Length)return false;
            }
            result=values;return true;
        }
        catch{return false;}
    }
    internal static bool TryLocalHeader(EntityPlayerLocal player,out string header)
    {
        header=null;
        if(player?.Buffs==null||player.Buffs.GetCustomVar(Prefix+"HeaderScope")!=1f)return false;
        var bytes=new byte[16];for(int i=0;i<8;i++)
        {
            float value=player.Buffs.GetCustomVar(Prefix+"Header"+i);if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>ushort.MaxValue||value!=(float)(ushort)value)return false;
            ushort part=(ushort)value;bytes[i*2]=(byte)part;bytes[i*2+1]=(byte)(part>>8);
        }
        var id=new Guid(bytes);if(id==Guid.Empty)return false;header=id.ToString("N");return true;
    }
    internal static bool TryReadDetachedHeader(PlayerDataFile saved,out string header)
    {
        header=null;
        try
        {
            if(!TryReadNativeValues(saved,out var values)||!values.TryGetValue(Prefix+"HeaderScope",out float scope)||scope!=1f)return false;
            var bytes=new byte[16];for(int i=0;i<8;i++)
            {
                float value=values.TryGetValue(Prefix+"Header"+i,out var word)?word:0f;
                if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>ushort.MaxValue||value!=(float)(ushort)value)return false;
                ushort part=(ushort)value;bytes[i*2]=(byte)part;bytes[i*2+1]=(byte)(part>>8);
            }
            var id=new Guid(bytes);if(id==Guid.Empty)return false;header=id.ToString("N");return true;
        }
        catch{return false;}
    }

}
// Native EntityBuffs.Write deliberately omits zero-valued CVars from final
// persistency files. Missing numeric words are therefore zero, but a fresh
// phase0 exists only with a nonempty original GUID AND actual saved Held1.
// This decoder is syntax only; actual native identity/file/scope and original
// immutable state remain mandatory at every caller before any effect.
internal static class RebirthLogisticsSavedOwnerMarker
{
    internal static bool TryRead(IReadOnlyDictionary<string,float> values,out Guid operation,out int phase,out float held)
    {
        operation=Guid.Empty;phase=0;held=0;if(values==null)return false;
        const string prefix="rbLogisticsOwner";var bytes=new byte[16];
        for(int i=0;i<8;i++)
        {
            values.TryGetValue(prefix+i,out float part);
            if(float.IsNaN(part)||float.IsInfinity(part)||part<0||part>ushort.MaxValue||part!=(int)part)return false;
            ushort word=(ushort)part;bytes[2*i]=(byte)word;bytes[2*i+1]=(byte)(word>>8);
        }
        var id=new Guid(bytes);values.TryGetValue(prefix+"Phase",out float actual);values.TryGetValue(prefix+"Held",out float currentHeld);
        if(id==Guid.Empty||float.IsNaN(actual)||float.IsInfinity(actual)||actual<0||actual>5||actual!=(int)actual||
            (currentHeld!=0f&&currentHeld!=1f)||currentHeld!=1f&&actual!=4f)return false;
        operation=id;phase=(int)actual;held=currentHeld;return true;
    }
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
internal static class RebirthLogisticsDeliveryGridSlot
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack)
        =>!RebirthLogisticsOwnerReceipt.Blocks(__instance)||RebirthLogisticsOwnerReceipt.Consume(__instance,_index,_stack);
}
[HarmonyPatch]
internal static class RebirthLogisticsDeliveryGridBulk
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","ReadInto","SetSlotLocks","SetLocked"})yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance)=>!RebirthLogisticsOwnerReceipt.Blocks(__instance);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
internal static class RebirthLogisticsDeliveryGridAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthLogisticsOwnerReceipt.Blocks(__instance))return true;__result=-1;return false;}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
internal static class RebirthLogisticsDeliveryGridCount
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthLogisticsOwnerReceipt.Blocks(__instance))return true;__result=0;return false;}
}
[HarmonyPatch]
internal static class RebirthLogisticsDeliveryDecItem
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(Bag),nameof(Bag.DecItem));yield return AccessTools.Method(typeof(Inventory),nameof(Inventory.DecItem));}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,ref int __result)
    {
        var grid=__instance is Bag bag?bag.ItemGrid:(__instance as Inventory)?.ItemGrid;
        if(!RebirthLogisticsOwnerReceipt.Blocks(grid))return true;__result=0;return false;
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
internal static class RebirthLogisticsDeliveryBagAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref bool __result){if(!RebirthLogisticsOwnerReceipt.Blocks(__instance.ItemGrid))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
internal static class RebirthLogisticsDeliveryBagPartial
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!RebirthLogisticsOwnerReceipt.Blocks(__instance.ItemGrid))return true;__result=(false,false);return false;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class RebirthLogisticsDeliveryHeldUse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityAlive __instance,ref bool __result){if(!RebirthLogisticsOwnerReceipt.IsHeld(__instance as EntityPlayerLocal))return true;__result=false;return false;}
}
[HarmonyPatch]
internal static class RebirthLogisticsDeliveryActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes)??throw new MissingMethodException(type.FullName,"OnActivated");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BaseItemActionEntry __instance)=>!RebirthLogisticsOwnerReceipt.IsHeld(__instance?.ItemController?.xui?.playerUI?.entityPlayer);
}

// Logistics receipts never own a cursor delta. Stop a whole gesture before its
// cursor-first/native slot effects, rather than only refusing its later bag write.
[HarmonyPatch]
internal static class RebirthLogisticsDeliveryCursorGestures
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(XUiC_DragAndDropWindow)))
            if(method.Name=="SetCurrentStack"||method.Name=="DropCurrentItem"||method.Name=="PlaceItemBackInInventory")yield return method;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance)
    {
        var player=__instance is XUiC_ItemStack slot?slot.xui?.playerUI?.entityPlayer:(__instance as XUiC_DragAndDropWindow)?.xui?.playerUI?.entityPlayer;
        return !RebirthLogisticsOwnerReceipt.IsHeld(player);
    }
}

