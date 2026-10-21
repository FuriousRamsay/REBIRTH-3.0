using System;
using System.Collections.Generic;
using System.Globalization;

public static partial class RebirthNpcNetworkEpoch
{
    // A retained remote projection group has no host financial authority.
    // Only its actual original native transport can prove its delivery ended.
    internal sealed class ClientProjectionRetirement
    {
        private readonly World original;private readonly ConnectionManager manager;
        private readonly INetConnection transport;private readonly string directory;
        internal readonly uint Epoch,Connection;
        private ClientProjectionRetirement(World world,string save,ConnectionManager owner,INetConnection channel,uint epoch,uint connection)
        {original=world;directory=save;manager=owner;transport=channel;Epoch=epoch;Connection=connection;}
        internal static ClientProjectionRetirement Capture(World world,string save,uint epoch,uint connection)
        {
            lock(Sync)
            {
                if(!TryObserveRetainedClient(world,save,out uint observedEpoch,out uint observedConnection)||observedEpoch!=epoch||observedConnection!=connection)return null;
                return new ClientProjectionRetirement(world,save,clientManager,clientTransport,epoch,connection);
            }
        }
        internal bool Matches(World world,string save)=>ReferenceEquals(original,world)&&directory==save;
        internal bool MatchesIncomingNamespace(RebirthNpcClientNativeReentry.Source source)
        {
            // Fresh authenticated projection admission does not consume the
            // captured original transport's still-live delivery obligations.
            return ThreadManager.IsMainThread()&&original?.IsRemote()==true&&manager!=null&&transport!=null&&Epoch!=0&&Connection!=0&&
                source?.Current()==true&&source.ScopeCurrent()&&!ReferenceEquals(source.World,original)&&
                RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&
                (source.Epoch!=Epoch||source.Connection!=Connection);
        }
        internal bool HasEnded
        {
            get
            {
                try
                {
                    if(!ThreadManager.IsMainThread()||original?.IsRemote()!=true||manager==null||transport==null||Epoch==0||Connection==0)return false;
                    var channels=manager.GetConnectionToServer();
                    return transport.IsDisconnected()||channels==null||channels.Length==0||!ReferenceEquals(channels[0],transport);
                }
                catch{return false;}
            }
        }
    }
    internal static bool TryObserveRetainedClient(World original,string directory,out uint epoch,out uint connection)
    {
        epoch=connection=0;
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||original?.IsRemote()!=true||!ReferenceEquals(clientWorld,original)||clientDirectory!=directory||
                clientManager==null||clientTransport==null||clientEpoch==0||clientConnectionEpoch==0||serverWorld!=null||serverManager!=null||activeServerEpoch!=0||ServerConnectionEpochs.Count!=0)return false;
            epoch=clientEpoch;connection=clientConnectionEpoch;return true;
        }
    }
    internal static bool CanRetireEpochState
    {
        get
        {
            lock(Sync)
            {
                if(!ThreadManager.IsMainThread()||ServerConnectionEpochs.Count>8192||(serverWorld==null&&(serverManager!=null||activeServerEpoch!=0||ServerConnectionEpochs.Count!=0))||(serverWorld!=null&&(serverManager==null||activeServerEpoch==0||(nextServerEpoch!=0&&activeServerEpoch>=nextServerEpoch))))return false;
                var tokens=new HashSet<uint>();foreach(var row in ServerConnectionEpochs)if(row.Key<=0||row.Value==0||(nextConnectionEpoch!=0&&row.Value>=nextConnectionEpoch)||!tokens.Add(row.Value))return false;
                if(clientWorld==null)return clientManager==null&&clientTransport==null&&clientDirectory==null&&clientEpoch==0&&clientConnectionEpoch==0;
                if(clientManager==null||clientTransport==null||clientDirectory==null||clientEpoch==0||clientConnectionEpoch==0)return false;
                var transports=clientManager.GetConnectionToServer();
                return clientTransport.IsDisconnected()||transports==null||transports.Length==0||!ReferenceEquals(transports[0],clientTransport);
            }
        }
    }
    // Process allocators are never rewound by detach/rollback. Captured values
    // invalidate preparation on any intervening issue, including another World.
    internal static RebirthNpcSaveQuarantineDomain PrepareEpochQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!ThreadManager.IsMainThread()||clientWorld!=null||clientManager!=null||clientTransport!=null||clientDirectory!=null||clientEpoch!=0||clientConnectionEpoch!=0||ServerConnectionEpochs.Count>8192||(serverWorld!=null&&(!ReferenceEquals(serverWorld,scope.World)||serverManager==null||activeServerEpoch==0||(nextServerEpoch!=0&&activeServerEpoch>=nextServerEpoch)))||(serverWorld==null&&(serverManager!=null||activeServerEpoch!=0||ServerConnectionEpochs.Count!=0)))throw new InvalidOperationException("Original native epoch scope unavailable");
            var originalWorld=serverWorld;var originalManager=serverManager;uint active=activeServerEpoch,nextWorld=nextServerEpoch,nextConnection=nextConnectionEpoch;var records=ServerConnectionEpochs;var copy=new Dictionary<int,uint>(records);var doc=RebirthNpcAncillaryOriginalScope.Image("networkEpoch",null,Array.Empty<long>());var root=doc.DocumentElement;root.SetAttribute("generation",scope.Generation.ToString(CultureInfo.InvariantCulture));root.SetAttribute("active",active.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nextWorld",nextWorld.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nextConnection",nextConnection.ToString(CultureInfo.InvariantCulture));root.SetAttribute("allocatorPolicy","process-monotone-no-rewind");
            var ids=new List<int>(copy.Keys);ids.Sort();var tokens=new HashSet<uint>();foreach(int id in ids){uint token=copy[id];if(id<=0||token==0||(nextConnection!=0&&token>=nextConnection)||!tokens.Add(token))throw new InvalidOperationException("Native connection epoch index corrupt");var row=doc.CreateElement("connection");row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("epoch",token.ToString(CultureInfo.InvariantCulture));root.AppendChild(row);}
            var empty=new Dictionary<int,uint>();
            return new RebirthNpcSaveQuarantineDomain("network-epoch",doc,
                ()=>{lock(Sync){if(!ThreadManager.IsMainThread()||!ReferenceEquals(serverWorld,originalWorld)||!ReferenceEquals(serverManager,originalManager)||clientWorld!=null||clientManager!=null||clientTransport!=null||clientDirectory!=null||clientEpoch!=0||clientConnectionEpoch!=0||activeServerEpoch!=active||nextServerEpoch!=nextWorld||nextConnectionEpoch!=nextConnection||!ReferenceEquals(ServerConnectionEpochs,records)||records.Count!=copy.Count)return false;foreach(var row in copy)if(!records.TryGetValue(row.Key,out uint current)||current!=row.Value)return false;return true;}},
                ()=>{lock(Sync){ServerConnectionEpochs=empty;serverWorld=null;serverManager=null;activeServerEpoch=0;}},
                ()=>{lock(Sync){ServerConnectionEpochs=records;serverWorld=originalWorld;serverManager=originalManager;activeServerEpoch=active;}});
        }
    }
    // Defined for the separate client projection bundle, not composed/activated
    // here. Exact original handles only; no host paths, disconnect or ACK.
    internal static RebirthNpcSaveQuarantineDomain PrepareClientEpochQuarantine(World world,string directory,uint epoch,uint connection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||serverWorld!=null||serverManager!=null||activeServerEpoch!=0||ServerConnectionEpochs.Count!=0||(clientWorld!=null&&(!ReferenceEquals(clientWorld,world)||clientDirectory!=directory||clientEpoch!=epoch||clientConnectionEpoch!=connection||clientManager==null||clientTransport==null))||(clientWorld==null&&(clientManager!=null||clientTransport!=null||clientDirectory!=null||clientEpoch!=0||clientConnectionEpoch!=0)))throw new InvalidOperationException("Original client epoch handles unresolved");
            var originalWorld=clientWorld;var manager=clientManager;var transport=clientTransport;string save=clientDirectory;uint oldEpoch=clientEpoch,oldConnection=clientConnectionEpoch;var doc=RebirthNpcAncillaryOriginalScope.Image("clientNetworkEpoch",null,Array.Empty<long>());var root=doc.DocumentElement;root.SetAttribute("epoch",oldEpoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",oldConnection.ToString(CultureInfo.InvariantCulture));root.SetAttribute("directory",save??string.Empty);
            return new RebirthNpcSaveQuarantineDomain("client-network-epoch",doc,
                ()=>{lock(Sync)return ThreadManager.IsMainThread()&&ReferenceEquals(clientWorld,originalWorld)&&ReferenceEquals(clientManager,manager)&&ReferenceEquals(clientTransport,transport)&&clientDirectory==save&&clientEpoch==oldEpoch&&clientConnectionEpoch==oldConnection&&serverWorld==null&&serverManager==null&&activeServerEpoch==0&&ServerConnectionEpochs.Count==0;},
                ()=>{lock(Sync){clientWorld=null;clientManager=null;clientTransport=null;clientDirectory=null;clientEpoch=clientConnectionEpoch=0;}},
                ()=>{lock(Sync){clientWorld=originalWorld;clientManager=manager;clientTransport=transport;clientDirectory=save;clientEpoch=oldEpoch;clientConnectionEpoch=oldConnection;}});
        }
    }
}
