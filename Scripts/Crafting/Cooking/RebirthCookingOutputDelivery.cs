using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// The existing batch marker retains original combined output and owner images.
// Native local setters and final native player file remain publication authority.
internal static class RebirthCookingOutputDelivery
{
    private const string Prefix="rebirth.cooking.delivery.";
    private const string Held="rbCookingTakeHeld";
    private const string Receipt="rbCookingTake_";
    private const int MaximumImage=1048576;
    private static bool installed;
    internal static bool IsInstalled=>installed;
    private sealed class Permit {internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal Func<bool> Current;internal bool Consumed;}
    private sealed class Attempt {internal bool Active,Awaiting;internal EntityPlayerLocal Player;internal World World;internal int Deaths;}
    [ThreadStatic] private static Permit permit;
    private static readonly ConditionalWeakTable<Recipe,Attempt> attempts=new ConditionalWeakTable<Recipe,Attempt>();
    private static readonly ConditionalWeakTable<EntityPlayerLocal,Attempt> owners=new ConditionalWeakTable<EntityPlayerLocal,Attempt>();
    internal static bool HasPrepared(Recipe recipe)=>recipe?.ingredients?.Count>0&&recipe.ingredients[0]?.itemValue?.HasMetadata(Prefix+"owner")==true;
    // A retry owns the original rich primary and every configured extra.
    // Recreating Preview here would create a new native primary ItemValue.
    internal static bool TryReadPreparedOutputs(Recipe recipe,out IReadOnlyList<ItemStack> outputs)
    {
        outputs=null;if(!HasPrepared(recipe))return false;
        try
        {
            var frozen=Read(recipe.ingredients[0].itemValue,"outputs");
            if(frozen.Length<1||frozen.Length>65||frozen.Any(item=>item==null||item.IsEmpty()||item.count<1))return false;
            outputs=Array.AsReadOnly(frozen);return true;
        }
        catch{return false;}
    }
    internal static string WorldKey(World world)
    {
        string text=world?.IsRemote()==true?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world?.Guid;
        return Guid.TryParse(text,out var id)&&id!=Guid.Empty?id.ToString("N"):null;
    }
    internal static bool HasOwnHold(EntityPlayerLocal player)
        =>player?.Buffs!=null&&player.IsSpawned()&&!player.IsDead()&&ReferenceEquals(player.world,GameManager.Instance?.World)&&
            ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&(player.Buffs.GetCustomVar(Held)==1f||
                owners.TryGetValue(player,out var attempt)&&attempt.Awaiting&&ReferenceEquals(attempt.World,player.world)&&attempt.Deaths==player.Died);
    internal static bool IsHeld(EntityPlayerLocal player)=>RebirthNpcTradeOwnerExchange.IsHeld(player)||RebirthCamperStationContentsDelivery.HoldsOwner(player)||RebirthConsumptionDurableDelivery.HoldsInventoryOwner(player)||RebirthRecyclerPlacedSettlement.HoldsOwner(player)||RebirthCookingMillingPublication.HoldsOwner(player)||HasOwnHold(player)||RebirthCookingOwnerDelta.IsHeld(player)||RebirthRemoteResourceOwnerAction.IsHeld(player);
    internal static bool Blocks(ItemStackGrid grid)
    {var player=GameManager.Instance?.World?.GetPrimaryPlayer();return IsHeld(player)&&(ReferenceEquals(grid,player.bag?.ItemGrid)||ReferenceEquals(grid,player.inventory?.ItemGrid));}
    internal static bool BlocksUnrelatedPersonalQueue(XUiC_RecipeStack card)
    {
        if(card==null||card.GetParentByType<XUiC_WorkstationWindowGroup>()!=null)return false;
        var player=card.xui?.playerUI?.entityPlayer;
        return player!=null&&(IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player));
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var entry=permit;
        if(entry==null)return RebirthNpcTradeOwnerExchange.Consume(grid,slot,value)||RebirthCamperStationContentsDelivery.ConsumeOwnerSlot(grid,slot,value)||RebirthConsumptionDurableDelivery.ConsumeInventorySlot(grid,slot,value)||RebirthRecyclerPlacedSettlement.ConsumeOwnerSlot(grid,slot,value)||RebirthCookingOwnerDelta.Consume(grid,slot,value);
        if(entry.Consumed||!ReferenceEquals(grid,entry.Grid)||entry.Slot!=slot||!ReferenceEquals(value,entry.Value)||entry.Current?.Invoke()!=true)return false;
        entry.Consumed=true;return true;
    }
    internal static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.cooking.owner.output.receipt.v1");
        try
        {
        foreach(var type in new[]{typeof(RebirthCookingDeliveryGridSlot),typeof(RebirthCookingDeliveryGridBulk),typeof(RebirthCookingDeliveryGridAdd),
            typeof(RebirthCookingDeliveryGridCount),typeof(RebirthCookingDeliveryBagAdd),typeof(RebirthCookingDeliveryBagPartial),
            typeof(RebirthCookingDeliveryHeldUse),typeof(RebirthCookingDeliveryActions),typeof(RebirthCookingDeliveryDecItem),
            typeof(RebirthCookingDeliverySlotGesture),typeof(RebirthCookingDeliveryCursorSet),typeof(RebirthCookingDeliveryCursorGesture),
            typeof(RebirthCookingDeliveryPersonalQueueUpdate),typeof(RebirthCookingDeliveryPersonalQueueCancel)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
            installed=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    private static string Image(ItemStack[] items)
    {var image=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));if(image.Length>MaximumImage)throw new InvalidOperationException("Cooking owner image exceeds native receipt bound.");return image;}
    private static ItemStack[] Read(ItemValue marker,string key)
    {
        if(!marker.TryGetMetadata(Prefix+key,out string image)||image.Length>MaximumImage)throw new InvalidOperationException("Cooking owner receipt image unavailable.");
        var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image));
        if(Image(items)!=image)throw new InvalidOperationException("Cooking owner image is not canonical.");return items;
    }
    private static bool Same(ItemStack[] a,ItemStack[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
    private static void Sparse(ItemStack[] before,ItemStack[] after)
    {for(int i=0;i<before.Length;i++)if(RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i])){before[i]=ItemStack.Empty.Clone();after[i]=ItemStack.Empty.Clone();}}
    private static bool Delta(ItemStack[] live,ItemStack[] before,ItemStack[] after,bool applied)
    {
        if(live==null||live.Length!=before.Length||before.Length!=after.Length)return false;
        for(int i=0;i<live.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i])&&
            !RebirthStationGridIngredients.IsSameStackSnapshot(live[i],applied?after[i]:before[i]))return false;
        return true;
    }
    private static bool Mixed(ItemStack[] live,ItemStack[] before,ItemStack[] after)
    {
        if(live==null||live.Length!=before.Length||live.Length!=after.Length)return false;
        for(int i=0;i<live.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i])&&
            !RebirthStationGridIngredients.IsSameStackSnapshot(live[i],before[i])&&!RebirthStationGridIngredients.IsSameStackSnapshot(live[i],after[i]))return false;
        return true;
    }
    private static bool Saved(RebirthStablePlayerIdentity identity,string receipt,float stage,ItemStack[] bag=null,ItemStack[] belt=null,ItemStack[] beforeBag=null,ItemStack[] beforeBelt=null)
    {
        if(!RebirthGearNativePlayerFile.TryRead(identity,out var data)||!SavedPhase(data,receipt,out float phase)||phase!=stage)return false;
        if(bag==null)return true;
        var actualBag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var actualBelt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
        if(beforeBag==null||beforeBelt==null)return false;
        return stage==1f?Mixed(actualBag,beforeBag,bag)&&Mixed(actualBelt,beforeBelt,belt):Delta(actualBag,beforeBag,bag,true)&&Delta(actualBelt,beforeBelt,belt,true);
    }
    private static bool SavedPhase(PlayerDataFile data,string receipt,out float phase)
    {
        phase=0f;if(data?.buffData==null||data.buffData.Length>MaximumImage)return false;
        using(var stream=new MemoryStream(data.buffData.ToArray()))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;
            for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key!=receipt)continue;if(found||float.IsNaN(value)||float.IsInfinity(value))return false;phase=value;found=true;}
            return stream.Position==stream.Length;
        }
    }
    internal static bool HasSavedOwner(RebirthStablePlayerIdentity identity,Recipe recipe,int phase)
    {
        try
        {
            var marker=recipe.ingredients[0].itemValue;
            if(!marker.TryGetMetadata("rebirth.cooking.queue.token",out string original)||!Guid.TryParse(original,out var token)||
                !marker.TryGetMetadata(Prefix+"owner",out string owner)||owner!=identity.CanonicalId)return false;
            string receipt=Receipt+token.ToString("N");
            if(phase==3)return Saved(identity,receipt,3f);
            var bag=Read(marker,"afterBag");var belt=Read(marker,"afterBelt");
            return (phase==1||phase==2)&&Saved(identity,receipt,phase,bag,belt,Read(marker,"beforeBag"),Read(marker,"beforeBelt"));
        }
        catch{return false;}
    }
    internal static bool HasSavedRetirement(RebirthStablePlayerIdentity identity,Guid batch)
        =>batch!=Guid.Empty&&Saved(identity,Receipt+batch.ToString("N"),3f);
    internal static bool TryGetOriginal(Recipe recipe,out Guid token,out string creation)
    {
        token=Guid.Empty;creation=null;var marker=recipe?.ingredients?.FirstOrDefault()?.itemValue;
        return marker!=null&&marker.TryGetMetadata("rebirth.cooking.queue.token",out string original)&&Guid.TryParse(original,out token)&&token!=Guid.Empty&&
            marker.TryGetMetadata(Prefix+"creation",out creation);
    }
    internal static bool TryGetBatch(Recipe recipe,out Guid token)
    {token=Guid.Empty;return recipe?.ingredients?.Count>0&&recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string text)&&Guid.TryParse(text,out token)&&token!=Guid.Empty;}
    private static bool[] Locks(PlayerDataFile saved,bool bag)
    {
        var blob=bag?saved.bagData:saved.inventoryData;
        using(var stream=new MemoryStream(blob.ToArray()))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);if(reader.ReadByte()!=(bag?2:1))throw new InvalidOperationException("Unknown native owner grid version.");
            var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);if(!bag)reader.ReadByte();
            if(stream.Position!=stream.Length)throw new InvalidOperationException("Native owner locks do not match its grid.");
            return RebirthCamperStationNativeActorPayment.Locks(grid.SlotLocks);
        }
    }
    // Host derives the mobile marker from the original authoritative cooking row
    // and authenticated native final player file, never client request images.
    internal static bool PrepareSavedOwner(EntityPlayer player,RebirthStablePlayerIdentity identity,Recipe recipe)
    {
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false||identity==null||!TryGetBatch(recipe,out var token)||
            !RebirthGearNativePlayerFile.TryRead(identity,out var saved))return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player),world=WorldKey(player.world);
        if(string.IsNullOrEmpty(creation)||world==null)return false;
        var marker=recipe.ingredients[0].itemValue;
        if(HasPrepared(recipe))
        {
            if(!marker.TryGetMetadata(Prefix+"owner",out string owner)||owner!=identity.CanonicalId||
                !marker.TryGetMetadata(Prefix+"creation",out string original)||original!=creation)return false;
            if(HasSavedOwner(identity,recipe,1)||HasSavedOwner(identity,recipe,2)||HasSavedOwner(identity,recipe,3))return true;
            // An attempted but unrecognized image is UNKNOWN, never a fresh
            // destination planning request after partial owner publication.
            if(!SavedPhase(saved,Receipt+token.ToString("N"),out float originalPhase)||originalPhase!=0f)return false;
        }
        else if(!SavedPhase(saved,Receipt+token.ToString("N"),out float freshPhase)||freshPhase!=0f)return false;
        var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
        if(bag==null||belt==null)return false;
        // A stage-zero remote marker is a projection, not authority over owner
        // destinations or rewards. Derive both from this native row and file.
        if(!RebirthCookingHeat.TryCombinedOutput(recipe,recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.portions",out int portions)?portions:0,out var outputs))return false;
        if(!RebirthRecyclerOwnerDelivery.Plan(bag,belt,Locks(saved,true),Locks(saved,false),RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),outputs.ToList(),out var afterBag,out var afterBelt))return false;
        Sparse(bag,afterBag);Sparse(belt,afterBelt);
        if(!HasPrepared(recipe)&&marker.Metadata?.Count>byte.MaxValue-10)return false;
        string a=Image(bag),b=Image(belt),c=Image(afterBag),d=Image(afterBelt),o=Image(outputs.Select(item=>item.Clone()).ToArray());
        marker.SetMetadata(Prefix+"beforeBag",a);marker.SetMetadata(Prefix+"beforeBelt",b);marker.SetMetadata(Prefix+"afterBag",c);marker.SetMetadata(Prefix+"afterBelt",d);
        marker.SetMetadata(Prefix+"outputs",o);marker.SetMetadata(Prefix+"world",world);marker.SetMetadata(Prefix+"creation",creation);marker.SetMetadata(Prefix+"deaths",saved.deaths);marker.SetMetadata(Prefix+"owner",identity.CanonicalId);
        return true;
    }
    internal static bool AdoptMarker(XUiC_RecipeStack card,Guid batch,string image)
    {
        if(card?.recipe==null||!TryGetBatch(card.recipe,out var original)||original!=batch||!RebirthNativeItemCodec.TryDecode(image,out var marker)||
            marker?.type!=card.recipe.ingredients[0].itemValue.type||!marker.TryGetMetadata("rebirth.cooking.queue.token",out string token)||!Guid.TryParse(token,out var received)||received!=batch)return false;
        if(card.xui.playerUI.entityPlayer.Buffs.GetCustomVar(Receipt+batch.ToString("N"))!=0f)return false;
        var copy=new Dictionary<string,TypedMetadataValue>();foreach(var pair in marker.Metadata)copy.Add(pair.Key,pair.Value.Clone());
        card.recipe.ingredients[0].itemValue.Metadata=copy;return true;
    }
    internal static void ObserveRetiredOwner(Recipe recipe,EntityPlayerLocal player)
    {
        if(player==null||!TryGetOriginal(recipe,out var token,out var creation)||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation||
            player.Buffs?.GetCustomVar(Receipt+token.ToString("N"))!=3f)return;
        if(attempts.TryGetValue(recipe,out var attempt)&&ReferenceEquals(attempt.Player,player)&&ReferenceEquals(attempt.World,player.world)&&attempt.Deaths==player.Died)
            attempt.Awaiting=false;
    }
    internal static bool TryDeliver(XUiC_RecipeStack card)
    {
        var recipe=card?.recipe;var player=card?.xui?.playerUI?.entityPlayer;
        var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(!HasPrepared(recipe))return false;
        RebirthCookingOutputPlanPublication.Install();
        if(!installed||player?.Buffs==null||window?.WorkstationData?.TileEntity==null||
            !ThreadManager.IsMainThread()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        var attempt=attempts.GetValue(recipe,_=>new Attempt());if(attempt.Active)return false;attempt.Active=true;
        try
        {
            string creation=RebirthCamperCharacterAccess.CurrentCreation(player);if(string.IsNullOrEmpty(creation))return false;
            string directory=GameIO.GetSaveGameDir(),worldGuid=WorldKey(player.world);
            if(!Guid.TryParse(worldGuid,out var worldId)||worldId==Guid.Empty)return false;
            var world=player.world;var bagGrid=player.bag?.ItemGrid;var beltGrid=player.inventory?.ItemGrid;
            if(bagGrid==null||beltGrid==null||card.xui.DragAndDropWindow?.IsEmpty()!=true||
                RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||
                RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory))return false;
            var marker=recipe.ingredients[0].itemValue;
            if(!marker.TryGetMetadata("rebirth.cooking.queue.token",out string original)||!Guid.TryParse(original,out var token)||token==Guid.Empty)return false;
            string receipt=Receipt+token.ToString("N");int deaths=player.Died;
            if(player.Buffs.GetCustomVar(receipt)==0f&&!attempt.Awaiting&&RebirthDogLifecycleService.IsHireInventoryHeld(player))return false;
            Func<bool> current=()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&
                ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&player.Died==deaths&&
                ReferenceEquals(card.recipe,recipe)&&ReferenceEquals(player.bag.ItemGrid,bagGrid)&&ReferenceEquals(player.inventory.ItemGrid,beltGrid)&&
                window.windowGroup?.isShowing==true&&!RebirthGearOwnerReservation.IsHeld(player)&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
                !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&
                !RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.inventory)&&
                GameIO.GetSaveGameDir()==directory&&WorldKey(player.world)==worldGuid&&
                RebirthCamperCharacterAccess.CurrentCreation(player)==creation;
            if(!current())return false;
            if(!marker.TryGetMetadata(Prefix+"owner",out string owner)||owner!=identity.CanonicalId||
                !marker.TryGetMetadata(Prefix+"world",out string originalWorld)||originalWorld!=worldGuid||
                !marker.TryGetMetadata(Prefix+"creation",out string originalCreation)||originalCreation!=creation||
                !marker.TryGetMetadata(Prefix+"deaths",out int originalDeaths)||originalDeaths!=deaths||!current())return false;
            var beforeBagImage=Read(marker,"beforeBag");var beforeBeltImage=Read(marker,"beforeBelt");
            var afterBagImage=Read(marker,"afterBag");var afterBeltImage=Read(marker,"afterBelt");
            float phase=player.Buffs.GetCustomVar(receipt);
            if(phase==3f)
            {
                if(player.Buffs.GetCustomVar(Held)!=0f||!Saved(identity,receipt,3f))
                {player.Buffs.SetCustomVar(Held,0f,true);GameManager.Instance.SaveLocalPlayerData();if(!current()||!Saved(identity,receipt,3f))return false;}
                if(!RebirthCookingOutputPlanPublication.Observe(card,recipe,3))return false;
                attempt.Awaiting=false;return current();
            }
            if(phase!=0f&&phase!=1f&&phase!=2f)return false;
            if(phase==0f&&(!Delta(bagGrid.items,beforeBagImage,afterBagImage,false)||!Delta(beltGrid.items,beforeBeltImage,afterBeltImage,false)))
            {
                // Before an attempt only, retain the original output parcel and
                // replan changed owner destinations; no delivery has begun.
                var frozen=Read(marker,"outputs");
                beforeBagImage=ItemStack.Clone(bagGrid.items);beforeBeltImage=ItemStack.Clone(beltGrid.items);
                if(!RebirthRecyclerOwnerDelivery.Plan(beforeBagImage,beforeBeltImage,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),
                    RebirthCamperStationNativeActorPayment.Locks(beltGrid.SlotLocks),RebirthToolbeltCapacity.GetOwnedSlotCount(player,beltGrid.Length),frozen,out afterBagImage,out afterBeltImage))return false;
                Sparse(beforeBagImage,afterBagImage);Sparse(beforeBeltImage,afterBeltImage);
                string a=Image(beforeBagImage),b=Image(beforeBeltImage),c=Image(afterBagImage),d=Image(afterBeltImage);
                marker.SetMetadata(Prefix+"beforeBag",a);marker.SetMetadata(Prefix+"beforeBelt",b);marker.SetMetadata(Prefix+"afterBag",c);marker.SetMetadata(Prefix+"afterBelt",d);window.syncTEfromUI();
            }
            if(phase==0f&&(!RebirthCookingOutputPlanPublication.Observe(card,recipe,0)||!current()||!Delta(bagGrid.items,beforeBagImage,afterBagImage,false)||!Delta(beltGrid.items,beforeBeltImage,afterBeltImage,false)))return false;
            player.Buffs.SetCustomVar(Held,1f,true);
            attempt.Awaiting=true;attempt.Player=player;attempt.World=world;attempt.Deaths=deaths;
            owners.Remove(player);owners.Add(player,attempt);
            if(phase==0f){player.Buffs.SetCustomVar(receipt,1f,true);GameManager.Instance.SaveLocalPlayerData();phase=1f;}
            if(!current())return false;
            if(phase==1f)
            {
                // Loaded pending receipts already hold native mutations. Only
                // exact before or exact original after images can resume.
                if(!Mixed(bagGrid.items,beforeBagImage,afterBagImage)||!Mixed(beltGrid.items,beforeBeltImage,afterBeltImage)||
                    !Saved(identity,receipt,1f,afterBagImage,afterBeltImage,beforeBagImage,beforeBeltImage))return false;
                if(!RebirthCookingOutputPlanPublication.Observe(card,recipe,1))return false;
                Apply(player,bagGrid,beforeBagImage,afterBagImage,true,current);
                Apply(player,beltGrid,beforeBeltImage,afterBeltImage,false,current);
                if(!current()||!Delta(bagGrid.items,beforeBagImage,afterBagImage,true)||!Delta(beltGrid.items,beforeBeltImage,afterBeltImage,true))return false;
                player.Buffs.SetCustomVar(receipt,2f,true);GameManager.Instance.SaveLocalPlayerData();
            }
            if(!current()||!Saved(identity,receipt,2f,afterBagImage,afterBeltImage,beforeBagImage,beforeBeltImage))return false;
            if(!RebirthCookingOutputPlanPublication.Observe(card,recipe,2))return false;
            // Phase3 is reachable only after a final-file phase2 exact postimage.
            // It remains a terminal dedup receipt after queue removal/item use.
            player.Buffs.SetCustomVar(receipt,3f,true);player.Buffs.SetCustomVar(Held,0f,true);GameManager.Instance.SaveLocalPlayerData();
            if(!current()||!Saved(identity,receipt,3f))return false;
            if(!RebirthCookingOutputPlanPublication.Observe(card,recipe,3))return false;
            attempt.Awaiting=false;
            return true;
        }
        catch(Exception error){Log.Warning("[Cooking] Original output receipt retained for reconciliation: "+error.Message);return false;}
        finally{attempt.Active=false;}
    }
    // Read-only terminal handoff for the owning native queue retirement. This
    // cannot authorize another inventory application or manufacture phase3.
    internal static bool TryReadSavedTakeProof(XUiC_RecipeStack card,out System.Xml.Linq.XElement proof)
    {
        proof=null;
        try
        {
        var player=card?.xui?.playerUI?.entityPlayer;var recipe=card?.recipe;
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false||player.IsDead()||!HasPrepared(recipe)||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        var marker=recipe.ingredients[0].itemValue;string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(!marker.TryGetMetadata("rebirth.cooking.queue.token",out string text)||!Guid.TryParse(text,out var token)||token==Guid.Empty||
            !marker.TryGetMetadata(Prefix+"owner",out string owner)||owner!=identity.CanonicalId||
            !marker.TryGetMetadata(Prefix+"creation",out string savedCreation)||savedCreation!=creation||
            !marker.TryGetMetadata(Prefix+"deaths",out int deaths)||deaths!=player.Died||
            !marker.TryGetMetadata(Prefix+"world",out string world)||world!=WorldKey(player.world)||
            !RebirthGearNativePlayerFile.TryRead(identity,out var data)||data.deaths!=deaths||
            !SavedPhase(data,Receipt+token.ToString("N"),out float phase)||phase!=3f||
            player.Buffs.GetCustomVar(Receipt+token.ToString("N"))!=3f||player.Buffs.GetCustomVar(Held)!=0f||
            !TryReadPreparedOutputs(recipe,out var outputs))return false;
        byte[] native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
        if(native==null||native.Length<6||native.Length>16*1024*1024)return false;
        proof=new System.Xml.Linq.XElement("cookingTakenOwnerSaved",new System.Xml.Linq.XAttribute("batch",token.ToString("N")),
            new System.Xml.Linq.XAttribute("owner",identity.StorageKey),new System.Xml.Linq.XAttribute("creation",creation),
            new System.Xml.Linq.XAttribute("deaths",deaths),new System.Xml.Linq.XAttribute("world",world),new System.Xml.Linq.XAttribute("phase",3),
            new System.Xml.Linq.XAttribute("nativeDigest",RebirthStationPublicationRecord.Digest(native)),new System.Xml.Linq.XAttribute("nativeLength",native.Length),
            new System.Xml.Linq.XElement("combinedOutputs",Image(outputs.Select(value=>value.Clone()).ToArray())));
        return true;
        }
        catch{proof=null;return false;}
    }
    private static void Apply(EntityPlayerLocal player,ItemStackGrid grid,ItemStack[] before,ItemStack[] after,bool bag,Func<bool> current)
    {
        if(grid.Length!=before.Length||before.Length!=after.Length)throw new InvalidOperationException("Cooking owner dimensions changed.");
        for(int slot=0;slot<after.Length;slot++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot]))continue;
            if(!current()||!Mixed(grid.items,before,after))throw new InvalidOperationException("Cooking original owner image changed.");
            if(RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[slot],after[slot]))continue;
            if(grid.IsLocked(slot))throw new InvalidOperationException("Cooking original destination became locked.");
            var value=after[slot].Clone();var entry=new Permit{Grid=grid,Slot=slot,Value=value,Current=current};
            if(permit!=null)throw new InvalidOperationException("Nested cooking owner publication.");
            permit=entry;
            try{if(bag)player.bag.SetSlot(slot,value);else player.inventory.SetStackAt(slot,value);}
            finally{permit=null;}
            if(!entry.Consumed||!current()||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[slot],after[slot]))
                throw new InvalidOperationException("Cooking native destination did not publish its exact original postimage.");
        }
    }
}

