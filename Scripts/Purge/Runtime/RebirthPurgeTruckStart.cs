using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

public sealed class RebirthPurgeTruckStartModApi:IModApi
{
    public void InitMod(Mod mod)=>RebirthPurgeTruckStart.Install();
}
internal static partial class RebirthPurgeTruckStart
{
    internal static bool Enabled=>GameManager.Instance?.IsEditMode()!=true&&RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge;
    private sealed class Job
    {
        internal Entry Entry;
        internal EntityVehicle VerifiedVehicle;
        internal Guid VerifiedAssembly;
        internal World World => Entry.World;
        internal EntityPlayer Player => Entry.Player;
        internal RebirthPurgeStarterClaim Record => Entry.Claim;
        internal int PlacementAttempts, SiteRevision;
        internal float SiteStarted, NextSiteSearch, ReadyAt, Next, LastLog;
        internal float HandoffSince = -1f;
        internal string Step;
        internal bool SiteRejected, OwnsProgress, OwnsCamera, NativeSpawnObserved, Ready, AttachmentVerified;
        internal readonly RebirthPurgeStartStability Stability=new RebirthPurgeStartStability();
    }
    private static readonly Dictionary<int,Job> jobs=new Dictionary<int,Job>();
    private static readonly Dictionary<int,(int Vehicle,ClientInfo Peer,INetConnection Connection)> finished=
        new Dictionary<int,(int,ClientInfo,INetConnection)>();
    private static readonly Dictionary<int,float> nextReply=new Dictionary<int,float>();
    private static PlayerMoveController localMove;private static int clientSiteRevision;
    private static EntityPlayerLocal clientPlayer,localRevealPending,localResumeProbed;
    private static INetConnection clientConnection;
    internal sealed class PendingClientStart
    {
        internal World World;
        internal INetConnection Connection;
        internal int Actor;
        internal EntityPlayerLocal Player;

