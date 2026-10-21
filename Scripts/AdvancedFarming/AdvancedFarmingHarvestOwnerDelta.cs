using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using System.Reflection;
using System.Xml;

// Farming-only original positive output receipt. No farming producer is wired
// until source reservation and saved native publication are complete.
internal static partial class AdvancedFarmingHarvestOwnerDelta
{
    internal sealed class Plan
    {
        internal Guid Operation;internal string World,Creation,Owner,SourceHash;internal int Deaths;
        internal ItemStack[] BeforeBag,AfterBag,BeforeBelt,AfterBelt;
        internal string Receipt=>"rbFarmingHarvest_"+Operation.ToString("N");
    }
    private sealed class Attempt
    {internal Plan Plan;internal string Image,Directory;internal EntityPlayerLocal Player;internal World World;internal ItemStackGrid Bag,Belt;internal Func<bool> Current;internal int Deaths;internal Permit PendingCell;}
    private sealed class Permit{internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used,Observed;internal Func<bool> Current;internal Attempt Owner;internal int Area,Invocation;internal string BagBefore,BeltBefore,BagAfter,BeltAfter,Cursor;internal IDisposable Memory;}
    private static Permit permit;
    private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
    private static void RequestNativeSave()
    {
        // A remote client's local final file is not the server's final file.
        // Keep native ownership: schedule the original NetPackagePlayerData path.
        GameManager.Instance.SaveLocalPlayerData();
        GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
    }
    internal static void Install(Harmony harmony)
    {
        foreach(var type in new[]{typeof(AdvancedFarmingHarvestDeliveryGridSlot),typeof(AdvancedFarmingHarvestDeliveryGridBulk),typeof(AdvancedFarmingHarvestDeliveryGridAdd),typeof(AdvancedFarmingHarvestDeliveryGridCount),typeof(AdvancedFarmingHarvestDeliveryDecItem),typeof(AdvancedFarmingHarvestDeliveryBagAdd),typeof(AdvancedFarmingHarvestDeliveryBagPartial),typeof(AdvancedFarmingHarvestDeliveryHeldUse),typeof(AdvancedFarmingHarvestDeliveryActions),typeof(AdvancedFarmingHarvestDeliverySlotGesture),typeof(AdvancedFarmingHarvestDeliveryCursorSet),typeof(AdvancedFarmingHarvestDeliveryCursorGesture)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
    }
    internal static bool HasOwnHold(EntityPlayerLocal player)=>player!=null&&player.IsSpawned()&&!player.IsDead()&&Attempts.Values.Any(attempt=>ReferenceEquals(attempt.Player,player)&&ReferenceEquals(attempt.World,GameManager.Instance?.World)&&ReferenceEquals(attempt.World,player.world)&&attempt.Plan.Deaths==player.Died);
    internal static bool IsHeld(EntityPlayerLocal player)=>HasOwnHold(player);
    internal static bool Blocks(ItemStackGrid grid)=>grid!=null&&Attempts.Values.Any(a=>HasOwnHold(a.Player)&&(ReferenceEquals(a.Bag,grid)||ReferenceEquals(a.Belt,grid)));
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {var current=permit;if(current==null)return false;if(current.Used||!ReferenceEquals(current.Grid,grid)||current.Slot!=slot||!ReferenceEquals(current.Value,value)||!current.Current())return false;current.Used=true;return true;}
    private static string Cells(ItemStack[] values)
    {if(values==null)throw new InvalidDataException();return Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(values));}
    internal static string Encode(Plan plan)
    {
        string image=new XElement("farmingOwnerDelta",new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),
            new XAttribute("world",plan.World),new XAttribute("creation",plan.Creation),new XAttribute("owner",plan.Owner),new XAttribute("source",plan.SourceHash),new XAttribute("deaths",plan.Deaths),
            new XElement("beforeBag",Cells(plan.BeforeBag)),new XElement("afterBag",Cells(plan.AfterBag)),
            new XElement("beforeBelt",Cells(plan.BeforeBelt)),new XElement("afterBelt",Cells(plan.AfterBelt))).ToString(SaveOptions.DisableFormatting);
        if(image.Length>4*1024*1024)throw new InvalidDataException("Farming owner delta exceeds bound.");return image;
    }
    internal static Plan Decode(string image)
    {
        if(image==null||image.Length>4*1024*1024)throw new InvalidDataException();XElement root;
        using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024}))root=XElement.Load(reader);
        if(root.Name!="farmingOwnerDelta"||(int?)root.Attribute("version")!=1)throw new InvalidDataException();
        ItemStack[] Read(string name,int maximum)
        {var values=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element(name).Value));if(values==null||values.Length>maximum)throw new InvalidDataException();return values;}
        var result=new Plan{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),World=(string)root.Attribute("world"),Creation=(string)root.Attribute("creation"),
            Owner=(string)root.Attribute("owner"),SourceHash=(string)root.Attribute("source"),Deaths=(int)root.Attribute("deaths"),BeforeBag=Read("beforeBag",169),AfterBag=Read("afterBag",169),BeforeBelt=Read("beforeBelt",20),AfterBelt=Read("afterBelt",20)};
        if(result.Operation==Guid.Empty||!Guid.TryParse(result.World,out _)||(!RebirthSurvivorRequestScope.TryNormalize(result.Creation,out _)&&result.Creation!="native-mode:"+result.Owner)||
            string.IsNullOrEmpty(result.Owner)||result.Owner.Length>512||result.SourceHash?.Length!=64||
            result.Deaths<0||!Positive(result.BeforeBag,result.AfterBag)||!Positive(result.BeforeBelt,result.AfterBelt)||Encode(result)!=image)throw new InvalidDataException();return result;
    }
    private static bool Positive(ItemStack[] before,ItemStack[] after)
    {
        if(before==null||after==null||before.Length!=after.Length)return false;
        for(int i=0;i<before.Length;i++)
        {
            var a=before[i];var b=after[i];if(a==null||b==null)return false;
            if(RebirthStationGridIngredients.IsSameStackSnapshot(a,b))continue;
            if(b.IsEmpty()||b.count<=0||b.count>1000000)return false;
            if(a.IsEmpty())continue;if(b.count<a.count)return false;
            var left=a.Clone();var right=b.Clone();left.count=right.count=1;
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(left,right))return false;
        }
        return true;
    }
    internal static string SourceHash(ItemStack[] bag,ItemStack[] belt,bool[] locks,int beltLength)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(Cells(bag));writer.Write(Cells(belt));writer.Write(locks?.Length??0);if(locks!=null)foreach(bool value in locks)writer.Write(value);writer.Write(beltLength);writer.Flush();
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
    internal static bool SavedPhase(RebirthStablePlayerIdentity identity,string receipt,out float phase)
    {
        phase=0f;return RebirthGearNativePlayerFile.TryRead(identity,out var saved)&&saved.buffData!=null&&SavedBuffPhase(saved.buffData.ToArray(),receipt,out phase);
    }
    internal static bool SavedPair(RebirthStablePlayerIdentity identity,string first,float firstValue,string second,float secondValue)
    {
        if(!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.buffData==null)return false;
        var bytes=saved.buffData.ToArray();return SavedBuffPhase(bytes,first,out float a)&&a==firstValue&&SavedBuffPhase(bytes,second,out float b)&&b==secondValue;
    }
    private static bool SavedBuffPhase(byte[] bytes,string receipt,out float phase)
    {
        phase=0f;if(bytes==null||bytes.Length>1024*1024)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;
            for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key!=receipt)continue;if(found||float.IsNaN(value)||float.IsInfinity(value))return false;phase=value;found=true;}
            return found&&stream.Position==stream.Length;
        }
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
        if(identity==null||identity.CanonicalId!=plan.Owner||!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.buffData==null||
            !SavedBuffPhase(saved.buffData.ToArray(),plan.Receipt,out float phase)||phase!=expectedPhase)return false;
        return Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,true),plan.BeforeBag,plan.AfterBag,expectedPhase>=2,expectedPhase==1)&&
            Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,false),plan.BeforeBelt,plan.AfterBelt,expectedPhase>=2,expectedPhase==1);
    }
    internal static bool SavedUnspent(RebirthStablePlayerIdentity identity,Plan plan)
    {
        if(identity==null||identity.CanonicalId!=plan.Owner||!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.buffData==null||
            !SavedBuffPhase(saved.buffData.ToArray(),plan.Receipt,out float phase)||phase!=1f)return false;
        return Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,true),plan.BeforeBag,plan.AfterBag,false)&&Delta(RebirthGearPlayerDataInventory.ReadSlots(saved,false),plan.BeforeBelt,plan.AfterBelt,false);
    }
    private static bool Current(Attempt attempt)
    {
        var player=attempt.Player;
        return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,attempt.World)&&ReferenceEquals(attempt.World.GetPrimaryPlayer(),player)&&
            ReferenceEquals(attempt.World.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&attempt.Plan.Deaths==player.Died&&GameIO.GetSaveGameDir()==attempt.Directory&&
            player.world.worldState?.Guid==attempt.Plan.World&&RebirthCamperCharacterAccess.CurrentCreation(player)==attempt.Plan.Creation&&
            ReferenceEquals(player.bag.ItemGrid,attempt.Bag)&&ReferenceEquals(player.inventory.ItemGrid,attempt.Belt)&&attempt.Current()&&
            !RebirthPlacedOriginal.IsHeld(player)&&!RebirthCookingOutputDelivery.IsHeld(player)&&!RebirthLogisticsOwnerReceipt.IsHeld(player)&&!RebirthGearOwnerReservation.IsHeld(player)&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
            !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&
            !RebirthCamperStationNativeActorPayment.Blocks(player.inventory);
    }
    internal static bool Advance(EntityPlayerLocal player,Plan plan,int serverPhase,Func<bool> authenticatedCurrent,Func<bool> sourceCleared)
    {
        if(player==null||plan==null||plan.Deaths!=player.Died||!Positive(plan.BeforeBag,plan.AfterBag)||!Positive(plan.BeforeBelt,plan.AfterBelt)||authenticatedCurrent==null||sourceCleared==null||!authenticatedCurrent()||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=plan.Owner)return false;
        string image=Encode(plan);
        if(Attempts.TryGetValue(plan.Operation,out var stale)&&!ReferenceEquals(stale.World,GameManager.Instance?.World))Attempts.Remove(plan.Operation);
        if(!Attempts.TryGetValue(plan.Operation,out var attempt))
        {
            if(RebirthDogLifecycleService.HasHireInventoryHold(player)||IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperEnergyOwnerHold.Blocks(player.inventory))return false;
            if(player.Buffs.GetCustomVar(plan.Receipt)==0f&&
                (SourceHash(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length)!=plan.SourceHash||
                !Delta(player.bag.ItemGrid.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(player.inventory.ItemGrid.items,plan.BeforeBelt,plan.AfterBelt,false)))return false;
            attempt=new Attempt{Plan=Decode(image),Image=image,Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Current=authenticatedCurrent,Deaths=player.Died};
            if(!Current(attempt))return false;
            Attempts.Add(plan.Operation,attempt);
        }
        if(attempt.Image!=image||!ReferenceEquals(attempt.Player,player))return false;attempt.Current=authenticatedCurrent;
        if(!Current(attempt))return false;float phase=player.Buffs.GetCustomVar(plan.Receipt);
        if(phase==0f)
        {
            if(SourceHash(attempt.Bag.items,attempt.Belt.items,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length)!=plan.SourceHash||
                !Delta(attempt.Bag.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(attempt.Belt.items,plan.BeforeBelt,plan.AfterBelt,false))return false;
            player.Buffs.SetCustomVar(plan.Receipt,1f,true);RequestNativeSave();return false;
        }
        if(phase==3f)return Saved(identity,plan,3);
        if(phase!=1f&&phase!=2f)return false;
        if(phase==1f)
        {
            if(serverPhase<1||!sourceCleared()||!Saved(identity,plan,1)||!Current(attempt))return false;
            for(int area=0;area<2;area++)
            {
                var grid=area==0?attempt.Bag:attempt.Belt;var before=area==0?plan.BeforeBag:plan.BeforeBelt;var after=area==0?plan.AfterBag:plan.AfterBelt;
                for(int i=0;i<after.Length;i++)
                {
                    if(!Current(attempt)||!Delta(grid.items,before,after,false,true))return false;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(before[i],after[i]))continue;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],after[i])){if(!HasNativeCell(attempt,area,i))return false;continue;}
                    if(grid.IsLocked(i)||permit!=null)return false;
                    var value=after[i].Clone();if(!PrepareNativeCell(attempt,area,i,value,out var single))return false;permit=single;
                    try{if(area==0)player.bag.SetSlot(i,value);else player.inventory.SetStackAt(i,value);}finally{permit=null;if(!single.Observed)ReleaseNativeCell(single);}
                    if(!single.Used||!single.Observed||!PublishNativeCell(single)||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],after[i]))return false;
                }
            }
            if(!Current(attempt)||!Delta(attempt.Bag.items,plan.BeforeBag,plan.AfterBag,true)||!Delta(attempt.Belt.items,plan.BeforeBelt,plan.AfterBelt,true)||!sourceCleared())return false;
            player.Buffs.SetCustomVar(plan.Receipt,2f,true);RequestNativeSave();
        }
        if(!Saved(identity,plan,2))RequestNativeSave();return Current(attempt)&&Saved(identity,plan,2);
    }
    internal static bool Retire(EntityPlayerLocal player,Plan plan,bool serverObservedOriginalAfter)
    {
        if(!serverObservedOriginalAfter||!Attempts.TryGetValue(plan.Operation,out var attempt)||attempt.Image!=Encode(plan)||!ReferenceEquals(attempt.Player,player)||!Current(attempt)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
        if(Saved(identity,plan,3)){Attempts.Remove(plan.Operation);return true;}
        if(!Saved(identity,plan,2))return false;
        player.Buffs.SetCustomVar(plan.Receipt,3f,true);RequestNativeSave();
        if(!Saved(identity,plan,3))return false;Attempts.Remove(plan.Operation);return true;
    }
    internal static bool AbandonRejectedWithoutEffect(EntityPlayerLocal player,Plan plan)
    {
        if(!Attempts.TryGetValue(plan.Operation,out var attempt))
        {if(player.Buffs.GetCustomVar(plan.Receipt)!=0f)return false;player.Buffs.SetCustomVar(plan.Receipt,4f,true);RequestNativeSave();return true;}
        if(!ReferenceEquals(attempt.Player,player)||!Current(attempt)||player.Buffs.GetCustomVar(plan.Receipt)!=1f||
            !Delta(player.bag.ItemGrid.items,plan.BeforeBag,plan.AfterBag,false)||!Delta(player.inventory.ItemGrid.items,plan.BeforeBelt,plan.AfterBelt,false))return false;
        player.Buffs.SetCustomVar(plan.Receipt,4f,true);RequestNativeSave();Attempts.Remove(plan.Operation);return true;
    }
    internal static bool HasPending(EntityPlayerLocal player)=>IsHeld(player);
}

