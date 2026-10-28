using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Scripting;

// Native command entry only: adapter setters/restoration never become manual
// input. The adapter service owns selection, eligibility, proof and power.
internal static class RebirthCamperPoiLightInteraction
{
    private static bool installed;
    private const string OnKey="xuiRebirthCamperPoiLightOn";
    private const string OffKey="xuiRebirthCamperPoiLightOff";
    private const string UnavailableKey="xuiRebirthCamperPoiLightPowerUnavailable";
    private const string DeniedKey="xuiRebirthCamperPoiLightDenied";

    internal static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.camper.poi.manual-light-input");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperPoiLightCommands));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperPoiLightActivate));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperPoiLightPrompt));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperPoiLightRadial));
        installed=true;
    }
    internal static IEnumerable<MethodBase> Targets(string name,Type[] signature)
    {
        foreach(var type in new[]{typeof(BlockLight),typeof(BlockPoweredLight)})
        {
            var method=AccessTools.DeclaredMethod(type,name,signature);
            if(method==null)throw new MissingMethodException(type.FullName,name);
            yield return method;
        }
    }
    internal static bool Managed(WorldBase world,Vector3i position,BlockValue expected,EntityPlayer actor,out EntityVehicle vehicle)
    {
        vehicle=null;
        if(!ThreadManager.IsMainThread()||world==null||world.IsEditor()||actor==null||
            !ReferenceEquals(actor.world,world)||RebirthPlacedOriginal.IsPendingTarget(world,position))return false;
        var current=world.GetBlock(position);
        if(current.type!=expected.type||current.rotation!=expected.rotation||
            !(current.Block is BlockLight)&&!(current.Block is BlockPoweredLight))return false;
        return RebirthCamperEnergyDeviceAdapter.TryPoiLightInteraction(actor,position,out vehicle);
    }
    internal static bool Active(EntityVehicle vehicle)
        =>RebirthCamperEnergyDeviceAdapter.TryObservePoiOn(vehicle,out bool on)&&on;
    internal static void Expose(WorldBase world,Vector3i position,BlockValue value,EntityPlayer actor,ref BlockActivationCommand[] commands)
    {
        if(commands==null||!Managed(world,position,value,actor,out var vehicle))return;
        // Remove the switch action; preserve native pickup eligibility and command order.
        var filtered=new List<BlockActivationCommand>(commands.Length);
        foreach(var command in commands)if(command.text!="light")filtered.Add(command);
        // Native SetCommonData executes a single entry immediately. Keep the
        // native pickup/cancel hold menu when ON, so short E can be special
        // without consuming the only pickup entry before the radial opens.
        int enabled=0;bool take=false,cancel=false;
        foreach(var command in filtered)
        {
            if(command.enabled){enabled++;take=command.text=="take";}
            if(command.text=="cancel")cancel=true;
        }
        if(enabled==1&&take&&Active(vehicle))
        {
            if(cancel)
            {
                for(int i=0;i<filtered.Count;i++)if(filtered[i].text=="cancel")
                {var command=filtered[i];command.enabled=true;filtered[i]=command;break;}
            }
            else filtered.Add(new BlockActivationCommand("cancel","x",true));
            commands=filtered.ToArray();return;
        }
        if(filtered.Count!=commands.Length||commands.Length!=0&&Array.Exists(commands,c=>c.text=="light"))
            commands=filtered.ToArray();
    }
    internal static void Prompt(WorldBase world,BlockValue value,Vector3i position,EntityAlive actor,ref string text)
    {
        if(!Managed(world,position,value,actor as EntityPlayer,out _))return;
        text=string.Empty;
        var commands=value.Block.GetBlockActivationCommands(world,value,position,actor);
        if(commands==null)return;
        foreach(var command in commands)
            if(command.enabled&&command.text=="take")
            {text=string.Format(Localization.Get("pickupPrompt"),value.Block.GetLocalizedBlockName());return;}
    }
    internal static void Radial(XUiC_Radial radial,ref XUiC_Radial.CommandHandlerDelegate handler,
        XUiC_Radial.RadialContextAbs context,ref bool special)
    {
        // Only native block activation: never replace another system's special action.
        if(special||handler==null||!ReferenceEquals(handler.Target,radial)||
            handler.Method.DeclaringType!=typeof(XUiC_Radial)||handler.Method.Name!="handleBlockCommand"||
            !(context is XUiC_Radial.RadialContextBlock block))return;
        var actor=block.EntityFocusing;
        if(!Managed(actor?.world,block.BlockPos,block.BlockValue,actor,out var vehicle)||!Active(vehicle))return;
        var native=handler;
        handler=(ui,index,current)=>
        {
            if(index>=0){native(ui,index,current);return;}
            if(index!=-1&&index!=-2)return;
            var world=actor?.world;
            if(world==null||!ReferenceEquals(GameManager.Instance?.World,world)||
                !ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.IsDead()||
                RebirthPlacedOriginal.IsPendingTarget(world,block.BlockPos))return;
            var value=world.GetBlock(block.BlockPos);
            if(value.type!=block.BlockValue.type||value.rotation!=block.BlockValue.rotation)return;
            if(Managed(world,block.BlockPos,block.BlockValue,actor,out var now)&&Active(now))
            {Request(actor,now,block.BlockPos,block.BlockValue);return;}
            // A released E after POI was switched OFF retains the native pickup path.
            // Native/common-pickup handlers still perform their full authorization.
            if(block.Commands==null)return;
            for(int i=0;i<block.Commands.Length;i++)
                if(block.Commands[i].enabled&&block.Commands[i].text=="take")
                {native(ui,i,current);return;}
        };
        special=true;
    }
    internal static void Request(EntityPlayerLocal actor,EntityVehicle vehicle,Vector3i position,BlockValue expected)
    {
        if(actor==null||!Active(vehicle)){Notify(actor,DeniedKey);return;}
        if(expected.type<0||expected.type>ushort.MaxValue){Notify(actor,DeniedKey);return;}
        var world=actor.world;
        var current=world.GetBlock(position);
        if(current.type!=expected.type||current.rotation!=expected.rotation){Notify(actor,DeniedKey);return;}
        var tile=world.GetTileEntity(position);
        uint stamp=tile==null?0:RebirthCamperWorldLighting.Stamp(tile);
        if(!world.IsRemote())
        {
            bool on=DesiredManualState(world,position);
            bool accepted=RebirthCamperEnergyDeviceAdapter.SetManualPoiLight(actor,vehicle,position,on,
                (ushort)expected.type,expected.rotation,stamp,out string status);
            Notify(actor,StatusKey(accepted,on,status));
            return;
        }
        try
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection==null||!connection.IsClient||connection.IsServer){Notify(actor,DeniedKey);return;}
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperPoiLightRequestV1>().Setup(
                actor.entityId,vehicle.entityId,position,(ushort)expected.type,expected.rotation,stamp),false);
        }
        catch(Exception){Notify(actor,DeniedKey);}
    }
    internal static void Receive(World world,ClientInfo sender,int actorId,int vehicleId,Vector3i position,
        ushort block,byte rotation,uint stamp)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,world)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return;
        var actor=world.GetEntity(actorId) as EntityPlayer;
        if(sender==null||!RebirthCamperTurretAccess.Authenticated(world,sender,actor))return;
        var requested=world.GetEntity(vehicleId) as EntityVehicle;
        bool accepted=false,on=false;
        string status=DeniedKey;
        if(requested!=null&&Active(requested)&&!RebirthPlacedOriginal.IsPendingTarget(world,position)&&
            RebirthCamperEnergyDeviceAdapter.TryPoiLightInteraction(actor,position,out var current)&&ReferenceEquals(current,requested))
        {
            on=DesiredManualState(world,position);
            accepted=RebirthCamperEnergyDeviceAdapter.SetManualPoiLight(actor,requested,position,on,block,rotation,stamp,out status);
        }
        sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperPoiLightResultV1>().Setup(actorId,StatusKey(accepted,on,status)));
    }
    // Toggle the durable manual choice, not just illumination. A pinned ON
    // light may be physically dark while its bank is off or lacks reserve.
    // Remote callers reach this read only after sender/current-target admission.
    private static bool DesiredManualState(World world,Vector3i position)
    {
        var tile=world.GetTileEntity(position);
        var choice=RebirthCamperPoiLightChoices.Read(world,tile);
        if(choice==RebirthCamperPoiLightChoice.On)return false;
        if(choice==RebirthCamperPoiLightChoice.Off)return true;
        return tile is TileEntityPoweredBlock powered?!powered.IsToggled:(world.GetBlock(position).meta&2)==0;
    }
    private static string StatusKey(bool accepted,bool on,string status)
    {
        if(!accepted)return DeniedKey;
        if(status==OnKey||status==OffKey||status==UnavailableKey)return status;
        return on?OnKey:OffKey;
    }
    private static void Notify(EntityPlayerLocal actor,string key)
    {if(actor!=null&&!actor.IsDead())GameManager.ShowTooltip(actor,Localization.Get(key));}
    internal static void Feedback(World world,int actorId,byte result)
    {
        if(world?.IsRemote()!=true)return;
        var actor=world.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor?.entityId!=actorId||!ReferenceEquals(actor.world,world))return;
        Notify(actor,result==1?OnKey:result==2?OffKey:result==3?UnavailableKey:DeniedKey);
    }
    internal static byte ResultCode(string status)=>status==OnKey?(byte)1:status==OffKey?(byte)2:status==UnavailableKey?(byte)3:(byte)4;
}

