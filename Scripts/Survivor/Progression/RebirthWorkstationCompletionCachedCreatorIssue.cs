using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // This source is issued by the whole selected native send group, never by
    // parsing the client request or finding a matching station afterwards.
    internal sealed class CachedCreatorIssue
    {
        internal readonly CachedCustody Cache;
        internal readonly Guid Ticket,Group;
        internal readonly Guid RetainedAction;
        internal readonly EntityPlayer Viewer;
        internal readonly ClientInfo Sender;
        internal readonly INetConnection Connection;
        internal readonly int Epoch,Deaths;
        internal readonly long ReadFence;
        internal readonly string Owner,Creation;
        internal readonly World IssuedWorld;
        internal readonly TileEntityWorkstation IssuedStation;
        private readonly WeakReference<byte[]> native,saved;
        private readonly byte[] retainedAttempt;
        private readonly string retainedAttemptPath;
        private CachedCreatorIssue(CachedCustody cache,XElement grant,Guid group,EntityPlayer viewer,ClientInfo sender,INetConnection connection,byte[] historicalAttempt)
        {
            Cache=cache;Ticket=Guid.ParseExact((string)grant.Attribute("ticket"),"N");Group=group;Viewer=viewer;Sender=sender;Connection=connection;
            IssuedWorld=cache.World;IssuedStation=cache.Station;
            RetainedAction=grant.Attribute("retainedAction")==null?Guid.Empty:Guid.ParseExact((string)grant.Attribute("retainedAction"),"N");
            retainedAttempt=historicalAttempt;
            retainedAttemptPath=historicalAttempt==null?null:cache.Path+".remote-action."+cache.Epoch;
            Epoch=cache.Epoch;Deaths=viewer.Died;RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner);Owner=owner.StorageKey;Creation=RebirthCamperCharacterAccess.CurrentCreation(viewer);
            native=new WeakReference<byte[]>(cache.Native);saved=new WeakReference<byte[]>(cache.SavedImage);
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(cache.World,cache.Station,out ReadFence);
        }
        internal bool RecordedIdentity()=>Cache.Active&&ReferenceEquals(Cache.CreatorIssue,this)&&Cache.SourceCurrent()&&Cache.PrimaryCurrent()&&
            ReferenceEquals(Cache.World,IssuedWorld)&&ReferenceEquals(Cache.Station,IssuedStation)&&
            (RetainedAction==Guid.Empty?retainedAttempt==null:retainedAttempt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(retainedAttemptPath,retainedAttempt))&&
            ReferenceEquals(GameManager.Instance?.World,Cache.World)&&ReferenceEquals(Cache.World.GetEntity(Viewer.entityId),Viewer)&&
            Viewer.Died==Deaths&&!Viewer.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(Viewer)==Creation&&
            Sender!=null&&Sender.entityId==Viewer.entityId&&!Sender.disconnecting&&Sender.loginDone&&
            ReferenceEquals(Sender.netConnection?[0],Connection)&&!Connection.IsDisconnected()&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var owner)&&owner.StorageKey==Owner;
        internal bool CurrentIdentity()=>RecordedIdentity()&&RebirthProductiveEarnedArtifact.RemoteAuthority(Viewer,Sender,Cache.Station,Creation);
        internal bool Current()=>CurrentIdentity()&&Cache.Epoch==Epoch&&native.TryGetTarget(out var nativeHead)&&saved.TryGetTarget(out var savedHead)&&
            ReferenceEquals(Cache.Native,nativeHead)&&ReferenceEquals(Cache.SavedImage,savedHead)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(Cache.World,Cache.Station,out var currentRead)&&currentRead==ReadFence;
        internal bool CurrentBeforeAction()=>Current()&&!Cache.Physical&&!Cache.PhysicalObserved&&!Cache.RestoreAttempted&&Cache.SavedCurrent()&&Cache.NativeCurrent();
        // The new native send group authenticates the current peer. It must
        // not replace the group/session of the saved action being resumed.
        internal bool TryCopyRetainedPrepare(out XElement request)
            =>TryCopyRetainedPrepare(true,out request);
        private bool TryCopyRetainedPrepare(bool requireCurrentNative,out XElement request)
        {
            request=null;
            try
            {
                if(RetainedAction==Guid.Empty||!(requireCurrentNative?Current():CurrentIdentity())||retainedAttempt==null||
                    !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(retainedAttemptPath,retainedAttempt))return false;
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(retainedAttemptPath);
                var attempt=record?.Element("cachedPhysicalAttempt");
                var saved=attempt?.Element("cachedPhysicalPrepare");
                if(saved!=null){saved=new XElement(saved);saved.Attribute("challenge")?.Remove();}
                if(!XNode.DeepEquals(record?.Element("before"),Cache.Original.Element("before"))||
                    !XNode.DeepEquals(attempt?.Element("workstationCompletionIntent"),Cache.Original)||
                    saved==null||saved.HasElements||saved.Attributes().Count()!=5||
                    (string)saved.Attribute("ticket")!=Ticket.ToString("N")||
                    (string)saved.Attribute("action")!=RetainedAction.ToString("N")||
                    !Guid.TryParseExact((string)saved.Attribute("group"),"N",out var oldGroup)||oldGroup==Guid.Empty||
                    (long?)saved.Attribute("session") is not long oldSession||oldSession<1||
                    requireCurrentNative&&(!TryDescribeRetainedCachedAction(Cache,out var oldTicket,out var oldAction)||oldTicket!=Ticket||oldAction!=RetainedAction)||
                    !(requireCurrentNative?Current():CurrentIdentity())||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(retainedAttemptPath,retainedAttempt))return false;
                request=new XElement(saved);return true;
            }
            catch{request=null;return false;}
        }
        internal bool MatchesPrepareRequest(XElement request)
        {
            try
            {
                if(!CurrentIdentity()||request?.Name!="cachedPhysicalPrepare"||request.HasElements)return false;
                var canonical=new XElement(request);canonical.Attribute("challenge")?.Remove();
                if(canonical.Attributes().Count()!=5||
                    (string)canonical.Attribute("ticket")!=Ticket.ToString("N")||
                    !Guid.TryParseExact((string)canonical.Attribute("action"),"N",out var action)||action==Guid.Empty||
                    (long?)canonical.Attribute("session") is not long session||session<1)return false;
                if(RetainedAction!=Guid.Empty)
                    return TryCopyRetainedPrepare(false,out var historical)&&XNode.DeepEquals(canonical,historical);
                return (string)canonical.Attribute("group")==Group.ToString("N");
            }
            catch{return false;}
        }
        internal static CachedCreatorIssue FromActualEnqueue(CachedCustody cache,XElement grant,Guid group,EntityPlayer viewer,ClientInfo sender,INetConnection connection)
        {
            var before=cache?.Original?.Element("before")?.Elements().SingleOrDefault();
            if(!ReferenceEquals(issuingCachedCreatorCache,cache)||group==Guid.Empty||viewer==null||sender==null||connection==null||
                !Guid.TryParseExact((string)grant?.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity)||
                before==null||identity.StorageKey!=(string)before.Attribute("viewer")||viewer.Died!=(int?)before.Attribute("deaths")||
                viewer.IsDead()||RebirthCamperCharacterAccess.CurrentCreation(viewer)!=(string)before.Attribute("creation")||
                !ReferenceEquals(sender.netConnection?[0],connection)||connection.IsDisconnected()||
                !cache.Active||cache.Physical||cache.PhysicalObserved||!cache.SavedCurrent()||!CurrentCachedCreatorFence(cache,cache.World)||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(viewer,sender,cache.Station,RebirthCamperCharacterAccess.CurrentCreation(viewer)))return null;
            if(!TryDescribeRetainedCachedAction(cache,out var oldTicket,out var oldAction)||
                cache.RestoreAttempted&&oldAction==Guid.Empty||
                (oldAction==Guid.Empty?grant.Attribute("retainedAction")!=null:
                    oldTicket!=ticket||(string)grant.Attribute("retainedAction")!=oldAction.ToString("N")))return null;
            byte[] historicalAttempt=null;
            if(oldAction!=Guid.Empty)
            {
                string path=cache.Path+".remote-action."+cache.Epoch;
                long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(length<1||length>RebirthWorkstationCompletionPublication.MaximumBytes||
                    !cache.ReserveRetirementWorkspace(checked(16*length+65536)))return null;
                historicalAttempt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                if(historicalAttempt==null||historicalAttempt.LongLength!=length||
                    !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,historicalAttempt)||
                    !TryDescribeRetainedCachedAction(cache,out var checkedTicket,out var checkedAction)||checkedTicket!=ticket||checkedAction!=oldAction)return null;
            }
            return new CachedCreatorIssue(cache,grant,group,viewer,sender,connection,historicalAttempt);
        }
    }
    [ThreadStatic]private static CachedCustody issuingCachedCreatorCache;
}
