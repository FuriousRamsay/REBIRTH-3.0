using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class OwnerFrameStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>NetPackageRebirthUiDeferredOwnerV1.Maximum-count)throw new InvalidDataException();base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){if(Position>=NetPackageRebirthUiDeferredOwnerV1.Maximum)throw new InvalidDataException();base.WriteByte(value);}
    }
    private static byte[] OwnerFrameBytes(XElement frame)
    {
        using(var stream=new OwnerFrameStream())
        {
            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,CloseOutput=false}))frame.WriteTo(writer);
            return stream.ToArray();
        }
    }
    internal static void ReceiveOwnerReply(NetPackageRebirthUiDeferredOwnerReplyV1 packet,World world)
    {
        if(AcceptHydrationMetadataAck(packet,world))return;
        if(AcceptCachedPhysicalReply(packet,world))return;
        if(AcceptRemoteRecipientCloseReply(packet,world))return;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!clientHydrations.TryGetValue(packet.Group,out var original)||
            !ReferenceEquals(original.World,world)||!original.CurrentSource()||
            !TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,original.Connection))return;
        string expected=packet.Status<=2?original.BeforeDigest:original.AfterDigest;
        if(expected==null||packet.Digest!=expected||packet.Status<original.OwnerStatus)return;
        // A status only advances this original's observation protocol. The
        // actual owner/file/native body witnesses remain mandatory below.
        original.OwnerStatus=packet.Status;original.SendIndex=0;
    }
    private static bool FreezeBefore(ClientHydration original)
    {
        if(!original.CurrentWindow()||original.Unknown||original.Attempted||original.Members.Count<1||original.Members.Count>4)return false;
        var recipes=original.Members.Select(value=>value.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
        if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(original.Actor,recipes,out int measured)||
            !original.ReserveOwnerFrame(measured,false))return false;
        var frame=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(original.Actor,recipes);
        var bytes=OwnerFrameBytes(frame);var digest=HydrationHash(bytes);
        var intent=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),
            new XElement("before",new XElement("recipientBefore",new XAttribute("version",1),new XAttribute("digest",digest),
                new XAttribute("window",original.WindowVersion),new XElement("members",original.Members.Select(value=>new XElement("operation",value.Operation.ToString("N")))))));
        if(!RebirthWorkstationCompletionPublication.Write(original.Path+".before.xml",intent))return false;
        original.BeforeFrame=frame;original.BeforeWire=bytes;original.BeforeDigest=digest;return true;
    }
    private static bool StillExactBefore(ClientHydration original)
    {
        if(!original.CurrentWindow()||original.BeforeFrame==null)return false;
        var recipes=original.Members.Select(value=>value.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
        if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(original.Actor,recipes,out int measured)||
            !original.ReserveOwnerFrame(measured,false))return false;
        return XNode.DeepEquals(original.BeforeFrame,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(original.Actor,recipes));
    }
    private static void InvokeOriginalCheck(ClientHydration original)
    {
        if(original.Attempted||original.OwnerStatus!=2||!StillExactBefore(original))return;
        var attempt=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),
            new XElement("before",new XElement("nativeCheckAttempt",new XAttribute("version",1),new XAttribute("before",original.BeforeDigest),
                new XAttribute("window",original.WindowVersion),new XElement("members",original.Members.Select(value=>new XElement("operation",value.Operation.ToString("N")))))));
        if(!RebirthWorkstationCompletionPublication.Write(original.Path+".attempt.xml",attempt))return;
        original.BeforeReady=true;original.Attempted=true;original.InvokingCheck=true;
        try{original.Station.CheckForCraftComplete(original.Actor);if(!original.Consumed)original.Unknown=true;}
        catch{original.Unknown=true;}
        finally{original.InvokingCheck=false;}
    }
    private static bool SendOwnerControl(ClientHydration original,byte action,byte[] frame,string digest)
    {
        if(!original.CurrentSource()||digest==null||!RebirthLogisticsOriginalTransport.CanSendNative(original.Connection))return false;
        int index=frame==null?0:original.SendIndex;int length=frame==null?0:Math.Min(NetPackageRebirthUiDeferredOwnerV1.Chunk,frame.Length-index*NetPackageRebirthUiDeferredOwnerV1.Chunk);
        if(length<0)return false;var lease=original.Retain();if(lease==null)return false;
        var part=length==0?Array.Empty<byte>():new byte[length];if(length!=0)Buffer.BlockCopy(frame,index*NetPackageRebirthUiDeferredOwnerV1.Chunk,part,0,length);
        var packet=NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerV1>().Setup(original.Group,action,original.Actor.entityId,digest,frame?.Length??0,index,part,lease);
        packet.RegisterSendQueue();try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);}finally{packet.SendQueueHandled();}
        if(frame!=null)original.SendIndex=(index+1)%((frame.Length+NetPackageRebirthUiDeferredOwnerV1.Chunk-1)/NetPackageRebirthUiDeferredOwnerV1.Chunk);
        return true;
    }
    internal static void PumpRecipients()
    {
        if(!ThreadManager.IsMainThread())return;
        SweepDeferredWorld();
        PumpHydrationMetadataControl(GameManager.Instance?.World);
        PumpHistoricalCachedRecipientCouriers(GameManager.Instance?.World);
        PumpReadyHydrationMetadata(GameManager.Instance?.World);
        PumpClosedCachedPhysicalGroups();
        PumpRemoteRecipientCloses();
        foreach(var original in clientHydrations.Values.ToArray())
        {
            if(original.Closed||original.Unknown||!original.CurrentSource()||UnityEngine.Time.realtimeSinceStartup<original.NextOwnerPump)continue;
            original.NextOwnerPump=UnityEngine.Time.realtimeSinceStartup+0.1f;
            try
            {
                if(PumpCachedPhysicalRecipient(original))continue;
                if(original.BeforeFrame==null&&!FreezeBefore(original))continue;
                if(original.OwnerStatus==0){for(int i=0;i<4;i++)if(!SendOwnerControl(original,0,original.BeforeWire,original.BeforeDigest))break;continue;}
                if(original.OwnerStatus==1)
                {
                    if(!StillExactBefore(original))continue;
                    SendOwnerControl(original,1,null,original.BeforeDigest);
                    GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);continue;
                }
                if(original.OwnerStatus==2&&!original.Attempted)InvokeOriginalCheck(original);
                if(!original.Consumed||original.AfterFrame==null)continue;
                if(original.AfterWire==null){original.AfterWire=OwnerFrameBytes(original.ConsumedReport);original.AfterDigest=HydrationHash(original.AfterWire);original.SendIndex=0;}
                if(original.OwnerStatus==2){for(int i=0;i<4;i++)if(!SendOwnerControl(original,2,original.AfterWire,original.AfterDigest))break;continue;}
                if(original.OwnerStatus==3)
                {
                    SendOwnerControl(original,3,null,original.AfterDigest);
                    GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);continue;
                }
                // Status4 requires a separate exact native removal sender and
                // status5 a private final/UQR permit; neither is inferred here.
                if(original.OwnerStatus==4)
                {
                    using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    {
                        if(decoder==null)continue;
                        if(original.NativeAfter==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.NativeAfter,original.NativeAfterOffset))
                        {original.Unknown=true;continue;}
                    }
                    if(RebirthLogisticsOriginalTransport.CanSendNative(original.Connection))
                        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Station,StreamModeWrite.ToServer));
                    SendOwnerControl(original,4,null,original.AfterDigest);
                }
                if(original.OwnerStatus==5&&original.Terminal==null&&ClientTerminalPublication.TryIssue(original,out var terminal))original.Terminal=terminal;
                if(original.Terminal!=null&&!original.HandoffAccepted&&
                    !RebirthWorkstationCompletionPipeline.ObserveCachedRecipientTerminal(original.Terminal)&&
                    RebirthProductiveEarnedUiCoordinator.TryAcceptRecipientTerminal(original.Terminal))original.HandoffAccepted=true;
                if(original.Terminal!=null&&!original.HandoffAccepted&&original.CachedPhysicalObservation?.Current()==true)
                    SendOwnerControl(original,4,null,original.AfterDigest);
            }
            catch{if(original.Attempted)original.Unknown=true;}
        }
    }
    private static void SweepDeferredWorld()
    {
        var world=GameManager.Instance?.World;
        SweepHydrationMetadata(world);
        SweepRemoteRecipientCloses(world);SweepRemoteHostCloses(world);
        SweepHostOwnerWorld(world);
        RebirthProductiveEarnedUiOwnerFile.SweepLocalOwnerWorld(world);
        foreach(var pair in hostLocalReceipts.Where(value=>!ReferenceEquals(value.Value.Original.World,world)).ToArray())
        {hostLocalReceipts.Remove(pair.Key);pair.Value.Close();}
        // World replacement destroys these actual native handles. Drop charged
        // detached graphs; immutable original/attempt/event files remain. This
        // is neither a terminal ACK nor permission to hydrate/replay cold work.
        foreach(var pair in clientHydrations.Where(value=>!ReferenceEquals(value.Value.World,world)).ToArray())
        {clientHydrations.Remove(pair.Key);pair.Value.Close();}
        foreach(var pair in assemblingHydration.Where(value=>!ReferenceEquals(value.Value.World,world)).ToArray())
        {assemblingHydration.Remove(pair.Key);pair.Value.Parts=null;pair.Value.Budget?.Dispose();pair.Value.Budget=null;}
        foreach(var pair in removals.Where(value=>!ReferenceEquals(value.Value.Scope.World,world)).ToArray())
        {removals.Remove(pair.Key);pair.Value.Dispose();}
        foreach(var value in recipients.Values.Where(value=>!ReferenceEquals(value.World,world)).ToArray())value.Dispose();
        foreach(var pair in dispatches.Where(value=>!ReferenceEquals(value.Value.World,world)).ToArray())
        {dispatches.Remove(pair.Key);pair.Value.Close();}
        foreach(var value in host.Values.Where(value=>!ReferenceEquals(value.World,world)).ToArray())value.Dispose();
    }
}
