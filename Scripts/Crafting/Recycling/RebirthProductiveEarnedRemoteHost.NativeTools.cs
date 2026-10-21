using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal sealed class NativeTools:IDisposable
    {
        internal Context Source;internal RebirthProductiveEarnedRemoteToolPlan.Description Plan;internal byte[] Record,OwnerBefore,OwnerAfter;
        internal WeakReference<PlayerDataFile> Previous,AcceptedData;internal string OwnerPath,OwnerRoot,OwnerName;
        internal volatile bool OwnerAccepted,OwnerWritten;internal bool OwnerSaved,Accepted,Saved,Closed;internal int Workers;internal readonly object Gate=new object();internal IDisposable Memory;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        public void Dispose(){Watch?.Dispose();Watch=null;lock(Gate){Closed=true;if(Workers==0)ReleaseMemory();}}
        internal void ReleaseMemory(){if(AcceptedData!=null&&AcceptedData.TryGetTarget(out var data))nativeToolOwnerFiles.Remove(data);Memory?.Dispose();Memory=null;Record=OwnerBefore=OwnerAfter=null;Previous=AcceptedData=null;Source=null;Plan=null;}
    }
    private sealed class NativeToolOwnerCall {internal NativeTools Tools;internal PlayerDataFile Data;internal byte[] Image;}
    private static readonly ConditionalWeakTable<PlayerDataFile,NativeToolOwnerCall> nativeToolOwnerFiles=new ConditionalWeakTable<PlayerDataFile,NativeToolOwnerCall>();
    internal static bool RegisterNativeTools(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,byte[] bytes,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(TryHistoricalViewerToolsReply(actor,peer,station,creation,operation,nonce,bytes,out reply))return true;
        bool viewer=pending.TryGetValue(operation,out var viewerSource)&&viewerSource.ViewerToolsOnly;
        if(viewer&&!viewerSource.ViewerToolsReleased&&viewerSource.Tools?.Saved==true&&ReferenceEquals(viewerSource.Actor,actor)&&ReferenceEquals(viewerSource.Sender,peer)&&
            viewerSource.Nonce==nonce&&viewerSource.Creation==creation&&actor.Died==viewerSource.Deaths&&viewerSource.Tools.Record.SequenceEqual(bytes)&&
            RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation))FinishViewerToolsHandoff(viewerSource);
        if(viewer&&viewerSource.ViewerToolsReleased)
        {
            if(!ReferenceEquals(viewerSource.Actor,actor)||!ReferenceEquals(viewerSource.Sender,peer)||!ReferenceEquals(viewerSource.Station,station)||
                viewerSource.Creation!=creation||actor.Died!=viewerSource.Deaths||viewerSource.Nonce!=nonce||!viewerSource.Tools.Record.SequenceEqual(bytes)||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,peer,station,creation)||
                !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(viewerSource.HeaderPath+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(bytes))))return false;
            reply=NativeOutputReply(viewerSource,false);reply.State=RebirthProductiveEarnedWire.Status.NativeToolSaved;reply.QueueOwner="ownerSaved";return true;
        }
        if(!viewer&&!TryRebindNativeManifest(actor,peer,station,creation,operation,nonce)||!pending.TryGetValue(operation,out var source)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Sender,peer)||!ReferenceEquals(source.Station,station)||
            source.Creation!=creation||source.Nonce!=nonce||!source.Current()||(!viewer&&(!RestoreNativeOutputDescription(source)||!RestoreNativeToolsDescription(source)))||source.NativeOutput?.PendingIndex<0||source.NativePending!=null)return false;
        if(source.Tools==null)
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
            if(decoder==null)return false;var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(!decoder.Admit(frame,source.Image.Length))return false;
            if((viewer?!ViewerToolSourceCurrent(source)||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.NativeOutput.Native,source.NativeOutput.Offset):!TrySaveNativeOutput(source)||!NativeOutputCurrent(source))||peer.latestPlayerData==null||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*bytes.Length+64L*1024*1024),out var held))return false;
            try
            {
                {
                    if(!RebirthProductiveEarnedRemoteToolPlan.TryRead(bytes,operation,frame.Birth,nonce,Digest(source.Image),source.NativeOutput.Native,source.NativeOutput.Offset,out var plan)||
                        plan.OwnedBelt!=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,plan.BeltCount)||peer.latestPlayerData.id!=actor.entityId||peer.latestPlayerData.deaths!=source.Deaths||!RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(peer.latestPlayerData,plan,false)||
                        !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.StorageKey!=source.Owner||
                        !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.id!=actor.entityId||saved.deaths!=source.Deaths||!RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(saved,plan,false))return false;
                    byte[] native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(peer.latestPlayerData);
                    string root=Path.GetFullPath(GameIO.GetPlayerDataDir()),path=Path.Combine(root,identity.CanonicalId+"."+PlayerDataFile.EXT);
                    if(!Safe(path)||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(path,native)||
                        viewer&&!RetainViewerToolPaymentHold(source)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeTools",bytes,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeTools.ownerBefore",native,16*1024*1024))return false;
                    source.Tools=new NativeTools{Source=source,Plan=plan,Record=(byte[])bytes.Clone(),OwnerBefore=native,OwnerPath=path,OwnerRoot=root,OwnerName=identity.CanonicalId,
                        Previous=new WeakReference<PlayerDataFile>(peer.latestPlayerData),Memory=held};held=null;
                }
            }
            finally{held?.Dispose();}
            }
        }
        var tools=source.Tools;
        if(!tools.Record.SequenceEqual(bytes)||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeTools",bytes,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return false;
        TrySaveNativeTools(source);
        reply=NativeOutputReply(source,false);reply.State=tools.Saved&&(!source.ViewerToolsOnly||source.ViewerToolsReleased)?RebirthProductiveEarnedWire.Status.NativeToolSaved:RebirthProductiveEarnedWire.Status.NativeToolHeader;
        // The separate owner-native save is an explicit phase, not permission
        // inferred from equal post-gesture arrays or a station upload.
        reply.QueueOwner=tools.OwnerSaved?"ownerSaved":"ownerBefore";return true;
    }
    internal static void EnterNativeToolOwner(ClientInfo peer,PlayerDataFile data,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null)return;
        foreach(var source in pending.Values)
        {
            var tools=source.Tools;
            if(tools==null||tools.OwnerSaved||!ReferenceEquals(source.Sender,peer)||!source.Current()||data.id!=source.Actor.entityId||data.deaths!=source.Deaths||
                tools.Plan.OwnedBelt!=RebirthToolbeltCapacity.GetOwnedSlotCount(source.Actor,tools.Plan.BeltCount)||tools.Previous==null||!tools.Previous.TryGetTarget(out var previous)||!ReferenceEquals(peer.latestPlayerData,previous)||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(previous).SequenceEqual(tools.OwnerBefore)||!RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(data,tools.Plan,true))continue;
            byte[] image=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
            if(tools.OwnerAfter!=null&&!tools.OwnerAfter.SequenceEqual(image))return;
            tools.OwnerAfter=image;tools.AcceptedData=new WeakReference<PlayerDataFile>(data);
            var call=new NativeToolOwnerCall{Tools=tools,Data=data,Image=image};nativeToolOwnerFiles.Remove(data);nativeToolOwnerFiles.Add(data,call);state=call;return;
        }
    }
    internal static void ObserveNativeToolOwner(ClientInfo peer,PlayerDataFile data,bool run,object state)
    {
        if(!run||state is not NativeToolOwnerCall call||!ThreadManager.IsMainThread()||!ReferenceEquals(call.Data,data)||
            !ReferenceEquals(peer.latestPlayerData,data)||!ReferenceEquals(call.Tools.Source.Sender,peer)||!call.Tools.Source.Current()||
            !ReferenceEquals(call.Tools.Source.Actor.QuestJournal,data.questJournal))return;
        call.Tools.OwnerAccepted=true;
        if(call.Tools.OwnerWritten)TrySaveNativeToolOwner(call.Tools.Source);
    }
    internal static void EnterNativeToolOwnerSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null||!nativeToolOwnerFiles.TryGetValue(data,out var call))return;
        var tools=call.Tools;
        if(tools.Memory==null||Path.GetFullPath(directory)!=tools.OwnerRoot||name!=tools.OwnerName||
            !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data).SequenceEqual(call.Image))return;
        lock(tools.Gate){if(tools.Closed||tools.Workers>=2)return;tools.Workers++;state=call;}
    }
    internal static void ObserveNativeToolOwnerSave(bool run,object state)
    {
        if(state is not NativeToolOwnerCall call)return;
        try
        {
            if(run&&call.Tools.Memory!=null&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(call.Tools.OwnerPath,call.Image))
            {
                call.Tools.OwnerWritten=true;
                if(call.Tools.OwnerAccepted&&!call.Tools.Closed)RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(call.Tools.Source.HeaderPath+".nativeTools.ownerAfter",call.Image,16*1024*1024);
            }
        }
        finally{lock(call.Tools.Gate){call.Tools.Workers--;if(call.Tools.Closed&&call.Tools.Workers==0){call.Image=null;call.Data=null;call.Tools.ReleaseMemory();}}}
    }
    private static bool TrySaveNativeToolOwner(Context source)
    {
        var tools=source.Tools;if(tools==null||!source.Current())return false;
        if(tools.OwnerSaved){TrimNativeToolsRetention(tools);return RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeTools.ownerAfter",tools.OwnerAfter,16*1024*1024);}
        if(!tools.OwnerAccepted||!tools.OwnerWritten||tools.AcceptedData==null||!tools.AcceptedData.TryGetTarget(out var accepted)||!ReferenceEquals(source.Sender.latestPlayerData,accepted)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(source.Sender,out var identity)||identity.StorageKey!=source.Owner||
            !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.id!=source.Actor.entityId||saved.deaths!=source.Deaths||!RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(saved,tools.Plan,true)||
            !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(tools.OwnerPath,tools.OwnerAfter)||
            !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeTools.ownerAfter",tools.OwnerAfter,16*1024*1024))return false;
        tools.OwnerSaved=true;if(tools.AcceptedData.TryGetTarget(out var data))nativeToolOwnerFiles.Remove(data);TrimNativeToolsRetention(tools);return true;
    }
    private static void TrimNativeToolsRetention(NativeTools tools)
    {
        if(!ThreadManager.IsMainThread()||!tools.OwnerSaved||tools.OwnerBefore==null||tools.OwnerAfter==null)return;
        lock(tools.Gate)
            if(!tools.Closed&&tools.Workers==0)RebirthProductiveEarnedObservationBudget.TryResizeDetached(tools.Memory,
                checked(4L*(tools.Record.Length+tools.OwnerBefore.Length+tools.OwnerAfter.Length)+2L*(tools.Plan.Before.Length+tools.Plan.After.Length)+
                    2L*(tools.Plan.BeforeBag.Length+tools.Plan.BeforeBelt.Length+tools.Plan.AfterBag.Length+tools.Plan.AfterBelt.Length+tools.Plan.Tools.Length)+65536));
    }
    private static bool NativeToolsCurrent(Context source)
        =>source?.Tools?.Accepted==true&&NativeToolsBodyCurrent(source);
    private static bool NativeToolsBodyCurrent(Context source)
    {
        var tools=source?.Tools;
        using(var callbacks=source?.Memory?.TryEnterCallbacks())return callbacks!=null&&source.Current()&&tools?.OwnerSaved==true&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeTools",tools.Record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeTools.ownerBefore",tools.OwnerBefore,16*1024*1024)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeTools.ownerAfter",tools.OwnerAfter,16*1024*1024)&&
            RebirthProductiveEarnedCoordinator.TryNativeMarker(source.Station,out var marker)&&source.Artifact.MatchesMarker(marker)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,tools.Plan.After,tools.Plan.AfterOffset);
    }
    private static void ObserveAcceptedNativeTools(Context source)
    {
        var tools=source.Tools;if(!NativeToolsBodyCurrent(source))return;
        tools.Accepted=true;source.NativeOutputWatch?.Dispose();source.NativeOutputWatch=null;
        if(tools.Watch==null&&RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),
            RemoteResourceSourceKind.WorkstationOutput,source.Artifact.CustodyBirth.ToString("N"),out var binding))
        {var p=source.Station.localChunkPos;tools.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,()=>NativeToolsCurrent(source),source.Memory,"RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z);}
    }
    private static bool TrySaveNativeTools(Context source)
    {
        var tools=source.Tools;if(tools==null||!TrySaveNativeToolOwner(source)||!tools.Accepted||!NativeToolsCurrent(source))return false;
        if(tools.Saved&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(tools.Record))))
        {tools.Watch?.Dispose();tools.Watch=null;return !source.ViewerToolsOnly||FinishViewerToolsHandoff(source);}
        if(tools.Watch==null)return false;
        var loaded=source.Artifact.LoadedSource;
        bool loadedSaved=loaded?.ToolsRebased==true&&loaded.Terminal&&loaded.PendingNextWork&&loaded.PrimaryCurrent()&&
            loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation)&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,tools.Plan.After,tools.Plan.AfterOffset);
        if(!loadedSaved&&!RebirthProductiveEarnedCoordinator.PublishRemoteNativeTools(source.Artifact,source.NativeOutput,tools.Plan,tools.Record,source.HeaderPath,source.Memory,tools.Watch,()=>NativeToolsCurrent(source)))return false;
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(tools.Record))))return false;
        tools.Saved=true;tools.Watch.Dispose();tools.Watch=null;
        if(source.ViewerToolsOnly&&!FinishViewerToolsHandoff(source))return false;
        return true;
    }
    private static RebirthProductiveEarnedNativeOutputPlan.Description EffectiveNativeOutput(Context source)
    {
        if(source.Tools?.Saved!=true)return source.NativeOutput;
        return EffectiveNativeOutput(source.NativeOutput,source.Tools.Plan);
    }
    internal static RebirthProductiveEarnedNativeOutputPlan.Description EffectiveNativeOutput(RebirthProductiveEarnedNativeOutputPlan.Description paid,RebirthProductiveEarnedRemoteToolPlan.Description tools)
        =>new RebirthProductiveEarnedNativeOutputPlan.Description{Operation=paid.Operation,Birth=paid.Birth,Nonce=paid.Nonce,OriginalDigest=paid.OriginalDigest,
            Native=tools.After,Offset=tools.AfterOffset,Primary=paid.Primary,Additional=paid.Additional,PendingIndex=paid.PendingIndex,PendingBefore=paid.PendingBefore,PendingTarget=paid.PendingTarget,Binding=paid.Binding,Extras=paid.Extras};
    private static bool RestoreNativeToolsDescription(Context source)
    {
        if(source.Tools!=null)return true;
        string path=source.HeaderPath+".nativeTools";
        if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(source.NativeOutput==null||!source.NativeOutputSaved||!source.Current())return false;
        IDisposable held=null;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
                if(!decoder.Admit(frame,source.Image.Length)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*new FileInfo(path).Length+64L*1024*1024),out held))return false;
                byte[] bytes=ReadNativeOutputMetadata(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(!RebirthProductiveEarnedRemoteToolPlan.TryRead(bytes,source.Operation,frame.Birth,source.Nonce,Digest(source.Image),source.NativeOutput.Native,source.NativeOutput.Offset,out var plan)||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(source.Sender,out var owner)||owner.StorageKey!=source.Owner)return false;
                var tools=new NativeTools{Source=source,Plan=plan,Record=bytes,Memory=held,OwnerBefore=ReadNativeOutputMetadata(path+".ownerBefore",16*1024*1024),
                    OwnerRoot=Path.GetFullPath(GameIO.GetPlayerDataDir()),OwnerName=owner.CanonicalId};
                tools.OwnerPath=Path.Combine(tools.OwnerRoot,owner.CanonicalId+"."+PlayerDataFile.EXT);
                if(File.Exists(path+".ownerAfter")){tools.OwnerAfter=ReadNativeOutputMetadata(path+".ownerAfter",16*1024*1024);tools.OwnerSaved=true;}
                else if(source.Sender.latestPlayerData!=null&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(source.Sender.latestPlayerData).SequenceEqual(tools.OwnerBefore))
                    tools.Previous=new WeakReference<PlayerDataFile>(source.Sender.latestPlayerData);
                source.Tools=tools;held=null;TrimNativeToolsRetention(tools);
            }
            if(source.Tools.OwnerSaved&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(source.Tools.Record))))
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(source.Artifact.OriginalPath,source.Image,source.Tools.Plan.After,source.Tools.Plan.AfterOffset,out var history,out _))return false;
                    using(history){if(!history.PlanImage.SequenceEqual(source.Tools.Record))return false;source.Tools.Saved=true;}
                }
            }
            if(source.Tools.OwnerSaved&&RebirthProductiveEarnedSavedNativeSource.TryObserved(source.Station,out var loaded)&&loaded.ToolsRebased&&loaded.Terminal&&loaded.PendingNextWork&&
                loaded.Frame.Operation==source.Operation&&loaded.Original.SequenceEqual(source.Image)&&loaded.PrimaryCurrent()&&loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation)&&
                RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,source.Tools.Plan.After,source.Tools.Plan.AfterOffset))
            {
                source.Artifact.LoadedSource=loaded;source.NativeOutputAccepted=true;source.Tools.Accepted=true;source.Tools.Saved=true;
                ObserveAcceptedNativeTools(source);
            }
            return true;
        }
        finally{held?.Dispose();}
    }
}