        internal bool Matches(World world, INetConnection connection, int actor)
            =>ReferenceEquals(World,world)&&ReferenceEquals(Connection,connection)&&Actor==actor;
    }
    private static PendingClientStart pendingClientStart;
    private static int clientVehicle;private static Guid clientRequest;
    private static byte clientState;private static float clientNext;
    private static bool clientOwnsProgress,clientOwnsCamera;
    private static float clientHandoffSince=-1f;
    private static readonly RebirthPurgeStartStability clientStability=new RebirthPurgeStartStability();
    private static float nextClientGroundingLog;
    private static bool installed;
    internal static string Diagnostic(EntityPlayerLocal player)
    {
        if(player==null)return "no-player";
        jobs.TryGetValue(player.entityId,out var job);
        return "enabled="+Enabled+", pending="+jobs.Count+", preparing="+(job?.Record.Stage<RebirthPurgeStarterStage.Published)+
            ", nativeSpawnObserved="+(job?.NativeSpawnObserved??false)+", clientState="+clientState+
            ", handoff="+StartHandoffActive(player)+", step="+(job?.Step??"none")+", stage="+(job==null?-1:(int)job.Record.Stage)+
            ", vehicle="+(job?.Record?.Vehicle??0)+", placementAttempts="+(job?.PlacementAttempts??0)+
            ", siteRevision="+(job?.SiteRevision??clientSiteRevision)+
            ", attached="+(player.AttachedToEntity?.entityId??0);
    }
    private const float ClientWaitSeconds=90f,AuthorityHandoffSeconds=75f;
    private static float clientWaitSince;
    private static bool clientWaitTimedOut;
    internal static bool StartSpawn(RespawnType reason,EntityPlayer player)
        =>reason==RespawnType.NewGame||reason==RespawnType.EnterMultiplayer||reason==RespawnType.JoinMultiplayer||
            reason==RespawnType.LoadedGame&&player is EntityPlayerLocal&&player.world?.IsRemote()==false;
    private static string Owner(EntityPlayer player)=>GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId?.CombinedString
        ??(player is EntityPlayerLocal?GameManager.Instance.getPersistentPlayerID(null)?.CombinedString:null);
    private static RebirthVehicleAssembly PrepareStarterParts(World world)
    {
        var content=RebirthVehicleDefinitionRegistry.GetContent("Camper80s");
        var family=RebirthVehicleDefinitionRegistry.GetFamily(content?.FamilyId);
        if(content==null||family==null)throw new InvalidOperationException("Starter truck part definition is unavailable.");
        var assembly=new RebirthVehicleAssembly{AssemblyId=Guid.NewGuid(),ContentId=content.ContentId,
            FamilyId=family.FamilyId,FuelPercent=1f};
        foreach(var slot in family.Slots)
        {
            var value=ItemClass.GetItem(slot.ItemName,false);
            if(value==null||value.IsEmpty())throw new InvalidOperationException("Starter truck part unavailable: "+slot.ItemName);
            value.Quality=1;
            value.UseTimes=0f;
            var part=RebirthVehicleInventoryTransactions.FromItemStack(new ItemStack(value,1),world);
            if(part==null)throw new InvalidOperationException("Starter truck part could not be prepared: "+slot.SlotId);
            part.SlotId=slot.SlotId;
            assembly.Parts.Add(slot.SlotId,part);
        }
        assembly.Performance=RebirthVehiclePerformanceCompiler.Compile(assembly);
        return assembly;
    }
    internal static void Install()
    {
        if(installed)return;installed=true;
        var harmony=new Harmony("rebirth.purge.initial-truck");
        harmony.CreateClassProcessor(typeof(RebirthPurgeRoadSpawnSelection)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeInitialWorldReadiness)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeNativeSpawnTrace)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeFreshRemoteRequest)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgePendingNativePlayerId)).Patch();
        if(!GameManager.IsDedicatedServer)
            harmony.CreateClassProcessor(typeof(RebirthPurgeInitialRevive)).Patch();
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(Spawned);ModEvents.GameUpdate.RegisterHandler(Update);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthDebug.Log("purge", "[PurgeTruckStart] step=shutdown pending="+jobs.Count);
        jobs.Clear();finished.Clear();entries.Clear();localEntry=RemoteEntry=null;RemoteRequestActive=false;nextReply.Clear();
        RebirthPurgeForestRoad.Reset();localMove=null;localRevealPending=localResumeProbed=null;clientPlayer=null;clientConnection=null;
        pendingClientStart=null;
        clientSiteRevision=clientVehicle=0;clientRequest=Guid.Empty;clientState=0;clientNext=0;
        clientOwnsProgress=clientOwnsCamera=clientWaitTimedOut=false;clientHandoffSince=-1f;clientStability.Reset();
    }
    private static void Step(Job job,string step,EntityVehicle inspect=null)
    {float now=Time.realtimeSinceStartup;if(job.Step==step&&now-job.LastLog<5)return;job.Step=step;job.LastLog=now;
        string line="[PurgeTruckStart] step="+step+" player="+job.Player.entityId+" vehicle="+job.Record.Vehicle+" stage="+job.Record.Stage+" position="+job.Player.position;
        RebirthDebug.Log("purge",line);if(inspect!=null)Stable(inspect,true);}
    private static bool CreatorWindowOpen(EntityPlayerLocal player)
    {
        var manager=player.PlayerUI?.windowManager;
        return manager!=null&&(manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId)||
                manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId));
    }
    private static INetConnection ClientChannel()
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var channels=manager?.GetConnectionToServer();
        if(manager?.IsServer!=false||channels==null||channels.Length==0)return null;
        var connection=channels[0];
        return connection!=null&&!connection.IsDisconnected()?connection:null;
    }
    private static bool ClientBindingCurrent(EntityPlayerLocal player)
        =>player!=null&&ReferenceEquals(player,clientPlayer)&&player.world?.IsRemote()==true&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            clientConnection!=null&&ReferenceEquals(ClientChannel(),clientConnection);
    private static bool ClientLeaseActive()=>clientState>=2&&clientState<=4&&!clientWaitTimedOut;

    private static bool HandoffExpired(ref float since,float now,bool canAttach,float limit)
    {
        if(!canAttach){since=-1f;return false;}
        if(since<0f)since=now;
        return now-since>=limit;
    }
    private static void RestoreStartPresentation(EntityPlayerLocal player,bool restoreControl=true)
    {
        player.bIntroAnimActive=false;
        player.PlayerUI?.windowManager?.ReEnableHUD();
        player.EnableCamera(true);
        if(restoreControl)player.SetControllable(true);
    }
    private static void ReleaseClientStart(EntityPlayerLocal player,bool creatorOpen,bool creationHeld,bool completed)
    {
        if(clientOwnsProgress){XUiC_ProgressWindow.Close(player.PlayerUI);clientOwnsProgress=false;}
        clientState=5;
        if(!player.IsSpawned())return;
        if(player.IsDead()){clientOwnsCamera=false;return;}
        // Creation owns its modal and control until commit. Keep only the camera cleanup due.
        if(creatorOpen)return;
        if(clientOwnsCamera||completed)RestoreStartPresentation(player,!creationHeld);
        clientOwnsCamera=false;
    }
    internal static void PresentStart(EntityPlayerLocal player)
    {
        if(player==null)return;
        bool creatorOpen=CreatorWindowOpen(player);
        bool creationHeld=player.IsSpawned()&&RebirthCharacterCreationHoldService.IsHeld(player);
        bool creatorActive=creatorOpen||creationHeld;
        if(ReferenceEquals(localRevealPending,player)&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
            ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&player.IsSpawned())
        {
            if(player.IsDead())localRevealPending=null;
            else if(!creatorOpen){localRevealPending=null;RestoreStartPresentation(player,!creationHeld);}
        }
        bool pending=jobs.TryGetValue(player.entityId,out var job)&&ReferenceEquals(job.Player,player);
        bool remote=ClientBindingCurrent(player);
        if(remote)
        {
            if(clientState==1||clientState==5||clientWaitTimedOut||player.IsSpawned()&&player.IsDead())
            {ReleaseClientStart(player,creatorOpen,creationHeld,false);return;}
            if(HandoffExpired(ref clientHandoffSince,Time.realtimeSinceStartup,player.IsSpawned()&&!creatorOpen,ClientWaitSeconds))
            {
                clientWaitTimedOut=true;
                ReleaseClientStart(player,creatorOpen,creationHeld,false);
                Log.Warning("[PurgeTruckStart] client starter handoff expired after native spawn; releasing native gameplay.");
                return;
            }
            QueryClient(player);
            var driven=player.AttachedToEntity as EntityVehicle;
            bool attached=clientState>=4&&clientVehicle>0&&driven!=null&&driven.entityId==clientVehicle&&driven.GetAttached(0)==player;
            bool settled=attached&&clientStability.Observe(Time.realtimeSinceStartup,Stable(driven));
            if(attached&&!settled&&Time.realtimeSinceStartup>=nextClientGroundingLog)
            {nextClientGroundingLog=Time.realtimeSinceStartup+5;Stable(driven,true);}
            if(!attached)clientStability.Reset();
            if(settled&&player.IsSpawned()&&!creatorActive)
            {
                RebirthPurgePlayerWraps.Tick(true);
                ReleaseClientStart(player,false,false,true);
                RebirthPurgeInformationService.RevealIntro(player);
                return;
            }
            pending=true;
        }
        if(!pending)return;
        // Native respawn can close loading and enable the camera in this Update.
        // Its postfix covers the same frame until the authoritative seat handoff.
        player.ClearMovementInputs();player.SetControllable(false);player.EnableCamera(false);
        if(job!=null)job.OwnsCamera=true;
        else if(remote)clientOwnsCamera=true;
        if(creatorActive)
        {
            // A queued loading close must not retire our separate camera obligation.
            if(job?.OwnsProgress==true){XUiC_ProgressWindow.Close(player.PlayerUI);job.OwnsProgress=false;}
            else if(clientOwnsProgress){XUiC_ProgressWindow.Close(player.PlayerUI);clientOwnsProgress=false;}
            return;
        }
        string text=Localization.Get("msgBuildingEnvironment");
        if(!XUiC_ProgressWindow.IsWindowOpen())XUiC_ProgressWindow.Open(player.PlayerUI,text);
        else XUiC_ProgressWindow.SetText(player.PlayerUI,text);
        if(job!=null)job.OwnsProgress=true;
        else clientOwnsProgress=true;
    }
    private static void Reveal(Job job)
    {
        if(job.OwnsCamera&&job.Player is EntityPlayerLocal player)
        {
            if(job.OwnsProgress)XUiC_ProgressWindow.Close(player.PlayerUI);
            // Native showUILater would otherwise hide the HUD through the briefing.
            RestoreStartPresentation(player);
        }
    }
    internal static bool BeforeWorldReady(PlayerMoveController controller)
    {
        var player=controller.entityPlayerLocal;var world=player?.world;
        if(!Enabled||world==null||player.IsSpawned()||
            !StartSpawn(controller.respawnReason,player))return true;
        if(world.IsRemote())
        {
            var connection=ClientChannel();
            if(connection==null)return true;
            if(!ReferenceEquals(clientPlayer,player)||!ReferenceEquals(clientConnection,connection))
            {
                var pending=pendingClientStart;
                if(pending==null||!ReferenceEquals(pending.Player,player)||!pending.Matches(world,connection,player.entityId))return true;
                pendingClientStart=null;
                clientPlayer=player;clientConnection=connection;clientSiteRevision=clientVehicle=0;
                clientRequest=Guid.NewGuid();clientState=2;clientNext=0;clientHandoffSince=-1f;
                clientOwnsProgress=clientOwnsCamera=clientWaitTimedOut=false;clientStability.Reset();
                clientWaitSince=Time.realtimeSinceStartup;
            }
            localMove=controller;
            // Owner 2026-11-09: the client waited for the truck with no limit and players force-closed the game on
            // "building environment". After ClientWaitSeconds the player spawns normally; the server start continues.
            if(Time.realtimeSinceStartup-clientWaitSince>ClientWaitSeconds)
            {
                if(!clientWaitTimedOut){clientWaitTimedOut=true;Log.Warning("[PurgeTruckStart] client waited "+ClientWaitSeconds+"s for the starter truck (state="+clientState+" vehicle="+clientVehicle+"); spawning normally");}
                return true;
            }
            QueryClient(player);
            if(clientState==1||clientState==5)return true; // Ordinary spawn, or this exact handoff retired.
            var replica=world.GetEntity(clientVehicle) as EntityVehicle;
            return clientState>=3&&replica!=null&&replica.ModelTransform!=null&&replica.RootTransform!=null;
        }
        localMove=controller;
        if(controller.respawnReason==RespawnType.LoadedGame&&!ReferenceEquals(localResumeProbed,player)&&
            ReferenceEquals(world.GetPrimaryPlayer(),player))
        {
            localResumeProbed=player;
            var manager=GameManager.Instance;
            localEntry=OpenEntry(manager,manager.getPersistentPlayerID(null),null,allowFresh:false);
        }
        Bind(localEntry,player);
        if(!jobs.TryGetValue(player.entityId,out var job)||!ReferenceEquals(job.Player,player))return true;
        var vehicle=job.Record.Vehicle>0?world.GetEntity(job.Record.Vehicle) as EntityVehicle:null;
        return job.Record.Stage>=RebirthPurgeStarterStage.Published&&job.Ready&&vehicle!=null&&vehicle.ModelTransform!=null&&vehicle.RootTransform!=null;
    }
    internal static void RelocateClient(World world,int actor,Guid id,Guid request,int revision,Vector3 position,float heading)
    {
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||!ClientBindingCurrent(player)||player.entityId!=actor||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var current)||current!=id||request!=clientRequest||
            revision<=clientSiteRevision||clientWaitTimedOut||clientState==1||clientState>=4||
            player.IsSpawned()||localMove?.entityPlayerLocal!=player)return;
        if(float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z)||float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsInfinity(position.z)||float.IsNaN(heading)||float.IsInfinity(heading))return;
        var valid=world.ClampToValidWorldPos(position);if((valid-position).sqrMagnitude>.01f)return;
        clientSiteRevision=revision;localMove.spawnPosition=new SpawnPosition(position,heading);player.SetPosition(position,true);clientStability.Reset();
    }
    private static bool RelocateSite(Job job,float now)
    {
        if(now<job.NextSiteSearch)return false;
        job.NextSiteSearch=now+5;
        var old=Position(job.Record);
        if(job.Record.HasAnchor&&!job.SiteRejected){RebirthPurgeForestRoad.RejectSite(job.World,old);job.SiteRejected=true;}
        if(!FindSite(job.Entry)){Step(job,"wait.new-road-site");return false;}
        job.PlacementAttempts=0;job.SiteStarted=now;job.SiteRejected=false;job.Stability.Reset();
        PlaceAtAnchor(job);
        RebirthDebug.Log("purge", "[PurgeTruckStart] step=road.new-site revision="+job.SiteRevision+" from="+old+" to="+Position(job.Record));
        job.Next=now+.5f;return true;
    }
    private static void PlaceAtAnchor(Job job)
    {
        if(!TryAnchor(job.Entry,out var anchor))return;
        job.SiteRevision++;
        if(job.Player is EntityPlayerLocal local&&localMove?.entityPlayerLocal==local)localMove.spawnPosition=anchor;
        if((job.Player.position-anchor.position).sqrMagnitude>.01f)
        {
            job.Player.SetPosition(anchor.position,true);
            job.Player.ChunkObserver?.SetPosition(anchor.position);
        }
    }
    private static void QueryClient(EntityPlayerLocal player)
    {
        if(Time.realtimeSinceStartup<clientNext||!Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var id))return;
        clientNext=Time.realtimeSinceStartup+.5f;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartQueryV2>().Setup(player.entityId,id,clientRequest));
    }
    internal static void Query(World world,ClientInfo sender,int actor,Guid id,Guid request)
    {
        if(!ThreadManager.IsMainThread()||!Enabled||world==null||world.IsRemote()||request==Guid.Empty||
            !CurrentPeer(sender,sender?.netConnection?[0])||sender.entityId!=actor||
            !ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor),sender)||
            !Guid.TryParse(world.worldState.Guid,out var current)||id!=current)return;
        float now=Time.realtimeSinceStartup;
        if(nextReply.TryGetValue(actor,out float next)&&now<next)return;
        nextReply[actor]=now+.4f;
        byte state=1;int vehicleId=0;
        if(jobs.TryGetValue(actor,out var job)&&ReferenceEquals(job.Entry.Peer,sender)&&EntryCurrent(job.Entry))
        {
            if(job.SiteRevision>0&&TryAnchor(job.Entry,out var anchor))
                sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartRelocateV2>().Setup(actor,id,request,job.SiteRevision,anchor));
            vehicleId=job.Record.Vehicle;var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
            state=(byte)(job.Record.Stage>=RebirthPurgeStarterStage.Published&&job.Ready&&vehicle!=null&&vehicle.ModelTransform!=null?3:2);
        }
        // A finished lease only belongs to the connection that actually entered.
        else if(finished.TryGetValue(actor,out var completed)&&ReferenceEquals(completed.Peer,sender)&&
            completed.Connection!=null&&ReferenceEquals(completed.Connection,sender.netConnection?[0]))
        {state=4;vehicleId=completed.Vehicle;}
        sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartStatusV2>().Setup(actor,id,request,state,vehicleId));
    }
    internal static void Status(World world,int actor,Guid id,Guid request,byte state,int vehicle)
    {
        if(!ThreadManager.IsMainThread()||!Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var current)||id!=current)return;
        if(request==Guid.Empty)
        {
            // Only the authority's positive classification precedes native PlayerId.
            if(state==2&&vehicle==0&&world?.IsRemote()==true&&ReferenceEquals(GameManager.Instance?.World,world)&&
                ClientChannel() is INetConnection connection)
                pendingClientStart=new PendingClientStart{World=world,Connection=connection,Actor=actor};
            return;
        }
        var player=world?.GetPrimaryPlayer();
        if(!ClientBindingCurrent(player)||player.entityId!=actor||clientWaitTimedOut||
            !RebirthPurgeStartWire.AcceptStatus(clientRequest,request,clientState,clientVehicle,state,vehicle))return;
        clientState=state;clientVehicle=vehicle;
    }
    internal static PendingClientStart TakePendingClientStart(GameManager manager,int actor)
    {
        var pending=pendingClientStart;
        pendingClientStart=null;
        return pending!=null&&pending.Player==null&&pending.Matches(manager.World,ClientChannel(),actor)?pending:null;
    }
    internal static void BindPendingClientStart(GameManager manager,PendingClientStart pending)
    {
        if(pending==null)return;
        var world=manager.World;
        var player=world?.GetPrimaryPlayer();
        if(player==null||!pending.Matches(world,ClientChannel(),player.entityId))return;
        pending.Player=player;
        pendingClientStart=pending;
    }
    internal static void SendNativePlayerId(ClientInfo peer,NetPackagePlayerId packet)
    {
        try
        {
            var entry=RemoteEntry;
            if(entry!=null&&ReferenceEquals(entry.Peer,peer)&&peer.entityId==packet.id&&EntryCurrent(entry)&&
                Guid.TryParse(entry.World.worldState.Guid,out var id))
            {
                // Native near-player spawning can bypass GetRandomSpawnPosition.
                // Send the same staging position that PrepareRemote will apply on the server.
                if(TryAnchor(entry,out var anchor))
                {
                    packet.playerDataFile.ecd.pos=anchor.position;
                    packet.playerDataFile.lastSpawnPosition=anchor;
                }
                peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartStatusV2>().Setup(packet.id,id,Guid.Empty,2,0));
            }
        }
        catch(Exception error){Log.Warning("[PurgeTruckStart] Pending native start notification was unavailable: "+error.Message);}
        peer.SendPackage(packet);
    }
    internal static void PrepareRemote(ClientInfo sender)
    {
        var entry=RemoteEntry;
        if(entry!=null&&ReferenceEquals(entry.Peer,sender)&&EntryCurrent(entry)&&
            entry.World.GetEntity(sender.entityId) is EntityPlayer player)Bind(entry,player);
    }
    internal static bool OwnsInitialStart(EntityPlayerLocal player)=>player!=null&&
        (jobs.TryGetValue(player.entityId,out var job)&&ReferenceEquals(job.Player,player)||
         ClientLeaseActive()&&ClientBindingCurrent(player));
    private static void Bind(Entry entry,EntityPlayer player)
    {
        if(entry==null||player==null||!ReferenceEquals(entry.World,player.world)||!EntryCurrent(entry)||
            !string.Equals(Owner(player),entry.Owner,StringComparison.Ordinal))return;
        if(jobs.TryGetValue(player.entityId,out var existing))
        {
            if(ReferenceEquals(existing.Player,player))return;
            RetireEntry(existing.Entry);jobs.Remove(player.entityId);
        }
        if(entry.Player!=null&&!ReferenceEquals(entry.Player,player))return;
        entry.Player=player;
        if(!EntryCurrent(entry)){entry.Player=null;return;}
        finished.Remove(player.entityId);nextReply.Remove(player.entityId);
        var job=new Job{Entry=entry};
        jobs.Add(player.entityId,job);
        PlaceAtAnchor(job);
        Step(job,"handoff.queued");
    }
    private static void Spawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        var world=GameManager.Instance?.World;
        if(!Enabled||world==null||world.IsRemote()||!(world.GetEntity(data.EntityId) is EntityPlayer player)||
            !StartSpawn(data.RespawnType,player))return;
        if(player is EntityPlayerLocal)Bind(localEntry,player);
        if(jobs.TryGetValue(player.entityId,out var job)&&ReferenceEquals(job.Player,player)&&EntryCurrent(job.Entry))
            job.NativeSpawnObserved=true;
    }
    internal static bool StartHandoffActive(EntityPlayerLocal player)=>player!=null&&
        (OwnsInitialStart(player)||ReferenceEquals(localRevealPending,player)||
         (clientOwnsProgress||clientOwnsCamera)&&ClientBindingCurrent(player));
    private static void EndJob(Job job)
    {
        jobs.Remove(job.Player.entityId);
        RetireEntry(job.Entry);
    }
    private static void RefuseJob(Job job,string reason)
    {
        var local=job.Player as EntityPlayerLocal;
        if(local!=null)
        {
            if(job.OwnsProgress){XUiC_ProgressWindow.Close(local.PlayerUI);job.OwnsProgress=false;}
            if(job.OwnsCamera&&!local.IsDead())
            {
                if(local.IsSpawned()&&!CreatorWindowOpen(local))
                    RestoreStartPresentation(local,!RebirthCharacterCreationHoldService.IsHeld(local));
                else localRevealPending=local;
            }
        }
        EndJob(job);
        Log.Warning("[PurgeTruckStart] Retained starter claim; releasing normal spawn: "+reason);
    }
    private static bool OriginalVehicle(Job job,EntityVehicle vehicle,out Guid assembly)
    {
        assembly=Guid.Empty;var record=job.Record;
        if(vehicle==null||vehicle.entityId!=record.Vehicle||!ReferenceEquals(job.World.GetEntity(vehicle.entityId),vehicle)||
            !RebirthCamperRadial.IsCamper(vehicle))return false;
        var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata("RebirthPurgeStarterOwner",out string owner)||owner!=record.Owner)return false;
        if(record.Assembly!=Guid.Empty&&
            (!item.TryGetMetadata("RebirthPurgeStarterClaim",out string claim)||claim!=record.Claim.ToString("N")))return false;
        if(!ReferenceEquals(job.VerifiedVehicle,vehicle))
        {
            if(!RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(vehicle,out var native)||native==null||native.AssemblyId==Guid.Empty)return false;
            job.VerifiedVehicle=vehicle;job.VerifiedAssembly=native.AssemblyId;
        }
        assembly=job.VerifiedAssembly;
        return record.Assembly==Guid.Empty||record.Assembly==assembly;
    }

    private static bool TryNearbySite(Job job)
    {
        var record=job.Record;
        var occupied=new List<Vector3>(OccupiedCampers(job.World,VehicleManager.Instance));
        for(int distance=8;distance<=64;distance+=2)
        foreach(int sign in new[]{1,-1})
        {
            if(!RebirthPurgeForestRoad.Nearby(job.World,Position(record),record.Heading,distance*sign,out var road)||
                !SiteAvailable(job.Entry,road)||!FarFromCampers(road,occupied)||
                !RebirthPurgeForestRoad.PlayerAnchor(job.World,road,out var anchor))continue;
            var probe=record.Copy();probe.X=road.position.x;probe.Y=road.position.y;probe.Z=road.position.z;
            if(!Clear(job.World,probe,job.Player,out bool clearing)&&!clearing)continue;
            if(!PublishSite(job.Entry,road,anchor))return false;
            PlaceAtAnchor(job);job.Next=Time.realtimeSinceStartup+.5f;return true;
        }
        return false;
    }

    private static EntityVehicle PublishOriginal(Job job)
    {
        var record=job.Record;
        Step(job,"truck.create-bare");
        var vehicle=RebirthVehicleEntityFactory.Create(job.World,"rebirthCamper80s",Position(record),new Vector3(0,record.Heading,0));
        if(!vehicle)throw new InvalidOperationException("Starter truck definition unavailable.");
        bool attempted=false;
        try
        {
            if(RebirthCamperBodyAttachment.HasBody(vehicle))throw new InvalidOperationException("Starter truck unexpectedly includes the camper body.");
            RebirthVehicleEntityFactory.AssignOwner(vehicle,job.Player);
            int maximum=vehicle.vehicle.GetUpdatedItemValue().MaxUseTimes;
            RebirthVehicleEntityFactory.ApplyRestoredState(vehicle,maximum,maximum,1f);
            var assembly=PrepareStarterParts(job.World);
            if(!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(vehicle,assembly))throw new InvalidOperationException("Starter assembly metadata unavailable.");
            RebirthPurgePlayerWraps.Prepare(vehicle,record.Owner);
            vehicle.SetLocked(true);
            if(!vehicle.IsLocked())throw new InvalidOperationException("Starter truck lock did not take effect.");
            var item=vehicle.vehicle.GetUpdatedItemValue();
            item.SetMetadata("RebirthPurgeStarterOwner",record.Owner);
            item.SetMetadata("RebirthPurgeStarterClaim",record.Claim.ToString("N"));
            var next=record.Copy();next.Vehicle=vehicle.entityId;next.Assembly=assembly.AssemblyId;
            next.Stage=RebirthPurgeStarterStage.PublicationAttempted;
            if(!Publish(job.Entry,next))throw new IOException("Native truck publication was not prepared.");
            attempted=true;
            Step(job,"record.publication-attempted");
            job.World.SpawnEntityInWorld(vehicle);vehicle.SendSyncData(2);
            VehicleManager.Instance.TriggerSave();
            return vehicle;
        }
        catch
        {
            if(!attempted)UnityEngine.Object.Destroy(vehicle.gameObject);
            throw;
        }
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        if(jobs.Count==0)return;
        foreach(var pair in jobs.ToArray())
        {
            var job=pair.Value;var p=job.Player;float now=Time.realtimeSinceStartup;
            if(!EntryCurrent(job.Entry)||job.NativeSpawnObserved&&p.IsDead()){EndJob(job);continue;}
            if(now<job.Next)continue;job.Next=now+.25f;
            if(job.ReadyAt==0)job.ReadyAt=now;
            try
            {
                bool nativeSpawned=RebirthPurgeStartHandoff.CanRun(job.NativeSpawnObserved,p is EntityPlayerLocal,p.IsSpawned());
                bool held=nativeSpawned&&RebirthCharacterCreationHoldService.IsHeld(p);
                if(!nativeSpawned&&now-job.ReadyAt>=AuthorityHandoffSeconds&&!job.Ready&&!job.AttachmentVerified||
                    HandoffExpired(ref job.HandoffSince,now,nativeSpawned&&!held,AuthorityHandoffSeconds))
                {RefuseJob(job,"starter handoff timed out");continue;}
                var record=job.Record;
                if(record.Stage==RebirthPurgeStarterStage.WaitingForSite||record.Stage==RebirthPurgeStarterStage.Prepared&&!record.HasAnchor)
                {
                    RelocateSite(job,now);
                    continue;
                }
                var vehicle=record.Vehicle>0?job.World.GetEntity(record.Vehicle) as EntityVehicle:null;
                bool original=OriginalVehicle(job,vehicle,out Guid existingAssembly);
                var plan=record.Publication(original);
                if(plan==RebirthPurgeStarterPublication.AwaitOriginal){Step(job,"wait.exact-native-original");continue;}
                if(plan==RebirthPurgeStarterPublication.Create)
                {
                    var vehicleManager=VehicleManager.Instance;
                    if(vehicleManager==null){Step(job,"wait.native-vehicle-manager");continue;}
                    if(!SiteAvailable(job.Entry,Road(record))||RebirthPurgeStartPlacementPolicy.Relocate(job.PlacementAttempts,now-job.SiteStarted)&&job.SiteStarted!=0)
                    {RelocateSite(job,now);continue;}
                    if(job.SiteStarted==0)job.SiteStarted=now;
                    if(job.World.GetChunkFromWorldPos(new Vector3i(Position(record)))==null){Step(job,"wait.road-chunk");continue;}
                    Step(job,"road.footprint-check");job.PlacementAttempts++;
                    bool roadClear=Clear(job.World,record,p,out bool clearing);
                    if(clearing){Step(job,"wait.road-decoration-cleanup");job.Next=now+.5f;continue;}
                    if(!roadClear)
                    {
                        if(TryNearbySite(job))continue;
                        Step(job,"wait.clear-loaded-road-footprint");job.Next=now+2;continue;
                    }
                    if(!ReferenceEquals(VehicleManager.Instance,vehicleManager)){Step(job,"wait.native-vehicle-manager");continue;}
                    if(!PreparedSiteAvailable(job.Entry,Road(record),vehicleManager)){RelocateSite(job,now);continue;}
                    vehicle=PublishOriginal(job);
                    if(!OriginalVehicle(job,vehicle,out existingAssembly))throw new InvalidOperationException("Native truck publication is uncertain.");
                    record=job.Record;
                }
                if(record.Stage==RebirthPurgeStarterStage.PublicationAttempted)
                {
                    if(record.Assembly==Guid.Empty)vehicle.vehicle.GetUpdatedItemValue().SetMetadata("RebirthPurgeStarterClaim",record.Claim.ToString("N"));
                    var published=record.Copy();published.Stage=RebirthPurgeStarterStage.Published;published.Assembly=existingAssembly;
                    if(!Publish(job.Entry,published))throw new IOException("Native truck publication could not be recorded.");
                    Step(job,"truck.published");job.Next=now+.5f;continue;
                }
                if(plan!=RebirthPurgeStarterPublication.UseOriginal)throw new InvalidOperationException("Starter truck identity is unavailable.");
                if(vehicle.ModelTransform==null||p.ModelTransform==null||vehicle.GetAttached(0)!=null&&vehicle.GetAttached(0)!=p){Step(job,"wait.truck-model-or-seat");continue;}
                // Validate real loaded collision and settled wheel contacts before
                // entry/attachment. A model transform alone proves neither.
                if(!job.AttachmentVerified)
                {
                    job.Ready=job.Stability.Observe(now,Stable(vehicle));
                    if(!job.Ready){Step(job,"wait.truck-grounded-and-stable",vehicle);continue;}
                }
                // Character commit follows PlayerSpawnedInWorld. Prepare the
                // truck before entry, but never make pre-entry wait for commit.
                if(!nativeSpawned){Step(job,"wait.native-spawn-with-truck-ready");continue;}
                if(held){Step(job,"wait.character-creation");continue;}
                if(p.AttachedToEntity!=vehicle){Step(job,"driver.attach-request");p.StartAttachToEntity(vehicle,0);}
                if(p.AttachedToEntity!=vehicle||vehicle.GetAttached(0)!=p){Step(job,"wait.driver-attachment");continue;}
                if(!job.AttachmentVerified){job.AttachmentVerified=true;job.Stability.Reset();job.Ready=false;}
                // Remote driver attachment transfers native physics ownership to
                // that client. Its own PresentStart performs this post-attach gate.
                if(!vehicle.isEntityRemote&&!job.Stability.Observe(now,Stable(vehicle)))
                {Step(job,"wait.attached-truck-grounded-and-stable",vehicle);continue;}
                Step(job,"driver.attached");
                if(p is EntityPlayerLocal)RebirthPurgePlayerWraps.Tick(true);
                Step(job,"gameplay.enable");
                var completed=record.Copy();completed.Stage=RebirthPurgeStarterStage.Completed;
                if(!Publish(job.Entry,completed))throw new IOException("Starter completion could not be recorded.");
                VehicleManager.Instance?.TriggerSave();Reveal(job);
                if(job.Entry.Peer!=null)finished[p.entityId]=(record.Vehicle,job.Entry.Peer,job.Entry.Connection);
                EndJob(job);
                if(p is EntityPlayerLocal introPlayer)RebirthPurgeInformationService.RevealIntro(introPlayer);
                RebirthDebug.Log("purge", "[PurgeTruckStart] Initial truck ready on a rural pine-forest road; driver attached.");
            }
            catch(Exception e){RefuseJob(job,e.Message);}
        }
    }
    private static bool Stable(EntityVehicle vehicle,bool diagnostic=false)
    {
        if(vehicle==null||vehicle.world==null||vehicle.ModelTransform==null||vehicle.PhysicsTransform==null||vehicle.vehicleRB==null||vehicle.isEntityRemote)
        {if(diagnostic)RebirthDebug.Log("purge", "[PurgeTruckStart] step=grounding.detail vehicle="+vehicle?.entityId+" pending=bindings-or-physics-owner origin="+Origin.position);return false;}
        var rigidbody=vehicle.vehicleRB;var world=vehicle.world;
        var position=rigidbody.position+Origin.position;
        var chunk=world.GetChunkFromWorldPos(new Vector3i(position)) as Chunk;
        bool loaded=chunk!=null&&chunk.IsCollisionMeshGenerated&&(GameManager.IsDedicatedServer||chunk.IsDisplayed);
        int grounded=0;
        if(vehicle.wheels!=null)foreach(var wheel in vehicle.wheels)
        {
            if(wheel?.wheelC==null||!wheel.wheelC.enabled||!wheel.wheelC.GetGroundHit(out var hit)||hit.collider==null||hit.normal.y<.7f)continue;
            if(hit.collider.attachedRigidbody==rigidbody||(hit.collider.transform.IsChildOf(vehicle.RootTransform)))continue;
            if(((1<<hit.collider.gameObject.layer)&1073807360)==0)continue;
            grounded++;
        }
        var terrain=(world.ChunkCache.ChunkProvider as ChunkProviderGenerateWorld)?.m_TerrainGenerator;
        if(terrain==null)return false;
        float height=terrain.GetTerrainHeightAt(Mathf.RoundToInt(position.x),Mathf.RoundToInt(position.z));
        float poseError=Mathf.Max((vehicle.ModelTransform.position+Origin.position-position).sqrMagnitude,(vehicle.position-position).sqrMagnitude);
        if(diagnostic)RebirthDebug.Log("purge", "[PurgeTruckStart] step=grounding.detail vehicle="+vehicle.entityId+" entity="+vehicle.position+" rigidbody="+position+
            " velocity="+rigidbody.velocity+" angular="+rigidbody.angularVelocity+" contacts="+grounded+" loaded="+loaded+" upright="+vehicle.PhysicsTransform.up.y+
            " poseErrorSquared="+poseError+" terrain="+height+" origin="+Origin.position+" kinematic="+rigidbody.isKinematic+" RBActive="+vehicle.RBActive);
        return RebirthPurgeStartStability.Accept(loaded,grounded,rigidbody.velocity.sqrMagnitude,rigidbody.angularVelocity.sqrMagnitude,
            vehicle.PhysicsTransform.up.y,poseError,position.y-height);
    }
    private static bool Clear(World world,RebirthPurgeStarterClaim record,EntityPlayer owner,out bool clearing)
    {
        clearing=false;var decorations=new Dictionary<Vector3i,BlockValue>();
        var rotation=Quaternion.Euler(0,record.Heading,0);
        // Nearby-road relocation must preserve the staging separation too; an
        // alternate 8m behind the truck would otherwise enclose the staged actor.
        if(owner!=null)
        {
            var staged=Quaternion.Inverse(rotation)*(owner.position-Position(record));
            if(!RebirthPurgeStartFootprint.PlayerOutside(staged.x,staged.z))return false;
        }
        var provider=world.ChunkCache.ChunkProvider as ChunkProviderGenerateWorld;
        if(provider?.m_TerrainGenerator==null)return false;
        var centre=Position(record)+Vector3.up*2;
        double radians=record.Heading*Math.PI/180;
        double extentX=RebirthPurgeStartFootprint.WorldExtentX(radians),extentZ=RebirthPurgeStartFootprint.WorldExtentZ(radians);
        // Check entire voxel boxes, including rotated edge intersections.
        for(int x=Mathf.FloorToInt(record.X-(float)extentX)-1;x<=Mathf.CeilToInt(record.X+(float)extentX);x++)
        for(int z=Mathf.FloorToInt(record.Z-(float)extentZ)-1;z<=Mathf.CeilToInt(record.Z+(float)extentZ);z++)
        {
            if(!RebirthPurgeStartFootprint.IntersectsVoxel(x-record.X,z-record.Z,radians))continue;
            float terrain=provider.m_TerrainGenerator.GetTerrainHeightAt(x,z);
            if(Mathf.Abs(terrain-(record.Y-.5f))>.8f)return false;
            if(world.GetChunkFromWorldPos(new Vector3i(x,Mathf.FloorToInt(terrain),z))==null)return false;
            for(int y=Mathf.FloorToInt(terrain);y<=Mathf.CeilToInt(record.Y+4);y++)
            {
                var block=world.GetBlock(x,y,z);
                var at=new Vector3i(x,y,z);
                if(block.ischild)
                {
                    if(block.Block?.multiBlockPos==null)return false;
                    at=block.Block.multiBlockPos.GetParentPos(at,block);block=world.GetBlock(at);
                    if(block.isair||block.ischild)return false;
                    if(!RebirthPurgeStartPlacementPolicy.Removable(block.Block.GetBlockName(),block.Block.IsTerrainDecoration,block.Block.Properties.GetString("Shape")))return false;
                }
                if(block.isair)continue;
                if(y<=Mathf.CeilToInt(terrain)&&block.Block.shape.IsTerrain())continue;
                if(RebirthPurgeStartPlacementPolicy.Removable(block.Block.GetBlockName(),block.Block.IsTerrainDecoration,block.Block.Properties.GetString("Shape")))
                {decorations[at]=block;continue;}
                RebirthDebug.Log("purge", "[PurgeTruckStart] step=road.obstruction block="+block.Block.GetBlockName()+" position="+new Vector3i(x,y,z));return false;
            }
        }
        if(decorations.Count>0)
        {
            foreach(var decoration in decorations)if(!world.GetBlock(decoration.Key).Equals(decoration.Value))return false;
            foreach(var decoration in decorations)
                world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)decoration.Key,BlockValue.Air,true,false));
            clearing=true;
            RebirthDebug.Log("purge", "[PurgeTruckStart] step=road.decorations-cleared count="+decorations.Count+" position="+Position(record));
            return false; // Let native collision rebuild, then repeat full clearance before creating a truck.
        }
        foreach(var entity in world.Entities.list)
        {
            if(entity==null||ReferenceEquals(entity,owner))continue;
            var bounds=entity.boundingBox;var local=Quaternion.Inverse(rotation)*(bounds.center-Position(record));
            float radius=bounds.extents.magnitude;
            if(Mathf.Abs(local.x)<=2.2f+radius&&Mathf.Abs(local.z)<=7+radius&&bounds.max.y>=record.Y-.5f&&bounds.min.y<=record.Y+4)return false;
        }
        foreach(var collider in Physics.OverlapBox(centre-Origin.position,new Vector3(2.2f,2,7),rotation,Physics.AllLayers,QueryTriggerInteraction.Ignore))
        {
            if(owner?.RootTransform!=null&&collider.transform.IsChildOf(owner.RootTransform))continue;
            var entity=collider.GetComponentInParent<Entity>();
            if(entity!=null&&!ReferenceEquals(entity,owner))return false;
            if(collider.gameObject.layer==21)return false;
        }
        return true;
    }
}
[HarmonyPatch(typeof(SpawnPointList),nameof(SpawnPointList.GetRandomSpawnPosition))]
internal static class RebirthPurgeRoadSpawnSelection
{
    private static bool Prefix(World _world,ref SpawnPosition __result)
    {
        if(!RebirthPurgeTruckStart.TryChooseAnchor(_world,out var player))return true;
        RebirthDebug.Log("purge", "[PurgeTruckStart] step=player.staging-anchor player="+player.position);
        __result=player;return false;
    }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.RequestToSpawnPlayer))]
