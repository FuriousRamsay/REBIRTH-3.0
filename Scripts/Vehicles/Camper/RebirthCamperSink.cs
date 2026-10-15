using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

internal static class RebirthCamperSink
{
    internal const int CapacityMl=30000;
    internal const string Key="RebirthCamperSinkV1", ReceiptPrefix="rbCamperSink_";
    private sealed class Tank {internal Guid Identity;internal long Revision;internal int Ml;internal float Query;}
    private sealed class Pending
    {
        internal EntityVehicle Vehicle;internal EntityPlayer Player;internal ClientInfo Sender;
        internal RebirthStablePlayerIdentity Owner;internal RebirthCamperSinkFrame Frame;
        internal RebirthCamperTransferJournal Journal;internal RebirthCamperSavedVehicle.Expected Save;
        internal string Directory;internal float Next;
    }
    private sealed class LocalHold {internal EntityPlayerLocal Player;internal EntityVehicle Vehicle;internal Guid Operation;internal int Slot;internal ItemStack Before,After;internal bool Permit;}
    private static ConditionalWeakTable<EntityVehicle,Tank> tanks=new ConditionalWeakTable<EntityVehicle,Tank>();
    private static readonly Dictionary<EntityVehicle,Pending> pending=new Dictionary<EntityVehicle,Pending>();
    private static readonly HashSet<EntityVehicle> preparing=new HashSet<EntityVehicle>();
    private static ConditionalWeakTable<ClientInfo,Dictionary<int,float>> queryGates=new ConditionalWeakTable<ClientInfo,Dictionary<int,float>>();
    private static LocalHold local;
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.camper.sink.v1");
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkPrimary)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkPrompt)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkMutation)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkIngress)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkOwnerActions)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkOtherCustody)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthCamperSinkResourceGate)).Patch();
        ModEvents.GameUpdate.RegisterHandler(Tick);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {pending.Clear();preparing.Clear();tanks=new ConditionalWeakTable<EntityVehicle,Tank>();queryGates=new ConditionalWeakTable<ClientInfo,Dictionary<int,float>>();local=null;}
    private static bool Current(EntityVehicle vehicle)=>ThreadManager.IsMainThread()&&vehicle?.vehicle!=null&&vehicle.world!=null&&!vehicle.IsDead()
        &&ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)&&RebirthCamperRadial.IsCamper(vehicle);
    internal static bool IsHeld(EntityPlayer player)=>local!=null&&ReferenceEquals(local.Player,player)&&ReferenceEquals(player?.world,GameManager.Instance?.World);
    private static bool WorldScope(World world,out Guid identity)
    {
        identity=Guid.Empty;
        return world!=null&&ReferenceEquals(GameManager.Instance?.World,world)
            &&Guid.TryParse(world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world.worldState?.Guid,out identity)
            &&identity!=Guid.Empty;
    }
    private static void ForgetDetachedOwner()
    {
        var player=local?.Player;
        if(player!=null&&(!ReferenceEquals(player.world,GameManager.Instance?.World)
            ||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)
            ||!ReferenceEquals(player.world?.GetEntity(player.entityId),player)))local=null;
    }
    private static bool Admitted(EntityVehicle vehicle,EntityPlayer player,ClientInfo sender=null)
    {
        if(!Current(vehicle)||player==null||player.IsDead()||!ReferenceEquals(vehicle.world,player.world)
            ||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.AttachedToEntity!=null
            ||(player.position-vehicle.position).sqrMagnitude>64f||!RebirthCamperPhysicalTargets.Validate(vehicle,player,"sink"))return false;
        int lockOwner=RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle);
        if(lockOwner>=0&&lockOwner!=player.entityId)return false;
        if(sender==null)return player is EntityPlayerLocal localPlayer&&RebirthCamperRadial.Allowed(vehicle,localPlayer);
        // Native service security evaluated against the authenticated remote user,
        // matching CampDoor.Execute without requiring a UI channel-zero lease.
        return !vehicle.world.IsRemote()&&sender.InternalId!=null&&sender.entityId==player.entityId
            &&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId),sender)
            &&(!vehicle.IsLocked()||!vehicle.hasLock()||(vehicle.vehicle.OwnerId!=null&&vehicle.vehicle.OwnerId.Equals(sender.InternalId))
                ||vehicle.isAllowedUser(sender.InternalId));
    }
    private static Guid Identity(EntityVehicle vehicle)
    {
        if(vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string text)&&Guid.TryParse(text,out var id)&&id!=Guid.Empty)return id;
        if(vehicle.world.IsRemote())return Guid.Empty;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);runtime.Persist(true);return runtime.Identity;
    }
    private static Tank Authority(EntityVehicle vehicle)
    {
        if(!Current(vehicle)||vehicle.world.IsRemote())throw new InvalidOperationException("Authoritative sink required.");
        Guid identity=Identity(vehicle);
        if(tanks.TryGetValue(vehicle,out var prior))
        {if(prior.Identity!=identity)throw new InvalidDataException("Sink identity changed.");return prior;}
        var tank=new Tank{Identity=identity};
        if(vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(Key,out string text))
        {
            var fields=text.Split('|');
            if(fields.Length!=4||fields[0]!="1"||!Guid.TryParse(fields[1],out var saved)||saved!=identity
                ||!long.TryParse(fields[2],NumberStyles.None,CultureInfo.InvariantCulture,out tank.Revision)||tank.Revision<0
                ||!int.TryParse(fields[3],NumberStyles.None,CultureInfo.InvariantCulture,out tank.Ml)||tank.Ml<0||tank.Ml>CapacityMl)
                throw new InvalidDataException("Invalid sink reservoir; retain camper data.");
        }
        tanks.Add(vehicle,tank);return tank;
    }
    internal static bool TryConversion(ItemStack before,out ItemStack after,out int delta)
    {
        after=null;delta=0;
        if(before==null||before.IsEmpty()||before.count<1||before.count>60||before.itemValue?.ItemClass==null)return false;
        string name=before.itemValue.ItemClass.GetItemName(),target;int unit;
        switch(name)
        {
            case "drinkJarRiverWater":
                if(!RebirthConsumableResolver.TryResolve(before.itemValue,out var definition)
                    ||!definition.IsDrink||RebirthLiquidContainerService.GetRemainingMl(before.itemValue,definition)<500f)return false;
                target="drinkJarEmpty";unit=500;break;
            case "drinkJarEmpty":target="drinkJarRiverWater";unit=-500;break;
            case "bucketRiverWater":target="bucketEmpty";unit=10000;break;
            case "bucketEmpty":target="bucketRiverWater";unit=-10000;break;
            default:return false;
        }
        var value=ItemClass.GetItem(target);if(value.IsEmpty())return false;
        delta=checked(unit*before.count);if(Math.Abs(delta)>CapacityMl)return false;
        after=new ItemStack(value,before.count);return true;
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperSinkTransfers"));
    private static string Projection(Tank tank)=>string.Join("|","1",tank.Identity.ToString("N"),tank.Revision.ToString(CultureInfo.InvariantCulture),tank.Ml.ToString(CultureInfo.InvariantCulture));
    private static void Persist(EntityVehicle vehicle,Tank tank)
    {vehicle.vehicle.GetUpdatedItemValue().SetMetadata(Key,Projection(tank));vehicle.SendSyncData(4);VehicleManager.Instance?.TriggerSave();}
    private static void Reply(EntityVehicle vehicle,Tank tank,ClientInfo sender,Guid operation=default(Guid),bool done=false,string error=null)
    {
        if(sender==null){if(operation!=Guid.Empty&&done&&local?.Operation==operation)local=null;if(error!=null)GameManager.ShowTooltip(vehicle.world.GetPrimaryPlayer(),Localization.Get(error));return;}
        if(!WorldScope(vehicle.world,out var world))return;
        sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperSinkReplyV1>().Setup(vehicle.entityId,world,tank.Identity,tank.Revision,tank.Ml,operation,done,error,null));
    }
    internal static bool Primary(Inventory inventory,int action,bool released)
    {
        ForgetDetachedOwner();
        var player=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(player==null||!ReferenceEquals(player.inventory,inventory)||player.IsDead()
            ||player.PlayerUI?.windowManager?.IsInputActive()!=false||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return true;
        if(local!=null&&ReferenceEquals(local.Player,player))return false;
        if(action!=0)return true;
        if(!RebirthCamperPhysicalTargets.TryGetFocused(player,out var vehicle,out var target)||target!="sink")return true;
        // A sink click must not drink, spill or swing the vessel, including rejections.
        if(released||!player.playerInput.Primary.WasPressed||!Admitted(vehicle,player))return false;
        int slot=inventory.holdingItemIdx;var cells=inventory.ItemGrid.items;
        if(slot<0||slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length)||!TryConversion(cells[slot],out _,out _))
        {GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperSinkVessel"));return false;}
        if(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)
            ||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||RebirthCamperEnergyOwnerHold.Blocks(inventory)
            ||RebirthCamperStationNativeActorPayment.Blocks(inventory))return false;
        var operation=Guid.NewGuid();local=new LocalHold{Player=player,Vehicle=vehicle,Operation=operation,Slot=slot,Before=cells[slot].Clone()};
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(string.IsNullOrEmpty(creation)||!WorldScope(player.world,out var world)){local=null;return false;}
        try
        {
            if(player.world.IsRemote())
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperSinkRequestV1>()
                    .Setup(player.entityId,vehicle.entityId,world,creation,operation,slot,RebirthCamperEnergyItems.Write(new[]{local.Before})),false);
            }
            else Receive(player.world,null,player.entityId,vehicle.entityId,world,creation,operation,slot,RebirthCamperEnergyItems.Write(new[]{local.Before}));
        }
        catch{local=null;}
        return false;
    }
    internal static void Prompt(PlayerMoveController controller)
    {
        var player=controller.entityPlayerLocal;
        if(player==null||player.PlayerUI?.windowManager?.IsInputActive()!=false||player.AttachedToEntity!=null
            ||!RebirthCamperPhysicalTargets.TryGetFocused(player,out var vehicle,out var target)||target!="sink")return;
        Tank tank=null;
        try
        {
            if(!vehicle.world.IsRemote())
            {
                tank=Authority(vehicle);
                if(Time.realtimeSinceStartup>=tank.Query){tank.Query=Time.realtimeSinceStartup+1f;Recover(vehicle,player,null);}
            }
            else
            {
                tank=tanks.GetValue(vehicle,ignored=>new Tank());
                if(Time.realtimeSinceStartup>=tank.Query&&WorldScope(player.world,out var world))
                {
                    tank.Query=Time.realtimeSinceStartup+1f;
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperSinkRequestV1>()
                        .Setup(player.entityId,vehicle.entityId,world,RebirthCamperCharacterAccess.CurrentCreation(player),Guid.Empty,-1,Array.Empty<byte>()),false);
                }
            }
            string amount=tank.Identity==Guid.Empty?"…":(tank.Ml/1000f).ToString("0.#",CultureInfo.InvariantCulture);
            string text=string.Format(Localization.Get("xuiRebirthCamperSinkGauge"),amount,"30");
            XUiC_InteractionPrompt.SetText(controller.playerUI,text);controller.strTextLabelPointingTo=text;
        }
        catch{ }
    }
    internal static bool ObserveFocused(EntityPlayerLocal player,out int ml)
    {
        ml=-1;
        if(player==null||player.IsDead()||player.AttachedToEntity!=null||player.PlayerUI?.windowManager?.IsInputActive()!=false
            ||!RebirthCamperPhysicalTargets.TryGetFocused(player,out var vehicle,out var target)||target!="sink")return false;
        try
        {
            if(!vehicle.world.IsRemote()){ml=Authority(vehicle).Ml;return true;}
            var tank=tanks.GetValue(vehicle,ignored=>new Tank());
            if(tank.Identity!=Guid.Empty)ml=tank.Ml;
            return true;
        }
        catch{return false;}
    }
    internal static void Receive(World world,ClientInfo sender,int actor,int entity,Guid worldId,string creation,Guid operation,int slot,byte[] offered)
    {
        if(!installed||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)
            ||!WorldScope(world,out var currentWorld)||currentWorld!=worldId)return;
        var player=world.GetEntity(actor) as EntityPlayer;var vehicle=world.GetEntity(entity) as EntityVehicle;
        if(sender!=null&&(sender.entityId!=actor||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor),sender)))return;
        if(sender==null&&!(player is EntityPlayerLocal))return;
        if(sender!=null&&operation==Guid.Empty)
        {
            var gates=queryGates.GetValue(sender,ignored=>new Dictionary<int,float>());
            if(gates.TryGetValue(entity,out float deadline)&&Time.realtimeSinceStartup<deadline)return;
            if(gates.Count>=128)gates.Clear();gates[entity]=Time.realtimeSinceStartup+1f;
        }
        if(string.IsNullOrEmpty(creation)||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation)return;
        if(!Admitted(vehicle,player,sender))
        {
            if(operation!=Guid.Empty&&Current(vehicle))
                try{Reply(vehicle,Authority(vehicle),sender,operation,true,"xuiRebirthCamperSinkBusy");}catch{ }
            return;
        }
        try
        {
            var tank=Authority(vehicle);
            if(Recover(vehicle,player,sender))
            {Reply(vehicle,tank,sender,operation,true,operation==Guid.Empty?null:"xuiRebirthCamperSinkBusy");return;}
            if(operation==Guid.Empty){Reply(vehicle,tank,sender);return;}
            if(slot<0||slot>=20||offered==null||offered.Length>512*1024||pending.Count+preparing.Count>=64||!preparing.Add(vehicle))
            {Reply(vehicle,tank,sender,operation,true,"xuiRebirthCamperSinkBusy");return;}
            GameManager.Instance.StartCoroutine(Prepare(vehicle,player,sender,worldId,creation,operation,slot,offered));
        }
        catch{if(local?.Operation==operation)local=null;}
    }
    private static IEnumerator Prepare(EntityVehicle vehicle,EntityPlayer player,ClientInfo sender,Guid world,string creation,Guid operation,int slot,byte[] offered)
    {
        Pending prepared=null;string error="xuiRebirthCamperSinkBusy";
        try
        {
            var items=RebirthCamperEnergyItems.Read(offered,1);
            if(items.Length!=1||!TryConversion(items[0],out var after,out int delta))yield break;
            RebirthStablePlayerIdentity owner;
            if(sender==null?!RebirthStablePlayerIdentity.TryFromLocalPlatform(out owner):!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out owner))yield break;
            float deadline=Time.realtimeSinceStartup+10f;bool witnessed=false;
            while(Time.realtimeSinceStartup<deadline&&Admitted(vehicle,player,sender)&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation)
            {
                if(sender==null)
                    witnessed=player is EntityPlayerLocal localPlayer&&slot<RebirthToolbeltCapacity.GetOwnedSlotCount(localPlayer,localPlayer.inventory.ItemGrid.items.Length)
                        &&localPlayer.inventory.holdingItemIdx==slot&&RebirthCamperEnergyItems.Same(localPlayer.inventory.ItemGrid.items[slot],items[0]);
                else if(RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out _))
                    witnessed=slot<image.Belt.Length&&slot<RebirthToolbeltCapacity.GetOwnedSlotCount(player,image.Belt.Length)
                        &&player.inventory.holdingItemIdx==slot&&RebirthCamperEnergyItems.Same(image.Belt[slot],items[0]);
                if(witnessed)break;
                yield return new WaitForSecondsRealtime(.25f);
            }
            if(!witnessed||!Admitted(vehicle,player,sender))yield break;
            var tank=Authority(vehicle);int next=tank.Ml+delta;
            if(next<0||next>CapacityMl){error=next<0?"xuiRebirthCamperSinkInsufficient":"xuiRebirthCamperSinkFull";yield break;}
            var journal=Journal();journal.AssertAvailable(tank.Identity,operation);
            if(journal.Read(operation)!=null)yield break;
            var frame=new RebirthCamperSinkFrame{Operation=operation,Vehicle=tank.Identity,World=world,Actor=player.entityId,Slot=slot,
                BeforeMl=tank.Ml,AfterMl=next,Revision=tank.Revision,Owner=owner.CanonicalId,OwnerKey=owner.StorageKey,Creation=creation,Before=items[0],After=after};
            var bytes=frame.Write();journal.Prepare(operation,tank.Identity,creation,tank.Revision,bytes,bytes);
            prepared=new Pending{Vehicle=vehicle,Player=player,Sender=sender,Owner=owner,Frame=frame,Journal=journal,Directory=GameIO.GetSaveGameDir()};
            pending.Add(vehicle,prepared);SendApply(prepared);
        }
        finally
        {
            preparing.Remove(vehicle);
            if(prepared==null&&Current(vehicle))Reply(vehicle,Authority(vehicle),sender,operation,true,error);
        }
    }
    private static void SendApply(Pending operation)
    {
        if(operation.Player is EntityPlayerLocal localPlayer)Apply(localPlayer,operation.Frame.Write(),operation.Vehicle);
        else operation.Sender?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperSinkReplyV1>()
            .Setup(operation.Vehicle.entityId,operation.Frame.World,operation.Frame.Vehicle,operation.Frame.Revision,operation.Frame.BeforeMl,
                operation.Frame.Operation,false,null,operation.Frame.Write()));
    }
    private static bool Recover(EntityVehicle vehicle,EntityPlayer player,ClientInfo sender)
    {
        if(preparing.Contains(vehicle))return true;
        if(pending.TryGetValue(vehicle,out var retained))
        {
            RebirthStablePlayerIdentity authenticated;
            if((sender==null?RebirthStablePlayerIdentity.TryFromLocalPlatform(out authenticated):RebirthStablePlayerIdentity.TryFromClientInfo(sender,out authenticated))
                &&authenticated.CanonicalId==retained.Frame.Owner&&authenticated.StorageKey==retained.Frame.OwnerKey
                &&RebirthCamperCharacterAccess.CurrentCreation(player)==retained.Frame.Creation)
            {retained.Player=player;retained.Sender=sender;}
            return true;
        }
        var tank=Authority(vehicle);var journal=Journal();var record=journal.FindPending(tank.Identity);if(record==null)return false;
        var frame=RebirthCamperSinkFrame.Read(record.Before);
        if(!record.Before.SequenceEqual(record.After)||frame.Operation!=record.Operation||frame.Vehicle!=tank.Identity
            ||!WorldScope(vehicle.world,out var world)||frame.World!=world
            ||frame.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player))return true;
        RebirthStablePlayerIdentity owner;
        if(sender==null?!RebirthStablePlayerIdentity.TryFromLocalPlatform(out owner):!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out owner))return true;
        if(owner.CanonicalId!=frame.Owner||owner.StorageKey!=frame.OwnerKey)return true;
        var operation=new Pending{Vehicle=vehicle,Player=player,Sender=sender,Owner=owner,Frame=frame,Journal=journal,Directory=GameIO.GetSaveGameDir()};
        pending.Add(vehicle,operation);return true;
    }
    private static bool Saved(Pending operation)
    {
        var frame=operation.Frame;
        // Native owner receipt1 is written only after the exact slot conversion.
        // As in RemoteOwnerApplication, subsequent ordinary owner moves must not
        // undo this paid proof or require reconstructing an old inventory image.
        return RebirthCamperSavedCursor.VerifyPublishedReceipt(frame.Owner,frame.Operation,
            ()=>Current(operation.Vehicle)&&GameIO.GetSaveGameDir()==operation.Directory,ReceiptPrefix);
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        foreach(var operation in pending.Values.ToArray())
        {
            if(Time.realtimeSinceStartup<operation.Next)continue;operation.Next=Time.realtimeSinceStartup+.5f;
            if(!Current(operation.Vehicle)||GameIO.GetSaveGameDir()!=operation.Directory){pending.Remove(operation.Vehicle);continue;}
            try
            {
                var record=operation.Journal.Read(operation.Frame.Operation);var tank=Authority(operation.Vehicle);var frame=operation.Frame;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                {
                    if(!Saved(operation))
                    {
                        if(operation.Player!=null&&!operation.Player.IsDead()&&ReferenceEquals(operation.Vehicle.world.GetEntity(operation.Player.entityId),operation.Player)
                            &&RebirthCamperCharacterAccess.CurrentCreation(operation.Player)==frame.Creation)SendApply(operation);
                        continue;
                    }
                    operation.Journal.Advance(frame.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                }
                if(tank.Revision==frame.Revision&&tank.Ml==frame.BeforeMl)
                {tank.Ml=frame.AfterMl;tank.Revision++;Persist(operation.Vehicle,tank);}
                if(tank.Revision!=frame.Revision+1||tank.Ml!=frame.AfterMl)continue;
                if(operation.Save==null){operation.Save=RebirthCamperSavedVehicle.RequestSave(operation.Vehicle);continue;}
                if(!RebirthCamperSavedVehicle.Verify(operation.Vehicle,operation.Save,()=>Current(operation.Vehicle)&&GameIO.GetSaveGameDir()==operation.Directory))continue;
                operation.Journal.Advance(frame.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                Reply(operation.Vehicle,tank,operation.Sender,frame.Operation,true);pending.Remove(operation.Vehicle);
            }
            catch{ /* Retain immutable receipt and custody; never repeat an unproven conversion. */ }
        }
    }
    private static void PublishOwner(EntityPlayerLocal player)
    {
        if(player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
        else GameManager.Instance.SaveLocalPlayerData();
    }
    internal static void Apply(EntityPlayerLocal player,byte[] bytes,EntityVehicle vehicle)
    {
        try
        {
            ForgetDetachedOwner();
            var frame=RebirthCamperSinkFrame.Read(bytes);
            if(player==null||player.IsDead()||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)
                ||!WorldScope(player.world,out var world)||world!=frame.World||!Current(vehicle)||Identity(vehicle)!=frame.Vehicle
                ||frame.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)
                ||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=frame.Owner||owner.StorageKey!=frame.OwnerKey)return;
            var cells=player.inventory.ItemGrid.items;
            if(frame.Slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length))return;
            string receipt=ReceiptPrefix+frame.Operation.ToString("N");float phase=player.Buffs.GetCustomVar(receipt);
            if(phase==1f){PublishOwner(player);return;}
            if(phase!=0f&&phase!=2f)return;
            if(local!=null&&(local.Player!=player||local.Operation!=frame.Operation||local.Slot!=frame.Slot))return;
            if(local==null)
            {
                if(phase!=2f)return; // reconnect can resume only its persisted original attempted receipt
                if(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)
                    ||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||RebirthCamperEnergyOwnerHold.Blocks(player.inventory)
                    ||RebirthCamperStationNativeActorPayment.Blocks(player.inventory))return;
                local=new LocalHold{Player=player,Operation=frame.Operation,Slot=frame.Slot,Before=frame.Before.Clone(),After=frame.After.Clone()};
            }
            if(phase==0f&&(local.Vehicle==null||Identity(local.Vehicle)!=frame.Vehicle||!Current(local.Vehicle)))return;
            if(!RebirthCamperEnergyItems.Same(local.Before,frame.Before))return;
            local.After=frame.After.Clone();
            if(!RebirthCamperEnergyItems.Same(cells[frame.Slot],frame.Before)&&!RebirthCamperEnergyItems.Same(cells[frame.Slot],frame.After))return;
            if(phase==0f){player.Buffs.SetCustomVar(receipt,2f,true);PublishOwner(player);}
            if(RebirthCamperEnergyItems.Same(cells[frame.Slot],frame.Before))
            {
                local.Permit=true;
                try{player.inventory.SetItem(frame.Slot,frame.After.Clone());}finally{local.Permit=false;}
            }
            if(!RebirthCamperEnergyItems.Same(player.inventory.ItemGrid.items[frame.Slot],frame.After))return;
            player.Buffs.SetCustomVar(receipt,1f,true);PublishOwner(player);
        }
        catch{ }
    }
    internal static bool Mutation(object target,MethodBase method,object[] args)
    {
        var hold=local;
        if(hold==null||!ReferenceEquals(hold.Player?.world,GameManager.Instance?.World))return true;
        if(!ReferenceEquals(target,hold.Player.inventory)&&!ReferenceEquals(target,hold.Player.inventory.ItemGrid))return true;
        return hold.Permit&&(method.Name=="SetItem"||method.Name=="SetSlot")&&args!=null&&args.Length>=2&&args[0] is int slot&&slot==hold.Slot
            &&(args[1] is ItemStack stack?RebirthCamperEnergyItems.Same(stack,hold.After)
                :args.Length>=3&&args[1] is ItemValue item&&args[2] is int count&&count==hold.After.count&&RebirthNativeItemCodec.Encode(item)==RebirthNativeItemCodec.Encode(hold.After.itemValue));
    }
    internal static void ReceiveReply(World world,int entity,Guid worldId,Guid identity,long revision,int ml,Guid operation,bool done,string error,byte[] apply)
    {
        if(!installed||world?.IsRemote()!=true||!WorldScope(world,out var current)||current!=worldId
            ||identity==Guid.Empty||revision<0||ml<0||ml>CapacityMl)return;
        var vehicle=world.GetEntity(entity) as EntityVehicle;if(!Current(vehicle)||Identity(vehicle)!=identity)return;
        var tank=tanks.GetValue(vehicle,ignored=>new Tank());
        if(tank.Identity!=Guid.Empty&&tank.Identity!=identity)return;
        // Presentation may already be newer than a delayed settlement reply.
        // Only the amount projection is monotonic; an exact operation's final
        // acknowledgement still releases its retained owner custody.
        if(revision>=tank.Revision){tank.Identity=identity;tank.Revision=revision;tank.Ml=ml;}
        var player=world.GetPrimaryPlayer() as EntityPlayerLocal;if(player==null)return;
        if(apply!=null&&apply.Length>0)
        {
            RebirthCamperSinkFrame frame;
            try{frame=RebirthCamperSinkFrame.Read(apply);}catch{return;}
            if(frame.Operation!=operation||frame.Vehicle!=identity||frame.World!=worldId)return;
            Apply(player,apply,vehicle);
        }
        if(done&&operation!=Guid.Empty&&local?.Operation==operation)local=null;
        if(error!=null&&!string.IsNullOrEmpty(error))GameManager.ShowTooltip(player,Localization.Get(error));
    }
    internal static string Protect(EntityVehicle vehicle)
    {if(!Current(vehicle)||vehicle.world.IsRemote())return null;var tank=Authority(vehicle);return Projection(tank);}
}

