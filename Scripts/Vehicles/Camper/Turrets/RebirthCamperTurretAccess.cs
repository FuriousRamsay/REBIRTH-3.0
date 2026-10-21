using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

internal static partial class RebirthCamperTurretAccess
{
    internal const string Window="rebirthCamperTurrets";
    private sealed class Session
    {internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal XUiC_RebirthCamperTurrets View;internal Func<bool> Current;internal Action Release;internal Guid Operation,SoundOperation;internal byte[] Before;internal RebirthCamperTurretAction Action;internal int Corner,Option;internal long Revision;internal float Retry;internal bool Waiting,Closing;}
    private sealed class Cached {internal ClientInfo Sender;internal int Vehicle;internal Guid Operation;internal byte[] State,Bag;internal string Status;}
    private sealed class Fence {internal byte[] Before,After;internal MemoryStream Blob;internal int BagCount;internal bool BagAck,BeltAck,CursorAck=true;internal ItemStack CursorAfter;}
    private static readonly Dictionary<int,Cached> results=new Dictionary<int,Cached>();
    private static readonly Dictionary<ClientInfo,Fence> fences=new Dictionary<ClientInfo,Fence>();
    private static Session current;
    internal static bool Pending=>current?.Waiting==true;
    internal static bool PendingMutation=>current?.Waiting==true&&current.Action!=RebirthCamperTurretAction.Query;
    internal static XUiC_RebirthCamperTurrets ViewFor(XUi ui)=>current!=null&&ReferenceEquals(current.Player.PlayerUI?.xui,ui)&&current.Current()?current.View:null;
    internal static Newtonsoft.Json.Linq.JObject ObservePresentation(EntityVehicle vehicle)=>current!=null&&ReferenceEquals(current.Vehicle,vehicle)?current.View.ObservePresentation():null;
    [ThreadStatic] private static bool publishingActor;
    internal static bool BlocksActorMutation(object target)
    {
        var s=current;if(publishingActor||s?.Waiting!=true||s.Action==RebirthCamperTurretAction.Query)return false;
        var p=s.Player;
        return ReferenceEquals(target,p.bag)||ReferenceEquals(target,p.inventory)||ReferenceEquals(target,p.bag.ItemGrid)||
            ReferenceEquals(target,p.inventory.ItemGrid)||ReferenceEquals(target,p.PlayerUI?.xui?.DragAndDropWindow);
    }
    private static void PublishActor(Session session,ItemStack[] items)
    {
        publishingActor=true;
        try
        {
            bool held=RebirthCamperTurretHeldTransfer.IsHeld(session.Action);
            int native=session.Player.bag.ItemGrid.items.Length+session.Player.inventory.ItemGrid.items.Length;
            if(items.Length!=native+(held?1:0))throw new InvalidDataException("Turret actor dimensions changed.");
            RebirthCamperTurretInventory.Publish(session.Player,held?items.Take(native).ToArray():items);
            if(held)session.Player.PlayerUI.xui.DragAndDropWindow.SetCurrentStack(items[native].Clone(),false);
        }
        finally{publishingActor=false;}
    }
    internal static void Install(){ModEvents.GameUpdate.RegisterHandler(Update);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);new Harmony("rebirth.camper.turret.physical").CreateClassProcessor(typeof(RebirthCamperTurretPhysicalInteraction)).Patch();}
    internal static void OpenCamera(EntityVehicle vehicle,EntityPlayerLocal player,int corner)
    {if(corner<0||corner>=4||!CanUse(vehicle,player))return;Open(vehicle,player);current?.View.RequestCamera(corner);}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){current=null;results.Clear();fences.Clear();}
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var s=current;if(s?.Waiting==true&&Time.realtimeSinceStartup>s.Retry&&ReferenceEquals(s.Player.world,GameManager.Instance?.World))Send(s);
    }
    internal static void Open(EntityVehicle vehicle,EntityPlayerLocal player)
    {
        var ui=LocalPlayerUI.GetUIForPlayer(player);
        if(!CanUse(vehicle,player)||ui==null||!ui.windowManager.TryGetWindow(Window,out GUIWindow window))throw new InvalidOperationException("Turret management lease/window missing.");
        var view=(window as XUiWindowGroup)?.Controller.GetChildByType<XUiC_RebirthCamperTurrets>();if(view==null)throw new InvalidOperationException("Turret management controller missing.");
        
        var s=new Session{Vehicle=vehicle,Player=player,View=view,Release=()=>{}};
        s.Current=()=>ReferenceEquals(current,s)&&CanUse(vehicle,player);
        current=s;
        view.BindCamera(vehicle,(c,t,y,p,f)=>Manual(s,c,t,y,p,f),(c,t,a)=>Control(s,c,t,a));
        view.Bind(s.Current,()=>Close(s),(a,c,o)=>Command(s,a,c,o));
        ui.windowManager.Open(window,true,false);Command(s,RebirthCamperTurretAction.Query,0,0);
    }
    private static void Manual(Session s,int corner,Guid token,float yaw,float pitch,bool fire)
    {
        if(!ReferenceEquals(current,s)||!s.Current())return;
        if(s.Player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretManual>().Setup(s.Player.entityId,s.Vehicle.entityId,corner,token,yaw,pitch,fire),false);
        else if(!RebirthCamperTurretRuntime.For(s.Vehicle).ManualInput(s.Player,corner,token,yaw,pitch,fire))s.View.ControlResult(token,corner,false);
    }
    private static void Close(Session s){if(!ReferenceEquals(current,s))return;if(s.Waiting){s.Closing=true;return;}current=null;s.Release();}
    private static void Command(Session s,RebirthCamperTurretAction action,int corner,int option)
    {
        if(s.Waiting||!s.Current())return;
        if(!s.Player.PlayerUI.xui.DragAndDropWindow.CurrentStack.IsEmpty()&&action!=RebirthCamperTurretAction.Query&&!RebirthCamperTurretHeldTransfer.IsHeld(action)){s.View.Status("xuiCamperTurretEmptyCursor");return;}
        s.Before=action==RebirthCamperTurretAction.Query?Array.Empty<byte>():RebirthCamperInventorySnapshot.Capture(RebirthCamperTurretHeldTransfer.Capture(s.Player,action));s.Revision=s.View.Revision;s.Operation=Guid.NewGuid();s.Action=action;s.Corner=corner;s.Option=option;s.Waiting=true;s.View.Wait(true);
        if(s.Player.world.IsRemote())
        {
            // Ordered native actor publication precedes this bounded command.
            if(action!=RebirthCamperTurretAction.Query)SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(s.Player),false);Send(s);
        }
        else ExecuteLocal(s);
    }
    private static void Send(Session s)
    {s.Retry=Time.realtimeSinceStartup+2;SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretCommand>().Setup(s.Player.entityId,s.Vehicle.entityId,s.Operation,s.Action,s.Corner,s.Option,s.Revision,s.Before),false);}
    private static void ExecuteLocal(Session s)
    {
        try
        {
            var state=RebirthCamperTurretRuntime.For(s.Vehicle).Observe();var bag=s.Action==RebirthCamperTurretAction.Query?null:RebirthCamperInventorySnapshot.Restore(s.Before);string error=null;
            if(s.Action!=RebirthCamperTurretAction.Query)
            {
                if(RebirthCamperTurretRuntime.For(s.Vehicle).ControlOwner(s.Corner)>=0)error="xuiCamperTurretBusy";else if(state.Revision!=s.Revision)error="xuiCamperTurretChanged";
                else if(!RebirthVehicleInventoryWrite.CanMutate(s.Player,true)||!RebirthVehicleInventoryWrite.CanMutate(s.Player,false))error="xuiCamperTurretChanged";else{var candidate=state.Copy();error=RebirthCamperTurretInventory.Apply(s.Player,candidate,bag,s.Player.bag.ItemGrid.items.Length,RebirthToolbeltCapacity.GetOwnedSlotCount(s.Player,s.Player.inventory.ItemGrid.items.Length),s.Action,s.Corner,s.Option,out var debit);if(error==null){candidate.Encode();debit?.Invoke();PublishActor(s,bag);RebirthCamperTurretRuntime.For(s.Vehicle).Changed(candidate);state=candidate;}}
            }
            if(error==null&&bag!=null)CommittedSound(s,bag);
            s.Waiting=false;s.View.Present(state,error);if(s.Closing)Close(s);
        }
        catch(Exception e){s.Waiting=false;s.View.Wait(false);s.View.Status("xuiCamperTurretInvalid");if(s.Closing)Close(s);Log.Warning("[CamperTurrets] Management refused: "+e.Message);}
    }
    internal static void Receive(World world,ClientInfo sender,int actor,int entity,Guid operation,RebirthCamperTurretAction action,int corner,int option,long revision,byte[] before)
    {
        if(world==null||world.IsRemote()||sender?.entityId!=actor||operation==Guid.Empty||!Enum.IsDefined(typeof(RebirthCamperTurretAction),action))return;
        var player=world.GetEntity(actor) as EntityPlayer;var vehicle=world.GetEntity(entity) as EntityVehicle;
        if((!Authenticated(world,sender,player)||!CanUse(vehicle,player)))
        {sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretResult>().Setup(entity,operation,Array.Empty<byte>(),Array.Empty<byte>(),"xuiCamperTurretInvalid"));return;}
        if(results.TryGetValue(actor,out var cached)&&ReferenceEquals(cached.Sender,sender)&&cached.Vehicle==entity&&cached.Operation==operation){Reply(cached);return;}
        try
        {
            var state=RebirthCamperTurretRuntime.For(vehicle).Observe();byte[] bagResult=Array.Empty<byte>();string error=null;
            if(action!=RebirthCamperTurretAction.Query)
            {
                var native=sender.latestPlayerData;var combined=RebirthCamperTurretInventory.Uploaded(native);int bagCount=RebirthGearPlayerDataInventory.ReadSlots(native,true).Length;
                bool held=RebirthCamperTurretHeldTransfer.IsHeld(action);var actorImage=held?combined.Concat(new[]{native.dragAndDropItem?.Clone()??ItemStack.Empty.Clone()}).ToArray():combined;
                var actual=RebirthCamperInventorySnapshot.Capture(actorImage);
                if(RebirthCamperTurretRuntime.For(vehicle).ControlOwner(corner)>=0)error="xuiCamperTurretBusy";else if(state.Revision!=revision||!actual.SequenceEqual(before))error="xuiCamperTurretChanged";
                else
                {
                    var candidate=state.Copy();var items=RebirthCamperInventorySnapshot.Restore(actual);
                    error=RebirthCamperTurretInventory.Apply(player,candidate,items,bagCount,RebirthToolbeltCapacity.GetOwnedSlotCount(player,combined.Length-bagCount),action,corner,option,out var debit);
                    if(error==null)
                    {
                        candidate.Encode();var blob=RebirthCamperTurretInventory.Rewrite(native.bagData,true,items.Take(bagCount).ToArray());var beltBlob=RebirthCamperTurretInventory.Rewrite(native.inventoryData,false,items.Skip(bagCount).Take(combined.Length-bagCount).ToArray());bagResult=RebirthCamperInventorySnapshot.Capture(items);debit?.Invoke();
                        sender.latestPlayerData.bagData=blob;sender.latestPlayerData.inventoryData=beltBlob;sender.latestPlayerData.bModifiedSinceLastSave=true;
                        // Remote EntityPlayer inventories are replicas; native uploaded grids are authoritative here.
                        if(held)sender.latestPlayerData.dragAndDropItem=items[items.Length-1].Clone();
                        fences[sender]=new Fence{Before=RebirthCamperInventorySnapshot.Capture(combined),After=RebirthCamperInventorySnapshot.Capture(items.Take(combined.Length).ToArray()),Blob=blob,BagCount=bagCount,CursorAck=!held,CursorAfter=held?items[items.Length-1].Clone():null};
                        RebirthCamperTurretRuntime.For(vehicle).Changed(candidate);state=candidate;
                        VehicleManager.Instance?.TriggerSave();
                    }
                }
            }
            var result=new Cached{Sender=sender,Vehicle=entity,Operation=operation,State=state.Encode(),Bag=bagResult,Status=error??""};results[actor]=result;Reply(result);
        }
        catch(Exception e){Log.Warning("[CamperTurrets] Remote management refused: "+e.Message);sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretResult>().Setup(entity,operation,Array.Empty<byte>(),Array.Empty<byte>(),"xuiCamperTurretInvalid"));}
    }
    private static void Reply(Cached c)=>c.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretResult>().Setup(c.Vehicle,c.Operation,c.State,c.Bag,c.Status));
    internal static void Present(int entity,Guid operation,byte[] state,byte[] bag,string status)
    {
        var s=current;if(s==null||s.Vehicle.entityId!=entity||s.Operation!=operation||!ReferenceEquals(s.Player.world,GameManager.Instance?.World))return;
        if(bag.Length>0)
        {
            var now=RebirthCamperInventorySnapshot.Capture(RebirthCamperTurretHeldTransfer.Capture(s.Player,s.Action));
            // The native actor grids are held while this acknowledged gesture is pending.
            // Committed transfer slots are authoritative; preserve all unrelated cells.
            var before=RebirthCamperInventorySnapshot.Restore(s.Before);var after=RebirthCamperInventorySnapshot.Restore(bag);var incoming=RebirthCamperInventorySnapshot.Restore(now);
            if(!Merge(before,after,incoming,out _)){s.View.Status("xuiCamperTurretChanged");return;}
            PublishActor(s,incoming);
            if(string.IsNullOrEmpty(status)&&state.Length>0)CommittedSound(s,after);
        }
        s.Waiting=false;
        if(state.Length>0)s.View.Present(RebirthCamperTurretState.Decode(state),status);else{s.View.Wait(false);s.View.Status(status);}
        if(RebirthCamperTurretHeldTransfer.IsHeld(s.Action))SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(s.Player),false);
        else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(s.Player,true,true,false,false),false);
        if(s.Closing||!s.Current())Close(s);
    }
    private static void CommittedSound(Session session,ItemStack[] after)
    {
        if(session.Action==RebirthCamperTurretAction.Query||session.Operation==session.SoundOperation||session.Before==null)return;
        session.SoundOperation=session.Operation;
        RebirthCamperCommittedItemSound.Play(session.Player,RebirthCamperInventorySnapshot.Restore(session.Before),after,Window);
    }
    private static Bag ReadBag(MemoryStream stream,EntityPlayer player)
    {
        if(stream==null||stream.Length>131072)throw new InvalidDataException("Actor bag not available.");
        using(var copy=new MemoryStream(stream.ToArray()))using(var r=MemoryPools.poolBinaryReader.AllocSync(false))
        {r.SetBaseStream(copy);return Bag.Read(r,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,player);}
    }
    private static MemoryStream WriteBag(Bag bag)
    {var result=new MemoryStream();using(var w=MemoryPools.poolBinaryWriter.AllocSync(false)){w.SetBaseStream(result);bag.Write(w,StreamModeWrite.Persistency);w.Flush();}result.Position=0;return result;}
    // Native client inventory updates can already be queued behind a command.
    // Preserve unrelated cells and fence committed cells until the client acknowledges its result.
    internal static void FilterBag(ClientInfo sender,ref MemoryStream blob,bool isBag=true)
    {
        if(sender==null||blob==null||!fences.TryGetValue(sender,out var f))return;
        var file=new PlayerDataFile();if(isBag)file.bagData=blob;else file.inventoryData=blob;
        var cells=RebirthGearPlayerDataInventory.ReadSlots(file,isBag);if(cells==null)return;
        var allBefore=RebirthCamperInventorySnapshot.Restore(f.Before);var allAfter=RebirthCamperInventorySnapshot.Restore(f.After);
        var before=isBag?allBefore.Take(f.BagCount).ToArray():allBefore.Skip(f.BagCount).ToArray();
        var after=isBag?allAfter.Take(f.BagCount).ToArray():allAfter.Skip(f.BagCount).ToArray();
        if(Merge(before,after,cells,out bool already)){if(!already)blob=RebirthCamperTurretInventory.Rewrite(blob,isBag,cells);else{if(isBag)f.BagAck=true;else f.BeltAck=true;if(f.BagAck&&f.BeltAck&&f.CursorAck)fences.Remove(sender);}}
    }
    internal static void FilterCursor(ClientInfo sender,PlayerDataFile data)
    {
        if(sender==null||data==null||!fences.TryGetValue(sender,out var f)||f.CursorAck)return;
        if(RebirthCamperEnergyItems.Same(data.dragAndDropItem,f.CursorAfter)){f.CursorAck=true;if(f.BagAck&&f.BeltAck)fences.Remove(sender);}
        else data.dragAndDropItem=f.CursorAfter.Clone();
    }
    private static bool Merge(ItemStack[] before,ItemStack[] after,ItemStack[] incoming,out bool already)
    {
        already=true;if(before.Length!=after.Length||before.Length!=incoming.Length)return false;
        var changes=new List<int>();
        for(int i=0;i<before.Length;i++)
        {
            if(RebirthCamperEnergyItems.Same(before[i],after[i]))continue;
            if(RebirthCamperEnergyItems.Same(incoming[i],after[i]))continue;
            // A delayed upload may conflict with a committed cell. Never admit its old item.
            // The pending actor hold prevents new local edits to these committed cells.
            already=false;changes.Add(i);
        }
        foreach(int i in changes)incoming[i]=after[i].Clone();return true;
    }
}
[HarmonyPatch(typeof(NetPackagePlayerInventory),nameof(NetPackagePlayerInventory.ProcessPackage))]
internal static class RebirthCamperTurretInventoryFence
{static void Prefix(NetPackagePlayerInventory __instance){var blob=__instance.bagData;RebirthCamperTurretAccess.FilterBag(__instance.Sender,ref blob);__instance.bagData=blob;var belt=__instance.inventoryData;RebirthCamperTurretAccess.FilterBag(__instance.Sender,ref belt,false);__instance.inventoryData=belt;}}
[HarmonyPatch(typeof(NetPackagePlayerData),nameof(NetPackagePlayerData.ProcessPackage))]
internal static class RebirthCamperTurretPlayerFence
{static void Prefix(NetPackagePlayerData __instance){var data=__instance.playerDataFile;if(data==null)return;var blob=data.bagData;RebirthCamperTurretAccess.FilterBag(__instance.Sender,ref blob);data.bagData=blob;var belt=data.inventoryData;RebirthCamperTurretAccess.FilterBag(__instance.Sender,ref belt,false);data.inventoryData=belt;RebirthCamperTurretAccess.FilterCursor(__instance.Sender,data);}}
[Preserve]
public sealed class NetPackageRebirthCamperTurretCommand:NetPackage
{
    int actor,entity,corner,option;Guid operation;RebirthCamperTurretAction action;long revision;byte[] before;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperTurretCommand Setup(int a,int e,Guid op,RebirthCamperTurretAction act,int c,int o,long r,byte[] b){actor=a;entity=e;operation=op;action=act;corner=c;option=o;revision=r;before=b;return this;}
    public override void read(PooledBinaryReader r){actor=r.ReadInt32();entity=r.ReadInt32();operation=new Guid(r.ReadBytes(16));action=(RebirthCamperTurretAction)r.ReadByte();corner=r.ReadByte();option=r.ReadInt32();revision=r.ReadInt64();int n=r.ReadInt32();if(n<0||n>131072)throw new InvalidDataException();before=r.ReadBytes(n);if(before.Length!=n)throw new EndOfStreamException();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(actor);w.Write(entity);w.Write(operation.ToByteArray());w.Write((byte)action);w.Write((byte)corner);w.Write(option);w.Write(revision);w.Write(before.Length);w.Write(before);}
    public override void ProcessPackage(World world,GameManager callbacks){if(ValidEntityIdForSender(actor))RebirthCamperTurretAccess.Receive(world,Sender,actor,entity,operation,action,corner,option,revision,before);}
    public int GetLength()=>39+(before?.Length??0);
}
[Preserve]
public sealed class NetPackageRebirthCamperTurretResult:NetPackage
{
    int entity;Guid operation;byte[] state,bag;string status;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperTurretResult Setup(int e,Guid op,byte[] s,byte[] b,string text){entity=e;operation=op;state=s;bag=b;status=text;return this;}
    static byte[] Frame(PooledBinaryReader r){int n=r.ReadInt32();if(n<0||n>131072)throw new InvalidDataException();var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
    public override void read(PooledBinaryReader r){entity=r.ReadInt32();operation=new Guid(r.ReadBytes(16));state=Frame(r);bag=Frame(r);status=r.ReadString();if(status.Length>128)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(entity);w.Write(operation.ToByteArray());w.Write(state.Length);w.Write(state);w.Write(bag.Length);w.Write(bag);w.Write(status);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthCamperTurretAccess.Present(entity,operation,state,bag,status);}
    public int GetLength()=>30+(state?.Length??0)+(bag?.Length??0)+(status?.Length??0);
}