internal static class RebirthPurgeFreshRemoteRequest
{
    private static void Prefix(GameManager __instance,ClientInfo _cInfo,out (RebirthPurgeTruckStart.Entry Entry,bool Active) __state)
    {
        __state=(RebirthPurgeTruckStart.RemoteEntry,RebirthPurgeTruckStart.RemoteRequestActive);
        RebirthPurgeTruckStart.RemoteRequestActive=true;
        RebirthPurgeTruckStart.RemoteEntry=RebirthPurgeTruckStart.EnterRemote(__instance,_cInfo);
    }
    private static void Postfix(ClientInfo _cInfo)=>RebirthPurgeTruckStart.PrepareRemote(_cInfo);
    private static Exception Finalizer(Exception __exception,(RebirthPurgeTruckStart.Entry Entry,bool Active) __state)
    {RebirthPurgeTruckStart.RemoteEntry=__state.Entry;RebirthPurgeTruckStart.RemoteRequestActive=__state.Active;return __exception;}

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code=instructions.ToList();
        var setup=AccessTools.Method(typeof(NetPackagePlayerId),nameof(NetPackagePlayerId.Setup),
            new[]{typeof(int),typeof(int),typeof(PlayerDataFile),typeof(int)});
        var send=AccessTools.Method(typeof(ClientInfo),nameof(ClientInfo.SendPackage),new[]{typeof(NetPackage)});
        var notify=AccessTools.Method(typeof(RebirthPurgeTruckStart),nameof(RebirthPurgeTruckStart.SendNativePlayerId));
        int index=code.FindIndex(instruction=>instruction.Calls(setup));
        if(index<0||index+1>=code.Count||code.Count(instruction=>instruction.Calls(setup))!=1||!code[index+1].Calls(send))
            throw new InvalidOperationException("Native PlayerId send changed.");
        code[index+1].opcode=System.Reflection.Emit.OpCodes.Call;
        code[index+1].operand=notify;
        return code;
    }
}

