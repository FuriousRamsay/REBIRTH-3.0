using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

// Framing/lifetime only. Reassembly cannot grant a removal or owner effect.
internal static class RebirthCamperRemovalParcelTransport
{
    private sealed class Outbound:IDisposable
    {
        internal EntityPlayer Actor;internal World World;internal string Save,Creation,NativeWorld;internal Guid Operation;
        internal RebirthRemoteResourceNetworkScope Host;internal bool Server;internal RebirthRemoteResourceFragments.Body Body;internal int Next;internal IDisposable Allocation;
        internal bool Current=>ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Actor?.world,World)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&GameIO.GetSaveGameDir()==Save&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            (Server?Host?.Current==true:ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&World.IsRemote());
        public void Dispose(){Allocation?.Dispose();Allocation=null;Body=null;}
    }
    private static readonly Dictionary<string,Outbound> outgoing=new Dictionary<string,Outbound>();
    internal static bool IsPacket(NetPackage packet)=>packet is NetPackageRebirthRemovalParcelRequest||packet is NetPackageRebirthRemovalParcelResult;
    internal static void Send(EntityPlayerLocal actor,Guid operation,XElement request)
    {
        if(actor?.world==null||!ThreadManager.IsMainThread())return;
        if(!actor.world.IsRemote())
        {var result=RebirthCamperRemovalParcelHost.Request(actor,request);if(result!=null)RebirthCamperStationContentsDelivery.AcceptParcelResult(actor,operation,result);return;}
        Queue(actor,operation,request,false);
    }
    internal static void Reply(EntityPlayer actor,Guid operation,XElement result)=>Queue(actor,operation,result,true);
    private static void Queue(EntityPlayer actor,Guid operation,XElement value,bool server)
    {
        try
        {
            byte[] bytes=RebirthCamperRemovalParcelPlan.Bytes(value);if(bytes.Length>24*1024*1024||operation==Guid.Empty)return;
            string key=(server?"host|":"owner|")+operation.ToString("N");
            var body=new RebirthRemoteResourceFragments.Body(bytes);
            if(outgoing.TryGetValue(key,out var previous))
            {if(previous.Current&&previous.Body.Hash==body.Hash)return;if(previous.Current&&previous.Next<previous.Body.Count)return;previous.Dispose();outgoing.Remove(key);}
            if(outgoing.Count>=8||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(3L*body.Length+65536,out var allocation))return;
            var entry=new Outbound{Actor=actor,World=actor.world,Save=GameIO.GetSaveGameDir(),Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),
                NativeWorld=actor.world.worldState.Guid,Operation=operation,Body=body,Server=server,Allocation=allocation,Host=server?RebirthRemoteResourceNetworkScope.Capture(actor):null};
            if(!entry.Current){entry.Dispose();return;}outgoing.Add(key,entry);
        }
        catch{/* Framing failure does not change original settlement. */}
    }
    internal static void Tick()
    {
        if(outgoing.Count==0)return;
        int budget=4;
        foreach(var pair in outgoing.ToArray())
        {
            var entry=pair.Value;
            if(!entry.Current){entry.Dispose();outgoing.Remove(pair.Key);continue;}
            while(budget>0&&entry.Next<entry.Body.Count)
            {
                if(!entry.Current)break;int part=entry.Next;
                if(entry.Server)
                {
                    var sender=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(entry.Actor.entityId);
                    if(sender==null)break;
                    sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRemovalParcelResult>().Setup(entry.Actor.entityId,entry.Operation,entry.NativeWorld,entry.Creation,entry.Body,part));
                }
                else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthRemovalParcelRequest>()
                    .Setup(entry.Actor.entityId,entry.Operation,entry.NativeWorld,entry.Creation,entry.Body,part));
                entry.Next++;budget--;
            }
            if(entry.Next>=entry.Body.Count){entry.Dispose();outgoing.Remove(pair.Key);}
            if(budget==0)break;
        }
    }
    internal static void Receive(RebirthCamperRemovalParcelPacket packet,World world,GameManager manager,bool server)
    {
        try
        {
            if(!RebirthRemoteResourceNetworkScope.Receive(packet,world,manager,server,out var connection)||world.worldState.Guid!=packet.NativeWorld||
                packet.Operation==Guid.Empty||packet.Total>24*1024*1024)return;
            var actor=world.GetEntity(packet.Player) as EntityPlayer;
            if(actor==null||actor.IsDead()||packet.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                server&&!packet.ValidActorSender()||!server&&!ReferenceEquals(world.GetPrimaryPlayer(),actor))return;
            string scope="removal-parcel|"+(server?"request|":"result|")+packet.NativeWorld+"|"+owner.StorageKey+"|"+packet.Creation+"|"+
                RebirthRemoteResourceNetworkScope.SessionKey(connection)+"|"+packet.Hash;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,packet.Operation,packet.Hash,packet.Total,packet.Count,packet.Index,packet.Fragment,Time.realtimeSinceStartup,out var complete)||complete==null)return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(complete),24*1024*1024);
            if((string)body.Attribute("operation")!=packet.Operation.ToString("N"))return;
            if(server){var result=RebirthCamperRemovalParcelHost.Request(actor,body);if(result!=null)Reply(actor,packet.Operation,result);}
            else RebirthCamperStationContentsDelivery.AcceptParcelResult((EntityPlayerLocal)actor,packet.Operation,body);
        }
        catch{/* Unknown/stale body cannot become original evidence. */}
    }
}

