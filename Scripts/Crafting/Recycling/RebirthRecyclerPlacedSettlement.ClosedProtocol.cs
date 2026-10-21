using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class ClosedGrantAssembly:IDisposable
    {
        internal World World;internal INetConnection Connection;internal Guid Group;internal int Total,Received;internal byte[][] Parts;internal IDisposable Memory;
        public void Dispose(){closedGrantAssemblies.Remove(this);World=null;Connection=null;Parts=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<ClosedGrantAssembly> closedGrantAssemblies=new List<ClosedGrantAssembly>();
    private static readonly Dictionary<ClosedAuthorityCompletion,float> closedGrantQueries=new Dictionary<ClosedAuthorityCompletion,float>();
    private static void PumpClosedAuthorityCompletions(World world)
    {
        foreach(var assembly in closedGrantAssemblies.ToArray())if(!ReferenceEquals(assembly.World,world)||assembly.Connection.IsDisconnected())assembly.Dispose();
        foreach(var source in closedAuthorityCompletions.ToArray())
        {
            if(!ReferenceEquals(source.World,world)){closedGrantQueries.Remove(source);source.Dispose();continue;}
            if(source.Attached==null&&!source.NativeLineageCurrent())RecoverClosedPendingHot(source);
            if(source.Attached==null&&!source.RecipientCurrent())
                foreach(var loaded in remoteLoadedRecyclerOwners.ToArray())if(source.AdoptUnacceptedNativeOwner(loaded))break;
            if(source.Attached!=null&&!source.Attached.ColdNeedsOwner||!source.Current())continue;
            if(closedGrantQueries.TryGetValue(source,out float next)&&UnityEngine.Time.realtimeSinceStartup<next)continue;
            closedGrantQueries[source]=UnityEngine.Time.realtimeSinceStartup+1f;
            if(source.Attached==null)
            {
                try{if(!source.EnsureOriginalPublished())continue;if(source.Pending==null)ClosedPendingPublication.Begin(source);if(source.Pending?.Saved()!=true)continue;}
                catch{continue;}
            }
            SendClosedAuthorityGrant(source);
        }
    }
    private static void SendClosedAuthorityGrant(ClosedAuthorityCompletion source)
    {
        if(!source.Current()||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*source.MeasureImage+65536),out var memory))return;
        using(memory)
        {
            var native=source.CopyImage();if(native==null)return;
            var envelope=new XElement("recyclerClosedAuthorityGrant",new XAttribute("version",1),new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("ticket",source.Ticket.ToString("N")),new XAttribute("group",source.Group.ToString("N")),native);
            var attached=source.Attached;
            if(attached!=null)
                envelope.Add(new XAttribute("restored",true),new XAttribute("nonce",attached.Nonce.ToString("N")),
                    new XAttribute("original",RebirthStationPublicationRecord.Digest(attached.Image)),new XAttribute("phase",attached.ColdPhase),
                    new XAttribute("completed",attached.ColdCompleted),new XAttribute("stage",attached.AcceptedStation?"after":"before"));
            else if(source.OwnerGeneration!=Guid.Empty)
                envelope.Add(new XAttribute("unacceptedRejoin",true),new XAttribute("generation",source.OwnerGeneration.ToString("N")),
                    new XAttribute("phase",0),new XAttribute("completed",0),new XAttribute("stage","before"));
            byte[] bytes=Encoding.UTF8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length>12*1024*1024)return;
            var packets=new NetPackageRebirthRecyclerClosedGrantV1[(bytes.Length+8999)/9000];Guid group=Guid.NewGuid();
            var peer=attached?.Peer??source.Peer;var connection=attached?.Connection??source.Connection;
            bool CurrentRecipient()=>source.Current()&&ReferenceEquals(source.Attached,attached)&&ReferenceEquals(attached?.Peer??source.Peer,peer)&&
                ReferenceEquals(attached?.Connection??source.Connection,connection)&&ReferenceEquals(peer?.netConnection?[0],connection)&&connection!=null&&!connection.IsDisconnected();
            try
            {
                for(int index=0;index<packets.Length;index++)
                {
                    if(!CurrentRecipient())return;
                    packets[index]=NetPackageManager.GetPackage<NetPackageRebirthRecyclerClosedGrantV1>();packets[index].SetupSlice(group,bytes,index);
                }
                for(int index=0;index<packets.Length;index++)
                {
                    if(!CurrentRecipient())return;
                    peer.SendPackage(packets[index]);packets[index]=null;
                }
            }
            finally{foreach(var packet in packets)if(packet!=null&&packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);}
        }
    }
    internal static void ReceiveClosedAuthorityGrant(NetPackageRebirthRecyclerClosedGrantV1 packet,World world)
    {
        if(!authorityInstalled||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||connection==null||connection.IsDisconnected()||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],connection))return;
        ClosedGrantAssembly assembly=null;
        try
        {
            if(packet.Total<1||packet.Total>12*1024*1024||packet.Group==Guid.Empty||packet.Count!=(packet.Total+8999)/9000||packet.Index<0||packet.Index>=packet.Count||
                packet.Payload?.Length!=Math.Min(9000,packet.Total-packet.Index*9000))return;
            assembly=closedGrantAssemblies.SingleOrDefault(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Connection,connection)&&value.Group==packet.Group);
            if(assembly==null)
            {
                if(closedGrantAssemblies.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*packet.Total+65536),out var memory))return;
                assembly=new ClosedGrantAssembly{World=world,Connection=connection,Group=packet.Group,Total=packet.Total,Parts=new byte[packet.Count][],Memory=memory};closedGrantAssemblies.Add(assembly);
            }
            if(assembly.Total!=packet.Total||assembly.Parts.Length!=packet.Count)return;
            if(assembly.Parts[packet.Index]!=null){if(!assembly.Parts[packet.Index].SequenceEqual(packet.Payload))assembly.Dispose();return;}
            assembly.Parts[packet.Index]=(byte[])packet.Payload.Clone();assembly.Received++;if(assembly.Received!=assembly.Parts.Length)return;
            var bytes=new byte[assembly.Total];int offset=0;foreach(var part in assembly.Parts){Buffer.BlockCopy(part,0,bytes,offset,part.Length);offset+=part.Length;}
            var envelope=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),12*1024*1024);
            receivingClosedGrantEnvelope=envelope;
            try{BeginActualClosedOwner(world,connection,envelope);}
            finally{receivingClosedGrantEnvelope=null;}
            assembly.Dispose();
        }
        catch{assembly?.Dispose();}
    }
    private static void BeginActualClosedOwner(World world,INetConnection connection,XElement envelope)
    {
        var grant=ClosedOwnerGrant.FromActualGrant(world,connection,envelope);if(grant==null)return;
        var retained=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==grant.Operation&&ReferenceEquals(value.Connection,connection));
        if(retained!=null)
        {if(retained.Source.ClosedOwner?.Ticket==grant.Ticket&&retained.Live())SendPrepare(retained);return;}
        if(recyclerClients.Count>=4)return;
        receivingClosedGrant=grant;Original original=null;IDisposable automaticMemory=null;bool owned=false;
        try
        {
            if((bool?)envelope.Attribute("restored")==true)
            {TryBeginActualClosedJoinedOwner(grant,envelope);return;}
            string retainedPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),grant.Operation.ToString("N")+".remote-client.xml");
            if((bool?)envelope.Attribute("unacceptedRejoin")==true&&ArtifactNodeExists(retainedPath))
            {TryBeginActualClosedJoinedOwner(grant,envelope);return;}
            original=Original.FromActualClosedOwner(grant);if(original==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out automaticMemory))return;
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),grant.Operation.ToString("N")+".remote-client.xml");
            if(new[]{path,path+".tmp",path+".bak"}.Any(ArtifactNodeExists))return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",grant.Operation.ToString("N")),new XAttribute("world",grant.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(original.Image))),new XElement("recyclerRemoteClientOriginal",new XAttribute("request",grant.Group.ToString("N")),
                    new XAttribute("slot",0),new XAttribute("units",0),new XAttribute("session",0),new XAttribute("closedAuthority",true)));
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!original.Current())return;
            var client=new RecyclerClient{Source=original,Connection=connection,Request=grant.Group,AutomaticMemory=automaticMemory};grant.Client=client;
            recyclerClients.Add(client);owned=true;SendPrepare(client);
        }
        finally{receivingClosedGrant=null;if(!owned){automaticMemory?.Dispose();original?.Dispose();}}
    }
}
