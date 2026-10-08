using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

internal enum RebirthTheorySoloStatusMode:byte { Unavailable,NeedsWork,Ready,Active,Paused,Saving,Cooldown,Maximum,Cancelling }
internal sealed class RebirthTheorySoloStatus
{
    internal int Player,Count,Required;
    internal string Creation,Subject,SessionId;
    internal bool Cancellable;
    internal Guid Request;
    internal RebirthTheorySoloStatusMode Mode;
    internal float Duration,Elapsed,Cooldown;
    internal bool Valid()=>Player>0&&Request!=Guid.Empty&&RebirthSurvivorRequestScope.TryNormalize(Creation,out var c)&&c==Creation&&RebirthTheorySoloRegistry.TryGet(Subject,out _)&&Mode<=RebirthTheorySoloStatusMode.Cancelling&&(string.IsNullOrEmpty(SessionId)||Guid.TryParseExact(SessionId,"N",out var session)&&session!=Guid.Empty)&&(!Cancellable||!string.IsNullOrEmpty(SessionId))&&Count>=0&&Count<=16&&Required>=1&&Required<=16&&RebirthTheorySoloState.Finite(Duration)&&Duration>0&&Duration<=3600&&RebirthTheorySoloState.Finite(Elapsed)&&Elapsed>=0&&Elapsed<=Duration&&RebirthTheorySoloState.Finite(Cooldown)&&Cooldown>=0&&Cooldown<=86400;
    internal string Text()
    {
        string state;
        switch(Mode){
        case RebirthTheorySoloStatusMode.Active:state=string.Format(RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusActive","Studying: {0}/{1} seconds"),Math.Floor(Elapsed),Duration);break;
        case RebirthTheorySoloStatusMode.Paused:state=string.Format(RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusPaused","Paused: {0}/{1} seconds"),Math.Floor(Elapsed),Duration);break;
        case RebirthTheorySoloStatusMode.Ready:state=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusReady","Ready to reflect");break;
        case RebirthTheorySoloStatusMode.Cancelling:state=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelWaiting","Cancellation is waiting to be saved.");break;
        case RebirthTheorySoloStatusMode.Saving:state=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryWaitingSave","Study is waiting to be saved.");break;
        case RebirthTheorySoloStatusMode.Cooldown:state=string.Format(RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusCooldown","Practice first: {0} seconds remaining"),Math.Ceiling(Cooldown));break;
        case RebirthTheorySoloStatusMode.Maximum:return RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusMaximum","Theory is complete.");
        case RebirthTheorySoloStatusMode.Unavailable:return RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusUnavailable","Solo study is not available for this skill yet.");
        default:state=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryNeedWork","Complete more relevant work before studying this skill.");break;}
        return string.Format(RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusCount","{0}/{1} relevant tasks"),Count,Required)+" · "+state;
    }
}
[Preserve]
public sealed class NetPackageRebirthTheorySoloStatusRequest:NetPackage
{
    private int player;private string creation,subject;private Guid request;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthTheorySoloStatusRequest Setup(int p,string c,string s,Guid r){player=p;creation=c;subject=s;request=r;return this;}
    public override void read(PooledBinaryReader reader){var b=(BinaryReader)reader;player=b.ReadInt32();creation=RebirthSurvivorNetworkCodec.ReadString(b,128);subject=RebirthSurvivorNetworkCodec.ReadString(b,128);request=new Guid(b.ReadBytes(16));}
    public override void write(PooledBinaryWriter writer){base.write(writer);var b=(BinaryWriter)writer;b.Write(player);RebirthSurvivorNetworkCodec.WriteString(b,creation,128);RebirthSurvivorNetworkCodec.WriteString(b,subject,128);b.Write(request.ToByteArray());}
    public int GetLength()=>22+RebirthSurvivorNetworkCodec.EstimateString(creation,128)+RebirthSurvivorNetworkCodec.EstimateString(subject,128);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||connection==null||!connection.IsServer||Sender==null||!ValidEntityIdForSender(player)||request==Guid.Empty||!RebirthTheorySoloStatusServer.Allow(world,Sender))return;
        var actor=world.GetEntity(player) as EntityPlayer;
        if(!RebirthTheorySoloStatusServer.TryBuild(actor,creation,subject,request,out var status))return;
        NetPackageManager.GetPackageId(typeof(NetPackageRebirthTheorySoloStatusResponse));
        connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthTheorySoloStatusResponse>().Setup(status),_attachedToEntityId:player);
    }
}
[Preserve]
public sealed class NetPackageRebirthTheorySoloStatusResponse:NetPackage
{
    private RebirthTheorySoloStatus value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthTheorySoloStatusResponse Setup(RebirthTheorySoloStatus status){value=status;return this;}
    public override void read(PooledBinaryReader reader){var b=(BinaryReader)reader;value=new RebirthTheorySoloStatus{Player=b.ReadInt32(),Creation=RebirthSurvivorNetworkCodec.ReadString(b,128),Subject=RebirthSurvivorNetworkCodec.ReadString(b,128),Request=new Guid(b.ReadBytes(16)),SessionId=RebirthSurvivorNetworkCodec.ReadString(b,32),Cancellable=b.ReadBoolean(),Mode=(RebirthTheorySoloStatusMode)b.ReadByte(),Count=b.ReadByte(),Required=b.ReadByte(),Duration=b.ReadSingle(),Elapsed=b.ReadSingle(),Cooldown=b.ReadSingle()};if(!value.Valid())throw new InvalidDataException("Invalid solo study status");}
    public override void write(PooledBinaryWriter writer){if(value==null||!value.Valid())throw new InvalidDataException("Invalid solo study status");base.write(writer);var b=(BinaryWriter)writer;b.Write(value.Player);RebirthSurvivorNetworkCodec.WriteString(b,value.Creation,128);RebirthSurvivorNetworkCodec.WriteString(b,value.Subject,128);b.Write(value.Request.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(b,value.SessionId,32);b.Write(value.Cancellable);b.Write((byte)value.Mode);b.Write((byte)value.Count);b.Write((byte)value.Required);b.Write(value.Duration);b.Write(value.Elapsed);b.Write(value.Cooldown);}
    public int GetLength()=>38+RebirthSurvivorNetworkCodec.EstimateString(value?.SessionId,32)+RebirthSurvivorNetworkCodec.EstimateString(value?.Creation,128)+RebirthSurvivorNetworkCodec.EstimateString(value?.Subject,128);
    public override void ProcessPackage(World world,GameManager callbacks){RebirthTheorySoloStatusClient.Receive(world,value);}
}
internal static class RebirthTheorySoloStatusServer
{
    private static World activeWorld;private static readonly Dictionary<object,float> Throttle=new Dictionary<object,float>();
    internal static bool Allow(World world,object sender)
    {
        if(!ReferenceEquals(activeWorld,world)){activeWorld=world;Throttle.Clear();}
        float now=Time.realtimeSinceStartup;if(Throttle.TryGetValue(sender,out var prior)&&now>=prior&&now-prior<.75f)return false;
        if(Throttle.Count>=128&&!Throttle.ContainsKey(sender)){var expired=new List<object>();foreach(var pair in Throttle)if(now-pair.Value>30)expired.Add(pair.Key);foreach(var key in expired)Throttle.Remove(key);if(Throttle.Count>=128)return false;}
        Throttle[sender]=now;return true;
    }
    internal static bool TryBuild(EntityPlayer player,string creation,string subject,Guid request,out RebirthTheorySoloStatus status)
    {
        status=null;
        if(player==null||player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out var record)||record.Origin?.CreationId!=creation||!RebirthTheorySoloRegistry.TryGet(subject,out var rule)||request==Guid.Empty)return false;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            if(!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
            var p=record.Progression;var solo=p.SoloTheory;float theory=p.SkillKnowledge.TryGetValue(subject,out var knowledge)&&knowledge!=null?knowledge.Value:100;
            var result=new RebirthTheorySoloStatus{Player=player.entityId,Creation=creation,Subject=subject,Request=request,Required=rule.MinimumOutcomes,Duration=rule.Duration};
            if(solo?.Session?.Subject==subject){result.SessionId=solo.Session.Id;result.Cancellable=!solo.Session.Consumed&&(p.PendingTheoryStudy==null||p.PendingTheoryStudy.Mode=="solo"&&p.PendingTheoryStudy.Id==solo.Session.Id&&!p.SkillAwardReceipts.Contains(p.PendingTheoryStudy.Receipt));}
            if(!RebirthTheorySoloAvailability.HasOriginalWork(solo,subject,creation))result.Mode=RebirthTheorySoloStatusMode.Unavailable;
            else if(!string.IsNullOrEmpty(solo?.CancelPendingId)&&solo.CancelPendingSubject==subject){result.Mode=RebirthTheorySoloStatusMode.Cancelling;result.SessionId=solo.CancelPendingId;result.Cancellable=true;}
            else if(theory>=100)result.Mode=RebirthTheorySoloStatusMode.Maximum;
            else
            {
                if(solo!=null)foreach(var e in solo.Evidence)if(e.Subject==subject&&rule.Relevant(theory,e.Difficulty))result.Count=Math.Min(16,result.Count+1);
                if(solo?.Session?.Subject==subject){result.SessionId=solo.Session.Id;result.Cancellable=!solo.Session.Consumed&&(p.PendingTheoryStudy==null||p.PendingTheoryStudy.Mode=="solo"&&p.PendingTheoryStudy.Id==solo.Session.Id&&!p.SkillAwardReceipts.Contains(p.PendingTheoryStudy.Receipt));result.Elapsed=solo.Session.Elapsed;result.Duration=solo.Session.Duration;result.Mode=p.PendingTheoryStudy?.Id==solo.Session.Id?RebirthTheorySoloStatusMode.Saving:RebirthTheorySoloService.IsRunning(player,solo.Session.Id)?RebirthTheorySoloStatusMode.Active:RebirthTheorySoloStatusMode.Paused;}
                else if(solo!=null&&solo.LastSettlement.TryGetValue(subject,out var settled)&&record.Condition.ActivePlaySeconds-settled<rule.Cooldown){result.Cooldown=(float)Math.Min(86400,Math.Max(0,rule.Cooldown-(record.Condition.ActivePlaySeconds-settled)));result.Mode=RebirthTheorySoloStatusMode.Cooldown;}
                else result.Mode=result.Count>=result.Required?RebirthTheorySoloStatusMode.Ready:RebirthTheorySoloStatusMode.NeedsWork;
            }
            if(!result.Valid())return false;status=result;return true;
        }
    }
}
internal static class RebirthTheorySoloStatusClient
{
    private static World world;private static EntityPlayerLocal owner;private static object nativeConnection;private static int entity;private static string creation,subject;private static Guid request;private static bool forceRefresh;private static float next,received;private static RebirthTheorySoloStatus status;
    internal static bool TryCancellation(EntityPlayerLocal player,string selected,out string id)
    {
        id=null;if(status==null||!status.Cancellable||!status.Valid()||!ReferenceEquals(player,owner)||!ReferenceEquals(player.world,world)||!ReferenceEquals(Connection(player),nativeConnection)||selected!=subject||RebirthSurvivorClientState.GetProjectedCreationId(player)!=creation||Time.realtimeSinceStartup-received<0||Time.realtimeSinceStartup-received>=12)return false;
        id=status.SessionId;return true;
    }
    internal static bool CanReflect(EntityPlayerLocal player,string selected)
    {
        if(player==null||status==null||!status.Valid()||!ReferenceEquals(player,owner)||!ReferenceEquals(player.world,world)||!ReferenceEquals(Connection(player),nativeConnection)||selected!=subject||RebirthSurvivorClientState.GetProjectedCreationId(player)!=creation||!RebirthTheorySoloState.Finite(Time.realtimeSinceStartup)||!RebirthTheorySoloState.Finite(received)||Time.realtimeSinceStartup-received<0||Time.realtimeSinceStartup-received>=12)return false;
        return status.Mode==RebirthTheorySoloStatusMode.NeedsWork||status.Mode==RebirthTheorySoloStatusMode.Ready||status.Mode==RebirthTheorySoloStatusMode.Paused||status.Mode==RebirthTheorySoloStatusMode.Cooldown;
    }
    internal static void Refresh(){forceRefresh=true;next=0;}
    internal static void Close(){world=null;owner=null;nativeConnection=null;entity=0;creation=subject=null;request=Guid.Empty;status=null;next=0;forceRefresh=false;}
    private static object Connection(EntityPlayerLocal player)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player?.world==null||manager==null||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return null;
        if(!player.world.IsRemote())return manager.IsServer?(object)player:null;
        var native=manager.connectionToServer;
        return !manager.IsServer&&native!=null&&native.Length>0&&native[0]!=null&&!native[0].IsDisconnected()?(object)native[0]:null;
    }
    internal static string View(EntityPlayerLocal player,string selected)
    {
        if(player?.world==null||!ReferenceEquals(player.world,GameManager.Instance?.World)){Close();return string.Empty;}
        var currentConnection=Connection(player);if(currentConnection==null){Close();return string.Empty;}
        string currentCreation=RebirthSurvivorClientState.GetProjectedCreationId(player);float now=Time.realtimeSinceStartup;
        if(!ReferenceEquals(world,player.world)||entity!=player.entityId||creation!=currentCreation||subject!=selected||!ReferenceEquals(owner,player)||!ReferenceEquals(nativeConnection,currentConnection)){Close();world=player.world;entity=player.entityId;creation=currentCreation;subject=selected;owner=player;nativeConnection=currentConnection;}
        if(now>=next)
        {
            request=Guid.NewGuid();next=now+(forceRefresh||status?.Mode==RebirthTheorySoloStatusMode.Active||status?.Mode==RebirthTheorySoloStatusMode.Saving?1:10);
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection!=null&&connection.IsServer){if(RebirthTheorySoloStatusServer.TryBuild(player,creation,subject,request,out var local)){status=local;received=now;forceRefresh=false;next=now+(local.Mode==RebirthTheorySoloStatusMode.Active||local.Mode==RebirthTheorySoloStatusMode.Saving?1:10);}}
            else if(connection!=null)connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTheorySoloStatusRequest>().Setup(entity,creation,subject,request));
        }
        return status!=null&&now-received>=0&&now-received<12?status.Text():RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStatusLoading","Checking study progress…");
    }
    internal static void Receive(World current,RebirthTheorySoloStatus value)
    {
        if(current==null||!current.IsRemote()||!ReferenceEquals(current,GameManager.Instance?.World)||!ReferenceEquals(current,world)||value==null||!value.Valid()||value.Player!=entity||value.Creation!=creation||value.Subject!=subject||value.Request!=request)return;
        var player=current.GetPrimaryPlayer();if(player==null||!ReferenceEquals(player,owner)||!ReferenceEquals(Connection(owner),nativeConnection)||player.entityId!=entity||RebirthSurvivorClientState.GetProjectedCreationId(player)!=creation)return;
        status=value;received=Time.realtimeSinceStartup;forceRefresh=false;next=received+(value.Mode==RebirthTheorySoloStatusMode.Active||value.Mode==RebirthTheorySoloStatusMode.Saving?1:10);
    }
}