[HarmonyPatch(typeof(GameManager),nameof(GameManager.PlayerId),new[]{typeof(int),typeof(int),typeof(PlayerDataFile),typeof(int)})]
internal static class RebirthPurgePendingNativePlayerId
{
    private static void Prefix(GameManager __instance,int _playerId,out RebirthPurgeTruckStart.PendingClientStart __state)
        =>__state=RebirthPurgeTruckStart.TakePendingClientStart(__instance,_playerId);

    private static void Postfix(GameManager __instance,bool __runOriginal,RebirthPurgeTruckStart.PendingClientStart __state)
    {if(__runOriginal)RebirthPurgeTruckStart.BindPendingClientStart(__instance,__state);}
}

// Called immediately before native Spawned=true and loading-screen close.
[HarmonyPatch(typeof(PlayerMoveController),nameof(PlayerMoveController.CheckWorldReadyForSpawn))]
internal static class RebirthPurgeInitialWorldReadiness
{
    private static void Postfix(PlayerMoveController __instance,ref bool __result)
    {if(__result&&RebirthPurgeTruckStart.StartSpawn(__instance.respawnReason,__instance.entityPlayerLocal))__result=RebirthPurgeTruckStart.BeforeWorldReady(__instance);}
}
[HarmonyPatch(typeof(EntityPlayerLocal),nameof(EntityPlayerLocal.AfterPlayerRespawn))]
internal static class RebirthPurgeInitialRevive
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code=new List<CodeInstruction>(instructions);
        bool IsReviveCall(CodeInstruction instruction)=>instruction.opcode==OpCodes.Callvirt&&
            instruction.operand is MethodInfo method&&method.DeclaringType==typeof(AvatarController)&&
            method.Name==nameof(AvatarController.PlayPlayerFPRevive)&&method.GetParameters().Length==0;
        int index=code.FindIndex(IsReviveCall);
        if(index<0||code.FindLastIndex(IsReviveCall)!=index)
        {
            Log.Warning("[PurgeTruckStart] Native first-person revive call changed. Keeping the native animation.");
            return code;
        }

        // Preserve the native receiver and supply the existing respawn context.
        var call=code[index];
        var player=new CodeInstruction(OpCodes.Ldarg_0);
        player.labels.AddRange(call.labels);
        player.blocks.AddRange(call.blocks);
        call.labels.Clear();
        call.blocks.Clear();
        code.Insert(index,player);
        code.Insert(index+1,new CodeInstruction(OpCodes.Ldarg_1));
        call.opcode=OpCodes.Call;
        call.operand=((Action<AvatarController,EntityPlayerLocal,RespawnType>)PlayRevive).Method;
        return code;
    }

    private static void PlayRevive(AvatarController avatar,EntityPlayerLocal player,RespawnType reason)
    {
        if((reason==RespawnType.NewGame||reason==RespawnType.EnterMultiplayer)&&
            RebirthPurgeTruckStart.Enabled&&ReferenceEquals(avatar?.Entity,player)&&
            RebirthPurgeTruckStart.OwnsInitialStart(player))return;
        avatar.PlayPlayerFPRevive();
    }
}

[HarmonyPatch(typeof(PlayerMoveController),nameof(PlayerMoveController.Update))]
internal static class RebirthPurgeNativeSpawnTrace
{
    private static float next;
    private static void Postfix(PlayerMoveController __instance){var player=__instance.entityPlayerLocal;if(RebirthPurgeTruckStart.StartHandoffActive(player))RebirthPurgeTruckStart.PresentStart(player);}
    [HarmonyPriority(Priority.First)]
    private static void Prefix(PlayerMoveController __instance)
    {
        var player=__instance.entityPlayerLocal;
        if(!RebirthPurgeTruckStart.Enabled||player==null||!RebirthPurgeTruckStart.StartHandoffActive(player)||player.IsSpawned()||Time.realtimeSinceStartup<next)return;
        next=Time.realtimeSinceStartup+5;
        RebirthDebug.Log("purge", "[PurgeTruckStart] step=wait.native-respawn player="+player.entityId+" reason="+__instance.respawnReason+" timer="+__instance.respawnTime+" position="+player.position+" uiReady="+player.PlayerUI.xui.IsReady);
    }
}
