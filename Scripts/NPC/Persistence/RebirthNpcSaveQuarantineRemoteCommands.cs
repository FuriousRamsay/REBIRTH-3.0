using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcRemoteCommandService
{
    private static World remoteCommandWorld;
    private static bool RemoteCommandScope()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(remoteCommandWorld!=null&&!ReferenceEquals(remoteCommandWorld,world))return false;if(remoteCommandWorld==null)remoteCommandWorld=world;return true;}
    }
    internal static bool CanRetireEmptyRemoteCommands
    {get{lock(Sync)return ThreadManager.IsMainThread()&&LastNonceByUser.Count==0&&ReplayOrder.Count==0&&PendingByCommand.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareRemoteCommandQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        if(!RoutingCommandsKnown())throw new InvalidOperationException("Remote routing original command unavailable");
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(remoteCommandWorld!=null&&!ReferenceEquals(remoteCommandWorld,scope.World))||(remoteCommandWorld==null&&!CanRetireEmptyRemoteCommands))throw new InvalidOperationException("Original remote command binding unavailable");
            var nonces=LastNonceByUser;var order=ReplayOrder;var pending=PendingByCommand;var world=remoteCommandWorld;ulong allocator=nextClientNonce;
            var nextNonces=new Dictionary<string,ulong>(StringComparer.Ordinal);var nextOrder=new Queue<string>();var nextPending=new Dictionary<ulong,PendingRemote>();var doc=RemoteCommandImage();
            return new RebirthNpcSaveQuarantineDomain("remote-command-routing",doc,
                ()=>{if(!RoutingCommandsKnown())return false;lock(Sync)return ReferenceEquals(LastNonceByUser,nonces)&&ReferenceEquals(ReplayOrder,order)&&ReferenceEquals(PendingByCommand,pending)&&ReferenceEquals(remoteCommandWorld,world)&&nextClientNonce==allocator&&RemoteCommandImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){LastNonceByUser=nextNonces;ReplayOrder=nextOrder;PendingByCommand=nextPending;remoteCommandWorld=null;/* client nonce remains process-monotone */}},
                ()=>{lock(Sync){LastNonceByUser=nonces;ReplayOrder=order;PendingByCommand=pending;remoteCommandWorld=world;nextClientNonce=allocator;}});
        }
    }
    private static bool RoutingCommandsKnown()
    {
        if(!ThreadManager.IsMainThread())return false;ulong[] keys;
        lock(Sync){if(PendingByCommand.Count>2560)return false;keys=new ulong[PendingByCommand.Count];PendingByCommand.Keys.CopyTo(keys,0);}
        // Do not nest remote-routing and command-gateway locks in reverse order.
        foreach(ulong key in keys)if(!RebirthNpcCommandGateway.TryGet(key,out var command)||command==null||command.Request.CommandId!=key)return false;return true;
    }
    private static XmlDocument RemoteCommandImage()
    {
        if(LastNonceByUser.Count>MaxReplayEntries||ReplayOrder.Count!=LastNonceByUser.Count||PendingByCommand.Count>2560)throw new InvalidOperationException("Remote command retention bound/index unavailable");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("remoteCommandRouting");doc.AppendChild(root);root.SetAttribute("nextClientNonce",nextClientNonce.ToString(CultureInfo.InvariantCulture));
        var seen=new HashSet<string>(StringComparer.Ordinal);foreach(string key in ReplayOrder)
        {if(key==null||key.Length>4096||!seen.Add(key)||!LastNonceByUser.TryGetValue(key,out ulong nonce))throw new InvalidOperationException("Remote nonce index unavailable");var row=doc.CreateElement("nonce");root.AppendChild(row);row.SetAttribute("key",key);row.SetAttribute("value",nonce.ToString(CultureInfo.InvariantCulture));}
        var keys=new List<ulong>(PendingByCommand.Keys);keys.Sort();foreach(ulong key in keys)
        {var value=PendingByCommand[key];if(value==null)throw new InvalidOperationException("Remote routing original missing");var row=doc.CreateElement("pending");root.AppendChild(row);row.SetAttribute("command",key.ToString(CultureInfo.InvariantCulture));row.SetAttribute("player",value.PlayerEntityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("connection",value.ConnectionEpoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("request",value.ClientRequestId.ToString(CultureInfo.InvariantCulture));}
        return doc;
    }
}