[HarmonyPatch]
internal static class RebirthCamperPoiLightCommands
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperPoiLightInteraction.Targets("GetBlockActivationCommands",
        new[]{typeof(WorldBase),typeof(BlockValue),typeof(Vector3i),typeof(EntityAlive)});
    [HarmonyAfter("rebirth.fresh.blockpickup"),HarmonyPriority(Priority.Last)]
    private static void Postfix(WorldBase __0,BlockValue __1,Vector3i __2,EntityAlive __3,ref BlockActivationCommand[] __result)
        =>RebirthCamperPoiLightInteraction.Expose(__0,__2,__1,__3 as EntityPlayer,ref __result);
}

[HarmonyPatch]
internal static class RebirthCamperPoiLightActivate
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperPoiLightInteraction.Targets("OnBlockActivated",
        new[]{typeof(string),typeof(WorldBase),typeof(Vector3i),typeof(BlockValue),typeof(EntityPlayerLocal)});
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(string __0,WorldBase __1,Vector3i __2,BlockValue __3,EntityPlayerLocal __4,ref bool __result)
    {
        if(__0!="light"||__1==null||__1.IsEditor())return true;
        // Keep the existing highest-priority placement gate, including if its
        // original-method skip means later stateful prefixes are not invoked.
        if(RebirthPlacedOriginal.IsPendingTarget(__1,__2)){__result=false;return false;}
        if(!RebirthCamperPoiLightInteraction.Managed(__1,__2,__3,__4,out var vehicle))return true;
        if(!RebirthCamperPoiLightInteraction.Active(vehicle)){__result=false;return false;}
        __result=true;
        RebirthCamperPoiLightInteraction.Request(__4,vehicle,__2,__3);
        return false; // A rejected authoritative request must not fall through and toggle natively.
    }
}

