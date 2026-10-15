using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Actor payment adapters. Sole native debit follows an existing saved attempted
// admission; an exact owner-file receipt is required before queue publication.
internal static class RebirthCamperStationPlayerPayment
{
    internal static Guid Operation(RebirthStationGridAdmission admission)
    {
        if(admission==null||!admission.IsPlayerPayment||!admission.IsPublicationAttempted)
            throw new ArgumentException("Attempted actor payment admission required.");
        using(var hash=SHA256.Create())
        {
            var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes("rebirth.camper.actor-payment.v1\n"+
                admission.WritePlayerPaymentBinding().ToString(SaveOptions.DisableFormatting)));
            var guid=new byte[16];Array.Copy(bytes,guid,16);return new Guid(guid);
        }
    }
    private static bool[] Locks(Bag bag)
    {
        var locks=bag?.LockedSlots;if(locks==null)return null;
        var result=new bool[locks.Length];for(int i=0;i<result.Length;i++)result[i]=locks[i];return result;
    }
    internal static bool TryPlan(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        out RebirthStationGridIngredients.Plan fragmentPlan,out ItemStack[] fragments,
        out RebirthCamperStationPlayerPaymentImage payment)
    {
        fragmentPlan=null;fragments=null;payment=null;
        if(!(player is EntityPlayerLocal)||!RebirthStationPlayerIngredients.UsesPlayerPayment(player,station)||
            RebirthBackpackLibraryReservation.BlocksResourceUse(player)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
        return TryPlanFromImages(player,station,recipe,batches,tier,identity,
            player.bag?.ItemGrid.items,player.inventory?.ItemGrid.items,Locks(player.bag),null,
            RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory?.ItemGrid.items.Length??0),
            out fragmentPlan,out fragments,out payment);
    }
    // Remote authority must provide authenticated final-owner-file images; live
    // server replicas are deliberately not substituted for owner custody.
    internal static bool TryPlanFromImages(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        RebirthStablePlayerIdentity identity,ItemStack[] bag,ItemStack[] belt,bool[] bagLocks,bool[] beltLocks,int owned,
        out RebirthStationGridIngredients.Plan fragmentPlan,out ItemStack[] fragments,
        out RebirthCamperStationPlayerPaymentImage payment)
    {
        fragmentPlan=null;fragments=null;payment=null;
        if(station==null||identity==null||!RebirthStationPlayerIngredients.TryPlan(player,recipe,batches,tier,
            bag,belt,bagLocks,beltLocks,owned,out var plan)||
            !RebirthCamperStationPlayerPaymentImage.TryCreate(identity,plan,station.Input,bagLocks,beltLocks,out payment))return false;
        fragments=payment.PaidSnapshots();
        var allocations=new List<RebirthStationGridIngredients.Allocation>();
        for(int i=0;i<fragments.Length;i++)allocations.Add(new RebirthStationGridIngredients.Allocation(i,fragments[i],fragments[i].count));
        fragmentPlan=new RebirthStationGridIngredients.Plan(recipe,batches,tier,allocations,fragments);
        return true;
    }
    internal static bool MatchesBefore(EntityPlayer player,RebirthStationGridAdmission admission)
    {
        if(!(player is EntityPlayerLocal local)||!Current(local,admission,out var payment,out _)||
            !payment.MatchesBefore(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,Locks(player.bag),null,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length)))return false;
        return true;
    }
    private static bool Current(EntityPlayerLocal player,RebirthStationGridAdmission admission,
        out RebirthCamperStationPlayerPaymentImage payment,out RebirthStablePlayerIdentity identity)
    {
        payment=null;identity=null;
        return ThreadManager.IsMainThread()&&player?.world!=null&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
            ReferenceEquals(player.world.GetEntity(player.entityId),player)&&!player.IsDead()&&
            !RebirthCharacterCreationHoldService.IsHeld(player)&&!RebirthBackpackLibraryReservation.BlocksResourceUse(player)&&
            !(player.world.IsRemote()&&RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player))&&
            admission!=null&&admission.TryGetMobileAddress(out var address)&&admission.TryGetPlayerPayment(out payment)&&
            RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out var currentAddress)&&currentAddress.Equals(address)&&
            RebirthStationPlayerIngredients.UsesPlayerPayment(player,station)&&payment.MatchesStation(station.Input)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity)&&identity!=null&&
            identity.StorageKey==payment.OwnerKey&&identity.CanonicalId==payment.OwnerId&&
            RebirthSurvivorRequestScope.Matches(admission.CreationId,player.world.IsRemote()
                ?RebirthSurvivorClientState.GetProjectedCreationId(player):RebirthCamperCharacterScope.ResolveLocal(player));
    }
    internal static bool TryApplyRemote(EntityPlayerLocal player,Guid operation,RebirthStationGridAdmission admission)
    {
        if(player?.world?.IsRemote()!=true||admission==null||!admission.IsPlayerPayment||!admission.IsPublicationAttempted||Operation(admission)!=operation||
            !Current(player,admission,out var payment,out _)||
            player.Buffs==null||player.Buffs.GetCustomVar("rbCamperOwner_"+operation.ToString("N"))!=2f||
            !MatchesBefore(player,admission))return false;
        return ApplyExact(player,admission,payment);
    }
    internal static bool TryApplyLocal(EntityPlayer player,RebirthStationGridAdmission admission,Func<bool> current)
    {
        if(!(player is EntityPlayerLocal local)||player.world?.IsRemote()!=false||current==null||!current()||
            admission==null||!admission.IsPublicationAttempted||!Current(local,admission,out var payment,out var identity)||
            player.Buffs==null)return false;
        var operation=Operation(admission);string receipt="rbCamperOwner_"+operation.ToString("N");
        if(player.Buffs.GetCustomVar(receipt)!=0f||!MatchesBefore(player,admission))return false;
        // Phase2 and original images are saved before the one native attempt.
        player.Buffs.SetCustomVar(receipt,2f,true);
        if(!SaveLocal(local,identity)||!current()||
            !RebirthCamperStationPlayerFileWitness.HasSaved(identity,admission,operation,false)||!MatchesBefore(player,admission))return false;
        if(!ApplyExact(local,admission,payment)||!current())return false;
        player.Buffs.SetCustomVar(receipt,1f,true);
        return SaveLocal(local,identity)&&current()&&
            RebirthCamperStationPlayerFileWitness.HasSaved(identity,admission,operation,true)&&
            payment.MatchesAfter(player.bag.ItemGrid.items,player.inventory.ItemGrid.items);
    }
    internal static bool HasSaved(EntityPlayer player,RebirthStationGridAdmission admission)
        =>player!=null&&admission!=null&&admission.IsPlayerPayment&&admission.IsPublicationAttempted&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&identity!=null&&
            admission.TryGetPlayerPayment(out var payment)&&identity.StorageKey==payment.OwnerKey&&identity.CanonicalId==payment.OwnerId&&
            RebirthCamperStationPlayerFileWitness.HasSaved(identity,admission,Operation(admission),true);
    internal static bool HasSavedReceipt(EntityPlayer player,RebirthStationGridAdmission admission)
        =>player!=null&&admission!=null&&admission.IsPlayerPaymentConfirmed&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&identity!=null&&
            admission.TryGetPlayerPayment(out var payment)&&identity.StorageKey==payment.OwnerKey&&identity.CanonicalId==payment.OwnerId&&
            RebirthCamperStationPlayerFileWitness.HasSavedReceipt(identity,admission,Operation(admission));
    internal static bool HasHistoricalSavedReceipt(int queuedOwner,RebirthStationGridAdmission admission)
    {
        if(!ThreadManager.IsMainThread()||admission==null||!admission.IsPlayerPaymentConfirmed||
            !admission.TryGetPlayerPayment(out var payment))return false;
        var persisted=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(queuedOwner);
        return RebirthStablePlayerIdentity.TryFromPersistentPlayer(persisted,out var identity)&&identity!=null&&
            identity.StorageKey==payment.OwnerKey&&identity.CanonicalId==payment.OwnerId&&
            RebirthCamperStationPlayerFileWitness.HasSavedReceipt(identity,admission,Operation(admission));
    }
    private static bool SaveLocal(EntityPlayerLocal player,RebirthStablePlayerIdentity identity)
    {
        var game=GameManager.Instance;
        if(game==null||!ReferenceEquals(game.World,player.world)||game.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
        game.SaveLocalPlayerData();
        return ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,player.world)&&
            game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId;
    }
    private static bool ApplyExact(EntityPlayerLocal player,RebirthStationGridAdmission admission,RebirthCamperStationPlayerPaymentImage payment)
    {
        try
        {
            payment.CopyImages(out var oldBag,out var newBag,out var oldBelt,out var newBelt);
            // Native callbacks may reenter gameplay; compare complete expected
            // custody after every setter, stopping on the first changed image.
            for(int area=0;area<2;area++)
            {
                var before=area==0?oldBag:oldBelt;var after=area==0?newBag:newBelt;
                for(int slot=0;slot<before.Length;slot++)
                {
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot]))continue;
                    if(!Current(player,admission,out _,out _)||!Same(player.bag.ItemGrid.items,oldBag)||!Same(player.inventory.ItemGrid.items,oldBelt))return false;
                    if(area==0)player.bag.SetSlot(slot,after[slot].Clone());else player.inventory.SetItem(slot,after[slot].Clone());
                    before[slot]=after[slot].Clone();
                    if(!Same(player.bag.ItemGrid.items,oldBag)||!Same(player.inventory.ItemGrid.items,oldBelt))return false;
                }
            }
            return Current(player,admission,out _,out _)&&payment.MatchesAfter(player.bag.ItemGrid.items,player.inventory.ItemGrid.items);
        }
        catch{return false;} // Uncertain phase2 retains custody; never refund/replay.
    }
    private static bool Same(ItemStack[] a,ItemStack[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
}
