using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class RemoteClose:IDisposable
    {
        internal ClientHydration Original;internal XUiC_WorkstationWindowGroup Window;internal long Session;internal Guid Operation;
        internal string Mode,Basis,Path;internal byte[] Native,Header,Observed;internal int Offset;
        internal bool SyncEntered,SyncRan,Enqueued,Closed,EchoObserved;internal int Setups,Sends,EchoReads;internal byte Handle;internal long EchoVersion;
        internal NetPackageTileEntity EchoPacket;
        internal NetPackageTileEntity Packet;internal byte[] PacketBody;internal IDisposable Memory,Source;
        internal float Next;
        internal bool Identity()=>!Closed&&Original.CurrentSource()&&ReferenceEquals(Original.Window,Window)&&
            ReferenceEquals(Window.WorkstationData?.TileEntity,Original.Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Original.Actor);
        public void Dispose(){if(Closed)return;Closed=true;Native=Header=Observed=PacketBody=null;Packet=null;Source?.Dispose();Memory?.Dispose();Source=Memory=null;}
    }
    private static readonly Dictionary<Guid,RemoteClose> remoteCloses=new Dictionary<Guid,RemoteClose>();
    [ThreadStatic]private static RemoteClose closingRecipient;
    private static string CloseNativeHash(byte[] bytes,int offset)
    {if(bytes==null||offset<0||offset>bytes.Length-8)throw new InvalidDataException();var copy=(byte[])bytes.Clone();Array.Clear(copy,offset,8);return HydrationHash(copy);}
    private static bool CloseOwnerUnchanged(RemoteClose close)
    {
        var original=close.Original;var expected=close.Mode=="unused"?original.BeforeFrame:original.AfterFrame;
        if(expected==null||original.InvokingCheck||close.Mode=="unused"&&(original.Attempted||original.Consumed))return false;
        var recipes=original.Members.Select(member=>member.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
        return RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(original.Actor,recipes,out int size)&&original.ReserveOwnerFrame(size,close.Mode!="unused")&&
            XNode.DeepEquals(expected,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(original.Actor,recipes));
    }
    internal static object BeginRemoteRecipientClose(XUiC_WorkstationWindowGroup window,bool run)
    {
        if(!run||closingRecipient!=null||!ThreadManager.IsMainThread()||!windowVersions.TryGetValue(window,out var version))return null;
        RemoteClose close=null;
        try
        {
            var found=clientHydrations.Values.Where(original=>!original.Closed&&!original.Unknown&&ReferenceEquals(original.Window,window)&&
                original.WindowVersion==version.Value&&original.CurrentSource()).ToArray();if(found.Length!=1)return null;
            var source=found[0];if(remoteCloses.ContainsKey(source.Group)||remoteCloses.Count>=4||LockManager.Instance?.IsLockedByLocalPlayer(source.Station,0)!=true)return null;
            string mode=source.OwnerStatus==2&&!source.Attempted&&!source.Consumed?"unused":source.OwnerStatus==5&&source.Terminal?.Current()==true?"consumed":null;
            if(mode==null)return null;
            close=new RemoteClose{Original=source,Window=window,Session=version.Value,Operation=Guid.NewGuid(),Mode=mode,
                Basis=mode=="unused"?source.BeforeDigest:source.AfterDigest,Path=source.Path+".close"};
            if(close.Basis==null||!CloseOwnerUnchanged(close))return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                var cards=window.craftingQueue?.GetRecipesToCraft();
                if(decoder==null||cards==null||cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(source.Station,cards.Select(CloseRow).ToArray(),window.inputWindow?.GetSlots(),window.outputWindow?.GetSlots(),
                        window.fuelWindow?.GetSlots(),window.toolWindow?.GetSlots(),out close.Native,out var projection,out close.Offset,out int projected)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(close.Native,close.Offset,projection,projected)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(close.Native,close.Offset,mode=="unused"?source.NativeBefore:source.NativeAfter,
                        mode=="unused"?source.NativeBeforeOffset:source.NativeAfterOffset)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*close.Native.Length+512*1024),out close.Memory))return null;
                var header=CloseFrame(close,"prepare");close.Header=OwnerFrameBytes(header);
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".original",OwnerFrameBytes(new XElement("recipientActualCloseOriginal",new XElement(header),
                    new XElement("native",Convert.ToBase64String(close.Native)),new XElement("ownerBasis",close.Basis)))))return null;
            }
            close.Source=source.Retain();if(close.Source==null||!close.Identity()||!SendCloseFrame(close,close.Header))return null;
            remoteCloses.Add(source.Group,close);closingRecipient=close;var result=close;close=null;return result;
        }
        catch{return null;}
        finally{close?.Dispose();}
    }
    private static RecipeQueueItem CloseRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),
        CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()};
    private static XElement CloseFrame(RemoteClose close,string phase)=>new XElement("recipientActualNativeClose",new XAttribute("version",1),new XAttribute("phase",phase),
        new XAttribute("group",close.Original.Group.ToString("N")),new XAttribute("operation",close.Operation.ToString("N")),new XAttribute("mode",close.Mode),
        new XAttribute("basis",close.Basis),new XAttribute("window",close.Session),new XAttribute("native",CloseNativeHash(close.Native,close.Offset)));
    private static bool SendCloseFrame(RemoteClose close,byte[] frame)
    {
        if(!close.Identity()||frame==null||frame.Length<1||frame.Length>4096||!RebirthLogisticsOriginalTransport.CanSendNative(close.Original.Connection))return false;
        var lease=close.Original.Retain();if(lease==null)return false;
        var packet=NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerV1>().Setup(close.Original.Group,5,close.Original.Actor.entityId,HydrationHash(frame),frame.Length,0,(byte[])frame.Clone(),lease);
        try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);return true;}
        catch{if(packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);return false;}
    }
    internal static bool CanSyncRemoteRecipientClose(XUiC_WorkstationWindowGroup group)
        =>closingRecipient is RemoteClose close&&ReferenceEquals(close.Window,group)&&close.Identity()&&!close.SyncEntered&&close.Observed==null&&CloseOwnerUnchanged(close);
    internal static void BeforeRemoteRecipientCloseSync(XUiC_WorkstationWindowGroup group,bool run,out object state)
    {
        state=null;var close=closingRecipient;
        if(!run||close==null||!CanSyncRemoteRecipientClose(group))return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(close.Original.Station,close.Native,close.Offset))
            {close.SyncEntered=true;state=close;}
    }
    internal static void AfterRemoteRecipientCloseSync(XUiC_WorkstationWindowGroup group,bool run,object state)
    {
        if(state is not RemoteClose close||!run||!ReferenceEquals(closingRecipient,close)||!ReferenceEquals(close.Window,group)||!close.Identity())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder!=null&&close.Enqueued&&close.Setups==1&&close.Sends==1&&RebirthProductiveEarnedNativeBody.MatchesCurrent(close.Original.Station,close.Native,close.Offset)&&
                close.Original.Station.lastTickTime==GameTimer.Instance.ticks)close.SyncRan=true;
    }
    internal static void ObserveRemoteRecipientClose(XUiC_WorkstationWindowGroup group,bool run,object state)
    {
        if(state is not RemoteClose close||!run||!ReferenceEquals(closingRecipient,close)||!ReferenceEquals(close.Window,group)||
            !close.SyncRan||!close.Enqueued||!close.Identity()||!CloseOwnerUnchanged(close))return;
        close.Observed=OwnerFrameBytes(CloseFrame(close,"observed"));
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".observed",close.Observed)){close.Observed=null;return;}
        SendCloseFrame(close,close.Observed);
    }
    internal static void EndRemoteRecipientClose(object state)
    {if(state is RemoteClose close&&ReferenceEquals(closingRecipient,close))closingRecipient=null;}
    private static void PumpRemoteRecipientCloses()
    {
        foreach(var close in remoteCloses.Values.ToArray())
            if(close.Observed!=null&&close.Identity()&&UnityEngine.Time.realtimeSinceStartup>=close.Next)
            {close.Next=UnityEngine.Time.realtimeSinceStartup+0.5f;SendCloseFrame(close,close.Observed);} // metadata only; never native sync/upload replay.
    }
    private static bool AcceptRemoteRecipientCloseReply(NetPackageRebirthUiDeferredOwnerReplyV1 reply,World world)
    {
        if(reply.Status!=6)return false;
        if(!remoteCloses.TryGetValue(reply.Group,out var close)||close.Observed==null||!close.EchoObserved||!close.Identity()||!ReferenceEquals(close.Original.World,world)||
            reply.Digest!=HydrationHash(close.Observed)||!TryGetReceivedConnection(reply,out var connection)||!ReferenceEquals(connection,close.Original.Connection))return true;
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(close.Path+".accepted",Encoding.ASCII.GetBytes("recipient-native-close:"+reply.Digest)))return true;
        SendCloseFrame(close,OwnerFrameBytes(CloseFrame(close,"acknowledged")));
        clientHydrations.Remove(reply.Group);close.Original.Close();remoteCloses.Remove(reply.Group);close.Dispose();return true;
    }
    private static void SweepRemoteRecipientCloses(World world)
    {foreach(var pair in remoteCloses.Where(value=>!ReferenceEquals(value.Value.Original.World,world)).ToArray()){remoteCloses.Remove(pair.Key);pair.Value.Dispose();}if(closingRecipient!=null&&!ReferenceEquals(closingRecipient.Original.World,world))closingRecipient=null;}
}


