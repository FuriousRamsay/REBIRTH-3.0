using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Typed actual MP015 native Add invocation source. It supplies observation
// lineage only, never completion/payment/setter permission or cold reentry.
internal static partial class RebirthWorkstationCompletionPipeline
{
    private static readonly System.Collections.Generic.Dictionary<Guid,NativeAddOriginal> nativeSources=new System.Collections.Generic.Dictionary<Guid,NativeAddOriginal>();
    internal static bool TryFindRetainedNativeAddOriginal(Guid operation,World world,TileEntityWorkstation station,out NativeAddOriginal source)
    {source=null;return nativeSources.TryGetValue(operation,out source)&&ReferenceEquals(source.World,world)&&ReferenceEquals(source.Station,station)&&source.SourceCurrentWithoutRow();}
    internal sealed partial class NativeAddOriginal
    {
        internal readonly Guid Operation;internal readonly RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal readonly Recipe Recipe;internal readonly World World;internal readonly TileEntityWorkstation Station;
        private readonly Frame frame;private readonly string save,path;private XElement image,before;
        private IDisposable allocation,sourceRetention;private int references=1;private bool frameReleased;
        private string authorityBeforePath;private byte[] authorityBeforeSeal,authorityAccountImage;
        internal bool AuthoritySavedBeforeCurrent()=>!World.IsRemote()&&SourceCurrentWithoutRow()&&authorityBeforeSeal!=null&&
            (string)image.Element("authoritySavedBefore")?.Attribute("path")==authorityBeforePath&&
            (string)image.Element("authoritySavedBefore")?.Attribute("digest")==RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(authorityBeforeSeal))&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(authorityBeforePath,authorityBeforeSeal);
        internal byte[] CopyAuthorityAccountImage()=>AuthoritySavedBeforeCurrent()?(byte[])authorityAccountImage.Clone():null;
        internal string AuthoritySourceHash=>image==null?null:RebirthWorkstationCompletionOriginal.Hash(image.ToString(SaveOptions.DisableFormatting));
        internal bool IsAuthorityCard(XUiC_RecipeStack card)=>SourceCurrentWithoutRow()&&ReferenceEquals(frame.Observation.Card,card);
        internal bool IsAuthorityWindow(XUiC_WorkstationWindowGroup window)=>SourceCurrentWithoutRow()&&ReferenceEquals(frame.Observation.Window,window);
        private bool cacheRestored;
        internal XElement CopyCachedOriginal()=>SourceCurrentWithoutRow()?new XElement(image):null;
        internal bool RemoteTerminalSaved {get;private set;}
        private NativeAddOriginal(Frame frame,string path,XElement image,IDisposable allocation,IDisposable sourceRetention)
        {
            this.frame=frame;this.path=path;this.image=image;this.allocation=allocation;this.sourceRetention=sourceRetention;
            Operation=frame.Observation.Operation;EarlierOwner=frame.EarlierOwner;Recipe=frame.Observation.Recipe;
            World=frame.Observation.World;Station=frame.Observation.Station;save=frame.Observation.SaveDirectory;
            before=new XElement(image.Element("before").Elements().Single());
        }
        internal XElement Before=>before==null?null:new XElement(before);
        internal Guid RemoteNativeNonce=>frame.RemoteNativeNonce;
        internal string RemoteNativeSourceHash=>frame.RemoteNativeSourceHash;
        internal long MeasureBefore()=>before==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before);
        internal bool SourceCurrentWithoutRow()
        {
            try{return references>0&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
                ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&
                EarlierOwner.SourceCurrent()&&ReferenceEquals(EarlierOwner.World,World)&&ReferenceEquals(EarlierOwner.Station,Station)&&
                XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(path));}
            catch{return false;}
        }
        internal bool CurrentAtNativeAdd(TileEntityWorkstation station,int owner)=>SourceCurrentWithoutRow()&&
            !cacheRestored&&ReferenceEquals(current,frame)&&ReferenceEquals(frame.NativeOriginal,this)&&frame.Prepared&&frame.Attempted&&frame.Saved&&
            ReferenceEquals(station,Station)&&owner==EarlierOwner.StartingEntityId&&
            CorroborateQueuedOwner(World,EarlierOwner)&&
            RebirthRecipeOutputIntegration.IsActualOwningUpdate(frame.Observation.Card)&&frame.Observation.Current()&&frame.Observation.RecipeMatches();
        internal bool MatchesNativeAdd(TileEntityWorkstation station,int owner,ItemValue item,string name,string scrapped,int experience,int count)=>
            CurrentAtNativeAdd(station,owner)&&ReferenceEquals(item,frame.Observation.Card.outputItemValue)&&item!=null&&
            item.ItemClass!=null&&item.type==Recipe.itemValueType&&name==Recipe.GetName()&&RebirthRecipeOutputIntegration.MatchesNativeOutputScrappedName(Recipe,scrapped)&&
            experience==Recipe.craftExpGain&&count==Recipe.count&&
            (item.ItemClass?.HasQuality==true?item.Quality==frame.Observation.OutputQuality:item.Quality==0);
        internal bool MatchesNativeAddArguments(ItemValue item,string name,string scrapped,int experience,int count)=>
            MatchesNativeAdd(Station,EarlierOwner.StartingEntityId,item,name,scrapped,experience,count);
        internal bool IsKnownCachedCard(XUiC_RecipeStack card)=>SourceCurrentWithoutRow()&&ReferenceEquals(frame.NativeOriginal,this)&&
            ReferenceEquals(frame.Observation.Card,card)&&frame.Observation.Operation==Operation&&frame.Observation.FullBoundary&&
            !frame.Observation.Continued&&!frame.Observation.Accepted&&!frame.NativeAccepted&&frame.ConfirmedOriginal==null;
        internal IDisposable RetainForCompletion()
        {if(!SourceCurrentWithoutRow())return null;references++;return new Retention(this);}
        internal bool TryCopyConfirmedDelivery(out XElement delivery)
        {
            delivery=null;if(!SourceCurrentWithoutRow()||frame.ConfirmedOriginal?.IsConfirmed!=true||
                frame.ConfirmedOriginal.Operation!=Operation.ToString("N"))return false;
            delivery=new XElement(frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single());return true;
        }
        internal bool SaveOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.DeferredWitness observed)
        {
            if(!ReferenceEquals(observed.NativeOriginal,this)||!observed.ObservedSavedEvent()||observed.Delivery==null||observed.Receipt==null)return false;
            string digest;using(var hash=System.Security.Cryptography.SHA256.Create())digest=Convert.ToBase64String(hash.ComputeHash(observed.Final));
            var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",frame.Observation.WorldGuid),
                new XElement("before",new XElement(before)),new XElement("nativeOwnerTerminal",new XAttribute("recipient",observed.Owner),
                    new XAttribute("creation",observed.Creation),new XAttribute("entity",observed.EntityId),new XAttribute("playerFile",observed.FinalPath),
                    new XAttribute("nativeFileDigest",digest),new XElement("delivery",new XElement(observed.Delivery)),
                    new XElement("ownerBefore",Convert.ToBase64String(observed.Before)),new XElement("ownerAfter",Convert.ToBase64String(observed.After)),
                    new XElement("bagBefore",Convert.ToBase64String(observed.BagBefore)),new XElement("bagAfter",Convert.ToBase64String(observed.BagAfter)),
                    new XElement("beltBefore",Convert.ToBase64String(observed.BeltBefore)),new XElement("beltAfter",Convert.ToBase64String(observed.BeltAfter)),
                    new XElement("equipmentBefore",Convert.ToBase64String(observed.EquipmentBefore)),new XElement("equipmentAfter",Convert.ToBase64String(observed.EquipmentAfter)),
                    new XElement("beforeComplete",Convert.ToBase64String(observed.Receipt.BeforeComplete)),
                    new XElement("afterComplete",Convert.ToBase64String(observed.Receipt.AfterComplete)),
                    new XElement("aggregate",Convert.ToBase64String(observed.Receipt.AggregateImage)),
                    new XElement("members",observed.Receipt.AllContributions.Select(member=>new XElement("member",new XAttribute("operation",member.Operation.ToString("N")),
                        new XAttribute("kind",member.HostLocal!=null?(string)member.HostLocal.Member.Attribute("kind"):member.Original!=null?"uio":"native"),new XAttribute("source",member.Source.OriginalOwnerKey),new XAttribute("creation",member.Source.Creation),
                        new XAttribute("contribution",member.ContributionCount))))));
            if(!RebirthWorkstationCompletionPublication.Write(path+".terminal.xml",terminal))return false;
            observed.OwnerTerminalSaved=true;return true;
        }
        internal bool SaveHostOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication terminal)
        {
            if(terminal?.IncludesNativeOriginal(this)!=true||!SourceCurrentWithoutRow()||
                !TryCopyConfirmedDelivery(out var delivery))return false;
            var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",frame.Observation.WorldGuid),new XElement("before",new XElement(before)),
                new XElement("nativeOwnerRemoteTerminal",terminal.CopyGroupEvidence(),new XElement("delivery",delivery)));
            if(!RebirthWorkstationCompletionPublication.Write(path+".remote-terminal.xml",saved))return false;
            RemoteTerminalSaved=true;return true;
        }
        internal bool SaveLocalOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.DeferredWitness observed,
            RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication terminal)
        {
            if(terminal?.Current()!=true||!terminal.Scope.Members.Contains(observed)||!ReferenceEquals(observed.NativeOriginal,this)||
                !observed.ObservedSavedEvent()||!TryCopyConfirmedDelivery(out var delivery))return false;
            long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before)+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(delivery)+65536;
            using(var workspace=terminal.Scope.EnterPublication(size))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",frame.Observation.WorldGuid),new XElement("before",new XElement(before)),
                    new XElement("localOwnerSaved",new XAttribute("group",terminal.Operation.ToString("N")),new XAttribute("proof",terminal.Path),
                        new XElement("delivery",delivery)));
                if(!RebirthWorkstationCompletionPublication.Write(path+".local-terminal.xml",record))return false;
                if(!observed.RetainDelivery(new XElement(delivery)))return false;
                return SaveOwnerTerminal(observed);
            }
        }
        private sealed class Retention:IDisposable
        {private NativeAddOriginal original;internal Retention(NativeAddOriginal original){this.original=original;}
         public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref original,null);value?.Release();}}
        private void Release()
        {if(--references!=0)return;nativeSources.Remove(Operation);ForgetRemoteNativeReceipt(this);image=before=null;authorityBeforeSeal=authorityAccountImage=null;sourceRetention?.Dispose();sourceRetention=null;allocation?.Dispose();allocation=null;}
        internal void ReleaseFrame(){if(frameReleased)return;frameReleased=true;Release();}
        internal static NativeAddOriginal Capture(Frame frame)
        {
            if(!ReferenceEquals(current,frame)||!frame.Prepared||!frame.Attempted||!frame.Saved||
                !RebirthRecipeOutputIntegration.IsActualOwningUpdate(frame.Observation.Card)||!frame.Observation.Current()||!frame.Observation.RecipeMatches()||
                frame.EarlierOwner?.Current()!=true||!frame.EarlierOwner.SourcePublished()||
                !CorroborateQueuedOwner(frame.Observation.World,frame.EarlierOwner))return null;
            var source=frame.EarlierOwner;var admission=ActiveBefore(frame.Intent);
            string localPath=null;byte[] localSeal=null,localAccount=null;
            if(!frame.Observation.World.IsRemote()&&(frame.NativeBefore?.Current()!=true||frame.LocalBefore?.TryCopyActualSaved(out localPath,out localSeal,out localAccount)!=true))return null;
            // The actual admitted attempted primary must already be durable.
            if(!XNode.DeepEquals(frame.Intent,RebirthWorkstationCompletionPublication.ReadPrimary(frame.Path)))return null;
            var frozen=new XElement("workstationCompletionIntent",new XAttribute("operation",frame.Observation.Operation.ToString("N")),
                new XAttribute("world",frame.Observation.WorldGuid),new XAttribute("nativeNonce",frame.RemoteNativeNonce==Guid.Empty?"":frame.RemoteNativeNonce.ToString("N")),
                new XAttribute("nativeSource",frame.RemoteNativeSourceHash??""),new XElement("before",new XElement(admission)),
                new XElement("originalQueueOwner",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("birth",source.Birth.ToString("N")),
                    new XAttribute("owner",source.OriginalOwnerKey),new XAttribute("creation",source.Creation),new XAttribute("entity",source.StartingEntityId),
                    new XElement("recipeBinding",source.RecipeBinding)));
            if(localSeal!=null)frozen.Add(new XElement("authoritySavedBefore",new XAttribute("path",localPath),
                new XAttribute("digest",RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(localSeal)))));
            string text=frozen.ToString(SaveOptions.DisableFormatting);int bytes=Encoding.UTF8.GetByteCount(text);
            if(bytes>12*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*bytes+4L*((localSeal?.Length??0)+(localAccount?.Length??0))+65536),out var allocation))return null;
            IDisposable retained=null;
            try
            {
                retained=source.RetainForCompletion();if(retained==null)return null;
                string path=Path.Combine(Path.GetDirectoryName(frame.Path),"nativeOwners",frame.Observation.Operation.ToString("N")+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,frozen))return null;
                if(nativeSources.ContainsKey(frame.Observation.Operation))return null;
                var result=new NativeAddOriginal(frame,path,frozen,allocation,retained){authorityBeforePath=localPath,authorityBeforeSeal=localSeal,authorityAccountImage=localAccount};nativeSources.Add(result.Operation,result);allocation=null;retained=null;return result;
            }
            finally{retained?.Dispose();allocation?.Dispose();}
        }
    }
    internal static bool TryGetNativeAddOriginal(TileEntityWorkstation station,int owner,out NativeAddOriginal original)
    {
        original=null;var frame=current;
        if(frame==null||frame.Closed||!frame.Prepared||!frame.Attempted||!frame.Saved||
            !ReferenceEquals(frame.Observation.Station,station)||frame.Observation.StartingOwnerEntityId!=owner||
            !RebirthRecipeOutputIntegration.IsActualOwningUpdate(frame.Observation.Card)||!frame.Observation.Current()||!frame.Observation.RecipeMatches())return false;
        frame.NativeOriginal??=NativeAddOriginal.Capture(frame);
        if(frame.NativeOriginal?.CurrentAtNativeAdd(station,owner)!=true)return false;original=frame.NativeOriginal;return true;
    }
}