[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
internal static class RebirthCookingDeliveryGridSlot
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack)
        =>!RebirthCookingOutputDelivery.Blocks(__instance)||RebirthCookingOutputDelivery.Consume(__instance,_index,_stack);
    private static void Postfix(ItemStackGrid __instance,int _index,ItemStack _stack,bool __runOriginal)
        =>RebirthRecyclerPlacedSettlement.ObserveOwnerSlot(__instance,_index,_stack,__runOriginal);
}
[HarmonyPatch]
internal static class RebirthCookingDeliveryGridBulk
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","ReadInto","SetSlotLocks","SetLocked"})yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance)=>!RebirthCookingOutputDelivery.Blocks(__instance);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
internal static class RebirthCookingDeliveryGridAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthCookingOutputDelivery.Blocks(__instance))return true;__result=-1;return false;}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
internal static class RebirthCookingDeliveryGridCount
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthCookingOutputDelivery.Blocks(__instance))return true;__result=0;return false;}
}
[HarmonyPatch]
internal static class RebirthCookingDeliveryDecItem
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(Bag),nameof(Bag.DecItem));yield return AccessTools.Method(typeof(Inventory),nameof(Inventory.DecItem));}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,ref int __result)
    {
        var grid=__instance is Bag bag?bag.ItemGrid:(__instance as Inventory)?.ItemGrid;
        if(!RebirthCookingOutputDelivery.Blocks(grid))return true;__result=0;return false;
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
internal static class RebirthCookingDeliveryBagAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref bool __result){if(!RebirthCookingOutputDelivery.Blocks(__instance.ItemGrid))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
internal static class RebirthCookingDeliveryBagPartial
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!RebirthCookingOutputDelivery.Blocks(__instance.ItemGrid))return true;__result=(false,false);return false;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class RebirthCookingDeliveryHeldUse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityAlive __instance,ref bool __result){if(!RebirthCookingOutputDelivery.IsHeld(__instance as EntityPlayerLocal))return true;__result=false;return false;}
}
[HarmonyPatch]
internal static class RebirthCookingDeliveryActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes)??throw new MissingMethodException(type.FullName,"OnActivated");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BaseItemActionEntry __instance)=>RebirthRecyclerPlacedSettlement.CanUseNativeEntry(__instance)&&
        (!RebirthCookingOutputDelivery.IsHeld(__instance?.ItemController?.xui?.playerUI?.entityPlayer)||RebirthRemoteResourceOwnerAction.AllowOriginalEntry(__instance));
}

