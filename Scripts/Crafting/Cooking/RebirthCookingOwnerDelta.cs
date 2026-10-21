using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Domain original receipt for an authenticated saved native owner delta.
// Cooking's existing grid/action guards delegate to this one-use permit.
internal static partial class RebirthCookingOwnerDelta
{
    internal sealed class Plan
    {
        internal Guid Operation;internal string World,Creation,Owner,SourceHash;
        internal int Deaths=-1;
        internal ItemStack[] BeforeBag,AfterBag,BeforeBelt,AfterBelt;
        internal ItemStack[] Overflow;
        internal int OverflowOwnerEntity=-1;
        internal string Receipt=>"rbCookingGridReturn_"+Operation.ToString("N");
    }
    private sealed class Attempt
    {internal Plan Plan;internal string Image,Directory;internal EntityPlayerLocal Player;internal World World;internal ItemStackGrid Bag,Belt;internal Func<bool> Current;internal int Deaths;}
    private sealed class Permit{internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;internal Func<bool> Current;}
    private static Permit permit;
    private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
    internal static bool HasOwnHold(EntityPlayerLocal player)=>player!=null&&player.IsSpawned()&&!player.IsDead()&&Attempts.Values.Any(attempt=>ReferenceEquals(attempt.Player,player)&&ReferenceEquals(attempt.World,player.world)&&attempt.Deaths==player.Died);
    internal static bool IsHeld(EntityPlayerLocal player)=>HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player);
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {var current=permit;if(current==null)return RebirthCookingGridCustody.Consume(grid,slot,value);if(current.Used||!ReferenceEquals(current.Grid,grid)||current.Slot!=slot||!ReferenceEquals(current.Value,value)||!current.Current())return false;current.Used=true;return true;}
    private static string Cells(ItemStack[] values)
    {if(values==null)throw new InvalidDataException();return Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(values));}
    internal static string Encode(Plan plan)
    {
        if(plan.Deaths < -1||plan.Overflow!=null&&(plan.Deaths<0||plan.OverflowOwnerEntity<0||plan.Overflow.Length<1||plan.Overflow.Length>byte.MaxValue||plan.Overflow.Any(item=>item==null||item.IsEmpty()||item.count<1)))throw new InvalidDataException();
        var root=new XElement("cookingOwnerDelta",new XAttribute("version",plan.Overflow!=null?3:plan.Deaths<0?1:2),new XAttribute("operation",plan.Operation.ToString("N")),
            new XAttribute("world",plan.World),new XAttribute("creation",plan.Creation),new XAttribute("owner",plan.Owner),new XAttribute("source",plan.SourceHash),
            new XElement("beforeBag",Cells(plan.BeforeBag)),new XElement("afterBag",Cells(plan.AfterBag)),
            new XElement("beforeBelt",Cells(plan.BeforeBelt)),new XElement("afterBelt",Cells(plan.AfterBelt)));
        if(plan.Deaths>=0)root.Add(new XAttribute("deaths",plan.Deaths));
        if(plan.Overflow!=null){root.Add(new XAttribute("overflowEntity",plan.OverflowOwnerEntity));root.Add(new XElement("overflow",Cells(plan.Overflow)));}
        string image=root.ToString(SaveOptions.DisableFormatting);
        if(image.Length>4*1024*1024)throw new InvalidDataException("Cooking owner delta exceeds bound.");return image;
    }
    internal static Plan Decode(string image)
    {
        if(image==null||image.Length>4*1024*1024)throw new InvalidDataException();var root=XElement.Parse(image);
        int? version=(int?)root.Attribute("version");
        if(root.Name!="cookingOwnerDelta"||(version!=1&&version!=2&&version!=3)||version>=2&&((int?)root.Attribute("deaths") is not int deaths||deaths<0))throw new InvalidDataException();
        ItemStack[] Read(string name,int maximum)
        {var values=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element(name).Value));if(values==null||values.Length>maximum)throw new InvalidDataException();return values;}
        var result=new Plan{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),World=(string)root.Attribute("world"),Creation=(string)root.Attribute("creation"),
            Owner=(string)root.Attribute("owner"),SourceHash=(string)root.Attribute("source"),Deaths=version>=2?(int)root.Attribute("deaths"):-1,
            BeforeBag=Read("beforeBag",169),AfterBag=Read("afterBag",169),BeforeBelt=Read("beforeBelt",20),AfterBelt=Read("afterBelt",20),Overflow=version==3?Read("overflow",byte.MaxValue):null,
            OverflowOwnerEntity=version==3?(int)root.Attribute("overflowEntity"):-1};
        if(result.Operation==Guid.Empty||!Guid.TryParse(result.World,out _)||!RebirthSurvivorRequestScope.TryNormalize(result.Creation,out _)||
            string.IsNullOrEmpty(result.Owner)||result.Owner.Length>512||result.SourceHash?.Length!=64||
            result.BeforeBag.Length!=result.AfterBag.Length||result.BeforeBelt.Length!=result.AfterBelt.Length||Encode(result)!=image)throw new InvalidDataException();return result;
    }
    private static bool Delta(ItemStack[] live,ItemStack[] before,ItemStack[] after,bool applied,bool mixed=false)
    {
        if(live==null||live.Length!=before.Length||before.Length!=after.Length)return false;
        for(int i=0;i<live.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i])&&
            !RebirthStationGridIngredients.IsSameStackSnapshot(live[i],applied?after[i]:before[i])&&
            (!mixed||!RebirthStationGridIngredients.IsSameStackSnapshot(live[i],applied?before[i]:after[i])))return false;return true;
    }
    internal static bool Saved(RebirthStablePlayerIdentity identity,Plan plan,int expectedPhase)
    {
        if(expectedPhase<1||expectedPhase>4||identity==null||plan==null||plan.Deaths<0||identity.CanonicalId!=plan.Owner||!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.deaths!=plan.Deaths||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out bool present)||!present||phase!=expectedPhase)return false;
        // Three is saved retirement after positively saved credit; four is
        // saved no-effect acknowledgement. Later unrelated inventory movement
        // cannot revoke either terminal original receipt.
        if(expectedPhase==3||expectedPhase==4)return true;
        bool applied=expectedPhase==2||expectedPhase==3;
        return Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,true),plan.BeforeBag,plan.AfterBag,applied,expectedPhase==1)&&
            Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,false),plan.BeforeBelt,plan.AfterBelt,applied,expectedPhase==1);
    }
    // Positive no-start evidence and replacement source must come from one
    // authenticated native file. A failed phase reader is never phase zero.
    internal static bool TryReadUnspentSource(RebirthStablePlayerIdentity identity,Plan plan,out ItemStack[] bag,out ItemStack[] belt,out bool[] locks)
    {
        bag=null;belt=null;locks=null;
        try
        {
            return identity!=null&&plan!=null&&plan.Deaths>=0&&identity.CanonicalId==plan.Owner&&
                RebirthGearNativePlayerFile.TryRead(identity,out var saved)&&saved.deaths==plan.Deaths&&
                RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out bool present)&&(!present||phase==0f)&&
                RebirthCamperStationPlayerSavedSource.TryCopySavedSource(saved,out bag,out belt,out locks);
        }
        catch{bag=null;belt=null;locks=null;return false;}
    }
    private static bool Current(Attempt attempt)
    {
        var player=attempt.Player;
        return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,attempt.World)&&ReferenceEquals(attempt.World.GetPrimaryPlayer(),player)&&
            ReferenceEquals(attempt.World.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&attempt.Deaths==player.Died&&attempt.Plan.Deaths==attempt.Deaths&&GameIO.GetSaveGameDir()==attempt.Directory&&
            player.world.worldState?.Guid==attempt.Plan.World&&RebirthCamperCharacterAccess.CurrentCreation(player)==attempt.Plan.Creation&&
            ReferenceEquals(player.bag.ItemGrid,attempt.Bag)&&ReferenceEquals(player.inventory.ItemGrid,attempt.Belt)&&attempt.Current()&&
            !RebirthCookingOutputDelivery.HasOwnHold(player)&&!RebirthRemoteResourceOwnerAction.IsHeld(player)&&!AdvancedFarmingHarvestOwnerDelta.IsHeld(player)&&!AdvancedFarmingWaterExchangeOriginal.IsHeld(player)&&!RebirthGearOwnerReservation.IsHeld(player)&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
            !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&
            !RebirthCamperStationNativeActorPayment.Blocks(player.inventory)&&!RebirthCamperEnergyOwnerHold.Blocks(player.bag);
    }
    internal static bool Advance(EntityPlayerLocal player,Plan plan,int serverPhase,Func<bool> authenticatedCurrent,Func<bool> sourceCleared)
    {
        if(player==null||plan==null||plan.Deaths<0||plan.Deaths!=player.Died||authenticatedCurrent==null||sourceCleared==null||!authenticatedCurrent()||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=plan.Owner)return false;
        string image=Encode(plan);
        if(Attempts.TryGetValue(plan.Operation,out var stale)&&!ReferenceEquals(stale.World,GameManager.Instance?.World))Attempts.Remove(plan.Operation);
        if(!Attempts.TryGetValue(plan.Operation,out var attempt))
        {
            if(IsHeld(player)||RebirthDogLifecycleService.IsHireInventoryHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthRemoteResourceOwnerAction.IsHeld(player)||RebirthCookingOutputDelivery.HasOwnHold(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player))return false;
            if(player.Buffs.GetCustomVar(plan.Receipt)==0f&&
                (RebirthCookingRemoteAdmission.SourceHash(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length)!=plan.SourceHash||
                !Delta(player.bag.ItemGrid.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(player.inventory.ItemGrid.items,plan.BeforeBelt,plan.AfterBelt,false)))return false;
            attempt=new Attempt{Plan=plan,Image=image,Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Current=authenticatedCurrent,Deaths=player.Died};
            Attempts.Add(plan.Operation,attempt);
        }
        if(attempt.Image!=image||!ReferenceEquals(attempt.Player,player))return false;attempt.Current=authenticatedCurrent;
        if(!Current(attempt))return false;float phase=player.Buffs.GetCustomVar(plan.Receipt);
        if(phase==0f)
        {
            if(RebirthCookingRemoteAdmission.SourceHash(attempt.Bag.items,attempt.Belt.items,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length)!=plan.SourceHash||
                !Delta(attempt.Bag.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(attempt.Belt.items,plan.BeforeBelt,plan.AfterBelt,false))return false;
            player.Buffs.SetCustomVar(plan.Receipt,1f,true);GameManager.Instance.SaveLocalPlayerData();return false;
        }
        if(phase==3f)return Saved(identity,plan,3);
        if(phase!=1f&&phase!=2f)return false;
        if(phase==1f)
        {
            if(serverPhase<1||!Saved(identity,plan,1)||!Current(attempt))return false;
            for(int area=0;area<2;area++)
            {
                var grid=area==0?attempt.Bag:attempt.Belt;var before=area==0?plan.BeforeBag:plan.BeforeBelt;var after=area==0?plan.AfterBag:plan.AfterBelt;
                for(int i=0;i<after.Length;i++)
                {
                    if(!Current(attempt)||!Delta(grid.items,before,after,false,true))return false;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i])||RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],after[i]))continue;
                    if(grid.IsLocked(i)||permit!=null)return false;
                    var value=after[i].Clone();var single=new Permit{Grid=grid,Slot=i,Value=value,Current=()=>Current(attempt)};permit=single;
                    try{if(area==0)player.bag.SetSlot(i,value);else player.inventory.SetStackAt(i,value);}finally{permit=null;}
                    if(!single.Used||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],after[i]))return false;
                }
            }
            if(!Current(attempt)||!Delta(attempt.Bag.items,plan.BeforeBag,plan.AfterBag,true)||!Delta(attempt.Belt.items,plan.BeforeBelt,plan.AfterBelt,true)||!sourceCleared())return false;
            player.Buffs.SetCustomVar(plan.Receipt,2f,true);GameManager.Instance.SaveLocalPlayerData();
        }
        if(!Saved(identity,plan,2))GameManager.Instance.SaveLocalPlayerData();return Current(attempt)&&Saved(identity,plan,2);
    }
    internal static bool Retire(EntityPlayerLocal player,Plan plan)
    {
        if(!Attempts.TryGetValue(plan.Operation,out var attempt)||!ReferenceEquals(attempt.Player,player)||!Current(attempt)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        if(Saved(identity,plan,3)){Attempts.Remove(plan.Operation);return true;}
        if(!Saved(identity,plan,2))return false;
        player.Buffs.SetCustomVar(plan.Receipt,3f,true);GameManager.Instance.SaveLocalPlayerData();
        if(!Saved(identity,plan,3))return false;Attempts.Remove(plan.Operation);return true;
    }
    internal static bool AbandonRejectedWithoutEffect(EntityPlayerLocal player,Plan plan)
    {
        if(!ThreadManager.IsMainThread()||player?.world==null||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !player.IsSpawned()||player.IsDead()||plan==null||plan.Deaths<0||plan.Deaths!=player.Died||plan.World!=player.world.worldState.Guid||plan.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=plan.Owner)return false;
        if(!Attempts.TryGetValue(plan.Operation,out var attempt))
        {
            // Only an authenticated retained rejection reaches this method. Phase four
            // is the saved no-effect acknowledgement, so a lost reply is idempotent.
            float phase=player.Buffs.GetCustomVar(plan.Receipt);if(phase==4f)return Saved(identity,plan,4);if(phase!=0f)return false;
            player.Buffs.SetCustomVar(plan.Receipt,4f,true);GameManager.Instance.SaveLocalPlayerData();return Saved(identity,plan,4);
        }
        if(!ReferenceEquals(attempt.Player,player)||!Current(attempt))return false;
        if(player.Buffs.GetCustomVar(plan.Receipt)==4f)
        {if(!Saved(identity,plan,4))return false;Attempts.Remove(plan.Operation);return true;}
        if(player.Buffs.GetCustomVar(plan.Receipt)!=1f||
            !Delta(player.bag.ItemGrid.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(player.inventory.ItemGrid.items,plan.BeforeBelt,plan.AfterBelt,false))return false;
        player.Buffs.SetCustomVar(plan.Receipt,4f,true);GameManager.Instance.SaveLocalPlayerData();
        if(!Saved(identity,plan,4))return false;Attempts.Remove(plan.Operation);return true;
    }
    internal static bool HasPending(EntityPlayerLocal player)=>IsHeld(player);
}
