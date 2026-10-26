using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

public sealed class RebirthPurgeTruckStartModApi:IModApi
{
    public void InitMod(Mod mod)=>RebirthPurgeTruckStart.Install();
}
internal static class RebirthPurgeTruckStart
{
    internal static bool Enabled=>RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge;
    [ThreadStatic] internal static bool FreshRemoteRequest;
    [ThreadStatic] internal static EntityPlayerLocal IntroOwner;
    private sealed class StartRecord
    {
        public string World,Owner;public int Vehicle,Stage;public float X,Y,Z,Heading;
        [JsonIgnore] public Vector3 Position=>new Vector3(X,Y,Z);
    }
    private sealed class Job {internal World World;internal EntityPlayer Player;internal StartRecord Record;internal float ReadyAt,Next,LastLog;internal string Step,Failure;internal bool OwnsProgress,OwnsCamera,NativeSpawnObserved,Ready,AttachmentVerified;internal readonly RebirthPurgeStartStability Stability=new RebirthPurgeStartStability();}
    private static readonly Dictionary<int,Job> jobs=new Dictionary<int,Job>();
    private static readonly Dictionary<int,int> finished=new Dictionary<int,int>();
    private static readonly HashSet<int> preparing=new HashSet<int>();
    private static readonly HashSet<int> nativeSpawnObserved=new HashSet<int>();
    private static readonly Dictionary<int,float> nextPreparation=new Dictionary<int,float>(),nextReply=new Dictionary<int,float>();
    private static World clientWorld;private static int clientActor,clientVehicle;private static byte clientState;private static float clientNext;private static bool clientOwnsProgress;
    private static readonly RebirthPurgeStartStability clientStability=new RebirthPurgeStartStability();
    private static float nextClientGroundingLog;
    private static bool installed;
    private static string RecordPath=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","PurgeRoadStart","initial-truck.json");
    private static string Owner(EntityPlayer player)=>GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId?.CombinedString
        ??(player is EntityPlayerLocal?GameManager.Instance.getPersistentPlayerID(null)?.CombinedString:null);
    private static void PrepareStarterParts(EntityVehicle vehicle,World world)
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
        RebirthVehicleAssemblyIndex.RegisterEntity(vehicle,assembly);
    }
    internal static bool CanChoose(World world)=>Enabled&&world!=null&&!world.IsRemote()&&!File.Exists(RecordPath)
        &&(FreshRemoteRequest||(!GameManager.IsDedicatedServer&&world.GetPrimaryPlayer()==null));
    internal static void Install()
    {
        if(installed)return;installed=true;
        var harmony=new Harmony("rebirth.purge.initial-truck");
        harmony.CreateClassProcessor(typeof(RebirthPurgeRoadSpawnSelection)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeInitialWorldReadiness)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeNativeSpawnTrace)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeFreshRemoteRequest)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeInitialRevive)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthPurgeInitialIntroScope)).Patch();
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(Spawned);ModEvents.GameUpdate.RegisterHandler(Update);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){Log.Out("[PurgeTruckStart] step=shutdown pending="+jobs.Count);jobs.Clear();finished.Clear();preparing.Clear();nativeSpawnObserved.Clear();nextPreparation.Clear();nextReply.Clear();clientWorld=null;clientActor=clientVehicle=0;clientState=0;clientNext=0;clientOwnsProgress=false;clientStability.Reset();}
    private static void Step(Job job,string step,EntityVehicle inspect=null)
    {float now=Time.realtimeSinceStartup;if(job.Step==step&&now-job.LastLog<5)return;job.Step=step;job.LastLog=now;Log.Out("[PurgeTruckStart] step="+step+" player="+job.Player.entityId+" vehicle="+job.Record.Vehicle+" stage="+job.Record.Stage+" position="+job.Player.position);if(inspect!=null)Stable(inspect,true);}
    internal static void PresentStart(EntityPlayerLocal player)
    {
        if(player==null)return;
        bool pending=jobs.TryGetValue(player.entityId,out var job);
        if(player.world?.IsRemote()==true&&ReferenceEquals(clientWorld,player.world)&&clientActor==player.entityId&&clientState!=1&&clientState!=5)
        {
            QueryClient(player);
            var driven=player.AttachedToEntity as EntityVehicle;
            bool attached=clientState>=4&&clientVehicle>0&&driven!=null&&driven.entityId==clientVehicle&&driven.GetAttached(0)==player;
            bool settled=attached&&clientStability.Observe(Time.realtimeSinceStartup,Stable(driven));
            if(attached&&!settled&&Time.realtimeSinceStartup>=nextClientGroundingLog)
            {nextClientGroundingLog=Time.realtimeSinceStartup+5;Stable(driven,true);}
            if(!attached)clientStability.Reset();
            pending=!settled;
            if(!pending&&player.IsSpawned())
            {
                XUiC_ProgressWindow.Close(player.PlayerUI);player.bIntroAnimActive=false;player.EnableCamera(true);player.SetControllable(true);
                clientOwnsProgress=false;
                clientState=5; // Retire this first-entry lease; later dismounts are ordinary gameplay.
                RebirthPurgeInformationTopics.Request(player,"PurgeIntro",false,Time.realtimeSinceStartup+5);
            }
        }
        if(!pending)return;
        // Native respawn can close loading and enable the camera in this Update.
        // Its postfix covers the same frame until the authoritative seat handoff.
        player.ClearMovementInputs();player.SetControllable(false);player.EnableCamera(false);
        if(job!=null)job.OwnsCamera=true;
        var manager=player.PlayerUI?.windowManager;
        bool creatorOpen=manager!=null&&(manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId)||manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId));
        if(creatorOpen||player.IsSpawned()&&RebirthCharacterCreationHoldService.IsHeld(player))
        {
            // Keep the world covered, but never put the depth-202 progress
            // popup above the creator/chooser that must complete this hold.
            if(job?.OwnsProgress==true||clientOwnsProgress)XUiC_ProgressWindow.Close(player.PlayerUI);
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
        {if(job.OwnsProgress)XUiC_ProgressWindow.Close(player.PlayerUI);player.bIntroAnimActive=false;player.EnableCamera(true);player.SetControllable(true);}
    }
    internal static bool BeforeWorldReady(PlayerMoveController controller)
    {
        var player=controller.entityPlayerLocal;var world=player?.world;
        if(!Enabled||world==null||player.IsSpawned()||
            (controller.respawnReason!=RespawnType.NewGame&&controller.respawnReason!=RespawnType.EnterMultiplayer))return true;
        if(world.IsRemote())
        {
            if(!ReferenceEquals(clientWorld,world)||clientActor!=player.entityId)
            {clientWorld=world;clientActor=player.entityId;clientVehicle=0;clientState=0;clientNext=0;clientOwnsProgress=false;clientStability.Reset();}
            QueryClient(player);
            if(clientState==1)return true; // Authority confirms no starter claim for this player.
            var replica=world.GetEntity(clientVehicle) as EntityVehicle;
            return clientState>=3&&replica!=null&&replica.ModelTransform!=null&&replica.RootTransform!=null;
        }
        if(!File.Exists(RecordPath))preparing.Add(player.entityId);
        Queue(world,player,controller.respawnReason);
        if(!jobs.TryGetValue(player.entityId,out var job))return !preparing.Contains(player.entityId);
        var vehicle=job.Record.Vehicle>0?world.GetEntity(job.Record.Vehicle) as EntityVehicle:null;
        return job.Failure==null&&job.Record.Stage>=1&&job.Ready&&vehicle!=null&&vehicle.ModelTransform!=null&&vehicle.RootTransform!=null;
    }
    private static void QueryClient(EntityPlayerLocal player)
    {
        if(Time.realtimeSinceStartup<clientNext||!Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var id))return;
        clientNext=Time.realtimeSinceStartup+.5f;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartQueryV1>().Setup(player.entityId,id));
    }
    internal static void Query(World world,ClientInfo sender,int actor,Guid id)
    {
        if(!ThreadManager.IsMainThread()||!Enabled||world==null||world.IsRemote()||sender==null||sender.entityId!=actor||
            !ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor),sender)||
            !Guid.TryParse(world.worldState.Guid,out var current)||id!=current)return;
        float now=Time.realtimeSinceStartup;
        if(nextReply.TryGetValue(actor,out float next)&&now<next)return;
        nextReply[actor]=now+.4f;
        if(preparing.Contains(actor)&&!jobs.ContainsKey(actor)&&world.GetEntity(actor) is EntityPlayer pending)
            Queue(world,pending,RespawnType.EnterMultiplayer);
        byte state=1;int vehicleId=0;
        if(jobs.TryGetValue(actor,out var job))
        {
            vehicleId=job.Record.Vehicle;var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
            state=(byte)(job.Failure==null&&job.Record.Stage>=1&&job.Ready&&vehicle!=null&&vehicle.ModelTransform!=null?3:2);
        }
        else if(finished.TryGetValue(actor,out vehicleId))state=4;
        else if(preparing.Contains(actor))state=2;
        sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeStartStatusV1>().Setup(actor,id,state,vehicleId));
    }
    internal static void Status(World world,int actor,Guid id,byte state,int vehicle)
    {
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(clientWorld,world)||clientActor!=actor||world.GetPrimaryPlayer()?.entityId!=actor||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var current)||id!=current)return;
        if(clientState==5)return;
        if(state<clientState||clientVehicle>0&&vehicle!=clientVehicle)return;
        clientState=state;clientVehicle=vehicle;
    }
    internal static void PrepareRemote(ClientInfo sender)
    {
        var world=GameManager.Instance?.World;
        if(Enabled&&FreshRemoteRequest&&world!=null&&!world.IsRemote()&&sender!=null&&world.GetEntity(sender.entityId) is EntityPlayer player)
        {if(!File.Exists(RecordPath))preparing.Add(player.entityId);Queue(world,player,RespawnType.EnterMultiplayer);}
    }
    internal static bool OwnsInitialStart(EntityPlayerLocal player)=>player!=null&&
        (jobs.ContainsKey(player.entityId)||finished.ContainsKey(player.entityId)||preparing.Contains(player.entityId)||
         ReferenceEquals(clientWorld,player.world)&&clientActor==player.entityId&&clientState>=2);
    private static void Write(StartRecord record)
    {
        string path=RecordPath;Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".tmp";
        File.WriteAllText(temporary,JsonConvert.SerializeObject(record));
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
    }
    private static void Spawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        var world=GameManager.Instance?.World;if(!Enabled||world==null||world.IsRemote())return;
        var player=world.GetEntity(data.EntityId) as EntityPlayer;if(player==null)return;
        // Server remote EntityPlayer.IsSpawned is not a client-entry witness.
        // Only the native event opens the post-entry driver/commit phase.
        nativeSpawnObserved.Add(player.entityId);
        if(jobs.TryGetValue(player.entityId,out var job))job.NativeSpawnObserved=true;
        Queue(world,player,data.RespawnType);
    }
    private static void Queue(World world,EntityPlayer player,RespawnType reason)
    {
        if(jobs.ContainsKey(player.entityId)||finished.ContainsKey(player.entityId))return;
        float now=Time.realtimeSinceStartup;
        if(nextPreparation.TryGetValue(player.entityId,out float next)&&now<next)return;
        nextPreparation[player.entityId]=now+5;
        if(!File.Exists(RecordPath)&&jobs.Count>0)return; // One world starter claim, as in the existing journal.
        string owner=Owner(player);if(string.IsNullOrEmpty(owner))return;
        try
        {
            StartRecord record=null;
            if(File.Exists(RecordPath))
            {
                var info=new FileInfo(RecordPath);if(info.Length>4096)throw new InvalidDataException("Purge starter record exceeds limit.");
                record=JsonConvert.DeserializeObject<StartRecord>(File.ReadAllText(RecordPath));
                if(record==null||record.World!=world.worldState.Guid)return;
                if(record.Owner!=owner||record.Stage==2){preparing.Remove(player.entityId);return;}
            }
            else
            {
                if(reason!=RespawnType.NewGame&&reason!=RespawnType.EnterMultiplayer)return;
                if(!RebirthPurgeForestRoad.Find(world,out var road))
                {RebirthPurgeForestRoad.RetryFailedSearch(world);return;}
                record=new StartRecord{World=world.worldState.Guid,Owner=owner,X=road.position.x,Y=road.position.y,Z=road.position.z,Heading=road.heading};
            }
            jobs[player.entityId]=new Job{World=world,Player=player,Record=record,NativeSpawnObserved=nativeSpawnObserved.Contains(player.entityId)};Step(jobs[player.entityId],"handoff.queued");
        }
        catch(Exception e){Log.Error("[PurgeTruckStart] Starter record retained; handoff refused: "+e.Message);}
    }
    // True from the first-entry request until the driver is released to gameplay (server job, or the client lease).
    internal static bool StartHandoffActive(EntityPlayerLocal player)=>player!=null&&(jobs.ContainsKey(player.entityId)||preparing.Contains(player.entityId)||
        player.world?.IsRemote()==true&&ReferenceEquals(clientWorld,player.world)&&clientActor==player.entityId&&clientState>=2&&clientState<=4);
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        // This client event also runs after the one-time truck handoff has finished.
        // Keep the existing journal-backed information queue alive on first entry and reload.
        if(!GameManager.IsDedicatedServer)RebirthPurgeInformationService.Pulse();
        if(jobs.Count==0)return;
        foreach(var pair in jobs.ToArray())
        {
            var job=pair.Value;var p=job.Player;float now=Time.realtimeSinceStartup;
            if(!ReferenceEquals(GameManager.Instance?.World,job.World)||!ReferenceEquals(job.World.GetEntity(pair.Key),p)||job.NativeSpawnObserved&&p.IsDead()){jobs.Remove(pair.Key);continue;}
            if(now<job.Next)continue;job.Next=now+.25f;
            if(job.Failure!=null)continue;
            if(job.ReadyAt==0)job.ReadyAt=now;
            try
            {
                var record=job.Record;
                var vehicle=record.Vehicle>0?(job.World.GetEntity(record.Vehicle) as EntityVehicle??VehicleManager.Instance.vehiclesActive.FirstOrDefault(v=>v.entityId==record.Vehicle)):null;
                if(vehicle==null)
                {
                    if(record.Vehicle>0&&VehicleManager.Instance.vehiclesUnloaded.Any(v=>v.id==record.Vehicle))continue;
                    if(record.Stage>0){if(now-job.ReadyAt>120)throw new InvalidOperationException("Saved starter truck is unavailable; refusing a duplicate.");continue;}
                    if(job.World.GetChunkFromWorldPos(new Vector3i(record.Position))==null){Step(job,"wait.road-chunk");continue;}
                    Step(job,"road.footprint-check");
                    if(!Clear(job.World,record,p))
                    {
                        bool relocated=false;
                        for(int distance=8;distance<=64&&!relocated;distance+=8)foreach(int sign in new[]{1,-1})
                        {
                            if(!RebirthPurgeForestRoad.Nearby(job.World,record.Position,record.Heading,distance*sign,out var alternate))continue;
                            var probe=new StartRecord{X=alternate.position.x,Y=alternate.position.y,Z=alternate.position.z,Heading=record.Heading};
                            if(!Clear(job.World,probe,p))continue;
                            record.X=probe.X;record.Y=probe.Y;record.Z=probe.Z;relocated=true;
                            Log.Out("[PurgeTruckStart] step=road.obstacle-avoided offset="+(distance*sign)+" position="+record.Position);break;
                        }
                        if(!relocated){Step(job,"wait.clear-loaded-road-footprint");job.Next=now+2;continue;}
                    }
                    Step(job,"truck.create-bare");
                    vehicle=RebirthVehicleEntityFactory.Create(job.World,"rebirthCamper80s",record.Position,new Vector3(0,record.Heading,0));
                    if(!vehicle)throw new InvalidOperationException("Starter truck definition unavailable.");
                    if(RebirthCamperBodyAttachment.HasBody(vehicle))throw new InvalidOperationException("Starter truck unexpectedly includes the camper body.");
                    RebirthVehicleEntityFactory.AssignOwner(vehicle,p);Step(job,"truck.owner-and-bare-state-confirmed");
                    int maximum=vehicle.vehicle.GetUpdatedItemValue().MaxUseTimes;RebirthVehicleEntityFactory.ApplyRestoredState(vehicle,maximum,maximum,1f);
                    PrepareStarterParts(vehicle,job.World);
                    vehicle.SetLocked(true);
                    if(!vehicle.IsLocked())throw new InvalidOperationException("Starter truck lock did not take effect.");
                    vehicle.vehicle.GetUpdatedItemValue().SetMetadata("RebirthPurgeStarterOwner",record.Owner);
                    record.Vehicle=vehicle.entityId;Write(record);Step(job,"record.prepared"); // Prepared identity precedes world publication.
                    job.World.SpawnEntityInWorld(vehicle);vehicle.SendSyncData(2);record.Stage=1;Write(record);VehicleManager.Instance.TriggerSave();Step(job,"truck.published");
                    job.Next=now+.5f;continue;
                }
                if(!RebirthCamperRadial.IsCamper(vehicle)||!vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthPurgeStarterOwner",out string stamp)||stamp!=record.Owner)
                    throw new InvalidDataException("Starter identity does not match recorded truck.");
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
                if(!RebirthPurgeStartHandoff.CanRun(job.NativeSpawnObserved,p is EntityPlayerLocal,p.IsSpawned())){Step(job,"wait.native-spawn-with-truck-ready");continue;}
                if(RebirthCharacterCreationHoldService.IsHeld(p)){Step(job,"wait.character-creation");continue;}
                if(p.AttachedToEntity!=vehicle){Step(job,"driver.attach-request");p.StartAttachToEntity(vehicle,0);}
                if(p.AttachedToEntity!=vehicle||vehicle.GetAttached(0)!=p){Step(job,"wait.driver-attachment");continue;}
                if(!job.AttachmentVerified){job.AttachmentVerified=true;job.Stability.Reset();job.Ready=false;}
                // Remote driver attachment transfers native physics ownership to
                // that client. Its own PresentStart performs this post-attach gate.
                if(!vehicle.isEntityRemote&&!job.Stability.Observe(now,Stable(vehicle)))
                {Step(job,"wait.attached-truck-grounded-and-stable",vehicle);continue;}
                Step(job,"driver.attached");
                if(p is EntityPlayerLocal local){local.bIntroAnimActive=false;local.SetControllable(true);}
                Step(job,"gameplay.enable");record.Stage=2;Write(record);VehicleManager.Instance.TriggerSave();Reveal(job);
                if(p is EntityPlayerLocal introPlayer)RebirthPurgeInformationTopics.Request(introPlayer,"PurgeIntro",false,Time.realtimeSinceStartup+5);
                finished[p.entityId]=record.Vehicle;preparing.Remove(p.entityId);jobs.Remove(pair.Key);
                Log.Out("[PurgeTruckStart] Initial truck ready on a rural pine-forest road; driver attached.");
            }
            catch(Exception e){job.Failure=e.Message;Log.Error("[PurgeTruckStart] Handoff stopped under loading cover without creating another truck: "+e.Message);}
        }
    }
    private static bool Stable(EntityVehicle vehicle,bool diagnostic=false)
    {
        if(vehicle==null||vehicle.world==null||vehicle.ModelTransform==null||vehicle.PhysicsTransform==null||vehicle.vehicleRB==null||vehicle.isEntityRemote)
        {if(diagnostic)Log.Out("[PurgeTruckStart] step=grounding.detail vehicle="+vehicle?.entityId+" pending=bindings-or-physics-owner origin="+Origin.position);return false;}
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
        if(diagnostic)Log.Out("[PurgeTruckStart] step=grounding.detail vehicle="+vehicle.entityId+" entity="+vehicle.position+" rigidbody="+position+
            " velocity="+rigidbody.velocity+" angular="+rigidbody.angularVelocity+" contacts="+grounded+" loaded="+loaded+" upright="+vehicle.PhysicsTransform.up.y+
            " poseErrorSquared="+poseError+" terrain="+height+" origin="+Origin.position+" kinematic="+rigidbody.isKinematic+" RBActive="+vehicle.RBActive);
        return RebirthPurgeStartStability.Accept(loaded,grounded,rigidbody.velocity.sqrMagnitude,rigidbody.angularVelocity.sqrMagnitude,
            vehicle.PhysicsTransform.up.y,poseError,position.y-height);
    }
    private static bool Clear(World world,StartRecord record,EntityPlayer owner)
    {
        var rotation=Quaternion.Euler(0,record.Heading,0);
        // Nearby-road relocation must preserve the staging separation too; an
        // alternate 8m behind the truck would otherwise enclose the staged actor.
        if(owner!=null)
        {
            var staged=Quaternion.Inverse(rotation)*(owner.position-record.Position);
            if(!RebirthPurgeStartFootprint.PlayerOutside(staged.x,staged.z))return false;
        }
        var provider=world.ChunkCache.ChunkProvider as ChunkProviderGenerateWorld;
        if(provider?.m_TerrainGenerator==null)return false;
        var centre=record.Position+Vector3.up*2;
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
                // Multiblock wreck child cells can carry an air block type.
                if(block.ischild)return false;
                if(block.isair)continue;
                if(y<=Mathf.CeilToInt(terrain)&&block.Block.shape.IsTerrain())continue;
                Log.Out("[PurgeTruckStart] step=road.obstruction block="+block.Block.GetBlockName()+" position="+new Vector3i(x,y,z));return false;
            }
        }
        foreach(var entity in world.Entities.list)
        {
            if(entity==null||ReferenceEquals(entity,owner))continue;
            var bounds=entity.boundingBox;var local=Quaternion.Inverse(rotation)*(bounds.center-record.Position);
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
        if(!RebirthPurgeTruckStart.CanChoose(_world))return true;
        Log.Out("[PurgeTruckStart] step=native.spawn-selection");
        if(!RebirthPurgeForestRoad.Find(_world,out var road))return true;
        if(!RebirthPurgeForestRoad.PlayerAnchor(_world,road,out var player))return true;
        Log.Out("[PurgeTruckStart] step=player.staging-anchor truck="+road.position+" player="+player.position);
        __result=player;return false;
    }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.RequestToSpawnPlayer))]