[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
internal static class AdvancedFarmingHarvestDeliveryGridSlot
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack,out object __state)
    {__state=null;if(!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance))return true;if(!AdvancedFarmingHarvestOwnerDelta.Consume(__instance,_index,_stack))return false;__state=AdvancedFarmingHarvestOwnerDelta.EnterNativeCell(__instance,_index,_stack);return __state!=null;}
    private static void Postfix(ItemStackGrid __instance,int _index,ItemStack _stack,bool __runOriginal,object __state)
        =>AdvancedFarmingHarvestOwnerDelta.ObserveNativeCell(__instance,_index,_stack,__runOriginal,__state);
}
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDeliveryGridBulk
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","ReadInto","SetSlotLocks","SetLocked"})yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance)=>!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
internal static class AdvancedFarmingHarvestDeliveryGridAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance))return true;__result=-1;return false;}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
internal static class AdvancedFarmingHarvestDeliveryGridCount
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance))return true;__result=0;return false;}
}
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDeliveryDecItem
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(Bag),nameof(Bag.DecItem));yield return AccessTools.Method(typeof(Inventory),nameof(Inventory.DecItem));}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,ref int __result)
    {
        var grid=__instance is Bag bag?bag.ItemGrid:(__instance as Inventory)?.ItemGrid;
        if(!AdvancedFarmingHarvestOwnerDelta.Blocks(grid))return true;__result=0;return false;
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
internal static class AdvancedFarmingHarvestDeliveryBagAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref bool __result){if(!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance.ItemGrid))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
internal static class AdvancedFarmingHarvestDeliveryBagPartial
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!AdvancedFarmingHarvestOwnerDelta.Blocks(__instance.ItemGrid))return true;__result=(false,false);return false;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class AdvancedFarmingHarvestDeliveryHeldUse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityAlive __instance,ref bool __result){if(!AdvancedFarmingHarvestOwnerDelta.IsHeld(__instance as EntityPlayerLocal))return true;__result=false;return false;}
}
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDeliveryActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes)??throw new MissingMethodException(type.FullName,"OnActivated");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BaseItemActionEntry __instance)=>!AdvancedFarmingHarvestOwnerDelta.IsHeld(__instance?.ItemController?.xui?.playerUI?.entityPlayer);
}

// Fence the gesture before its first cursor/native-slot side effect. Guarding only
// the eventual bag setter is too late: native SwapItem writes the cursor first.
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDeliverySlotGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance)
        =>!AdvancedFarmingHarvestOwnerDelta.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack))]
internal static class AdvancedFarmingHarvestDeliveryCursorSet
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance,ItemStack stack)
        =>!AdvancedFarmingHarvestOwnerDelta.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDeliveryCursorGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(XUiC_DragAndDropWindow)))
            if(method.Name=="DropCurrentItem"||method.Name=="PlaceItemBackInInventory")yield return method;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance)
        =>!AdvancedFarmingHarvestOwnerDelta.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
