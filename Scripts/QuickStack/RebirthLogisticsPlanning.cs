using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

// Pure native-stack plan composition. Callers supply authenticated native owner
// and endpoint observations, then validate every identity/lock before effects.
internal sealed class RebirthLogisticsTransferPlan
{
    internal sealed class Slots
    {
        internal string Id,Identity;internal RemoteResourceSourceKind Kind;
        internal ItemStack[] Before,After;internal bool[] Locks;
    }
    private readonly Dictionary<string,Slots> views=new Dictionary<string,Slots>(StringComparer.Ordinal);
    private readonly Dictionary<string,RebirthLogisticsTransferJournal.NpcLeg> companions=new Dictionary<string,RebirthLogisticsTransferJournal.NpcLeg>(StringComparer.Ordinal);
    internal RebirthLogisticsTransferJournal.NpcLeg CaptureNpc(string stableId,uint revision,IReadOnlyDictionary<string,int> quantities,IReadOnlyDictionary<string,int> reservations,IEnumerable<string> incomingKeys)
    {
        if(string.IsNullOrEmpty(stableId)||revision>uint.MaxValue-2||companions.ContainsKey(stableId)||quantities==null||quantities.Count>4096||reservations?.Count>4096)throw new InvalidDataException("Original logical companion observation differs.");
        var keys=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var key in quantities.Keys)keys.Add(key);if(reservations!=null)foreach(var key in reservations.Keys)keys.Add(key);if(incomingKeys!=null)foreach(var key in incomingKeys)keys.Add(key);
        if(keys.Count>4096)throw new InvalidDataException();var npc=new RebirthLogisticsTransferJournal.NpcLeg{StableId=stableId,Revision=revision,DebitId=Guid.NewGuid(),CreditId=Guid.NewGuid()};
        foreach(var key in keys)
        {
            quantities.TryGetValue(key,out int count);int held=0;reservations?.TryGetValue(key,out held);
            if(string.IsNullOrWhiteSpace(key)||key.Length>128||key.Trim()!=key||count<0||held<0||held>count)throw new InvalidDataException();
            npc.Entries.Add(new RebirthLogisticsTransferJournal.NpcEntry{Key=key,Before=count,Reserved=held,After=count});
        }
        companions.Add(stableId,npc);return npc;
    }
    internal int MoveNpcToNpc(RebirthLogisticsTransferJournal.NpcLeg source,RebirthLogisticsTransferJournal.NpcLeg destination,string key,int maximum)
    {
        if(source==null||destination==null||ReferenceEquals(source,destination)||maximum<=0||!companions.TryGetValue(source.StableId,out var originalSource)||!ReferenceEquals(originalSource,source)||
            !companions.TryGetValue(destination.StableId,out var originalDestination)||!ReferenceEquals(originalDestination,destination))throw new InvalidDataException();
        var from=source.Entries.FirstOrDefault(entry=>string.Equals(entry.Key,key,StringComparison.OrdinalIgnoreCase));
        var to=destination.Entries.FirstOrDefault(entry=>string.Equals(entry.Key,key,StringComparison.OrdinalIgnoreCase));if(from==null||to==null||from.Locked)return 0;
        int quantity=Math.Min(maximum,Math.Max(0,from.After-from.Reserved-from.Custodied-from.PendingNative));quantity=(int)Math.Min(quantity,(long)int.MaxValue-to.After);
        from.After-=quantity;to.After=checked(to.After+quantity);return quantity;
    }
    internal int MoveToNpc(Slots source,RebirthLogisticsTransferJournal.NpcLeg target,string canonicalKey,int type,int maximum)
    {
        if(source==null||target==null||maximum<=0||!views.TryGetValue(source.Id,out var original)||!ReferenceEquals(original,source)||
            !companions.TryGetValue(target.StableId,out var npc)||!ReferenceEquals(npc,target))throw new InvalidDataException();
        var to=target.Entries.FirstOrDefault(entry=>string.Equals(entry.Key,canonicalKey,StringComparison.OrdinalIgnoreCase));if(to==null)return 0;
        int limit=(int)Math.Min(maximum,(long)int.MaxValue-to.After),moved=0;
        for(int slot=0;slot<source.After.Length&&moved<limit;slot++)
        {
            var item=source.After[slot];if(source.Locks[slot]||item==null||item.IsEmpty()||item.itemValue.type!=type)continue;
            int count=Math.Min(limit-moved,item.count);if(target.NativeCredits.Count>=4096)throw new InvalidDataException("Original native companion credit bound exceeded.");
            var credit=item.Clone();credit.count=count;target.NativeCredits.Add(new RebirthLogisticsTransferJournal.NpcCredit{StackId=Guid.NewGuid(),Key=canonicalKey,Source=source.Id,Slot=slot,Stack=credit});
            to.PendingNative=checked(to.PendingNative+count);
            var after=item.Clone();after.count-=count;source.After[slot]=after.count>0?after:ItemStack.Empty.Clone();moved=checked(moved+count);
        }
        // Conversion into authored logical inventory intentionally retains the
        // existing item-key quantity semantics, not fabricated item metadata.
        to.After=checked(to.After+moved);return moved;
    }
    internal int MoveFromNpc(RebirthLogisticsTransferJournal.NpcLeg source,Slots target,string canonicalKey,ItemStack nativePrototype,int maximum,bool allowEmpty)
    {
        if(source==null||target==null||maximum<=0||nativePrototype==null||nativePrototype.IsEmpty()||!views.TryGetValue(target.Id,out var original)||!ReferenceEquals(original,target)||
            !companions.TryGetValue(source.StableId,out var npc)||!ReferenceEquals(npc,source))throw new InvalidDataException();
        var from=source.Entries.FirstOrDefault(entry=>string.Equals(entry.Key,canonicalKey,StringComparison.OrdinalIgnoreCase));if(from==null||from.Locked)return 0;
        int offered=Math.Min(maximum,Math.Max(0,from.After-from.Reserved-from.Custodied-from.PendingNative));if(offered==0)return 0;var incoming=nativePrototype.Clone();incoming.count=offered;
        for(int slot=0;slot<target.After.Length&&incoming.count>0;slot++)
        {
            var current=target.After[slot];if(current==null||current.IsEmpty()||!current.CanStackPartlyWith(incoming,out int count)||count<=0)continue;
            count=Math.Min(count,incoming.count);var after=current.Clone();after.count=checked(after.count+count);target.After[slot]=after;incoming.count-=count;
        }
        if(allowEmpty)
        {
            int limit=Math.Max(1,incoming.itemValue.ItemClass.Stacknumber.Value);
            for(int slot=0;slot<target.After.Length&&incoming.count>0;slot++)
            {
                if(target.Locks[slot]||target.After[slot]!=null&&!target.After[slot].IsEmpty())continue;
                int count=Math.Min(limit,incoming.count);var after=incoming.Clone();after.count=count;target.After[slot]=after;incoming.count-=count;
            }
        }
        int moved=offered-incoming.count;from.After-=moved;return moved;
    }
    internal Slots Capture(string id,string identity,RemoteResourceSourceKind kind,ItemStack[] actual,bool[] locks)
    {
        if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(identity)||actual==null||actual.Length<1||actual.Length>256||locks?.Length!=actual.Length)
            throw new InvalidDataException("Original logistics observation is incomplete.");
        if(views.TryGetValue(id,out var existing))
        {
            if(existing.Identity!=identity||existing.Kind!=kind||actual.Length!=existing.Before.Length||
                !existing.Locks.SequenceEqual(locks))throw new InvalidDataException("Original logistics endpoint changed during planning.");
            // Planning validates all cells, before sparse owner deltas exist.
            for(int i=0;i<actual.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],existing.Before[i]))throw new InvalidDataException("Original endpoint changed.");
            return existing;
        }
        var value=new Slots{Id=id,Identity=identity,Kind=kind,Before=ItemStack.Clone(actual),After=ItemStack.Clone(actual),Locks=(bool[])locks.Clone()};
        views.Add(id,value);return value;
    }
    internal int Move(Slots source,Slots target,int itemType,int maximum,bool allowEmpty)
    {
        if(source==null||target==null||ReferenceEquals(source,target)||itemType<1||maximum<1)return 0;int moved=0;
        for(int s=0;s<source.After.Length&&moved<maximum;s++)
        {
            var original=source.After[s];if(source.Locks[s]||original==null||original.IsEmpty()||original.itemValue.type!=itemType)continue;
            var incoming=original.Clone();incoming.count=Math.Min(incoming.count,maximum-moved);int offered=incoming.count;
            for(int d=0;d<target.After.Length&&incoming.count>0;d++)
            {
                var destination=target.After[d];if(destination==null||destination.IsEmpty()||!destination.CanStackPartlyWith(incoming,out int amount)||amount<=0)continue;
                amount=Math.Min(amount,incoming.count);var after=destination.Clone();after.count=checked(after.count+amount);target.After[d]=after;incoming.count-=amount;
            }
            if(allowEmpty)
            {
                int maximumStack=Math.Max(1,incoming.itemValue.ItemClass.Stacknumber.Value);
                for(int d=0;d<target.After.Length&&incoming.count>0;d++)
                {
                    if(target.Locks[d]||target.After[d]!=null&&!target.After[d].IsEmpty())continue;
                    int amount=Math.Min(maximumStack,incoming.count);var after=incoming.Clone();after.count=amount;target.After[d]=after;incoming.count-=amount;
                }
            }
            int count=offered-incoming.count;if(count==0)continue;
            var remainder=original.Clone();remainder.count-=count;source.After[s]=remainder.count>0?remainder:ItemStack.Empty.Clone();moved=checked(moved+count);
        }
        return moved;
    }
    internal int MoveSlot(Slots source,Slots target,int slot,int type,int maximum)
    {
        if(source==null||target==null||ReferenceEquals(source,target)||slot<0||slot>=source.After.Length||maximum<=0||
            !views.TryGetValue(source.Id,out var from)||!ReferenceEquals(from,source)||!views.TryGetValue(target.Id,out var to)||!ReferenceEquals(to,target))throw new InvalidDataException();
        var original=source.After[slot];if(source.Locks[slot]||original==null||original.IsEmpty()||original.itemValue.type!=type)return 0;
        int offered=Math.Min(maximum,original.count);var incoming=original.Clone();incoming.count=offered;
        for(int i=0;i<target.After.Length&&incoming.count>0;i++)
        {
            var current=target.After[i];if(current==null||current.IsEmpty()||!current.CanStackPartlyWith(incoming,out int amount)||amount<=0)continue;
            amount=Math.Min(amount,incoming.count);var after=current.Clone();after.count=checked(after.count+amount);target.After[i]=after;incoming.count-=amount;
        }
        int limit=Math.Max(1,incoming.itemValue.ItemClass.Stacknumber.Value);
        for(int i=0;i<target.After.Length&&incoming.count>0;i++)
        {
            if(target.Locks[i]||target.After[i]!=null&&!target.After[i].IsEmpty())continue;
            int amount=Math.Min(limit,incoming.count);var after=incoming.Clone();after.count=amount;target.After[i]=after;incoming.count-=amount;
        }
        int moved=offered-incoming.count;if(moved>0){var after=original.Clone();after.count-=moved;source.After[slot]=after.count>0?after:ItemStack.Empty.Clone();}return moved;
    }
    internal void Complete(RebirthLogisticsTransferJournal.Record record,Slots owner)
    {
        if(record==null||owner==null||record.Legs.Count!=0||record.Npcs.Count!=0||!views.TryGetValue(owner.Id,out var original)||!ReferenceEquals(original,owner))throw new InvalidDataException();
        record.OriginalOwnerBag=ItemStack.Clone(owner.Before);record.BeforeOwner=ItemStack.Clone(owner.Before);record.AfterOwner=ItemStack.Clone(owner.After);
        for(int i=0;i<owner.Before.Length;i++)if(RebirthStationGridIngredients.IsSameStackSnapshot(owner.Before[i],owner.After[i]))
        {record.BeforeOwner[i]=ItemStack.Empty.Clone();record.AfterOwner[i]=ItemStack.Empty.Clone();}
        record.DebitedOwner=new ItemStack[record.BeforeOwner.Length];
        for(int i=0;i<record.DebitedOwner.Length;i++)record.DebitedOwner[i]=RebirthLogisticsTransferJournal.Debited(record.BeforeOwner[i],record.AfterOwner[i]);
        foreach(var view in views.Values)
        {
            if(ReferenceEquals(view,owner))continue;bool changed=false;
            for(int i=0;i<view.Before.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(view.Before[i],view.After[i])){changed=true;break;}
            if(changed||companions.Values.Any(npc=>npc.NativeCredits.Any(credit=>credit.Source==view.Id)))record.Legs.Add(new RebirthLogisticsTransferJournal.Leg{Id=view.Id,NativeIdentity=view.Identity,Kind=view.Kind,Before=ItemStack.Clone(view.Before),After=ItemStack.Clone(view.After),Locks=(bool[])view.Locks.Clone()});
        }
        foreach(var npc in companions.Values)
        {
            if(npc.Entries.All(entry=>entry.Before==entry.After))continue;
            var detached=new RebirthLogisticsTransferJournal.NpcLeg{StableId=npc.StableId,Revision=npc.Revision,DebitId=npc.DebitId,CreditId=npc.CreditId,NativeBefore=npc.NativeBefore};
            foreach(var entry in npc.Entries)detached.Entries.Add(new RebirthLogisticsTransferJournal.NpcEntry{Key=entry.Key,Before=entry.Before,Reserved=entry.Reserved,Custodied=entry.Custodied,PendingNative=entry.PendingNative,After=entry.After,Locked=entry.Locked});
            foreach(var credit in npc.NativeCredits)detached.NativeCredits.Add(new RebirthLogisticsTransferJournal.NpcCredit{StackId=credit.StackId,Key=credit.Key,Source=credit.Source==owner.Id?"$owner":credit.Source,Slot=credit.Slot,Stack=credit.Stack.Clone()});
            record.Npcs.Add(detached);
        }
    }
}

