using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthPoiRallyRequest:NetPackage
{
    private byte[] bytes;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthPoiRallyRequest Setup(RebirthPoiRallyWireFrame frame){bytes=RebirthPoiRallyWireFrame.Encode(frame);return this;}
    public override void read(PooledBinaryReader reader){int length=reader.ReadInt32();if(length<72 || length>RebirthPoiRallyWireFrame.MaximumBytes)throw new InvalidDataException("Invalid rally request size.");bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(bytes.Length);writer.Write(bytes);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        RebirthPoiRallyWireFrame frame;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || world==null || world.IsRemote() || Sender==null || !Sender.loginDone || !RebirthPoiRallyWireFrame.TryDecode(bytes,out frame) || frame.Status!=RebirthPoiRallyWireStatus.Request || !ValidEntityIdForSender(frame.Player))return;
        RebirthPoiRemoteRallyOwner.ReceiveRequest(world,Sender,frame);
    }
    public int GetLength()=>8+(bytes==null?0:bytes.Length);
}
[Preserve]
public sealed class NetPackageRebirthPoiRallyReply:NetPackage
{
    private byte[] bytes;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPoiRallyReply Setup(RebirthPoiRallyWireFrame frame){bytes=RebirthPoiRallyWireFrame.Encode(frame);return this;}
    public override void read(PooledBinaryReader reader){int length=reader.ReadInt32();if(length<72 || length>RebirthPoiRallyWireFrame.MaximumBytes)throw new InvalidDataException("Invalid rally reply size.");bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(bytes.Length);writer.Write(bytes);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;RebirthPoiRallyWireFrame frame;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || connection==null || connection.IsServer || world==null || !world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World,world) || !RebirthPoiRallyWireFrame.TryDecode(bytes,out frame) || frame.Status==RebirthPoiRallyWireStatus.Request)return;
        RebirthPoiRemoteRallyOwner.ReceiveReply(world,frame);
    }
    public int GetLength()=>8+(bytes==null?0:bytes.Length);
}
// The client owns only its unchanged request. The server authenticates and owns
// the reset using the same batch execution as local play; payloads supply no tags,
// party IDs, reset targets, native requirements or copied-volume success claims.
internal static class RebirthPoiRemoteRallyOwner
{
    private sealed class Peer
    {
        internal ClientInfo Sender;
        internal World World;
        internal RebirthPoiRallyWireFrame Request,Reply;
        internal RebirthPoiAuthenticatedRallyScope Scope;
        internal double Expires,NextReply;
        internal bool Current=>ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(Request.Player)==Sender&&Sender.entityId==Request.Player&&Sender.loginDone&&World.worldState.Guid==Request.World.ToString("N").ToUpperInvariant();
    }
    private static readonly Dictionary<ClientInfo,Peer> peers=new Dictionary<ClientInfo,Peer>();
    private static RebirthPoiOriginalRallyRequest pending;
    private static RebirthPoiRallyWireFrame original;
    private static World clientWorld;
    private static double deadline,nextSend,nextPrune;
    private static double Now=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal static void Reset(){peers.Clear();pending=null;original=null;clientWorld=null;deadline=nextSend=nextPrune=0;}
    internal static bool TryStart(ObjectiveRallyPoint objective,Vector3 poi)
    {
        if(pending!=null)return pending.IsOriginalCurrent&&original.Poi.Equals(poi)&&original.QuestId==objective.OwnerQuest.ID&&original.Unique==objective.OwnerQuest.QuestUniqueId;
        RebirthPoiOriginalRallyRequest request;
        if(!RebirthPoiOriginalRallyRequest.TryCapture(objective,poi,out request))return false;
        var world=GameManager.Instance.World;if(!world.IsRemote())return false;
        original=new RebirthPoiRallyWireFrame(request.RequestId,request.SavedWorldId,request.PlayerId,request.QuestId,request.QuestUniqueId,request.QuestCode,objective.OwnerQuest.CurrentPhase,poi);
        pending=request;clientWorld=world;deadline=Now+120;nextSend=0;Pulse();return true;
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled){Reset();return;}
        double now=Now;
        if(now>=nextPrune)
        {
            nextPrune=now+1;
            foreach(var key in peers.Where(p=>!p.Value.Current || p.Value.Expires<now).Select(p=>p.Key).ToArray())peers.Remove(key);
        }
        if(pending==null)return;
        if(!pending.IsOriginalCurrent || !ReferenceEquals(GameManager.Instance?.World,clientWorld) || now>=deadline)
        { if(now>=deadline && pending.IsOriginalCurrent && ReferenceEquals(GameManager.Instance?.World,clientWorld))GameManager.ShowTooltip(clientWorld.GetPrimaryPlayer(),Localization.Get("xuiRebirthPoiResetUncertain"));pending=null;original=null;clientWorld=null;return; }
        if(now<nextSend)return;nextSend=now+2;
        try { SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPoiRallyRequest>().Setup(original)); }
        catch(Exception error) { Log.Warning("[RebirthPurge] Original rally request awaits connection retry: "+error.Message); }
    }
    internal static void ReceiveReply(World world,RebirthPoiRallyWireFrame reply)
    {
        if(pending==null || original==null || !ReferenceEquals(world,clientWorld) || Now>=deadline || !original.SameRequest(reply) || !pending.IsOriginalCurrent)return;
        var request=pending;pending=null;original=null;clientWorld=null;
        bool success=reply.Status==RebirthPoiRallyWireStatus.Completed && request.TryApplyConfirmed(new RebirthPoiRallyCompletionReceipt(reply.Request,reply.World,reply.Unique,reply.QuestCode,reply.Player,reply.Revision));
        if(success)return;
        if(reply.Status!=RebirthPoiRallyWireStatus.Completed)request.RefuseOriginal(reply.Request,reply.World);
        GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get(reply.Status==RebirthPoiRallyWireStatus.Refused?"xuiRebirthPoiResetUnavailable":"xuiRebirthPoiResetUncertain"));
    }
    private static void Send(Peer peer)
    {
        if(!peer.Current || peer.Reply==null || Now<peer.NextReply)return;peer.NextReply=Now+.5;
        try { peer.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPoiRallyReply>().Setup(peer.Reply)); }
        catch(Exception error){Log.Warning("[RebirthPurge] Rally reply awaits original connection retry: "+error.Message);}
    }
    private static bool Eligible(RebirthPoiAuthenticatedRallyScope scope,World world)
    {
        if(!scope.IsOriginalCurrent || Twitch.TwitchManager.HasInstance&&Twitch.TwitchManager.Current.IsVoting)return false;
        var quest=scope.OriginalQuest;var rally=quest.Objectives.OfType<ObjectiveRallyPoint>().Single(o=>o.Phase==0 || o.Phase==quest.CurrentPhase);
        int hour=GameUtils.WorldTimeToHours(world.worldTime),start=rally.startTime,end=rally.endTime;
        if(start!=-1 && end!=-1 && (start<end?(hour<start || hour>=end):(hour<start && hour>=end)))return false;
        bool satisfied;if(!RebirthPoiServerQuestRequirements.TryCheck(quest,world.GetEntity(scope.PlayerId) as EntityPlayer,out satisfied) || !satisfied || !quest.QuestClass.CanActivate())return false;
        ulong reason;return QuestEventManager.Current.CheckForPOILockouts(scope.PlayerId,new Vector2(scope.Poi.x,scope.Poi.z),out reason)==QuestEventManager.POILockoutReasonTypes.None;
    }
    internal static void ReceiveRequest(World world,ClientInfo sender,RebirthPoiRallyWireFrame frame)
    {
        if(!ReferenceEquals(GameManager.Instance?.World,world))return;
        Peer peer;
        if(peers.TryGetValue(sender,out peer))
        {
            if(peer.Current&&peer.Request.SameRequest(frame)){Send(peer);return;}
            if(peer.Current&&peer.Reply==null)return;
            peers.Remove(sender);
        }
        if(peers.Count>=256)return;
        peer=new Peer{Sender=sender,World=world,Request=frame,Expires=Now+150};if(!peer.Current)return;peers.Add(sender,peer);
        RebirthPoiAuthenticatedRallyScope scope;
        if(!RebirthPoiAuthenticatedRallyScope.TryCapture(world,sender,frame.Request,frame.World,frame.Player,frame.QuestId,frame.Unique,frame.QuestCode,frame.Phase,frame.Poi,out scope) || !Eligible(scope,world))
        { peer.Reply=frame.Reply(RebirthPoiRallyWireStatus.Refused);Send(peer);return; }
        peer.Scope=scope;
        bool started=RebirthPoiLocalRallyOwner.TryStartAuthenticated(scope,receipt=>{
            if(!peer.Current || !scope.IsOriginalCurrent)return;
            peer.Reply=frame.Reply(RebirthPoiRallyWireStatus.Completed,receipt.GlobalRevision);Send(peer);
        },unknown=>{if(peer.Current){peer.Reply=frame.Reply(unknown?RebirthPoiRallyWireStatus.Unknown:RebirthPoiRallyWireStatus.Refused);Send(peer);}});
        if(!started){peer.Reply=frame.Reply(RebirthPoiRallyWireStatus.Refused);Send(peer);}
    }
}
// The old client LockPOI packet has no original request/world/generation custody.
// New clients reach the shared owner above; never let a raw packet bypass admission.
[HarmonyPatch(typeof(NetPackageQuestEvent),nameof(NetPackageQuestEvent.ProcessPackage))]
internal static class RebirthPoiRemoteRallyLegacyLockHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NetPackageQuestEvent __instance,World _world)
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || _world==null || _world.IsRemote() || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true || __instance.eventType!=NetPackageQuestEvent.QuestEventTypes.LockPOI)return true;
        return false;
    }
}