internal static class RebirthPurgeFreshRemoteRequest
{
    private static void Prefix(GameManager __instance,ClientInfo _cInfo,out bool __state)
    {
        __state=RebirthPurgeTruckStart.FreshRemoteRequest;
        RebirthPurgeTruckStart.FreshRemoteRequest=RebirthPurgeTruckStart.Enabled&&_cInfo!=null
            &&!PlayerDataFile.Exists(GameIO.GetPlayerDataDir(),__instance.getPersistentPlayerID(_cInfo).CombinedString);
    }
    private static void Postfix(ClientInfo _cInfo)=>RebirthPurgeTruckStart.PrepareRemote(_cInfo);
    private static Exception Finalizer(Exception __exception,bool __state){RebirthPurgeTruckStart.FreshRemoteRequest=__state;return __exception;}
}

// Called immediately before native Spawned=true and loading-screen close.
[HarmonyPatch(typeof(PlayerMoveController),nameof(PlayerMoveController.CheckWorldReadyForSpawn))]
internal static class RebirthPurgeInitialWorldReadiness
{
    private static void Postfix(PlayerMoveController __instance,ref bool __result)
    {if(__result)__result=RebirthPurgeTruckStart.BeforeWorldReady(__instance);}
}
[HarmonyPatch]
internal static class RebirthPurgeInitialRevive
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(AvatarController).Assembly.GetTypes().Where(t=>typeof(AvatarController).IsAssignableFrom(t))
        .Select(t=>AccessTools.DeclaredMethod(t,nameof(AvatarController.PlayPlayerFPRevive))).Where(m=>m!=null&&!m.IsAbstract);
    private static bool Prefix(AvatarController __instance)=>!RebirthPurgeTruckStart.Enabled||!ReferenceEquals(__instance.Entity,RebirthPurgeTruckStart.IntroOwner);
}
[HarmonyPatch(typeof(EntityPlayerLocal),nameof(EntityPlayerLocal.AfterPlayerRespawn))]
internal static class RebirthPurgeInitialIntroScope
{
    private static void Prefix(EntityPlayerLocal __instance,RespawnType __0,out EntityPlayerLocal __state)
    {__state=RebirthPurgeTruckStart.IntroOwner;RebirthPurgeTruckStart.IntroOwner=(__0==RespawnType.NewGame||__0==RespawnType.EnterMultiplayer)&&RebirthPurgeTruckStart.OwnsInitialStart(__instance)?__instance:null;}
    private static Exception Finalizer(Exception __exception,EntityPlayerLocal __state){RebirthPurgeTruckStart.IntroOwner=__state;return __exception;}
}

[HarmonyPatch(typeof(PlayerMoveController),nameof(PlayerMoveController.Update))]
internal static class RebirthPurgeNativeSpawnTrace
{
    private static float next;
    private static void Postfix(PlayerMoveController __instance){if(RebirthPurgeTruckStart.Enabled)RebirthPurgeTruckStart.PresentStart(__instance.entityPlayerLocal);}
    [HarmonyPriority(Priority.First)]
    private static void Prefix(PlayerMoveController __instance)
    {
        var player=__instance.entityPlayerLocal;
        if(!RebirthPurgeTruckStart.Enabled||player==null||player.IsSpawned()||Time.realtimeSinceStartup<next)return;
        next=Time.realtimeSinceStartup+5;
        Log.Out("[PurgeTruckStart] step=wait.native-respawn player="+player.entityId+" reason="+__instance.respawnReason+" timer="+__instance.respawnTime+" position="+player.position+" uiReady="+player.PlayerUI.xui.IsReady);
    }
}