public abstract class RebirthCamperRemovalParcelPacket:NetPackage
{
    internal int Player,Total,Count,Index;internal Guid Operation;internal string NativeWorld,Creation,Hash,Fragment;
    internal bool ValidActorSender()=>ValidEntityIdForSender(Player);
    internal void Set(int player,Guid operation,string world,string creation,RebirthRemoteResourceFragments.Body body,int part)
    {RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Player=player;Operation=operation;NativeWorld=world;Creation=creation;Hash=body.Hash;Total=body.Length;Count=body.Count;Index=part;Fragment=body.Part(part);}
    public override void read(PooledBinaryReader reader)
    {RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);if(reader.ReadUInt32()!=0x31505243)throw new System.IO.InvalidDataException();Player=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new System.IO.EndOfStreamException();Operation=new Guid(bytes);NativeWorld=RebirthCookingGridCustody.ReadWireString(reader,64);Creation=RebirthCookingGridCustody.ReadWireString(reader,512);Hash=RebirthCookingGridCustody.ReadWireString(reader,64);Total=reader.ReadInt32();Count=reader.ReadInt32();Index=reader.ReadInt32();Fragment=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(0x31505243u);writer.Write(Player);writer.Write(Operation.ToByteArray());RebirthCookingGridCustody.WriteWireString(writer,NativeWorld,64);RebirthCookingGridCustody.WriteWireString(writer,Creation,512);RebirthCookingGridCustody.WriteWireString(writer,Hash,64);writer.Write(Total);writer.Write(Count);writer.Write(Index);RebirthCookingGridCustody.WriteWireString(writer,Fragment,RebirthRemoteResourceFragments.ChunkText);}
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthRemovalParcelRequest:RebirthCamperRemovalParcelPacket
{
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthRemovalParcelRequest Setup(int player,Guid operation,string world,string creation,RebirthRemoteResourceFragments.Body body,int part){Set(player,operation,world,creation,body,part);return this;}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthCamperRemovalParcelTransport.Receive(this,world,callbacks,true);
}
[Preserve]
public sealed class NetPackageRebirthRemovalParcelResult:RebirthCamperRemovalParcelPacket
{
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthRemovalParcelResult Setup(int player,Guid operation,string world,string creation,RebirthRemoteResourceFragments.Body body,int part){Set(player,operation,world,creation,body,part);return this;}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthCamperRemovalParcelTransport.Receive(this,world,callbacks,false);
}