[HarmonyPatch]
internal static class RebirthCamperPoiLightPrompt
{
    private static IEnumerable<MethodBase> TargetMethods()=>RebirthCamperPoiLightInteraction.Targets("GetActivationText",
        new[]{typeof(WorldBase),typeof(BlockValue),typeof(Vector3i),typeof(EntityAlive)});
    [HarmonyAfter("rebirth.fresh.blockpickup"),HarmonyPriority(Priority.Last)]
    private static void Postfix(WorldBase __0,BlockValue __1,Vector3i __2,EntityAlive __3,ref string __result)
        =>RebirthCamperPoiLightInteraction.Prompt(__0,__1,__2,__3,ref __result);
}

[HarmonyPatch(typeof(XUiC_Radial),"SetCommonData")]
internal static class RebirthCamperPoiLightRadial
{
    private static void Prefix(XUiC_Radial __instance,ref XUiC_Radial.CommandHandlerDelegate __1,
        XUiC_Radial.RadialContextAbs __2,ref bool __4)
        =>RebirthCamperPoiLightInteraction.Radial(__instance,ref __1,__2,ref __4);
}

// The native HasBlockActivationCommands on both exact classes already returns
// true in installed 3.4; no redundant Has patch or global runtime-switch edit.

[Preserve]
public sealed class NetPackageRebirthCamperPoiLightRequestV1:NetPackage
{
    private int actor,vehicle;private Vector3i position;private ushort block;private byte rotation;private uint stamp;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperPoiLightRequestV1 Setup(int a,int v,Vector3i p,ushort b,byte r,uint s)
    {actor=a;vehicle=v;position=p;block=b;rotation=r;stamp=s;return this;}
    public override void read(PooledBinaryReader r)
    {if(r.ReadByte()!=1)throw new InvalidDataException("POI light request version changed.");actor=r.ReadInt32();vehicle=r.ReadInt32();
        position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());block=r.ReadUInt16();rotation=r.ReadByte();stamp=r.ReadUInt32();}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)1);w.Write(actor);w.Write(vehicle);w.Write(position.x);w.Write(position.y);w.Write(position.z);w.Write(block);w.Write(rotation);w.Write(stamp);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(ValidEntityIdForSender(actor))RebirthCamperPoiLightInteraction.Receive(world,Sender,actor,vehicle,position,block,rotation,stamp);}
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthCamperPoiLightResultV1:NetPackage
{
    private int actor;private byte result;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperPoiLightResultV1 Setup(int a,string status)
    {actor=a;result=RebirthCamperPoiLightInteraction.ResultCode(status);return this;}
    public override void read(PooledBinaryReader r)
    {if(r.ReadByte()!=1)throw new InvalidDataException("POI light result version changed.");actor=r.ReadInt32();result=r.ReadByte();if(result<1||result>4)throw new InvalidDataException("POI light result invalid.");}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((byte)1);w.Write(actor);w.Write(result);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthCamperPoiLightInteraction.Feedback(world,actor,result);
    public int GetLength()=>0;
}