// Scoped routing overlay, never a temporary replacement of a live server bag.
// Preparation supplies complete authenticated native observations before Enter.
internal sealed class RebirthLogisticsPlanning:IDisposable
{
    internal sealed class Observation
    {
        internal IRemoteResourceSource Source;internal object NativeObject;internal string Identity;
        internal ItemStack[] Items;internal PackedBoolArray Locks;
    }
    private sealed class Resource:IRemoteResourceSource
    {
        internal RebirthLogisticsPlanning Scope;internal Observation Original;internal RebirthLogisticsTransferPlan.Slots View;internal PackedBoolArray Locks;
        public string StableId=>Original.Source.StableId;public string DisplayName=>Original.Source.DisplayName;
        public RemoteResourceSourceKind Kind=>Original.Source.Kind;public Vector3 Position=>Original.Source.Position;
        public bool IsLoaded=>Scope.Current()&&Scope.SameObject(Original)&&Original.Source.IsLoaded;
        public bool IsBusy=>Original.Source.IsBusy;public bool IsActivated=>Original.Source.IsActivated;public bool IsExcluded=>Original.Source.IsExcluded;
        public ItemStack[] Slots{get{Scope.Check();return View.After;}}
        public PackedBoolArray SlotLocks=>Locks;
        public int Revision=>RemoteResourceAccess.ComputeRevision(Slots);
        public bool IsAuthorized(EntityPlayer player,bool exactOwner,out string reason)
        {Scope.Check(player);return Original.Source.IsAuthorized(player,exactOwner,out reason);}
        public void SetSlot(int slot,ItemStack value)
        {Scope.Check();if(slot<0||slot>=View.After.Length)throw new InvalidDataException();View.After[slot]=value?.Clone()??ItemStack.Empty.Clone();}
        public void MarkModified(){Scope.Check();} // No callbacks/network/native setters during planning.
    }
    [ThreadStatic] private static RebirthLogisticsPlanning active;
    private readonly EntityPlayer actor;private readonly World world;private readonly string creation;
    private readonly RebirthLogisticsTransferJournal.Record record;
    private readonly RebirthLogisticsTransferPlan plan=new RebirthLogisticsTransferPlan();
    private readonly RebirthLogisticsTransferPlan.Slots owner;
    private readonly PackedBoolArray ownerLocks;
    private readonly Dictionary<string,Resource> resources=new Dictionary<string,Resource>(StringComparer.Ordinal);
    private sealed class LogicalObservation{internal EntityRebirthNPC Entity;internal RebirthNpcStableId Id;internal RebirthLogisticsTransferJournal.NpcLeg Plan;}
    private readonly Dictionary<string,LogicalObservation> logical=new Dictionary<string,LogicalObservation>(StringComparer.Ordinal);
    private bool disposed;
    internal static bool IsActive=>active!=null;
    internal static bool IsOriginalOwner(EntityPlayer player)=>active!=null&&ReferenceEquals(active.actor,player)&&active.Current();
    internal static object NativeEndpoint(IRemoteResourceSource source)
    {
        if(source is Resource view)return view.Original.NativeObject;
        if(source is StaticContainerResourceSource container)return container.NativeTile;
        if(source is WorkstationOutputResourceSource station)return station.NativeStation;
        if(source is VehicleResourceSource vehicle)return vehicle.Vehicle;
        if(source is DroneResourceSource drone)return drone.Drone;
        if(source is RemoteNpcResourceSource npc)return npc.NativeActor;
        return null;
    }
    internal RebirthLogisticsPlanning(EntityPlayer actor,RebirthLogisticsTransferJournal.Record record,PlayerDataFile nativeOwner,IReadOnlyList<Observation> observed,IReadOnlyList<EntityRebirthNPC> logicalNpcs=null)
    {
        if(active!=null||!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||record==null||nativeOwner==null||observed==null||observed.Count>512)
            throw new InvalidOperationException("Nested or incomplete logistics planning is unavailable.");
        this.actor=actor;world=actor.world;creation=RebirthCamperCharacterAccess.CurrentCreation(actor);this.record=record;
        if(!Current()||nativeOwner.deaths!=record.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=record.Owner)throw new InvalidDataException("Original native owner differs.");
        var bag=RebirthGearPlayerDataInventory.ReadSlots(nativeOwner,true);ownerLocks=ReadLocks(nativeOwner);
        record.OriginalOwnerBelt=RebirthGearPlayerDataInventory.ReadSlots(nativeOwner,false);if(record.OriginalOwnerBelt==null)throw new InvalidDataException("Original native belt unavailable.");
        owner=plan.Capture("$owner",record.Owner+":"+record.Creation,RemoteResourceSourceKind.StaticContainer,bag,LockImage(ownerLocks,bag?.Length??0));
        foreach(var item in observed)
        {
            if(item?.Source==null||item.NativeObject==null||string.IsNullOrEmpty(item.Identity)||item.Identity.Length>1024||!Known(item.Source)||!SameObject(item)||
                resources.ContainsKey(item.Source.StableId))throw new InvalidDataException("Original native endpoint observation differs.");
            var detached=new Observation{Source=item.Source,NativeObject=item.NativeObject,Identity=item.Identity,Items=ItemStack.Clone(item.Items),Locks=CopyLocks(item.Locks,item.Items.Length)};
            var view=plan.Capture(detached.Source.StableId,detached.Identity,detached.Source.Kind,detached.Items,LockImage(detached.Locks,detached.Items.Length));
            resources.Add(view.Id,new Resource{Scope=this,Original=detached,View=view,Locks=CopyLocks(detached.Locks,detached.Items.Length)});
        }
        if(logicalNpcs?.Count>128)throw new InvalidDataException();
        if(logicalNpcs!=null)
        {
            var snapshots=new Dictionary<EntityRebirthNPC,RebirthNpcInventorySnapshot>();var keys=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var stack in owner.Before.Concat(resources.Values.SelectMany(resource=>resource.View.Before)))
                if(stack!=null&&!stack.IsEmpty()){var item=ItemClass.GetForId(stack.itemValue.type);if(item!=null&&!string.IsNullOrWhiteSpace(item.Name))keys.Add(item.Name);}
            foreach(var npc in logicalNpcs)
            {
                var state=npc?.RebirthRuntimeState;
                if(state==null||state.StableId.IsEmpty||!ReferenceEquals(world.GetEntity(npc.entityId),npc)||npc.IsDead()||resources.Values.Any(resource=>ReferenceEquals(resource.Original.NativeObject,npc))||
                    !RebirthNpcProfileRegistry.TryResolve(state.ProfileId,out var profile)||profile.Category==RebirthNpcCategory.DogCompanion||
                    !RemoteNpcResourceSource.IsAuthorized(npc,actor,false,out _)||RebirthNpcInteractionSessionService.IsInventoryAccessing(state.StableId))throw new InvalidDataException("Original logical companion unavailable.");
                var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(state.StableId);if(snapshot?.Quantities==null||snapshots.ContainsKey(npc))throw new InvalidDataException();
                snapshots.Add(npc,snapshot);foreach(var key in snapshot.Quantities.Keys)keys.Add(key);
            }
            foreach(var entry in snapshots)
            {
                var id=entry.Key.RebirthRuntimeState.StableId;var snapshot=entry.Value;
                var planned=plan.CaptureNpc(id.ToString(),snapshot.Revision,snapshot.Quantities,snapshot.Reservations,keys);
                if(!RebirthNpcInventoryTransactionService.CanAdmitLogisticsOriginalEffects(id,planned.DebitId,planned.CreditId))throw new InvalidDataException("Original NPC effect receipt capacity unavailable before admission.");
                planned.NativeBefore=snapshot.NativeStacks?.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)??string.Empty;
                foreach(var item in planned.Entries){item.Locked=RebirthCompanionInventoryLockService.IsNpcItemLocked(id,item.Key);item.Custodied=snapshot.NativeStacks?.GetQuantity(item.Key)??0;if((long)item.Reserved+item.Custodied>item.Before)throw new InvalidDataException();}
                logical.Add(id.ToString(),new LogicalObservation{Entity=entry.Key,Id=id,Plan=planned});
            }
        }
        active=this;
    }
    private bool Current()=>!disposed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
        ReferenceEquals(world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==record.Deaths&&
        creation==record.Creation&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&RebirthLogisticsTransferJournal.WorldKey(world)==record.World;
    private void Check(EntityPlayer player=null)
    {if(!Current()||player!=null&&!ReferenceEquals(player,actor))throw new InvalidOperationException("Original logistics planning scope expired.");}
    private static bool Known(IRemoteResourceSource source)=>source is StaticContainerResourceSource||source is WorkstationOutputResourceSource||
        source is VehicleResourceSource||source is DroneResourceSource||source is RemoteNpcResourceSource;
    private bool SameObject(Observation observation)
    {
        var source=observation.Source;object native;
        if(source is VehicleResourceSource vehicle)native=vehicle.Vehicle;
        else if(source is DroneResourceSource drone)native=drone.Drone;
        else if(source is RemoteNpcResourceSource companion)
        {
            if(!source.StableId.StartsWith("N:",StringComparison.Ordinal)||!RebirthNpcStableId.TryParse(source.StableId.Substring(2),out var id)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||!companion.IsSnapshotCurrent()||
                !(world.GetEntity(entity) is EntityRebirthDogCompanion)||!RebirthDogStateService.TryGetView(id,out _,out var dog)||dog==null||dog.IsTamedWild)return false;
            // RemoteNpcResourceSource owns a detached working view. Comparing
            // that view to itself would miss a concurrent canonical dog commit.
            var saved=RebirthDogInventoryService.GetSnapshot(id);
            if(saved.Slots==null||source.Slots==null||saved.Slots.Length!=source.Slots.Length||
                !RebirthCamperInventorySnapshot.Capture(saved.Slots).SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Slots)))return false;
            var actualLocks=LockImage(saved.Locks,saved.Slots.Length);var originalLocks=LockImage(source.SlotLocks,source.Slots.Length);
            if(!actualLocks.SequenceEqual(originalLocks))return false;
            native=world.GetEntity(entity);
        }
        else native=world.GetTileEntity(new Vector3i(source.Position));
        return native!=null&&ReferenceEquals(native,observation.NativeObject);
    }
    private static PackedBoolArray ReadLocks(PlayerDataFile data)
    {
        using(var stream=new MemoryStream(data.bagData.ToArray()))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);if(reader.ReadByte()!=2)throw new InvalidDataException();var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);
            if(stream.Position!=stream.Length)throw new InvalidDataException();return CopyLocks(grid.SlotLocks,grid.Length);
        }
    }
    private static bool[] LockImage(PackedBoolArray source,int count)
    {if(count<1||count>256||source!=null&&source.Length!=count)throw new InvalidDataException();var result=new bool[count];for(int i=0;i<count;i++)result[i]=source!=null&&source[i];return result;}
    private static PackedBoolArray CopyLocks(PackedBoolArray source,int count)
    {var bits=LockImage(source,count);var result=new PackedBoolArray(count);for(int i=0;i<count;i++)result[i]=bits[i];return result;}
    internal static ItemStack[] OwnerSlots(EntityPlayer player)
    {if(active==null)return player?.bag?.ItemGrid?.items;active.Check(player);return active.owner.After;}
    internal static PackedBoolArray OwnerLocks(EntityPlayer player)
    {if(active==null)return player?.bag?.LockedSlots;active.Check(player);return active.ownerLocks;}
    internal static void SetOwnerSlot(EntityPlayer player,int slot,ItemStack value)
    {if(active==null){player.bag.SetSlot(slot,value);return;}active.Check(player);if(slot<0||slot>=active.owner.After.Length)throw new InvalidDataException();active.owner.After[slot]=value?.Clone()??ItemStack.Empty.Clone();}
    internal static void SetOwnerSlots(EntityPlayer player,ItemStack[] value)
    {
        if(active==null){player.bag.SetSlots(value);return;}active.Check(player);
        if(value?.Length!=active.owner.After.Length)throw new InvalidDataException();active.owner.After=ItemStack.Clone(value);
    }
    internal static IRemoteResourceSource Wrap(IRemoteResourceSource source)
    {
        if(active==null||source==null)return source;active.Check();
        if(source is Resource resource){if(!ReferenceEquals(resource.Scope,active))throw new InvalidDataException();return resource;}
        if(!active.resources.TryGetValue(source.StableId,out var found)||!active.SameObject(found.Original)||source.Kind!=found.Kind)throw new InvalidDataException("Unobserved original logistics endpoint.");
        return found;
    }
    internal static List<IRemoteResourceSource> ResolveNearby(World world,EntityPlayer player,float radius,bool includeStatic,bool includeWorkstations,bool includeMobile)
    {
        var result=RemoteResourceRegistry.ResolveNearby(world,player,radius,includeStatic,includeWorkstations,includeMobile);if(active==null)return result;
        active.Check(player);if(!ReferenceEquals(world,active.world))throw new InvalidDataException();
        for(int i=0;i<result.Count;i++)if(active.resources.ContainsKey(result[i].StableId))result[i]=Wrap(result[i]);
        // Unobserved candidates remain available to native read-only policy
        // filtering. Any attempted mutation must call strict Wrap first.
        return result;
    }
    private Resource Storage(TEFeatureStorage storage)
    {
        Check();if(storage?.Parent==null)throw new InvalidDataException("Native storage missing during planning.");
        foreach(var resource in resources.Values)
            if(resource.Kind==RemoteResourceSourceKind.StaticContainer&&ReferenceEquals(resource.Original.NativeObject,storage.Parent)&&
                ReferenceEquals(storage.Parent.GetFeature<TEFeatureStorage>(),storage)&&SameObject(resource.Original))return resource;
        throw new InvalidDataException("Unobserved original native storage.");
    }
    internal static ItemStack[] StorageSlots(TEFeatureStorage storage)=>active==null?storage?.ItemGrid?.items:active.Storage(storage).View.After;
    internal static PackedBoolArray StorageLocks(TEFeatureStorage storage)=>active==null?storage?.ItemGrid?.SlotLocks:active.Storage(storage).Locks;
    internal static void SetStorageSlot(TEFeatureStorage storage,int slot,ItemStack value)
    {if(active==null){storage.UpdateSlot(slot,value);return;}active.Storage(storage).SetSlot(slot,value);}
    internal static void ActivateStorage(string id)
    {
        if(active==null){RemoteResourceStateStore.Activate(id);return;}active.Check();
        if(!active.resources.ContainsKey(id))throw new InvalidDataException("Unobserved original storage activation.");
        // Activation belongs to actual authoritative publication, never planning.
    }
    internal static void SetWorkstationOutput(World world,TileEntityWorkstation station,ItemStack[] output)
    {
        if(active==null){station.Output=output;return;}
        active.Check();if(!ReferenceEquals(world,active.world)||!ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station))throw new InvalidDataException();
        var resource=Wrap(new WorkstationOutputResourceSource(world,station.ToWorldPos()));
        if(output?.Length!=resource.Slots.Length)throw new InvalidDataException();
        for(int i=0;i<output.Length;i++)resource.SetSlot(i,output[i]);
        // Actual native output assignment and its existing paid completion
        // observers remain mandatory when the retained plan is later applied.
    }
    private RebirthLogisticsTransferJournal.NpcLeg Logical(RebirthNpcStableId id)
    {Check();if(!logical.TryGetValue(id.ToString(),out var observed))throw new InvalidDataException("Unobserved original logical companion.");return observed.Plan;}
    private RebirthLogisticsTransferPlan.Slots View(IRemoteResourceSource source)
    {var resource=Wrap(source) as Resource;if(resource==null||!ReferenceEquals(resource.Scope,this))throw new InvalidDataException();return resource.View;}
    internal static int OwnerToNpc(EntityPlayer player,RebirthNpcStableId target,int type,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var item=ItemClass.GetForId(type);if(item==null)return 0;
        if(active.resources.TryGetValue("N:"+target,out var dog))return active.plan.Move(active.owner,dog.View,type,maximum,true);
        return active.plan.MoveToNpc(active.owner,active.Logical(target),item.Name,type,maximum);
    }
    internal static int ResourceToNpc(EntityPlayer player,IRemoteResourceSource source,RebirthNpcStableId target,int type,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var item=ItemClass.GetForId(type);if(item==null)return 0;
        if(active.resources.TryGetValue("N:"+target,out var dog))return active.plan.Move(active.View(source),dog.View,type,maximum,true);
        return active.plan.MoveToNpc(active.View(source),active.Logical(target),item.Name,type,maximum);
    }
    internal static int NpcToOwner(EntityPlayer player,RebirthNpcStableId source,int type,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var item=ItemClass.GetForId(type);if(item==null)return 0;
        if(active.resources.TryGetValue("N:"+source,out var dog))return active.plan.Move(dog.View,active.owner,type,maximum,true);
        var value=ItemClass.GetItem(item.Name,false);if(value==null||value.IsEmpty()||value.type!=type)return 0;
        return active.plan.MoveFromNpc(active.Logical(source),active.owner,item.Name,new ItemStack(value,1),maximum,true);
    }
    internal static int NpcToResource(EntityPlayer player,RebirthNpcStableId source,IRemoteResourceSource target,int type,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var item=ItemClass.GetForId(type);if(item==null)return 0;
        if(active.resources.TryGetValue("N:"+source,out var dog))return active.plan.Move(dog.View,active.View(target),type,maximum,true);
        var value=ItemClass.GetItem(item.Name,false);if(value==null||value.IsEmpty()||value.type!=type)return 0;
        return active.plan.MoveFromNpc(active.Logical(source),active.View(target),item.Name,new ItemStack(value,1),maximum,true);
    }
    internal static int NpcToNpc(EntityPlayer player,RebirthNpcStableId source,RebirthNpcStableId target,int type,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var item=ItemClass.GetForId(type);if(item==null)return 0;
        bool fromDog=active.resources.TryGetValue("N:"+source,out var from);bool toDog=active.resources.TryGetValue("N:"+target,out var to);
        if(fromDog&&toDog)return active.plan.Move(from.View,to.View,type,maximum,true);
        if(fromDog)return active.plan.MoveToNpc(from.View,active.Logical(target),item.Name,type,maximum);
        if(toDog){var value=ItemClass.GetItem(item.Name,false);return value==null||value.IsEmpty()||value.type!=type?0:active.plan.MoveFromNpc(active.Logical(source),to.View,item.Name,new ItemStack(value,1),maximum,true);}
        return active.plan.MoveNpcToNpc(active.Logical(source),active.Logical(target),item.Name,maximum);
    }
    internal static int NpcToStatic(World world,EntityPlayer player,RebirthNpcStableId source,int type,bool owned,int maximum)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);if(!ReferenceEquals(world,active.world))throw new InvalidDataException();
        var item=ItemClass.GetForId(type);if(item==null||maximum<=0)return 0;
        var from=active.Logical(source).Entries.FirstOrDefault(entry=>string.Equals(entry.Key,item.Name,StringComparison.OrdinalIgnoreCase));if(from==null||from.Locked)return 0;
        int offered=Math.Min(maximum,Math.Max(0,from.After-from.Reserved-from.Custodied-from.PendingNative));if(offered==0)return 0;var value=ItemClass.GetItem(item.Name,false);if(value==null||value.IsEmpty()||value.type!=type)return 0;
        var incoming=new ItemStack(value,offered);var destinations=QuickStackTransferPlanner.Find(world,player,incoming,owned,RebirthSandboxOptionManager.Current.QuickStack);
        if(destinations==null)return 0;int moved=QuickStackTransferExecutor.CommitExternal(world,player,incoming,destinations,owned,out var remaining);
        if(moved<0||moved>offered||remaining==null||remaining.count!=offered-moved)throw new InvalidDataException();from.After-=moved;return moved;
    }
    internal static void AllOwnerToNpc(EntityPlayer player,RebirthNpcStableId target)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);
        foreach(int type in active.owner.After.Where(item=>item!=null&&!item.IsEmpty()).Select(item=>item.itemValue.type).Distinct().ToArray())OwnerToNpc(player,target,type,int.MaxValue);
    }
    internal static void AllResourceToNpc(EntityPlayer player,IRemoteResourceSource source,RebirthNpcStableId target)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);var view=active.View(source);
        foreach(int type in view.After.Where(item=>item!=null&&!item.IsEmpty()).Select(item=>item.itemValue.type).Distinct().OrderBy(type=>ItemClass.GetForId(type)?.Name,StringComparer.OrdinalIgnoreCase).ToArray())ResourceToNpc(player,source,target,type,int.MaxValue);
    }
    internal static void AllNpcToOwner(EntityPlayer player,RebirthNpcStableId source)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);
        foreach(int type in active.NpcTypes(source))NpcToOwner(player,source,type,int.MaxValue);
    }
    internal static void AllNpcToResource(EntityPlayer player,RebirthNpcStableId source,IRemoteResourceSource target)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);
        foreach(int type in active.NpcTypes(source))NpcToResource(player,source,target,type,int.MaxValue);
    }
    internal static void AllNpcToNpc(RebirthNpcStableId source,RebirthNpcStableId target)
    {
        if(active==null)throw new InvalidOperationException();active.Check();
        foreach(int type in active.NpcTypes(source))NpcToNpc(active.actor,source,target,type,int.MaxValue);
    }
    private int[] NpcTypes(RebirthNpcStableId id)
    {
        if(resources.TryGetValue("N:"+id,out var native))return native.View.After.Where(item=>item!=null&&!item.IsEmpty()).Select(item=>item.itemValue.type).Distinct().ToArray();
        return Logical(id).Entries.Select(entry=>ItemClass.GetItem(entry.Key,false)).Where(value=>value!=null&&!value.IsEmpty()).Select(value=>value.type).Distinct().ToArray();
    }
    internal static int CompanionPull(EntityPlayer player,RebirthCompanionListEntry target,int slot,int type,string key,int quantity)
    {
        if(active==null)throw new InvalidOperationException();active.Check(player);if(target==null||quantity<=0||target.IsBoundUndead)return 0;
        if(target.Kind==RebirthCompanionTargetKind.Drone)
        {
            var drone=active.world.GetEntity(target.EntityId) as EntityDrone;if(drone==null)return 0;
            return active.plan.MoveSlot(active.View(new DroneResourceSource(drone)),active.owner,slot,type,quantity);
        }
        if(!RebirthNpcStableId.TryParse(target.StableId,out var id))return 0;
        if(target.IsDog)
        {
            var npc=active.world.GetEntity(target.EntityId) as EntityRebirthNPC;if(npc==null)return 0;
            return active.plan.MoveSlot(active.View(new RemoteNpcResourceSource(npc)),active.owner,slot,type,quantity);
        }
        var value=ItemClass.GetItem(key,false);if(value==null||value.IsEmpty()||value.type!=type)return 0;
        return active.plan.MoveFromNpc(active.Logical(id),active.owner,key,new ItemStack(value,1),quantity,true);
    }
    internal void Complete()
    {
        Check();foreach(var companion in logical.Values)
        {
            if(!ReferenceEquals(world.GetEntity(companion.Entity.entityId),companion.Entity)||companion.Entity.IsDead()||companion.Entity.RebirthRuntimeState?.StableId!=companion.Id||
                !RemoteNpcResourceSource.IsAuthorized(companion.Entity,actor,false,out _)||RebirthNpcInteractionSessionService.IsInventoryAccessing(companion.Id))throw new InvalidDataException("Original logical companion scope changed.");
            var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(companion.Id);if(snapshot?.Quantities==null||snapshot.Revision!=companion.Plan.Revision)throw new InvalidDataException("Original logical companion revision changed.");
            string native=snapshot.NativeStacks?.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)??string.Empty;
            if(native!=companion.Plan.NativeBefore||(snapshot.NativeStacks?.Write().Elements().Count()??0)+companion.Plan.NativeCredits.Count>RebirthNpcNativeStackSet.MaximumRecords)throw new InvalidDataException("Original retained native companion custody changed or lacks capacity.");
            foreach(var entry in companion.Plan.Entries)
            {
                snapshot.Quantities.TryGetValue(entry.Key,out int count);int reserved=0;snapshot.Reservations?.TryGetValue(entry.Key,out reserved);
                if(count!=entry.Before||reserved!=entry.Reserved||(snapshot.NativeStacks?.GetQuantity(entry.Key)??0)!=entry.Custodied||RebirthCompanionInventoryLockService.IsNpcItemLocked(companion.Id,entry.Key)!=entry.Locked)throw new InvalidDataException("Original logical companion quantities/locks changed.");
            }
            if(snapshot.Quantities.Keys.Any(key=>!companion.Plan.Entries.Any(entry=>string.Equals(entry.Key,key,StringComparison.OrdinalIgnoreCase))))throw new InvalidDataException();
            if(snapshot.Reservations?.Keys.Any(key=>!companion.Plan.Entries.Any(entry=>string.Equals(entry.Key,key,StringComparison.OrdinalIgnoreCase)))==true)throw new InvalidDataException();
        }
        foreach(var resource in resources.Values)
        {
            var original=resource.Original;if(!SameObject(original)||original.Source is RemoteNpcResourceSource npc&&!npc.IsSnapshotCurrent())throw new InvalidDataException("Original native endpoint changed.");
            var slots=original.Source.Slots;if(slots?.Length!=resource.View.Before.Length)throw new InvalidDataException();
            for(int i=0;i<slots.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(slots[i],resource.View.Before[i]))throw new InvalidDataException("Native endpoint changed during planning.");
            if(!LockImage(original.Source.SlotLocks,slots.Length).SequenceEqual(resource.View.Locks))throw new InvalidDataException("Native endpoint locks changed.");
        }
        plan.Complete(record,owner);
    }
    public void Dispose(){if(disposed)return;disposed=true;if(ReferenceEquals(active,this))active=null;}
}


