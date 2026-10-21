using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class RemoteOutputColdScan:IDisposable
    {
        internal IEnumerator<string> Files;internal bool Complete;internal readonly List<RemoteOutputColdClient> Candidates=new List<RemoteOutputColdClient>();
        internal readonly List<RemoteOutputColdHold> Holds=new List<RemoteOutputColdHold>();
        public void Dispose(){Files?.Dispose();Files=null;foreach(var candidate in Candidates)candidate.Dispose();Candidates.Clear();Holds.Clear();}
    }
    private sealed class RemoteOutputColdHold{internal Guid Operation;internal Vector3i Position;internal int Slot,Count;internal string Recipe;}
    private static bool MatchesRemoteOutputColdHold(RemoteOutputColdHold hold,XUiC_RecipeStack card)
    {
        var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();
        return window?.WorkstationData?.TileEntity?.ToWorldPos()==hold.Position&&card.GetRecipeCount()==hold.Count&&
            Array.FindIndex(window.craftingQueue.GetRecipesToCraft(),value=>ReferenceEquals(value,card))==hold.Slot&&
            UiOriginalHash(Encoding.UTF8.GetBytes(RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(card)})))==hold.Recipe;
    }
    internal sealed partial class RemoteOutputColdClient:IDisposable
    {
        private RemoteOutputColdClient(){}
        private RemoteOutputOwnerLoad loaded;private IDisposable memory;private byte[] originalBytes,acceptedBytes,effectBytes,ownerSavedBytes,requestBytes;
        private string originalPath,acceptedPath,effectPath,ownerSavedPath,requestPath;private XElement original,remote,accepted,effect,request;
        private string deferredRequestPath;private byte[] deferredRequestBytes;private XElement deferredContribution;
        internal Guid Operation,LocalOperation;internal int Prefix;internal string OriginalHash,FramePath;internal bool Attached;
        internal XUiC_RecipeStack Card;private XUiC_WorkstationWindowGroup window;private TileEntityWorkstation station;private long session;
        internal RebirthProductiveEarnedUiQueueOwner.Witness OwnerSource;
        internal EntityPlayerLocal Actor=>loaded?.Actor;
        internal bool EpisodeCurrent=>loaded?.EpisodeCurrent()==true;
        internal XElement Original=>new XElement(original);internal byte[] OriginalBytes=>(byte[])originalBytes.Clone();
        internal XElement Source=>new XElement(remote);internal XElement Request=>new XElement(request);
        internal XElement Effect=>request.Element("sameOwnerEffect")?.Elements().SingleOrDefault() is XElement value?new XElement(value):null;
        internal XElement DeferredContribution=>deferredContribution is XElement retained?new XElement(retained):
            request?.Element("deferredNativeContribution") is XElement value?new XElement(value):null;
        internal bool IsDeferred=>(deferredContribution!=null||request?.Element("deferredNativeContribution")!=null)&&request.Element("sameOwnerEffect")==null;
        internal bool CallbackReturned,FinalCallbackSaved,QueueTerminalSaved,QueueOnlyAuthorized;
        internal bool ExternalCached;
        internal bool FilesCurrent()=>MatchesRemotePrefixFile(originalPath,originalBytes)&&MatchesRemotePrefixFile(acceptedPath,acceptedBytes)&&
            MatchesRemotePrefixFile(effectPath,effectBytes)&&MatchesRemotePrefixFile(ownerSavedPath,ownerSavedBytes)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(requestPath,requestBytes)&&
            (deferredRequestPath==null||RebirthWorkstationCompletionPublication.MatchesPublishedBytes(deferredRequestPath,deferredRequestBytes))&&
            (RetainedCached==null||RetainedCached.Current())&&(!FinalCallbackSaved||FinalFilesCurrent());
        internal bool BeforeBindingCurrent()=>!Attached&&loaded?.Current()==true&&FilesCurrent();
        internal bool BindingCurrent()=>Attached&&loaded?.EpisodeCurrent()==true&&FilesCurrent()&&Card!=null&&ReferenceEquals(Card.xui.playerUI.entityPlayer,Actor)&&
            ReferenceEquals(Card.GetParentByType<XUiC_WorkstationWindowGroup>(),window)&&ReferenceEquals(window.WorkstationData?.TileEntity,station)&&
            ReferenceEquals(loaded.World.GetTileEntity(station.ToWorldPos()),station)&&OwnerSource?.SourceCurrent()==true&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var current)&&current==session;
        internal bool MatchesCard(XUiC_RecipeStack card)
        {
            var view=card?.GetParentByType<XUiC_WorkstationWindowGroup>();var tile=view?.WorkstationData?.TileEntity;var before=remote.Element("activeBefore")?.Elements().SingleOrDefault();
            return before!=null&&ReferenceEquals(card.xui?.playerUI?.entityPlayer,Actor)&&tile!=null&&tile.ToWorldPos()==new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"))&&
                Array.FindIndex(view.craftingQueue.GetRecipesToCraft(),value=>ReferenceEquals(value,card))==(int)before.Attribute("slot")&&
                card.GetRecipeCount()==(int)original.Attribute("beforeCount")&&RebirthRemoteResourcePlacedStationQueue.Encode(new[]{ActualCardRow(card)})==original.Element("recipe").Value;
        }
        internal bool BindNativeCard(XUiC_RecipeStack card,RebirthProductiveEarnedUiQueueOwner.Witness owner)
        {
            if(!BeforeBindingCurrent()||!MatchesCard(card)||owner?.SourcePublished()!=true||owner.NativeHydration==null||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(card.GetParentByType<XUiC_WorkstationWindowGroup>(),out session))return false;
            Card=card;window=card.GetParentByType<XUiC_WorkstationWindowGroup>();station=window.WorkstationData.TileEntity;OwnerSource=owner;Attached=true;return BindingCurrent();
        }
        internal void UndoUnenteredBinding()
        {if(Card!=null&&!HasPendingUiOutput(Card)){Attached=false;Card=null;window=null;station=null;OwnerSource=null;session=0;}}
        internal bool TryPrimary(out ItemValue primary)=>RebirthNativeItemCodec.TryDecode(effect.Element("primaryAfterEffects").Value,out primary);
        internal ItemStack[] Outputs()=>RebirthCamperInventorySnapshot.Restore(PersonalColdImage(original,"outputs")).Skip(1).ToArray();
        internal AdditionalOutputPrefix AttachOriginal(out bool finalAttached)
        {
            finalAttached=false;
            IDisposable memory=null;UiOutputOriginal bound=null;
            try
            {
                if(!BindingCurrent()||ui!=null||HasPendingUiOutput(Card)||!TryPrimary(out var primary)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*UiOriginalMaximum+65536,out memory))return null;
                var scope=new UiScope{Card=Card,Grid=null,Outputs=Outputs(),Cached=(bool)original.Attribute("cached")};
                bound=new UiOutputOriginal(scope,this,primary){Memory=memory};memory=null;scope.Original=bound;
                var binding=new XElement("recipeOutputRemoteColdBinding",new XAttribute("operation",LocalOperation.ToString("N")),new XAttribute("original",OriginalHash),
                    new XAttribute("nativeOperation",Operation.ToString("N")),new XAttribute("nonce",(string)remote.Attribute("nonce")),new XAttribute("source",(string)remote.Attribute("source")),
                    new XAttribute("generation",(string)request.Attribute("generation")),new XAttribute("prefix",Prefix),new XAttribute("request",RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request)));
                string stamp=UiOriginalHash(UiOriginalBytes(binding)).Replace('/','_').Replace('+','-');
                if(!PublishPersonalColdBinding(originalPath+".remote-cold-"+stamp,UiOriginalBytes(binding))||!BindingCurrent())return null;
                var result=FinalCallbackSaved?null:AdditionalOutputPrefix.FromRemoteCold(bound,this,acceptedBytes,request.Element("nativeOutputPrefix"));
                if(result==null&&!FinalCallbackSaved)return null;
                Card.outputItemValue=primary;Card.isInventoryFull=ExternalCached&&!FinalCallbackSaved;RetainUiOutput(bound);
                if(CallbackReturned&&pendingUiOutputs.TryGetValue(Card,out var pending))pending.NativeCallbackEntered=pending.NativeContinuationEntered=pending.Returned=true;
                finalAttached=FinalCallbackSaved;
                bound=null;return result;
            }
            finally{memory?.Dispose();if(bound!=null&&!RetainsUiOutput(bound))bound.Dispose();}
        }
        public void Dispose(){RetainedCached?.Dispose();RetainedCached=null;memory?.Dispose();memory=null;loaded=null;Card=null;OwnerSource=null;originalBytes=acceptedBytes=effectBytes=ownerSavedBytes=requestBytes=deferredRequestBytes=null;original=remote=accepted=effect=request=deferredContribution=null;deferredRequestPath=null;}
        internal static RemoteOutputColdClient Read(object nativeOwner,string path,bool finalCallback=false)
        {
            if(nativeOwner is not RemoteOutputOwnerLoad loaded)return null;
            RemoteOutputColdClient value=null;IDisposable memory=null;
            try
            {
                if(!loaded.Current()||!ReadPersonalOutputSidecar(path,out var bytes,out var image)||
                    !ExactPersonalOutputFields(image,"recipeOutputOriginal","version,operation,world,owner,creation,deaths,entity,beforeCount,startingOwner,cached,nativeVersion,sink,callbackFence",
                        "recipe,primaryBeforeEffects,outputs,bagBefore,beltBefore,equipmentBefore,remoteOutputSource")||
                    (int?)image.Attribute("version")!=1||(string)image.Attribute("sink")!="Inventory"||!bool.TryParse((string)image.Attribute("cached"),out bool cached)||
                    (int?)image.Attribute("callbackFence")!=1||(string)image.Attribute("world")!=GamePrefs.GetString(EnumGamePrefs.GameGuidClient)||
                    (string)image.Attribute("owner")!=loaded.Owner||(string)image.Attribute("creation")!=loaded.Creation||(int?)image.Attribute("deaths")!=loaded.Deaths||
                    !Guid.TryParseExact((string)image.Attribute("operation"),"N",out var local)||Path.GetFileName(path)!=local.ToString("N")+".xml")return null;
                var remote=image.Element("remoteOutputSource");
                if(!ExactPersonalOutputFields(remote,"remoteOutputSource","version,operation,nonce,source,generation,cached,intentPath","activeBefore")||
                    (int?)remote.Attribute("version")!=1||!bool.TryParse((string)remote.Attribute("cached"),out bool externalCached)||
                    !Guid.TryParseExact((string)remote.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return null;
                string oldFramePath=(string)remote.Attribute("intentPath");
                if(File.Exists(oldFramePath+".same-owner-ack")&&!File.Exists(oldFramePath+".same-owner-ack.tmp"))
                {
                    var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(oldFramePath+".same-owner-ack");var ack=terminal?.Element("nativeSameOwnerAck");
                    if((string)terminal?.Attribute("operation")==operation.ToString("N")&&(string)terminal?.Attribute("world")==((string)image.Attribute("world"))&&
                        (string)ack?.Attribute("nonce")==((string)remote.Attribute("nonce"))&&(string)ack?.Attribute("source")==((string)remote.Attribute("source"))&&
                        RebirthWorkstationCompletionOriginal.TryRead(ack?.Element("workstationCompletionOriginal"),out var confirmed)&&confirmed.IsConfirmed&&
                        confirmed.Operation==operation.ToString("N")&&confirmed.ViewerKey==loaded.Owner&&confirmed.ViewerCreation==loaded.Creation)return null;
                }
                var heldBefore=remote.Element("activeBefore")?.Elements().SingleOrDefault();
                if(heldBefore==null)return null;
                if(loaded.Scan!=null&&!loaded.Scan.Holds.Any(value=>value.Operation==local))loaded.Scan.Holds.Add(new RemoteOutputColdHold{Operation=local,
                    Position=new Vector3i((int)heldBefore.Attribute("x"),(int)heldBefore.Attribute("y"),(int)heldBefore.Attribute("z")),Slot=(int)heldBefore.Attribute("slot"),
                    Count=(int)image.Attribute("beforeCount"),Recipe=UiOriginalHash(Encoding.UTF8.GetBytes(image.Element("recipe").Value))});
                // An old external cached action must not become a new action
                // merely because its old volatile grant is absent. Its distinct
                // saved action/native-load adoption owns continuation authority.
                string hash=UiOriginalHash(bytes);
                if(!ReadPersonalOutputSidecar(path+".effect-observed",out var effectBytes,out var effect)||
                    !PersonalColdIdentity(effect,"recipeOutputEffectObserved","operation,nativeRan,original","primaryAfterEffects",local,hash)||(bool?)effect.Attribute("nativeRan")!=!cached)return null;
                int maximum=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(image,"outputs")).Length;if(maximum<2||maximum>65)return null;
                int count=0;byte[] acceptedBytes=null;XElement accepted=null;
                while(count<maximum&&ReadPersonalOutputSidecar(path+".output-"+count+".accepted",out var nextBytes,out var next)&&
                    PersonalColdIdentity(next,"recipeOutputAccepted","operation,index,attempt,original","bag,belt,equipment",local,hash)&&(int?)next.Attribute("index")==count)
                {accepted=next;acceptedBytes=nextBytes;count++;}
                if(count<1||!PersonalColdImage(accepted,"bag").SequenceEqual(loaded.BagFrame)||!PersonalColdImage(accepted,"belt").SequenceEqual(loaded.BeltFrame)||
                    !finalCallback&&!PersonalColdImage(accepted,"equipment").SequenceEqual(loaded.EquipmentFrame))return null;
                if(finalCallback)return ReadFinal(loaded,path,bytes,image,remote,local,operation,hash,count,maximum,acceptedBytes,accepted,effectBytes,effect);
                string ownerPath=path+".output-"+(count-1)+".owner-saved";
                if(!ReadPersonalOutputSidecar(ownerPath,out var ownerBytes,out var ownerSaved)||
                    !PersonalColdIdentity(ownerSaved,"recipeOutputOwnerPrefixSaved","operation,index,original,accepted,owner,creation,deaths,entity,remote,nativeOperation,nativeGeneration,nativeNonce,nativeSource,nativeDigest,nativeLength,requestDigest","",local,hash)||
                    (int?)ownerSaved.Attribute("index")!=count-1||(bool?)ownerSaved.Attribute("remote")!=true||
                    (string)ownerSaved.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||(string)ownerSaved.Attribute("nativeOperation")!=operation.ToString("N")||
                    (string)ownerSaved.Attribute("nativeNonce")!=(string)remote.Attribute("nonce")||(string)ownerSaved.Attribute("nativeSource")!=(string)remote.Attribute("source")||
                    (string)ownerSaved.Attribute("owner")!=loaded.Owner||(string)ownerSaved.Attribute("creation")!=loaded.Creation||(int?)ownerSaved.Attribute("deaths")!=loaded.Deaths)return null;
                string framePath=(string)remote.Attribute("intentPath"),generation=(string)ownerSaved.Attribute("nativeGeneration");
                string requestPath=framePath+".viewer."+generation+".prefix-"+count;
                var savedRequest=RebirthWorkstationCompletionPublication.ReadPrimary(requestPath);var request=savedRequest?.Element("before")?.Element(externalCached?"cachedViewer":"nativeViewer");
                string counterRecipe=(string)request?.Element("sameOwnerEffect")?.Elements().SingleOrDefault()?.Attribute("recipe");
                if(externalCached)
                {
                    if((string)request?.Attribute("action")!=(string)remote.Attribute("nonce")||request.Element("sameOwnerEffect")!=null||
                        request.Element("deferredNativeContribution")!=null||!RebirthRemoteResourcePlacedStationQueue.TryDecode(image.Element("recipe").Value,out var cachedRows)||
                        cachedRows.Length!=1||cachedRows[0]?.Recipe==null)return null;
                    counterRecipe=cachedRows[0].Recipe.GetName();
                }
                if(counterRecipe==null&&request?.Element("deferredNativeContribution") is XElement deferred)
                {
                    if(request.Element("sameOwnerEffect")!=null||!RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(
                        Convert.FromBase64String(deferred.Element("nativeAddArgument").Value),out var nativeAdd))return null;
                    counterRecipe=nativeAdd.RecipeName;
                }
                if(request==null||RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request)!=(string)ownerSaved.Attribute("requestDigest")||
                    string.IsNullOrEmpty(counterRecipe)||!MatchesRemoteColdRequestSource(remote,request,externalCached)||
                    (string)request.Attribute("generation")!=generation||
                    (string)request.Attribute("pdsDigest")!=(string)ownerSaved.Attribute("nativeDigest")||(int?)request.Attribute("pdsLength")!=(int?)ownerSaved.Attribute("nativeLength")||
                    !XNode.DeepEquals(request.Element("activeBefore"),remote.Element("activeBefore"))||
                    request.Element("nativeOutputPrefix") is not XElement prefix||(int?)prefix.Attribute("count")!=count||(string)prefix.Attribute("original")!=hash||
                    (string)prefix.Attribute("operation")!=local.ToString("N")||
                    !XNode.DeepEquals(RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(loaded.Actor,new[]{counterRecipe}),request.Element("nativeOwnerFrame")))return null;
                bool callback=ReadPersonalOutputSidecar(path+".native-continuation-returned",out _,out var returned)&&
                    PersonalColdIdentity(returned,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",local,hash)&&(int?)returned.Attribute("outputPrefix")==maximum;
                if(!callback&&!PersonalColdAbsent(path+".native-continuation-entered"))return null;
                if(callback&&count!=maximum)return null;
                // A local callback return is not a saved native owner quest /
                // cosmetic image. Only the unentered callback prefix is admitted
                // here; a completed callback needs its distinct final native PDS
                // source and cannot be laundered through an earlier prefix PDS.
                if(callback)return null;
                var requestBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(requestPath);
                if(requestBytes==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(bytes.Length+acceptedBytes.Length+effectBytes.Length+ownerBytes.Length+requestBytes.Length)+65536),out memory))return null;
                value=new RemoteOutputColdClient{loaded=loaded,memory=memory,originalBytes=bytes,acceptedBytes=acceptedBytes,effectBytes=effectBytes,ownerSavedBytes=ownerBytes,requestBytes=requestBytes,
                    originalPath=path,acceptedPath=path+".output-"+(count-1)+".accepted",effectPath=path+".effect-observed",ownerSavedPath=ownerPath,requestPath=requestPath,
                    original=image,remote=remote,accepted=accepted,effect=effect,request=request,Operation=operation,LocalOperation=local,Prefix=count,OriginalHash=hash,FramePath=framePath,CallbackReturned=callback,ExternalCached=externalCached};
                memory=null;return value;
            }
            catch{value?.Dispose();return null;}
            finally{memory?.Dispose();}
        }
    }
    internal static bool CanAdvanceRemoteOutputRecovery(XUiC_RecipeStack card)
    {try{return CanAdvanceRemoteOutputRecoveryCore(card);}catch{return false;}}
    private static bool CanAdvanceRemoteOutputRecoveryCore(XUiC_RecipeStack card)
    {
        var loaded=loadedRemoteOutputOwner;
        if(card?.xui?.playerUI?.entityPlayer?.world?.IsRemote()!=true||card.GetParentByType<XUiC_WorkstationWindowGroup>()==null||loaded==null||!loaded.EpisodeCurrent())return true;
        if(HasPendingUiOutput(card))return true;
        PumpRemoteOutputColdDiscovery();var scan=loaded.Scan;
        if(scan?.Complete!=true)return false;
        var matches=scan.Candidates.Where(value=>!value.Attached&&!value.QueueTerminalSaved&&value.MatchesCard(card)).ToArray();
        if(matches.Length==0)return !scan.Holds.Any(value=>MatchesRemoteOutputColdHold(value,card));if(matches.Length!=1)return false;
        // An old cache requires its actual reissued retained Action/ticket.
        // The native Add/direct restart dispatcher cannot supply that source.
        if(matches[0].ExternalCached)
        {
            if(RebirthProductiveEarnedUiDeferredTransport.TryGetRemoteCachedPrimaryGrant(card.GetParentByType<XUiC_WorkstationWindowGroup>(),out var grant))
                if(matches[0].TryBindRetainedCachedGrant(grant))
                {
                    RebirthWorkstationCompletionPipeline.RequestRetainedCachedOutputRestart(matches[0]);
                    if(matches[0].RetainedCached.HostPrefixRestored)return RebirthWorkstationCompletionPipeline.TryAttachRetainedCachedOutput(matches[0]);
                }
            return false;
        }
        return RebirthWorkstationCompletionPipeline.TryResumeRemoteOutputCold(card,matches[0]);
    }
    private static void PumpRemoteOutputColdDiscovery()
    {
        var loaded=loadedRemoteOutputOwner;if(loaded?.EpisodeCurrent()!=true)return;
        var scan=loaded.Scan;
        if(scan==null)
        {
            string root=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","recipe-outputs","ui");
            loaded.Scan=scan=new RemoteOutputColdScan();
            if(Directory.Exists(root))scan.Files=Directory.EnumerateFiles(root,"*.xml",SearchOption.TopDirectoryOnly).GetEnumerator();else scan.Complete=true;
        }
        if(!scan.Complete)
        {
            for(int step=0;step<4;step++)
            {
                if(scan.Files==null||!scan.Files.MoveNext()){scan.Files?.Dispose();scan.Files=null;scan.Complete=true;break;}
                var candidate=RemoteOutputColdClient.Read(loaded,scan.Files.Current)??RemoteOutputColdClient.Read(loaded,scan.Files.Current,true);if(candidate!=null)scan.Candidates.Add(candidate);
            }
        }
        foreach(var candidate in scan.Candidates.Where(value=>value.FinalCallbackSaved&&!value.Attached&&!value.QueueTerminalSaved).Take(4))
            RebirthWorkstationCompletionPipeline.QueryRemoteOutputFinalTerminal(candidate);
    }
    private static bool ResumeRemoteOutputColdObservation(OwningObservation observed)
    {
        var authority=observed.RemoteOutputRestart;
        if(owningObservation!=null||authority?.BindingCurrent()!=true||!ReferenceEquals(authority.Card,observed.Card)||observed.Accepted||observed.Updated||observed.Taken||
            observed.RanUpdate||observed.Continued||!observed.Current()||!observed.RecipeMatches()||OwningNativeVersion(observed.Card)!=observed.NativeVersionBefore||
            !pendingUiOutputs.TryGetValue(observed.Card,out var pending)||!ReferenceEquals(pending.Source?.RemoteCold,authority)||
            !RebirthWorkstationCompletionPipeline.CanResumeAdditionalOutputPrefix(observed.Card,pending.Source.OutputPrefix))return false;
        var before=ui;ui=pending.Source.Scope;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!pending.Source.Current()||!pending.Source.AcceptedOwnerCurrent()||
                    RebirthRemoteResourcePlacedStationQueue.Encode(observed.Window.craftingQueue.GetRecipesToCraft().Select(ActualCardRow).ToArray())!=observed.UiQueueFull||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(observed.Station,observed.NativeBefore,observed.NativeElapsedOffset))return false;
                observed.Previous=null;owningObservation=observed;return true;
            }
        }
        finally{ui=before;}
    }
}
