using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// The owner observes native self outcomes; the server calculates/persists training and publishes its existing owner projection.
internal static class RebirthSelfMedicalPracticeAwards
{
    sealed class Life { internal readonly object Stamp=new object(); }
    static ConditionalWeakTable<EntityPlayer,Life> lives=new ConditionalWeakTable<EntityPlayer,Life>();
    internal sealed class OwnerScope
    {
        internal EntityPlayer Player;internal World World;internal EntityBuffs Buffs;
        internal ConnectionManager Connection;internal INetConnection Peer;internal string Creation,Definition;
        internal RebirthWorldCharacterRecord Record;internal string Canonical,Storage;internal object Life;
        internal bool Current(){
            if(Player==null || Player.IsDead() || !ReferenceEquals(Player.world,World) || !ReferenceEquals(Player.Buffs,Buffs)
                || !ReferenceEquals(GameManager.Instance?.World,World) || !ReferenceEquals(World.GetEntity(Player.entityId),Player)
                || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Connection)
                || !lives.TryGetValue(Player,out var life) || !ReferenceEquals(life.Stamp,Life)
                || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || Definition!=RebirthSurvivorDefinitionRegistry.SemanticHash)return false;
            if(World.IsRemote())return RebirthSelfMedicalSimulationOwner.Current(Player)
                && Connection.connectionToServer!=null && Connection.connectionToServer.Length>0
                && ReferenceEquals(Connection.connectionToServer[0],Peer) && !Peer.IsDisconnected()
                && RebirthSurvivorRequestScope.Matches(Creation,RebirthSurvivorClientState.GetProjectedCreationId(Player));
            return Connection.IsServer && RebirthWorldCharacterRepository.IsServerAuthority
                && RebirthWorldCharacterService.TryGet(Player,out var record) && ReferenceEquals(record,Record) && record.IsComplete
                && RebirthSurvivorRequestScope.Matches(Creation,record.Origin?.CreationId)
                && RebirthWorldCharacterService.TryGetIdentity(Player,out var identity) && identity!=null
                && Canonical==identity.CanonicalId && Storage==identity.StorageKey;
        }
    }
    internal static OwnerScope Capture(EntityPlayer player,bool remoteServerReplica=false){
        if(player?.world==null || player.IsDead() || !ReferenceEquals(GameManager.Instance?.World,player.world)
            || !ReferenceEquals(player.world.GetEntity(player.entityId),player) || player.Buffs==null
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return null;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;if(connection==null)return null;
        if(!remoteServerReplica && !RebirthSelfMedicalSimulationOwner.Current(player))return null;
        string creation;var scope=new OwnerScope{Player=player,World=player.world,Buffs=player.Buffs,Connection=connection,
            Definition=RebirthSurvivorDefinitionRegistry.SemanticHash,Life=lives.GetValue(player,_=>new Life()).Stamp};
        if(player.world.IsRemote()){
            if(!RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out creation))return null;
            scope.Peer=connection.connectionToServer[0];
        }else{
            if(!connection.IsServer || !RebirthWorldCharacterService.TryGet(player,out var record) || record==null || !record.IsComplete
                || !RebirthWorldCharacterService.TryGetIdentity(player,out var identity) || identity==null
                || !RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId,out creation))return null;
            scope.Record=record;scope.Canonical=identity.CanonicalId;scope.Storage=identity.StorageKey;
        }
        scope.Creation=creation;return scope.Current()?scope:null;
    }
    internal sealed class Claim {internal OwnerScope Scope;internal string Item,Receipt;internal float Fraction;internal long Sequence;}
    sealed class OwnerQueue {internal readonly Queue<Claim> Claims=new Queue<Claim>();internal readonly HashSet<Reservation> Reservations=new HashSet<Reservation>();internal OwnerScope Scope;internal Guid Epoch;internal long Next=1;internal float LastSend=-10;}
    sealed class ServerSession {internal OwnerScope Scope;internal INetConnection Peer;internal Guid Epoch=Guid.NewGuid();internal long Next=1;internal Claim Pending,Last;}
    static ConditionalWeakTable<EntityPlayer,OwnerQueue> owners=new ConditionalWeakTable<EntityPlayer,OwnerQueue>();
    static ConditionalWeakTable<ClientInfo,ServerSession> servers=new ConditionalWeakTable<ClientInfo,ServerSession>();
    const int Capacity=64;
    // Bound reserved clinical observations + completed claims before native dose completion.
    internal sealed class Reservation
    {
        internal readonly Claim Claim;
        private readonly OwnerQueue Queue;
        internal bool Published,Released;
        private Reservation(OwnerQueue queue,OwnerScope scope,string item,string receipt)
        {Queue=queue;Claim=new Claim{Scope=scope,Item=item,Receipt=receipt};}
        internal static Reservation Acquire(OwnerScope scope,string item,string receipt)
        {
            if(scope==null || !scope.Current() || !RebirthDifficultyPractice.HasTreatment(item) || !Guid.TryParseExact(receipt,"N",out _))return null;
            var queue=owners.GetValue(scope.Player,_=>new OwnerQueue{Scope=scope});
            if(!queue.Scope.Current())return null; // Preserve prior accepted custody; never remint it for a new owner.
            foreach(var claim in queue.Claims)if(claim.Receipt==receipt)return null;
            foreach(var held in queue.Reservations)if(held.Claim.Receipt==receipt)return null;
            if(queue.Claims.Count+queue.Reservations.Count>=Capacity)return null;
            var reservation=new Reservation(queue,scope,item,receipt);
            queue.Reservations.Add(reservation);return reservation;
        }
        internal bool Commit(float fraction)
        {
            if(Released || !Claim.Scope.Current() || !Queue.Scope.Current() || !Valid(Claim.Item,fraction))return false;
            if(Published)return Claim.Fraction==fraction;
            if(!Queue.Reservations.Remove(this))return false;
            Claim.Fraction=fraction;Published=true;Queue.Claims.Enqueue(Claim);return true;
        }
        internal void Cancel()
        {
            if(Published || Released)return; // Completed custody cannot be erased by a late action finalizer.
            Released=true;Queue.Reservations.Remove(this);
        }
    }
    internal static Reservation Reserve(OwnerScope scope,string item,string receipt)=>Reservation.Acquire(scope,item,receipt);
    internal static bool Submit(Reservation reservation,float fraction)=>reservation!=null && reservation.Commit(fraction);
    internal static void Release(Reservation reservation){reservation?.Cancel();}
    static bool Valid(string item,float fraction)=>RebirthDifficultyPractice.HasTreatment(item) && fraction>0 && fraction<=1 && !float.IsInfinity(fraction) && !float.IsNaN(fraction) && (RebirthMedicalPractice.HealthUnit(item)>0 || RebirthMedicalPractice.InfectionUnit(item)>0 || fraction==1f);
    internal static bool AwardServer(EntityPlayer player,string item,float fraction,string receipt){
        if(player?.world==null || player.world.IsRemote() || !Valid(item,fraction) || string.IsNullOrEmpty(receipt))return false;
        float level,progress;if(!RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player,"skill.medicine",out level,out progress))return false;
        var evidence=new RebirthSkillTrainingEvidence{SkillId="skill.medicine",SourceKey="phase9:medicine:"+item,
            DurableReceiptId=receipt,ReferenceDescription="completed native self dose and realized treatment outcome",
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=fraction,
            DiscreteRawAward=RebirthDifficultyPractice.Treatment(item,level+progress)*fraction};
        RebirthSkillTrainingComputation computation;float gained,attribute;
        bool awarded=RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out computation,out gained,out attribute);
        if(awarded && (item=="medicalSplint" || item=="medicalPlasterCast")){
            float insightApplied;bool alreadyEarned;
            RebirthTheoryProgressionService.TryAwardInsight(player,"insight.medicine.fracture_successfully_treated","medical-treatment:"+item,out insightApplied,out alreadyEarned);
        }
        return awarded;
    }
    internal static void Tick(){
        var world=GameManager.Instance?.World;if(world==null)return;
        var player=world.GetPrimaryPlayer();if(player==null || !owners.TryGetValue(player,out var queue))return;
        if(!queue.Scope.Current())return; // Suspended exact claims remain bounded; no award to a stale/new owner.
        if(queue.Claims.Count==0 || Time.time-queue.LastSend<2f)return;queue.LastSend=Time.time;
        var claim=queue.Claims.Peek();if(!claim.Scope.Current())return;
        if(!world.IsRemote()){
            if(AwardServer(player,claim.Item,claim.Fraction,"selfmedical:"+claim.Scope.Creation+":"+claim.Receipt))queue.Claims.Dequeue();return;
        }
        if(queue.Epoch==Guid.Empty){
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthSelfMedicalPracticeResponse));
            queue.Scope.Connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSelfMedicalPracticeRequest>()
                .Setup(player.entityId,queue.Scope.Creation,queue.Scope.Definition,Guid.Empty,0,"",0));return;
        }
        if(claim.Sequence==0)claim.Sequence=queue.Next;
        queue.Scope.Connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSelfMedicalPracticeRequest>()
            .Setup(player.entityId,queue.Scope.Creation,queue.Scope.Definition,queue.Epoch,claim.Sequence,claim.Item,claim.Fraction));
    }
    static bool SenderCurrent(World world,EntityPlayer player,ClientInfo sender){
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World,world) || player==null || !player.isEntityRemote
            || sender==null || !sender.loginDone || sender.entityId!=player.entityId || connection==null || !connection.IsServer
            || connection.Clients==null || !ReferenceEquals(connection.Clients.ForEntityId(player.entityId),sender)
            || sender.netConnection==null || sender.netConnection.Length==0 || sender.netConnection[0]==null || sender.netConnection[0].IsDisconnected())return false;
        foreach(var client in connection.Clients.List)if(ReferenceEquals(client,sender))return true;return false;
    }
    internal static void ReceiveRequest(World world,ClientInfo sender,int entityId,string creation,string definition,Guid epoch,long sequence,string item,float fraction,bool nativeSenderValid){
        var player=world?.GetEntity(entityId) as EntityPlayer;
        if(!nativeSenderValid || !SenderCurrent(world,player,sender))return;
        var scope=Capture(player,true);if(scope==null || !RebirthSurvivorRequestScope.Matches(creation,scope.Creation) || definition!=scope.Definition)return;
        var session=servers.GetValue(sender,_=>new ServerSession{Scope=scope,Peer=sender.netConnection[0]});
        if(!session.Scope.Current() || !ReferenceEquals(session.Scope.Player,player) || !ReferenceEquals(session.Peer,sender.netConnection[0])){
            servers.Remove(sender);session=servers.GetValue(sender,_=>new ServerSession{Scope=scope,Peer=sender.netConnection[0]});
        }
        if(epoch==Guid.Empty && sequence==0 && item=="" && fraction==0){Reply(sender,session,0);return;}
        if(epoch!=session.Epoch || sequence<1 || !Valid(item,fraction))return;
        if(sequence==session.Next-1 && session.Last!=null){
            if(session.Last.Sequence==sequence && session.Last.Item==item && session.Last.Fraction==fraction)Reply(sender,session,sequence);return;
        }
        if(sequence!=session.Next || sequence==long.MaxValue)return;
        if(session.Pending==null)session.Pending=new Claim{Scope=session.Scope,Sequence=sequence,Item=item,Fraction=fraction};
        if(session.Pending.Sequence!=sequence || session.Pending.Item!=item || session.Pending.Fraction!=fraction)return;
        string receipt="selfmedical:"+scope.Creation+":"+epoch.ToString("N")+":"+sequence;
        if(!AwardServer(player,item,fraction,receipt))return; // Failed persistence retains immutable body for exact retry.
        session.Last=session.Pending;session.Pending=null;session.Next++;Reply(sender,session,sequence);
    }
    static void Reply(ClientInfo owner,ServerSession session,long sequence){
        NetPackageManager.GetPackageId(typeof(NetPackageRebirthSelfMedicalPracticeResponse));
        owner.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSelfMedicalPracticeResponse>()
            .Setup(session.Scope.Player.entityId,session.Scope.Creation,session.Scope.Definition,session.Epoch,sequence));
    }
    internal static void ReceiveResponse(World world,ClientInfo sender,int entityId,string creation,string definition,Guid epoch,long sequence){
        var player=world?.GetPrimaryPlayer();if(world==null || !world.IsRemote() || sender!=null || player==null || player.entityId!=entityId
            || !owners.TryGetValue(player,out var queue) || !queue.Scope.Current() || epoch==Guid.Empty
            || !RebirthSurvivorRequestScope.Matches(creation,queue.Scope.Creation) || definition!=queue.Scope.Definition)return;
        if(sequence==0){if(queue.Epoch==Guid.Empty)queue.Epoch=epoch;return;}
        if(epoch!=queue.Epoch || queue.Claims.Count==0 || sequence!=queue.Next || queue.Claims.Peek().Sequence!=sequence)return;
        queue.Claims.Dequeue();queue.Next++;queue.LastSend=-10;
    }
    internal static void Died(EntityPlayer player,bool ranOriginal){if(ranOriginal && player!=null && player.IsDead()){lives.Remove(player);owners.Remove(player);}}
    internal static void Reset(){RebirthMedicalPractice.ClearRuntime();lives=new ConditionalWeakTable<EntityPlayer,Life>();owners=new ConditionalWeakTable<EntityPlayer,OwnerQueue>();servers=new ConditionalWeakTable<ClientInfo,ServerSession>();}
    static int installStage;
    internal static void Install(){
        if(installStage<1){ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));installStage=1;}
        if(installStage<2){ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnStarting));installStage=2;}
        if(installStage<3){ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnShutdown));installStage=3;}
        if(installStage<4){ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnExit));installStage=4;}
    }
    static void OnUpdate(ref ModEvents.SGameUpdateData data)=>Tick();
    static void OnStarting(ref ModEvents.SGameStartingData data)=>Reset();
    static void OnShutdown(ref ModEvents.SWorldShuttingDownData data)=>Reset();
    static void OnExit(ref ModEvents.SGameShutdownData data)=>Reset();
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.SetDead))]
internal static class RebirthSelfMedicalPracticeDeathPatch
{
    static void Postfix(EntityAlive __instance,bool __runOriginal)=>RebirthSelfMedicalPracticeAwards.Died(__instance as EntityPlayer,__runOriginal);
}

