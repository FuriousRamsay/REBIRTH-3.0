using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Runtime hold only. Durable custody stays in the server journal and native receipt.
// Dispatcher must authenticate session/creation before acquiring or settling.
internal static class RebirthGearOwnerReservation
{
    private sealed class Entry { internal object World, Session; internal Guid SavedWorld; internal RebirthGearTransferState Offer; internal RebirthGearPreparationIntent Intent; internal bool Cold; }
    private static readonly Dictionary<EntityPlayerLocal, Entry> Entries = new Dictionary<EntityPlayerLocal, Entry>();
    // Early hold only; caller must save the original intent before uploading or
    // requesting preparation. Exact selected live actions enter through the initial dispatcher.
    internal static bool TryAcquireIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
    {
        if(intent==null||!RebirthGearPreparationIntent.TryRead(intent.Write(),out var validated)||
            !AuthenticatedIntent(player,session,validated)||!CanAcquire(player)||!TrySavedWorld(out var savedWorld))return false;
        if(Entries.TryGetValue(player,out var old))
            return ReferenceEquals(old.World,player.world)&&ReferenceEquals(old.Session,session)&&old.SavedWorld==savedWorld&&old.Intent!=null&&
                XNode.DeepEquals(old.Intent.Write(),validated.Write());
        if(!TryCapture(player,out var original)||!validated.MatchesInventory(original)||
            !AuthenticatedIntent(player,session,validated))return false;
        Entries.Add(player,new Entry{World=player.world,Session=session,SavedWorld=savedWorld,Intent=validated});return true;
    }
    // Explicit cold admission only. The original final file, live native receipt
    // and every physical slot agree; original preimage is reconstructed only
    // after the server returns the retained bound offer. No new ID or item move.
    internal static bool TryRestoreOriginal(EntityPlayerLocal player,object session)
        =>TryRestoreOriginalCore(player,session,false);
    // Original cold custody before owner projection: native JoinMultiplayer caller
    // must already have hydrated all grids/CVars. This grants no apply authority.
    internal static bool TryRestoreOriginalBeforeProjection(EntityPlayerLocal player,object session)
        =>TryRestoreOriginalCore(player,session,true);
    private static bool TryRestoreOriginalCore(EntityPlayerLocal player,object session,bool beforeProjection)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||session==null||!Current(player)||!CanAcquireCold(player,beforeProjection)||
                !TrySavedWorld(out var savedWorld)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId||
                !RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,savedWorld,out var marker,out var intent,out var saved,out var receipt)||
                !AuthenticatedColdIntent(player,session,intent,beforeProjection)||
                !RebirthGearPreparationMarker.TryRead(marker,1f,out var markerWorld,out var originalOwned,out var canonical)||
                markerWorld!=savedWorld||!XNode.DeepEquals(intent.Write(),canonical.Write())||
                player.Buffs==null||player.Buffs.GetCustomVar(marker)!=1f||
                player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=receipt||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,originalOwned,out var live)||
                !SamePhysical(saved,live)||!CanAcquireCold(player,beforeProjection)||!AuthenticatedColdIntent(player,session,intent,beforeProjection)||
                !TrySavedWorld(out var finalWorld)||finalWorld!=savedWorld)return false;
            if(Entries.TryGetValue(player,out var old))
                return ReferenceEquals(old.World,player.world)&&ReferenceEquals(old.Session,session)&&old.SavedWorld==savedWorld&&
                    old.Intent!=null&&XNode.DeepEquals(old.Intent.Write(),intent.Write());
            Entries.Add(player,new Entry{World=player.world,Session=session,SavedWorld=savedWorld,Intent=intent,Cold=true});return true;
        }
        catch{return false;}
    }
    private static bool AuthenticatedColdIntent(EntityPlayerLocal player,object session,
        RebirthGearPreparationIntent intent,bool beforeProjection)
    {
        if(!beforeProjection)return AuthenticatedIntent(player,session,intent);
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var connections=manager?.connectionToServer;
        if(intent==null||!Current(player)||!player.world.IsRemote()||manager==null||manager.IsServer||
            connections==null||connections.Length==0||connections[0]==null||
            !ReferenceEquals(connections[0],session)||connections[0].IsDisconnected())return false;
        string projected=RebirthSurvivorClientState.GetProjectedCreationId(player);
        // Unknown publication is not a guessed character. A known different or
        // malformed publication refuses early admission before any hold is added.
        return string.IsNullOrEmpty(projected)||
            (RebirthSurvivorRequestScope.TryNormalize(projected,out var normalized)&&
                RebirthSurvivorRequestScope.Matches(intent.CreationId,normalized));
    }
    // Original identity for deferred native maintenance only, not transfer authority.
    internal static bool TryGetHeldOriginalCreation(EntityPlayerLocal player,out string creation)
    {
        creation=null;
        if(!ThreadManager.IsMainThread()||!IsHeld(player)||!Entries.TryGetValue(player,out var entry)||
            entry.Intent==null||!TrySavedWorld(out var saved)||saved!=entry.SavedWorld||
            !AuthenticatedColdIntent(player,entry.Session,entry.Intent,true))return false;
        creation=entry.Intent.CreationId;return true;
    }
    private static bool CanAcquireCold(EntityPlayerLocal player,bool beforeProjection)
    {
        if(!beforeProjection)return CanAcquire(player);
        var xui=player?.PlayerUI?.xui;
        return player!=null&&player.IsSpawned()&&!player.IsDead()&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
            !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&player.inventory!=null&&
            !player.inventory.IsHoldingItemActionRunning()&&xui!=null&&!xui.IsUsingItemActionEntryUse&&
            xui.DragAndDropWindow?.CurrentStack!=null&&xui.DragAndDropWindow.CurrentStack.IsEmpty();
    }
    private static bool SamePhysical(RebirthGearInventorySnapshot a,RebirthGearInventorySnapshot b)
    {
        if(a==null||b==null||a.Bag.Length!=b.Bag.Length||a.Belt.Length!=b.Belt.Length)return false;
        for(int area=0;area<2;area++)
        {
            var left=area==0?a.Bag:a.Belt;var right=area==0?b.Bag:b.Belt;
            for(int i=0;i<left.Length;i++)
                if(left[i]==null||right[i]==null||left[i].Count!=right[i].Count||left[i].ItemData!=right[i].ItemData)return false;
        }
        return true;
    }
    // Same-session initial save may have failed before any final file exists.
    // Return only its retained original, never a cold/restored or adopted offer.
    internal static bool TryGetInitialIntent(EntityPlayerLocal player,object session,out RebirthGearPreparationIntent intent)
    {
        intent=null;
        if(!IsHeld(player)||!Entries.TryGetValue(player,out var entry)||entry.Cold||entry.Offer!=null||
            entry.Intent==null||!MatchesIntent(player,session,entry.Intent)||
            !AuthenticatedIntent(player,session,entry.Intent))return false;
        return RebirthGearPreparationIntent.TryRead(entry.Intent.Write(),out intent);
    }
    internal static bool MatchesIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
        =>IsHeld(player)&&intent!=null&&ReferenceEquals(Entries[player].Session,session)&&TrySavedWorld(out var world)&&world==Entries[player].SavedWorld&&Entries[player].Intent!=null&&
            XNode.DeepEquals(Entries[player].Intent.Write(),intent.Write());
    // Refusal applies only to the retained ORIGINAL with no adopted server offer.
    internal static bool MatchesUnpreparedIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
        =>MatchesIntent(player,session,intent)&&Entries[player].Offer==null&&AuthenticatedIntent(player,session,intent);
    internal static bool ReleaseRefusedOriginal(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent,Func<bool> savedRetirement)
    {
        try
        {
            if(savedRetirement==null||!MatchesUnpreparedIntent(player,session,intent))return false;
            var original=Entries[player];
            if(!savedRetirement()||!MatchesUnpreparedIntent(player,session,intent)||
                !ReferenceEquals(Entries[player],original))return false;
            if(player.Buffs==null||player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=-1f)return false;
            foreach(string key in player.Buffs.CVars.Keys)
                if(key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))return false;
            return Entries.Remove(player);
        }
        catch{return false;}
    }
    private static bool AuthenticatedIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var native=manager?.connectionToServer;
        return Current(player)&&player.world.IsRemote()&&manager!=null&&!manager.IsServer&&native!=null&&native.Length>0&&
            native[0]!=null&&ReferenceEquals(native[0],session)&&!native[0].IsDisconnected()&&
            RebirthSurvivorRequestScope.Matches(intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player));
    }
    private static bool TryCapture(EntityPlayerLocal player,out RebirthGearInventorySnapshot snapshot)
    {
        snapshot=null;var belt=player?.inventory?.ItemGrid?.items;
        return belt!=null&&RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,belt,
            RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out snapshot);
    }
    private static bool CanAcquire(EntityPlayerLocal player)
    {
        var xui=player?.PlayerUI?.xui;
        return player!=null&&player.IsSpawned()&&!player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
            !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&player.inventory!=null&&
            !player.inventory.IsHoldingItemActionRunning()&&xui!=null&&!xui.IsUsingItemActionEntryUse&&
            xui.DragAndDropWindow?.CurrentStack!=null&&xui.DragAndDropWindow.CurrentStack.IsEmpty();
    }
    internal static bool TryAcquire(EntityPlayerLocal player, object session, RebirthGearTransferState offer)
    {
        if (player == null || session == null || offer == null ||
            !RebirthGearTransferState.TryRead(offer.ToXml(), out var validated) || !Current(player)) return false;
        if (Entries.TryGetValue(player, out var old))
        {
            if(!ReferenceEquals(old.World,player.world)||!ReferenceEquals(old.Session,session))return false;
            if(old.Offer!=null)return (old.Offer.PreparationRequestDigest==null||(old.Intent!=null&&AuthenticatedIntent(player,session,old.Intent)&&TrySavedWorld(out var replayWorld)&&replayWorld==old.SavedWorld))&&XNode.DeepEquals(old.Offer.ToXml(),validated.ToXml());
            if(old.Cold)return TryAdoptCold(player,session,old,validated);
            if(old.Intent==null||!CanAcquire(player)||!AuthenticatedIntent(player,session,old.Intent)||!TryCapture(player,out var original)||
                !TrySavedWorld(out var currentWorld)||currentWorld!=old.SavedWorld||
                !RebirthGearPreparationOfferBinding.MatchesBound(old.Intent,original,validated,old.SavedWorld)||
                !CanAcquire(player)||!AuthenticatedIntent(player,session,old.Intent)||!TrySavedWorld(out var finalWorld)||finalWorld!=old.SavedWorld||!ReferenceEquals(Entries[player],old))return false;
            old.Offer=validated;return true; // The original hold remains continuously active.
        }
        if(validated.PreparationRequestDigest!=null)return false; // Bound offers require the original early hold.
        var xui = player.PlayerUI?.xui;
        if (!player.IsSpawned() || player.IsDead() || RebirthCharacterCreationHoldService.IsHeld(player) ||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld() || RebirthBackpackLibraryReservation.IsHeld(player) || RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player) ||
            player.inventory == null || player.inventory.IsHoldingItemActionRunning() ||
            xui == null || xui.IsUsingItemActionEntryUse || xui.DragAndDropWindow?.CurrentStack == null ||
            !xui.DragAndDropWindow.CurrentStack.IsEmpty()) return false;
        Entries.Add(player, new Entry { World = player.world, Session = session, Offer = validated });
        return true;
    }
    private static bool TryAdoptCold(EntityPlayerLocal player,object session,Entry entry,RebirthGearTransferState offer)
    {
        try
        {
            if(entry.Intent==null||offer.PreparationRequestDigest==null||!CanAcquire(player)||
                !AuthenticatedIntent(player,session,entry.Intent)||!TrySavedWorld(out var world)||world!=entry.SavedWorld||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId||
                !RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,world,out var marker,out var intent,out var saved,out var receipt)||
                !XNode.DeepEquals(intent.Write(),entry.Intent.Write())||
                !RebirthGearPreparationMarker.TryRead(marker,1f,out var markerWorld,out var owned,out _)||markerWorld!=world||
                player.Buffs?.GetCustomVar(marker)!=1f||player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=receipt||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,owned,out var live)||
                !SamePhysical(saved,live)||
                !RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,saved,world,owned,receipt,out _)||
                !CanAcquire(player)||!AuthenticatedIntent(player,session,entry.Intent)||
                !TrySavedWorld(out var finalWorld)||finalWorld!=world||!ReferenceEquals(Entries[player],entry)||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,owned,out var finalLive)||
                !SamePhysical(saved,finalLive)||player.Buffs.GetCustomVar(marker)!=1f||
                player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=receipt)return false;
            entry.Offer=offer;return true;
        }
        catch{return false;}
    }
    private static bool TrySavedWorld(out Guid world)
    {
        world=Guid.Empty;
        try {return Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out world)&&world!=Guid.Empty;}
        catch {return false;}
    }
    private static bool Current(EntityPlayerLocal player)
        => player?.world != null && GameManager.Instance != null &&
            ReferenceEquals(player.world, GameManager.Instance.World) &&
            ReferenceEquals(player.world.GetPrimaryPlayer(), player) &&
            ReferenceEquals(player.world.GetEntity(player.entityId), player);
    internal static bool IsHeld(EntityPlayerLocal player)
        => player != null && Entries.TryGetValue(player, out var entry) &&
            ReferenceEquals(entry.World, player.world) && Current(player);
    internal static bool BlocksInventory(object inventory, bool bag)
    {
        if (inventory == null) return false;
        foreach (var pair in Entries)
        {
            if (!IsHeld(pair.Key)) continue;
            object actual = bag ? (object)pair.Key.bag : pair.Key.inventory;
            if (ReferenceEquals(actual, inventory)) return true;
        }
        return false;
    }
    internal static bool Matches(EntityPlayerLocal player, object session, RebirthGearTransferState offer)
        => IsHeld(player) && session != null && offer != null &&
            ReferenceEquals(Entries[player].Session, session) &&
            Entries[player].Offer!=null && XNode.DeepEquals(Entries[player].Offer.ToXml(), offer.ToXml());
    internal static bool ReleaseSettled(EntityPlayerLocal player, object session,
        RebirthGearTransferState offer, Func<bool> verifyAuthoritativeSettlement)
    {
        if (!Matches(player, session, offer) || verifyAuthoritativeSettlement == null) return false;
        try
        {
            if (!verifyAuthoritativeSettlement() || !Matches(player, session, offer)) return false;
            return Entries.Remove(player);
        }
        catch { return false; }
    }
    // Transport finalization has no native item effects. Keep custody held until
    // its final saved-witness check and exact inbox removal have succeeded.
    internal static bool ReleaseSettledWithTransport(EntityPlayerLocal player,object session,
        RebirthGearTransferState offer,Func<bool> verifyAuthoritativeSettlement,Func<bool> finalizeTransport)
    {
        if(!Matches(player,session,offer)||verifyAuthoritativeSettlement==null||finalizeTransport==null)return false;
        try
        {
            if(!verifyAuthoritativeSettlement()||!Matches(player,session,offer)||!finalizeTransport())return false;
            // The owned synchronous inbox callback performs its final witness
            // before clearing. Do not read a file again after that clear.
            if(!Matches(player,session,offer))return false;
            return Entries.Remove(player);
        }
        catch{return false;}
    }
    internal static void ResetSession() { Entries.Clear(); }
}