using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine.Scripting;

// Physical targets select existing installations; native channel zero owns the UI.
internal static class RebirthCamperPhysicalInteraction
{
    private sealed class Intent
    {
        internal EntityVehicle Vehicle; internal EntityPlayerLocal Player;
        internal string Target,Action; internal Guid Token,Selection; internal long Revision;
        internal float Expires; internal Func<bool> NativeCurrent;
    }
    private sealed class Admission
    {
        internal EntityVehicle Vehicle; internal EntityPlayer Player; internal ClientInfo Sender;
        internal string Target,Creation; internal Guid Selection; internal long Revision; internal float Expires;
    }
    private static readonly ConditionalWeakTable<EntityPlayerLocal,Intent> pending=new ConditionalWeakTable<EntityPlayerLocal,Intent>();
    private static readonly Dictionary<int,Admission> admissions=new Dictionary<int,Admission>();
    private static bool installed;
    private static readonly Dictionary<string,string> blocks=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        {"forge","forge"},{"campfire","campfire"},{"workbench","workbench"},{"cement_mixer","cementMixer"},
        {"iron_oven","WorkbenchIronOven001_FR"},{"circular_saw","WorkbenchCircularMachine001_FR"},
        {"outfit_designer","SonjaOutfitDesigner"},{"recycling_station","SonjaRecyclerStation"},
        {"shredder","WorkbenchShredder001_FR"},{"ammo_recycler","SonjaAmmoRecyclerStation"},
        {"gas_stovetop","WorkbenchGasStove001_FR"},{"mortar_pestle","WorkbenchMortarPestle001_FR"},
        {"toolbox","WorkbenchToolbox001_FR"},{"chemistry_station","chemistryStation"},
        {"distiller","WorkbenchDistiller001_FR"},{"research_table","WorkbenchResearchTable001_FR"}
    };
    internal static void Install()
    {
        if(installed)return;
        new Harmony("rebirth.camper.physical.interaction").Patch(AccessTools.Method(typeof(PlayerMoveController),"HandleInteraction"),
            prefix:new HarmonyMethod(typeof(RebirthCamperPhysicalInteraction),nameof(HandleInteraction)),
            postfix:new HarmonyMethod(typeof(RebirthCamperPhysicalInteraction),nameof(ShowPhysicalPrompt)));
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;
    }
    private static void HandleInteraction(PlayerMoveController __instance,ref bool __7,out string __state)
    {
        __state=null;
        var player=__instance.entityPlayerLocal;
        if(player==null||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            ||player.AttachedToEntity!=null||player.TPCameraCheckResult==eTPCameraCheckResult.LineOfSightCheckFailed
            ||!RebirthCamperPhysicalTargets.TryGetFocused(player,out var vehicle,out var target))return;
        string name;
        if(target=="rear_door")name=Localization.Get("xuiRebirthCamperPhysicalDoor");
        else
        {
            string blockName;
            if(!blocks.TryGetValue(target,out blockName))
                blockName=target=="generator"?"generatorbank":target=="battery_bank"?"batterybank":target=="solar_bank"?"solarbank":null;
            if(blockName==null)return;
            var value=Block.GetBlockValue(blockName);
            name=value.type==0?Localization.Get(blockName):value.Block.GetLocalizedBlockName();
        }
        var input=player.playerInput;
        string binding=input.Activate.GetBindingXuiMarkupString()+input.PermanentActions.Activate.GetBindingXuiMarkupString();
        __state=string.Format(Localization.Get("tooltipInteract"),binding,name);
        if(!RebirthCamperRadial.Allowed(vehicle,player))__state=Localization.Get("ttLocked")+"\n"+__state;
        if(!__7)return;
        __7=false; // Only the focused authored target consumes native activation.
        if(target=="rear_door"){RebirthCamperCampDoor.Request(vehicle,player);return;}
        Request(vehicle,player,target);
    }
    private static void ShowPhysicalPrompt(PlayerMoveController __instance,string __state)
    {
        if(__state==null||__instance.entityPlayerLocal==null||!__instance.entityPlayerLocal.IsAlive())return;
        // The native method still updates focus boxes and unrelated hover state.
        // Replace its broad vehicle caption only for the same validated target
        // used above to consume activation, including targets outside the hull.
        if(string.Equals(__instance.strTextLabelPointingTo,__state,StringComparison.Ordinal))return;
        XUiC_InteractionPrompt.SetText(__instance.playerUI,__state);
        __instance.strTextLabelPointingTo=__state;
    }
    internal static bool IsPower(string target,out RebirthCamperEnergySourceKind kind)
    {
        kind=RebirthCamperEnergySourceKind.Generator;
        if(target=="generator")return true;
        if(target=="battery_bank"){kind=RebirthCamperEnergySourceKind.Battery;return true;}
        if(target=="solar_bank"){kind=RebirthCamperEnergySourceKind.Solar;return true;}
        return false;
    }
    private static bool Camp(EntityVehicle vehicle,out long revision)
        =>RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out revision,out _)&&mode==RebirthCamperEnergyMode.Camp;
    internal static Guid ResolveStation(RebirthCamperStationInstallations stations,string target)
    {
        if(stations==null||!blocks.TryGetValue(target,out var name))return Guid.Empty;
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
        {
            var station=stations.GetSlot(slot);
            if(station!=null&&RebirthCamperCraftingStation.TryGetPresentation(station.InstalledItem,out var presentation)
                &&presentation.InstalledBlockName==name)return station.Id;
        }
        return Guid.Empty;
    }
    internal static bool TryGetStationBlockName(string target,out string blockName)
        =>blocks.TryGetValue(target,out blockName);
    private static Guid Resolve(EntityVehicle vehicle,string target)
    {
        if(IsPower(target,out var kind))
        {
            var snapshot=RebirthCamperEnergyRuntime.Observe(vehicle);
            if(snapshot!=null)foreach(var source in snapshot.Sources)if(source.Kind==kind)return source.Id;
            return Guid.Empty;
        }
        return ResolveStation(RebirthCamperModuleRuntime.For(vehicle).Stations,target);
    }
    private static void Request(EntityVehicle vehicle,EntityPlayerLocal player,string target)
    {
        if(!Camp(vehicle,out var revision)||!RebirthCamperPhysicalTargets.Validate(vehicle,player,target)
            ||!RebirthCamperRadial.Allowed(vehicle,player))return;
        bool power=IsPower(target,out _);if(!power&&!blocks.ContainsKey(target))return;
        Guid selection=vehicle.world.IsRemote()?Guid.Empty:Resolve(vehicle,target);
        if(!vehicle.world.IsRemote()&&selection==Guid.Empty){Unavailable(player);return;}
        var intent=new Intent {Vehicle=vehicle,Player=player,Target=target,Revision=revision,Selection=selection,
            Token=Guid.NewGuid(),Action=power?RebirthCamperRadial.Energy:RebirthCamperRadial.Craft,
            Expires=UnityEngine.Time.realtimeSinceStartup+30f};
        pending.Remove(player);pending.Add(player,intent);
        RebirthCamperModuleInteraction.Request(vehicle,player,intent.Action);
    }
    internal static bool TryOpenGranted(EntityVehicle vehicle,EntityPlayerLocal player,string action)
    {
        if(player==null||!pending.TryGetValue(player,out var intent))return false;
        pending.Remove(player);
        if(!ReferenceEquals(intent.Vehicle,vehicle)||intent.Action!=action)return false;
        intent.NativeCurrent=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player);
        if(!Valid(intent,true))throw new InvalidOperationException("Physical camper target expired.");
        pending.Add(player,intent);
        if(vehicle.world.IsRemote())
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperPhysicalTarget>()
                .Setup(false,player.entityId,vehicle.entityId,intent.Token,intent.Target,intent.Revision,Guid.Empty,false),false);
        else Open(intent);
        if(vehicle.world.IsRemote())GameManager.Instance.StartCoroutine(WaitForReply(intent));
        return true;
    }
    private static System.Collections.IEnumerator WaitForReply(Intent intent)
    {
        while(pending.TryGetValue(intent.Player,out var live)&&ReferenceEquals(live,intent))
        {
            if(!Valid(intent,true))
            {
                pending.Remove(intent.Player);
                Unavailable(intent.Player);
                RebirthCamperModuleInteraction.Release(intent.Vehicle,intent.Player);
                yield break;
            }
            yield return new UnityEngine.WaitForSecondsRealtime(.25f);
        }
    }
    private static bool Valid(Intent intent,bool initial)
        =>(!initial||UnityEngine.Time.realtimeSinceStartup<=intent.Expires)&&intent.NativeCurrent?.Invoke()==true
            &&Camp(intent.Vehicle,out var revision)&&(!initial||revision==intent.Revision)
            &&RebirthCamperPhysicalTargets.Validate(intent.Vehicle,intent.Player,intent.Target);
    private static void Open(Intent intent)
    {
        if(!Valid(intent,true)||intent.Selection==Guid.Empty
            ||!intent.Vehicle.world.IsRemote()&&Resolve(intent.Vehicle,intent.Target)!=intent.Selection)
            throw new InvalidOperationException("Physical camper target unavailable.");
        if(IsPower(intent.Target,out _))RebirthCamperEnergySelectionIntent.Set(intent.Player,intent.Vehicle,intent.Selection,()=>Valid(intent,false));
        else RebirthCamperStationSelectionIntent.SetPhysical(intent.Player,intent.Vehicle,intent.Selection,()=>Valid(intent,false));
        RebirthCamperRadial.OpenSession(intent.Vehicle,intent.Player,intent.Action);
        pending.Remove(intent.Player);
    }
    internal static void Receive(World world,ClientInfo sender,bool reply,int actor,int vehicleId,Guid token,string target,long revision,Guid selection,bool accepted)
    {
        if(!ThreadManager.IsMainThread()||world==null)return;
        var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
        if(world.IsRemote())
        {
            var player=world.GetPrimaryPlayer();
            if(!reply||player==null||!pending.TryGetValue(player,out var intent)||intent.Token!=token
                ||!ReferenceEquals(intent.Vehicle,vehicle)||intent.Target!=target||intent.Revision!=revision)return;
            try {if(!accepted)throw new InvalidOperationException("Physical camper target unavailable.");intent.Selection=selection;Open(intent);}
            catch {pending.Remove(player);Unavailable(player);RebirthCamperModuleInteraction.Release(vehicle,player);}
            return;
        }
        if(reply||token==Guid.Empty||sender?.entityId!=actor)return;
        var actorPlayer=world.GetEntity(actor) as EntityPlayer;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actorPlayer);
        bool valid=!string.IsNullOrEmpty(creation)&&Camp(vehicle,out var liveRevision)&&liveRevision==revision
            &&RebirthCamperRemoteAdmission.CanAccess(vehicle,actorPlayer,sender,()=>RebirthCamperCharacterAccess.CurrentCreation(actorPlayer)==creation)
            &&RebirthCamperPhysicalTargets.Validate(vehicle,actorPlayer,target);
        Guid resolved=valid?Resolve(vehicle,target):Guid.Empty;
        if(resolved!=Guid.Empty)
        {
            admissions[actor]=new Admission {Vehicle=vehicle,Player=actorPlayer,Sender=sender,Creation=creation,
                Target=target,Selection=resolved,Revision=revision,Expires=UnityEngine.Time.realtimeSinceStartup+30f};
            accepted=true;
        }
        else accepted=false;
        sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperPhysicalTarget>()
            .Setup(true,actor,vehicleId,token,target,revision,resolved,accepted));
    }
    internal static Func<bool> TakeServerGuard(EntityVehicle vehicle,EntityPlayer player,Guid selection)
    {
        if(player==null||!admissions.TryGetValue(player.entityId,out var admission))return null;
        admissions.Remove(player.entityId);
        if(UnityEngine.Time.realtimeSinceStartup>admission.Expires
            ||!ReferenceEquals(admission.Vehicle,vehicle)||!ReferenceEquals(admission.Player,player)||admission.Selection!=selection||!Camp(vehicle,out var revision)
            ||revision!=admission.Revision||Resolve(vehicle,admission.Target)!=selection
            ||!RebirthCamperRemoteAdmission.CanAccess(vehicle,player,admission.Sender,()=>RebirthCamperCharacterAccess.CurrentCreation(player)==admission.Creation)
            ||!RebirthCamperPhysicalTargets.Validate(vehicle,player,admission.Target))throw new InvalidOperationException("Physical camper admission expired.");
        return ()=>Camp(vehicle,out _)&&Resolve(vehicle,admission.Target)==selection
            &&RebirthCamperRemoteAdmission.CanAccess(vehicle,player,admission.Sender,()=>RebirthCamperCharacterAccess.CurrentCreation(player)==admission.Creation)
            &&RebirthCamperPhysicalTargets.Validate(vehicle,player,admission.Target);
    }
    internal static Guid ServerSelection(EntityPlayer player)
        =>player!=null&&admissions.TryGetValue(player.entityId,out var admission)?admission.Selection:Guid.Empty;
    private static void Unavailable(EntityPlayerLocal player)=>GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperPhysicalUnavailable"));
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){admissions.Clear();}
}

[Preserve]
public sealed class NetPackageRebirthCamperPhysicalTarget:NetPackage
{
    private bool reply,accepted;private int actor,vehicle;private Guid token,selection;private string target;private long revision;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.Both;
    internal NetPackageRebirthCamperPhysicalTarget Setup(bool r,int a,int v,Guid t,string id,long rev,Guid s,bool ok)
    {reply=r;actor=a;vehicle=v;token=t;target=id;revision=rev;selection=s;accepted=ok;return this;}
    public override void read(PooledBinaryReader r)
    {reply=r.ReadBoolean();actor=r.ReadInt32();vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));target=r.ReadString();if(target.Length>64)throw new System.IO.InvalidDataException("Target id too long.");revision=r.ReadInt64();selection=new Guid(r.ReadBytes(16));accepted=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(reply);w.Write(actor);w.Write(vehicle);w.Write(token.ToByteArray());w.Write(target);w.Write(revision);w.Write(selection.ToByteArray());w.Write(accepted);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world!=null&&(world.IsRemote()||ValidEntityIdForSender(actor)))RebirthCamperPhysicalInteraction.Receive(world,Sender,reply,actor,vehicle,token,target,revision,selection,accepted);}
    public int GetLength()=>0;
}