[HarmonyPatch(typeof(Inventory),"Execute",new[]{typeof(int),typeof(bool),typeof(PlayerActionsLocal)})]
internal static class RebirthCamperSinkPrimary
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Inventory __instance,int __0,bool __1)=>RebirthCamperSink.Primary(__instance,__0,__1);
}
[HarmonyPatch(typeof(PlayerMoveController),"HandleInteraction")]
internal static class RebirthCamperSinkPrompt
{[HarmonyPriority(Priority.Last)]private static void Postfix(PlayerMoveController __instance)=>RebirthCamperSink.Prompt(__instance);}
[HarmonyPatch]
internal static class RebirthCamperSinkMutation
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        string[] names={"SetSlot","SetSlots","SetItem","SetItems","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex","ChangeCount","ClearItems","Compact","Sort","CompactSlots","Reset","Clear","CopyFrom","SetSlotLocks","SetLocked"};
        foreach(var type in new[]{typeof(Inventory),typeof(ItemStackGrid)})
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(method.DeclaringType==type&&names.Contains(method.Name))yield return method;
    }
    [HarmonyPriority(Priority.First)]private static bool Prefix(object __instance,MethodBase __originalMethod,object[] __args)=>RebirthCamperSink.Mutation(__instance,__originalMethod,__args);
}
[HarmonyPatch(typeof(EntityVehicle),"ReadSyncData")]
internal static class RebirthCamperSinkIngress
{
    private static void Prefix(EntityVehicle __instance,ushort __1,int __2,out string __state)
    {__state=__2>0&&(__1&4)!=0?RebirthCamperSink.Protect(__instance):null;}
    private static void Finalizer(EntityVehicle __instance,string __state)
    {if(__state!=null)__instance.vehicle.GetUpdatedItemValue().SetMetadata(RebirthCamperSink.Key,__state);}
}
