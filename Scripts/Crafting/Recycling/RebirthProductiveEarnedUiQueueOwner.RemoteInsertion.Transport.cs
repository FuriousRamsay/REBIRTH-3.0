using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class RemotePacket {internal RemoteInsertion[] Members;}
    private static readonly ConditionalWeakTable<NetPackageTileEntity,RemotePacket> remotePackets=new ConditionalWeakTable<NetPackageTileEntity,RemotePacket>();
    private sealed class RemoteSend
    {internal NetConnectionAbs Connection;internal NetPackageTileEntity Packet;internal RemoteInsertion[] Members;internal NetPackage[] Prefix;internal object Gate;internal bool Locked,HeaderQueued;}
    private static object RemoteQueueGate(NetConnectionAbs connection)=>connection is NetConnectionSimple simple?simple.writerListLockObj:
        connection is NetConnectionSteam steam?steam.packetsToSend:null;
    private static NetPackage[] RemoteQueueImage(NetConnectionAbs connection)
    {
        // Called only under this connection's actual native queue lock. The
        // original's fixed detached allowance includes these bounded views.
        if(connection is NetConnectionSimple simple)
        {if(simple.writerListPending.Count>8192)throw new InvalidDataException();return simple.writerListPending.ToArray();}
        if(connection is NetConnectionSteam steam)
        {if(steam.packetsToSend.Count>8192)throw new InvalidDataException();return steam.packetsToSend.ToArray();}
        return Array.Empty<NetPackage>();
    }
    internal static void ForgetRemoteNativeSetup(NetPackage packet)
    {if(packet is NetPackageTileEntity native)remotePackets.Remove(native);}
    internal static void ObserveRemoteNativeSetup(NetPackageTileEntity packet,TileEntity tile,global::StreamModeWrite mode,bool run)
    {
        try
        {
            remotePackets.Remove(packet);
            if(!run||mode!=global::StreamModeWrite.ToServer||remoteSync==null||tile is not TileEntityWorkstation station||packet.ms==null||
                packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return;
            var members=remoteSync.Members.Where(value=>ReferenceEquals(value.Station,station)&&value.CardCurrent()&&!value.Sent&&value.Body==null).ToArray();
            if(members.Length==0)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return;
                foreach(var value in members)
                {
                    var row=station.Queue?[value.Index];
                    if(row?.Recipe==null||!ReferenceEquals(row.Recipe,value.Recipe)||row.Multiplier!=value.Count||Binding(row)!=value.FrozenBinding||
                        !packet.teWorldPos.Equals(station.ToWorldPos())||packet.teBlockId!=value.Block||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*packet.ms.Length+262144),out var allocation))continue;
                    try
                    {
                        var body=new byte[(int)packet.ms.Length];lock(packet.ms)
                        {long position=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<body.Length){int n=packet.ms.Read(body,read,body.Length-read);if(n<1)throw new EndOfStreamException();read+=n;}}finally{packet.ms.Position=position;}}
                        if(!Capture(station,out var current,out int offset)||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,offset,current,offset))continue;
                        value.Digest=ProofHash(body);var p=station.ToWorldPos();
                        value.Header=new XElement("remoteQueueInsertion",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),
                            new XAttribute("player",value.Actor.entityId),new XAttribute("owner",value.Owner),new XAttribute("creation",value.Creation),new XAttribute("deaths",value.Deaths),
                            new XAttribute("x",p.x),new XAttribute("y",p.y),new XAttribute("z",p.z),new XAttribute("index",value.Index),new XAttribute("count",value.Count),
                            new XAttribute("block",value.Block),new XAttribute("digest",value.Digest),new XAttribute("world",value.WorldKey),new XElement("row",value.FrozenBinding)).ToString(SaveOptions.DisableFormatting);
                        if(value.Header.Length>65536)continue;
                        value.UploadIndex=value.Index;value.Body=body;value.BodyAllocation=allocation;allocation=null;
                    }
                    finally{allocation?.Dispose();}
                }
            }
            members=members.Where(value=>value.Body!=null).ToArray();if(members.Length>0)remotePackets.Add(packet,new RemotePacket{Members=members});
        }
        catch{/* Native packet remains unchanged. No actual Setup means no source. */}
    }
    internal static void EnterRemoteNativeSend(NetConnectionAbs connection,NetPackage packet,bool run,out object state)
    {
        state=null;RemoteSend scope=null;
        try
        {
            if(!run||packet is not NetPackageTileEntity native||!ThreadManager.IsMainThread()||remoteSync==null||
                !remotePackets.TryGetValue(native,out var setup)||setup.Members.Length<1)return;
            var members=setup.Members.Where(value=>!value.Sent&&value.CardCurrent()&&RemotePacketMatches(native,value)&&ReferenceEquals(connection,value.Connection)&&
                remoteSync.Members.Contains(value)).ToArray();if(members.Length!=setup.Members.Length||native.Channel!=0)return;
            scope=new RemoteSend{Connection=connection,Packet=native,Members=members,Gate=RemoteQueueGate(connection)};
            if(scope.Gate==null)return;
            Monitor.Enter(scope.Gate);scope.Locked=true;state=scope;
            // Same native queue lock/order as the unchanged native upload. A
            // skipped/throwing native enqueue never sets Sent or permits import.
            foreach(var value in members)
            {
                var header=NetPackageManager.GetPackage<NetPackageRebirthUiQueueInsertionRequestV1>().Setup(value.Header,false);
                var before=RemoteQueueImage(connection);connection.AddToSendQueue(header);var after=RemoteQueueImage(connection);
                if(after.Length!=before.Length+1||!after.Take(before.Length).SequenceEqual(before)||!ReferenceEquals(after[before.Length],header))return;
            }
            scope.Prefix=RemoteQueueImage(connection);scope.HeaderQueued=true;
        }
        catch{if(scope!=null&&state==null&&scope.Locked){scope.Locked=false;Monitor.Exit(scope.Gate);}}
    }
    internal static void ObserveRemoteNativeSend(NetConnectionAbs connection,NetPackage packet,bool run,object state)
    {
        try
        {
            if(!run||state is not RemoteSend scope||!scope.Locked||!scope.HeaderQueued||!ReferenceEquals(scope.Packet,packet)||!ReferenceEquals(scope.Connection,connection))return;
            var image=RemoteQueueImage(connection);
            if(image.Length!=scope.Prefix.Length+1||!image.Take(scope.Prefix.Length).SequenceEqual(scope.Prefix)||!ReferenceEquals(image[scope.Prefix.Length],packet))return;
            foreach(var value in scope.Members)if(value.CardCurrent()&&RemotePacketMatches(scope.Packet,value))value.Sent=true;
        }catch{}
    }
    internal static void EndRemoteNativeSend(object state)
    {if(state is RemoteSend scope&&scope.Locked){scope.Locked=false;Monitor.Exit(scope.Gate);}}
    private static bool RemotePacketMatches(NetPackageTileEntity packet,RemoteInsertion value)
    {
        if(packet.ms==null||value.Body==null||packet.ms.Length!=value.Body.Length||packet.teBlockId!=value.Block||!packet.teWorldPos.Equals(value.Station.ToWorldPos()))return false;
        lock(packet.ms)
        {long position=packet.ms.Position;try{packet.ms.Position=0;foreach(byte b in value.Body)if(packet.ms.ReadByte()!=b)return false;return true;}finally{packet.ms.Position=position;}}
    }
}
