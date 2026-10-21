using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Historical metadata courier only. It cannot recreate a native receipt,
    // hydration/action source, current station body, primary or owner effect.
    private sealed class CachedRecipientCourier:IDisposable
    {
        internal Guid Group;internal World World;internal EntityPlayerLocal Actor;internal INetConnection Connection;
        internal string Owner,Creation,Digest,Path;internal byte[] Image;internal IDisposable Allocation;internal float Next;
        internal bool Current()=>Allocation!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&!Actor.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&!Connection.IsDisconnected()&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        public void Dispose(){Image=null;Actor=null;Connection=null;World=null;Allocation?.Dispose();Allocation=null;}
    }
    private static readonly Dictionary<Guid,CachedRecipientCourier> cachedRecipientCouriers=new Dictionary<Guid,CachedRecipientCourier>();
    private static float nextCachedRecipientCourierScan;
    private static XElement CachedRecipientAcknowledgement(Guid group,string world,string digest,string owner,string creation)=>
        new XElement("workstationCompletionIntent",new XAttribute("operation",group.ToString("N")),new XAttribute("world",world),new XElement("before",
            new XElement("cachedPhysicalRecipientRetired",new XAttribute("version",1),new XAttribute("native",digest),new XAttribute("group",group.ToString("N")),
                new XAttribute("owner",owner),new XAttribute("creation",creation))));
    private static void PumpHistoricalCachedRecipientCouriers(World world)
    {
        foreach(var pair in cachedRecipientCouriers.Where(value=>!ReferenceEquals(value.Value.World,world)||!value.Value.Current()).ToArray())
        {cachedRecipientCouriers.Remove(pair.Key);pair.Value.Dispose();}
        if(world?.IsRemote()!=true)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(now>=nextCachedRecipientCourierScan&&cachedRecipientCouriers.Count<4)
        {
            nextCachedRecipientCourierScan=now+1f;
            var actor=world.GetPrimaryPlayer();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];
            if(actor!=null&&!actor.IsDead()&&connection!=null&&!connection.IsDisconnected()&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))
            {
                string folder=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"uiDeferred");
                RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);
                if(Directory.Exists(folder))
                {
                    int scanned=0;foreach(string path in Directory.EnumerateFiles(folder,"*.xml.cached-physical-final"))
                    {
                        if(++scanned>4096||cachedRecipientCouriers.Count>=4)break;
                        IDisposable memory=null;CachedRecipientCourier courier=null;
                        try
                        {
                            long size=RebirthWorkstationCompletionPublication.PublishedLength(path);if(size<1||size>65536||
                                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*size+262144),out memory))continue;
                            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var node=record?.Element("before")?.Element("cachedPhysicalRecipientFinal");
                            if(node==null||(int?)node.Attribute("version")!=1||!Guid.TryParseExact((string)record.Attribute("operation"),"N",out var group)||group==Guid.Empty||
                                (string)node.Attribute("group")!=group.ToString("N")||(string)record.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                                (string)node.Attribute("owner")!=owner.StorageKey||(string)node.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                                cachedRecipientCouriers.ContainsKey(group)||clientHydrations.TryGetValue(group,out var live)&&live.CurrentSource())continue;
                            string digest=(string)node.Attribute("native");if(digest?.Length!=64||digest.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f')))continue;
                            var expected=CachedRecipientAcknowledgement(group,(string)record.Attribute("world"),digest,owner.StorageKey,(string)node.Attribute("creation"));
                            if(XNode.DeepEquals(expected,RebirthWorkstationCompletionPublication.ReadPrimary(path+".ack")))continue;
                            string observed=(string)node.Attribute("observation");long observedSize=RebirthWorkstationCompletionPublication.PublishedLength(observed);
                            if(observedSize<1||observedSize>65536||HydrationHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(observed))!=(string)node.Attribute("observationDigest"))continue;
                            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)continue;
                            courier=new CachedRecipientCourier{Group=group,World=world,Actor=actor,Connection=connection,Owner=owner.StorageKey,
                                Creation=(string)node.Attribute("creation"),Digest=digest,Path=path,Image=image,Allocation=memory};memory=null;
                            if(!courier.Current())continue;cachedRecipientCouriers.Add(group,courier);courier=null;
                        }
                        catch{/* Unknown/staged metadata cannot restore an action. */}
                        finally{courier?.Dispose();memory?.Dispose();}
                    }
                }
            }
        }
        foreach(var courier in cachedRecipientCouriers.Values.ToArray())
        {
            if(!courier.Current()||now<courier.Next||!RebirthLogisticsOriginalTransport.CanSendNative(courier.Connection))continue;
            courier.Next=now+1f;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var packetMemory))continue;
            var packet=NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerV1>().Setup(courier.Group,6,courier.Actor.entityId,courier.Digest,0,0,Array.Empty<byte>(),packetMemory);
            packet.RegisterSendQueue();try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);}finally{packet.SendQueueHandled();}
        }
    }
    private static bool AcceptHistoricalCachedRecipientReply(NetPackageRebirthUiDeferredOwnerReplyV1 packet,World world)
    {
        if(packet?.Status!=8||!cachedRecipientCouriers.TryGetValue(packet.Group,out var courier))return false;
        if(!ReferenceEquals(courier.World,world)||!courier.Current()||packet.Digest!=courier.Digest||!TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,courier.Connection))return true;
        var record=CachedRecipientAcknowledgement(courier.Group,RebirthWorkstationCompletionOutbox.CurrentGuid(world),courier.Digest,courier.Owner,courier.Creation);
        if(!RebirthWorkstationCompletionPublication.Write(courier.Path+".ack",record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(courier.Path+".ack")))return true;
        if(clientHydrations.TryGetValue(courier.Group,out var old)&&ReferenceEquals(old.World,world)){clientHydrations.Remove(courier.Group);old.Close();}
        clientCachedPhysicalReceipts.Remove(courier.Group);cachedRecipientCouriers.Remove(courier.Group);courier.Dispose();return true;
    }
}