// Fence the gesture before its first cursor/native-slot side effect. Guarding only
// the eventual bag setter is too late: native SwapItem writes the cursor first.
[HarmonyPatch]
internal static class RebirthCookingDeliverySlotGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance)
        =>!RebirthCookingOutputDelivery.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack))]
internal static class RebirthCookingDeliveryCursorSet
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance,ItemStack stack)
        =>!RebirthCookingOutputDelivery.IsHeld(__instance?.xui?.playerUI?.entityPlayer)||RebirthCamperStationContentsDelivery.AllowOwnerCursor(__instance,stack)||RebirthCookingGridCustody.AllowCursor(__instance,stack);
}
[HarmonyPatch]
internal static class RebirthCookingDeliveryCursorGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(XUiC_DragAndDropWindow)))
            if(method.Name=="DropCurrentItem"||method.Name=="PlaceItemBackInInventory")yield return method;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance)
        =>!RebirthCookingOutputDelivery.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}

// Native personal completion/cancellation must pause before it constructs an
// unrelated output/refund for an exclusively held native owner. Station cards
// retain their existing managed processing and original Take handlers.
[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.Update))]
internal static class RebirthCookingDeliveryPersonalQueueUpdate
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance)=>!RebirthCookingOutputDelivery.BlocksUnrelatedPersonalQueue(__instance);
}
[HarmonyPatch(typeof(XUiC_RecipeStack), "HandleOnPress")]
internal static class RebirthCookingDeliveryPersonalQueueCancel
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance)=>!RebirthCookingOutputDelivery.BlocksUnrelatedPersonalQueue(__instance);
}
