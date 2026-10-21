using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    private const int MaximumInventoryPurpose=16*1024*1024+65536;
    private static readonly RebirthRemoteResourceRepairSendQueue inventoryPurposeWire=new RebirthRemoteResourceRepairSendQueue();
    private static string PurposeFragment(Guid operation,RebirthRemoteResourceFragments.Body body,int index,bool refusal)
        =>new XElement(refusal?"playerOwnerConsumptionRefusalFragment":"playerOwnerConsumptionFragment",new XAttribute("version",1),
            new XAttribute("operation",operation.ToString("N")),new XAttribute("hash",body.Hash),new XAttribute("total",body.Length),
            new XAttribute("count",body.Count),new XAttribute("index",index),body.Part(index)).ToString(SaveOptions.DisableFormatting);
    private static bool QueueInventoryPurpose(InventoryOriginal original,byte phase,ClientInfo peer,INetConnection connection)
    {
        string key="player-consumption-owner|"+original.Operation.ToString("N")+"|"+phase;
        if(inventoryPurposeWire.IsQueued(key))return true;
        int length=Encoding.UTF8.GetByteCount(original.Purpose);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(3L*length+65536),out var memory))return false;
        try
        {
            var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(original.Purpose));
            bool Current()=>original.Current()&&original.LastPurpose==phase&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(original.Actor.entityId),peer)&&
                peer.netConnection?.Length>0&&ReferenceEquals(peer.netConnection[0],connection)&&!connection.IsDisconnected();
            bool queued=inventoryPurposeWire.EnqueueObserved(key,body,Current,()=>RebirthRemoteResourceRepairProtocol.TransportReady(connection),index=>
                peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwner>().Setup(original.Actor.entityId,phase,PurposeFragment(original.Operation,body,index,false))),_=>memory.Dispose());
            if(!queued)memory.Dispose();return queued;
        }
        catch{memory.Dispose();throw;}
    }
    private static void QueueInventoryRefusal(EntityPlayerLocal actor,Guid operation,string purpose)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=actor.world;var connection=manager?.connectionToServer?.Length>0?manager.connectionToServer[0]:null;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);int deaths=actor.Died;
        string key="player-consumption-refusal|"+operation.ToString("N");if(inventoryPurposeWire.IsQueued(key)||connection==null)return;
        int length=Encoding.UTF8.GetByteCount(purpose);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(3L*length+65536),out var memory))return;
        try
        {
            var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(purpose));
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),actor)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&
                !actor.IsDead()&&actor.Died==deaths&&creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&
                manager.connectionToServer?.Length>0&&ReferenceEquals(manager.connectionToServer[0],connection)&&!connection.IsDisconnected();
            bool queued=inventoryPurposeWire.EnqueueObserved(key,body,Current,()=>RebirthRemoteResourceRepairProtocol.TransportReady(connection),index=>
                manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwnerReturn>().Setup(actor.entityId,PurposeFragment(operation,body,index,true),0),false),_=>memory.Dispose());
            if(!queued)memory.Dispose();
        }
        catch{memory.Dispose();throw;}
    }
    private static bool TryReceiveInventoryPurposeFragment(NetPackage packet,World world,GameManager callbacks,int actor,byte phase,string text,bool refusal,out string complete)
    {
        complete=null;string name=refusal?"playerOwnerConsumptionRefusalFragment":"playerOwnerConsumptionFragment";
        if(text==null||!text.StartsWith("<"+name,StringComparison.Ordinal))return false;
        try
        {
            if(!RebirthRemoteResourceNetworkScope.Receive(packet,world,callbacks,refusal,out var connection)||connection==null||text.Length>48000)return true;
            if(refusal?world.GetEntity(actor) is not EntityPlayer:world.GetPrimaryPlayer()?.entityId!=actor)return true;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(text,48000);
            if(root.Name!=name||root.HasElements||root.Attributes().Count()!=6||(int?)root.Attribute("version")!=1||
                !Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                (int?)root.Attribute("total") is not int total||total<1||total>MaximumInventoryPurpose)return true;
            string scope=name+"|"+world.worldState.Guid+"|"+actor+"|"+phase+"|"+RebirthRemoteResourceNetworkScope.SessionKey(connection);
            if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,(string)root.Attribute("hash"),total,(int)root.Attribute("count"),(int)root.Attribute("index"),root.Value,
                System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency,out var bytes)||bytes==null)return true;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(6L*total+65536),out var memory))return true;
            using var decoderScratch=memory;
            string candidate=new UTF8Encoding(false,true).GetString(bytes);
            var purpose=RebirthCookingRemoteAdmission.ReadProtocolXml(candidate,MaximumInventoryPurpose);
            if(purpose.Name!="playerOwnerConsumption"||(string)purpose.Attribute("operation")!=operation.ToString("N"))return true;
            complete=candidate;return true;
        }
        catch{return true;}
    }
}
