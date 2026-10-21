using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        // The immediate native giveExp image and the later physical-completion
        // PDS are distinct observations: CraftedItem legitimately runs later.
        private static bool ProjectionCounters(XElement frame,string recipe,out int total,out float count)
        {
            total=0;count=0;
            bool equipment=(int?)frame?.Attribute("version")==2;
            if(frame?.Name!="nativeOwnerFrame"||(!equipment&&(int?)frame.Attribute("version")!=1)||frame.Elements().Count()!=(equipment?4:3)||
                frame.Elements("equipment").Count()!=(equipment?1:0)||
                frame.Elements("bag").Count()!=1||frame.Elements("belt").Count()!=1||frame.Elements("recipes").Count()!=1)return false;
            var rows=frame.Element("recipes").Elements().ToArray();
            if(rows.Length!=1||rows[0].Name!="recipe"||(string)rows[0].Attribute("name")!=recipe||rows[0].Elements().Count()!=1||
                rows[0].Element("projection")==null)return false;
            var image=Convert.FromBase64String(rows[0].Element("projection").Value);
            if(image.Length<8||image.Length>16*1024*1024||Convert.ToBase64String(image)!=rows[0].Element("projection").Value)return false;
            // Live/Projection append native Int32 totalItemsCrafted + Single
            // craftCount after the full progression/quest/challenge streams.
            total=BitConverter.ToInt32(image,image.Length-8);count=BitConverter.ToSingle(image,image.Length-4);
            return !float.IsNaN(count)&&!float.IsInfinity(count)&&count>=0&&count<int.MaxValue-1&&total>=0&&total<int.MaxValue;
        }
        private static bool ValidateDirectEffect(ViewerSource source,XElement effect,XElement delivery,bool custody=false)
        {
            var original=source.Original;
            if(effect?.Name!="nativeViewerEffect"||(int?)effect.Attribute("version")!=1||(bool?)effect.Attribute("nativeOriginalReturned")!=true||
                (string)effect.Attribute("operation")!=original.Operation.ToString("N")||(string)effect.Attribute("nonce")!=original.Nonce.ToString("N")||
                (string)effect.Attribute("source")!=original.SourceHash||(string)effect.Attribute("world")!=original.WorldGuid||
                (string)effect.Attribute("viewer")!=original.ViewerKey||(string)effect.Attribute("creation")!=original.ViewerCreation||
                (int?)effect.Attribute("deaths")!=original.ViewerDeaths||(int?)effect.Attribute("startingOwner")!=(original.HasRemoteOutputFinalQueuePrefix?original.EarlierOwner.StartingEntityId:original.Viewer.entityId)||
                !original.HasRemoteOutputFinalQueuePrefix&&original.EarlierOwner.StartingEntityId!=original.Viewer.entityId||original.EarlierOwner.OriginalOwnerKey!=original.ViewerKey||
                original.EarlierOwner.Creation!=original.ViewerCreation||original.EarlierOwner.Deaths!=original.ViewerDeaths||
                (int?)effect.Attribute("slot")!=original.Index||(int?)effect.Attribute("beforeCount")!=original.BeforeCount||
                (string)effect.Attribute("recipe")!=original.Recipe.GetName()||(int?)effect.Attribute("experience")!=original.Recipe.craftExpGain||
                (int?)effect.Attribute("count")!=original.Recipe.count||
                !initialBefore.TryGetValue(original,out var initial)||!(custody?initial.SavedCustodyCurrent():initial.Current())||
                !XNode.DeepEquals(effect.Element("initialBefore")?.Elements().SingleOrDefault(),original.Before)||
                effect.Element("initialViewerBefore")?.Elements().SingleOrDefault() is not XElement beforeRequest||
                RequestDigest(beforeRequest)!=initial.Digest||
                !XNode.DeepEquals(effect.Element("ownerBefore")?.Elements().SingleOrDefault(),initial.Frame)||
                (string)effect.Element("recipeNative")!=(string)original.Before.Element("recipeNative")||
                (string)effect.Element("recipeFull")!=(string)original.Before.Element("recipeFull"))return false;
            var before=effect.Element("ownerBefore")?.Elements().SingleOrDefault();
            var after=effect.Element("ownerAfterGiveExp")?.Elements().SingleOrDefault();
            if(!ProjectionCounters(before,original.Recipe.GetName(),out int total,out float count)||
                !ProjectionCounters(after,original.Recipe.GetName(),out int nextTotal,out float nextCount)||
                nextTotal!=total+1||nextCount!=(int)count+1||
                (string)before.Element("bag")!=(string)after.Element("bag")||(string)before.Element("belt")!=(string)after.Element("belt")||
                (string)before.Element("equipment")!=(string)after.Element("equipment"))return false;
            var primary=RebirthProductiveEarnedUiRemote.ReadGrid(effect,"primaryNative");
            return primary.Length==1&&primary[0]?.itemValue!=null&&primary[0].count==original.Recipe.count&&
                primary[0].itemValue.type==original.Recipe.itemValueType&&
                NativePhysicalSame(primary,RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"primaryNative"));
        }
        private static bool ValidateCachedEffectHeader(ViewerSource source,XElement effect,XElement full,XElement owner,bool custody=false)
        {
            if(full?.Name!="nativeOwningFullBoundary"||(string)full.Attribute("operation")!=source.Original.Operation.ToString("N")||
                (long?)full.Attribute("nativeVersion") is not long version||version<1||
                (bool?)full.Attribute("freshCallback")!=true&&(bool?)full.Attribute("cachedCallback")!=true||
                !source.Original.IsSavedFullBoundaryBefore(full)||
                (string)full.Element("recipeFull")!=(string)source.Active.Element("recipeFull")||
                (string)full.Element("nativeBefore")!=(string)source.Active.Element("nativeBefore")||
                (string)owner.Element("bag")!=(string)full.Element("bagBefore")||(string)owner.Element("belt")!=(string)full.Element("beltBefore")||
                !EquipmentFrameMatches(owner,full,"equipmentBefore")||
                !ValidateDirectEffect(source,effect,full,custody)||
                !ProjectionCounters(effect.Element("ownerAfterGiveExp")?.Elements().SingleOrDefault(),source.Original.Recipe.GetName(),out int total,out float count)||
                !ProjectionCounters(owner,source.Original.Recipe.GetName(),out int finalTotal,out float finalCount)||total!=finalTotal||count!=finalCount)return false;
            var native=CachedSpaceBytes(full,"nativeAfter");int offset=(int)full.Attribute("nativeElapsedOffset");
            var before=CachedSpaceBytes(source.Active,"nativeBefore");int beforeOffset=(int)source.Active.Attribute("nativeElapsedOffset");
            return RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,native,offset);
        }
        internal static bool TryValidateSameOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
            byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,
            out RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission proof)
            =>RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission.TryIssue(source,contribution,prior,priorOffset,proposed,proposedOffset,phase,out proof);
        private static XElement InitialSavedEffectBefore(RebirthWorkstationCompletionPipeline.NativeHostOriginal original)
        {
            var first=initialBefore[original];
            return new XElement("initialSavedBefore",new XAttribute("path",first.ReceiptPath),new XAttribute("digest",HydrationHash(first.Receipt)),
                new XAttribute("nativePath",first.NativePath),new XAttribute("nativeLength",first.Native.Length),new XAttribute("nativeDigest",HydrationHash(first.Native)));
        }
        internal static bool ValidateSavedSameOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement contribution,out XElement evidence,bool custody=false)
        {
            evidence=null;
            try{lock(gate)
            {
                var active=contribution?.Element("activeBefore")?.Elements().SingleOrDefault();
                var delivery=contribution?.Element("physicalDelivery")?.Elements().SingleOrDefault();
                var effect=contribution?.Element("nativeViewerEffect");
                if(contribution?.Name!="sameOwnerEffect"||effect==null)return false;
                int kind=(int?)contribution.Attribute("phase")??0;bool cached=kind==4||kind==5;
                if(cached)
                {
                    var full=kind==5?contribution.Element("fullBoundary")?.Elements().SingleOrDefault():
                        contribution.Element("actualCachedSpaceTransfer")?.Element("cachedFull")?.Elements().SingleOrDefault();
                    var candidates=sources.Values.Where(value=>ReferenceEquals(value.Original,original)&&(custody?value.SavedCustodyCurrent():value.Current())&&
                        (custody?value.CachedEffect?.SavedCustodyCurrent():value.CachedEffect?.Current())==true&&
                        value.EffectInventoryCurrent()&&XNode.DeepEquals(value.CachedEffect.Request.Element("fullBoundary")?.Elements().SingleOrDefault(),full)).ToArray();
                    if(candidates.Length!=1)return false;var saved=candidates[0];
                    if(!XNode.DeepEquals(saved.DirectEffect,effect)||!ValidateCachedEffectHeader(saved,effect,full,saved.CachedEffect.Frame,custody))return false;
                    evidence=new XElement("nativeViewerEffectSaved",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),
                        new XAttribute("source",original.SourceHash),new XAttribute("nonce",original.Nonce.ToString("N")),new XAttribute("generation",saved.Generation),
                        new XAttribute("boundary",true),new XElement(effect),InitialSavedEffectBefore(original),new XElement("fullBoundary",new XElement(full)),
                        new XElement("savedBefore",new XAttribute("path",saved.Before.ReceiptPath),new XAttribute("digest",HydrationHash(saved.Before.Receipt))),
                        new XElement("savedEffect",new XAttribute("path",saved.CachedEffect.ReceiptPath),new XAttribute("digest",HydrationHash(saved.CachedEffect.Receipt))),
                        new XElement("inventoryAccepted",new XAttribute("path",saved.EffectInventoryPath),new XAttribute("digest",HydrationHash(saved.EffectInventoryReceipt))));
                    return true;
                }
                if(active==null||delivery==null)return false;
                string generation=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting));
                if(!sources.TryGetValue(Key(original,generation),out var source)||!ReferenceEquals(source.Original,original)||!(custody?source.SavedCustodyCurrent():source.Current())||
                    (custody?source.Before?.SavedCustodyCurrent():source.Before?.Current())!=true||!source.BeforeInventoryCurrent()||
                    (custody?source.After?.SavedCustodyCurrent():source.After?.Current())!=true||!source.InventoryCurrent()||
                    !XNode.DeepEquals(source.Active,active)||!XNode.DeepEquals(source.Delivery,delivery)||!XNode.DeepEquals(source.DirectEffect,effect)||
                    !ValidateDirectEffect(source,effect,delivery,custody)||
                    !ProjectionCounters(effect.Element("ownerAfterGiveExp")?.Elements().SingleOrDefault(),original.Recipe.GetName(),out int total,out float count)||
                    !ProjectionCounters(source.After.Frame,original.Recipe.GetName(),out int finalTotal,out float finalCount)||total!=finalTotal||count!=finalCount)return false;
                evidence=new XElement("nativeViewerEffectSaved",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),
                    new XAttribute("source",original.SourceHash),new XAttribute("nonce",original.Nonce.ToString("N")),new XAttribute("generation",generation),
                    new XElement(effect),InitialSavedEffectBefore(original),new XElement("savedBefore",new XAttribute("path",source.Before.ReceiptPath),new XAttribute("digest",HydrationHash(source.Before.Receipt))),
                    new XElement("savedAfter",new XAttribute("path",source.After.ReceiptPath),new XAttribute("digest",HydrationHash(source.After.Receipt))),
                    new XElement("inventoryAccepted",new XAttribute("path",source.InventoryPath),new XAttribute("digest",HydrationHash(source.InventoryReceipt))));
                return true;
            }}catch{return false;}
        }
        internal sealed class SavedCachedBoundaryOwner:IDisposable
        {
            internal readonly RebirthWorkstationCompletionPipeline.NativeHostOriginal Source;
            private XElement original,evidence;private IDisposable allocation,retention;
            private SavedCachedBoundaryOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement original,XElement evidence,IDisposable allocation,IDisposable retention)
            {Source=source;this.original=original;this.evidence=evidence;this.allocation=allocation;this.retention=retention;}
            internal bool Current()=>original!=null&&Source.SourceCurrent()&&ValidateSavedSameOwner(Source,original,out var saved,true)&&XNode.DeepEquals(saved,evidence);
            internal long MeasureEvidence()=>Current()?MeasurePublicationElement(evidence):0;
            internal XElement CopyEvidence()=>Current()?new XElement(evidence):null;
            internal static bool TryIssue(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,out SavedCachedBoundaryOwner proof)
            {
                proof=null;IDisposable allocation=null,retention=null;
                try
                {
                    if(source?.CurrentViewer()!=true||!source.AdmissionCurrent()||(int?)contribution?.Attribute("phase")!=5||
                        !ValidateSavedSameOwner(source,contribution,out var evidence))return false;
                    long measured=checked(MeasurePublicationElement(evidence)+MeasurePublicationElement(contribution));
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*measured+65536),out allocation)||(retention=source.Retain())==null)return false;
                    proof=new SavedCachedBoundaryOwner(source,new XElement(contribution),evidence,allocation,retention);allocation=retention=null;
                    if(proof.Current())return true;proof.Dispose();proof=null;return false;
                }
                catch{proof?.Dispose();proof=null;return false;}
                finally{allocation?.Dispose();retention?.Dispose();}
            }
            public void Dispose(){original=evidence=null;retention?.Dispose();retention=null;allocation?.Dispose();allocation=null;}
        }
        internal static bool TryValidateCachedBoundaryOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,out SavedCachedBoundaryOwner proof)
            =>SavedCachedBoundaryOwner.TryIssue(source,contribution,out proof);
        // Historical evidence only: native cache load/primary custody is a
        // separate private issuer. No parsed file recreates a viewer source.
        internal static bool MatchesSavedCachedBoundaryOwner(XElement evidence)
        {
            try
            {
                if(evidence?.Name!="nativeViewerEffectSaved"||(bool?)evidence.Attribute("boundary")!=true||
                    !Guid.TryParseExact((string)evidence.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)return false;
                string root=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
                var sourceBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(root);
                if(sourceBytes==null||RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(sourceBytes))!=(string)evidence.Attribute("source"))return false;
                var original=RebirthWorkstationCompletionPublication.ReadPrimary(root)?.Element("before")?.Elements().SingleOrDefault();
                var effect=evidence.Element("nativeViewerEffect");
                if(original?.Name!="remoteNativeOriginal"||(string)original.Attribute("nonce")!=(string)evidence.Attribute("nonce")||
                    effect==null||(string)effect.Attribute("operation")!=operation.ToString("N")||(string)effect.Attribute("nonce")!=(string)evidence.Attribute("nonce")||
                    (string)effect.Attribute("source")!=(string)evidence.Attribute("source"))return false;
                string generation=(string)evidence.Attribute("generation");
                if(generation?.Length!=64||!HistoricalViewerSeal(evidence.Element("savedEffect"),root+".viewer."+generation+".effect",evidence,out var saved)||
                    !HistoricalViewerSeal(evidence.Element("savedBefore"),root+".viewer."+generation+".before",evidence,out _)||
                    effect.Element("initialViewerBefore")?.Elements().SingleOrDefault() is not XElement initialRequest||
                    !HistoricalViewerSeal(evidence.Element("initialSavedBefore"),root+".viewer."+(string)initialRequest.Attribute("generation")+".before",evidence,out var first)||
                    !XNode.DeepEquals(first.Element("nativeOwnerFrame"),effect.Element("ownerBefore")?.Elements().SingleOrDefault()))return false;
                var request=RebirthWorkstationCompletionPublication.ReadPrimary(root+".viewer."+generation+".effect.intent")?.Element("before")?.Elements().SingleOrDefault();
                if(request?.Name!="nativeViewer"||(string)request.Attribute("stage")!="effect"||RequestDigest(request)!=(string)saved.Attribute("digest")||
                    !XNode.DeepEquals(request.Element("sameOwnerEffect")?.Elements().SingleOrDefault(),effect)||
                    !XNode.DeepEquals(request.Element("fullBoundary"),evidence.Element("fullBoundary"))||
                    !XNode.DeepEquals(request.Element("nativeOwnerFrame"),saved.Element("nativeOwnerFrame")))return false;
                var inventory=evidence.Element("inventoryAccepted");string expected=root+".viewer."+generation+".effect-inventory";
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(expected);
                return (string)inventory?.Attribute("path")==expected&&bytes!=null&&HydrationHash(bytes)==(string)inventory.Attribute("digest");
            }
            catch{return false;}
        }
        private static bool HistoricalViewerSeal(XElement link,string expected,XElement original,out XElement receipt)
        {
            receipt=null;if((string)link?.Attribute("path")!=expected)return false;
            var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(expected);
            if(bytes==null||HydrationHash(bytes)!=(string)link.Attribute("digest"))return false;
            receipt=RebirthWorkstationCompletionPublication.ReadPrimary(expected)?.Element("before")?.Elements().SingleOrDefault();
            string native=expected+".native";int length=(int?)receipt?.Attribute("nativeLength")??0;
            if(receipt?.Name!="nativeViewerSaved"||(string)receipt.Attribute("source")!=(string)original.Attribute("source")||
                (string)receipt.Attribute("nonce")!=(string)original.Attribute("nonce")||(string)receipt.Attribute("retainedNative")!=native||
                length<1||length>16*1024*1024||File.Exists(native+".tmp")||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(native))return false;
            using(var input=new FileStream(native,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())
                return input.Length==length&&BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant()==(string)receipt.Attribute("nativeDigest")&&
                    input.Position==length&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(native);
        }
    }
}

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Admission is not an accepted native event. Only the existing actual
    // Process/read completion can turn this into saved station publication.
    internal sealed class NativeViewerEffectAdmission:IDisposable
    {
        internal readonly NativeHostOriginal Source;internal readonly int Phase;
        internal byte[] Body {get;private set;}internal readonly int Offset;
        private XElement contribution,evidence;private IDisposable allocation,retention;
        private NativeViewerEffectAdmission(NativeHostOriginal source,int phase,XElement contribution,XElement evidence,byte[] body,int offset,IDisposable allocation,IDisposable retention)
        {Source=source;Phase=phase;this.contribution=contribution;this.evidence=evidence;Body=body;Offset=offset;this.allocation=allocation;this.retention=retention;}
        internal bool MatchesAccepted(RemoteNativeEvent actual)=>Body!=null&&contribution!=null&&actual!=null&&ReferenceEquals(actual.SameOwnerEffect,this)&&
            ReferenceEquals(actual.Source,Source)&&(int)actual.Phase==Phase&&actual.Offset==Offset&&actual.IsActualAcceptedRead()&&
            actual.Body.SequenceEqual(Body)&&XNode.DeepEquals(actual.Contribution,contribution)&&CurrentSavedSource();
        internal bool CurrentSavedSource()=>contribution!=null&&Source.SourceCurrent()&&
            RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ValidateSavedSameOwner(Source,contribution,out var current,true)&&XNode.DeepEquals(current,evidence);
        internal long MeasureEvidence()=>CurrentSavedSource()?RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(evidence):0;
        internal XElement CopyEvidence()=>CurrentSavedSource()?new XElement(evidence):null;
        internal static bool TryIssue(NativeHostOriginal source,XElement contribution,byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,out NativeViewerEffectAdmission proof)
        {
            proof=null;IDisposable allocation=null,retention=null;
            try
            {
                if(source?.CurrentViewer()!=true||!source.AdmissionCurrent()||(phase!=2&&phase!=3&&phase!=4)||
                    (int?)contribution?.Attribute("phase")!=phase)return false;
                using(var callbacks=source.Memory.TryEnterCallbacks())
                {
                if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,prior,priorOffset))return false;
                if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ValidateSavedSameOwner(source,contribution,out var evidence)||
                    !(phase==4?RebirthProductiveEarnedUiDeferredTransport.ValidateSameOwnerNativeCachedSpace(source,contribution,prior,priorOffset,proposed,proposedOffset,out _):
                        RebirthProductiveEarnedUiDeferredTransport.ValidateSameOwnerNativePhysical(source,contribution,prior,priorOffset,proposed,proposedOffset,phase,out _)))return false;
                long measured=checked(RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(evidence)+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(contribution));
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*measured+4L*proposed.Length+65536),out allocation)||(retention=source.Retain())==null)return false;
                proof=new NativeViewerEffectAdmission(source,phase,new XElement(contribution),evidence,(byte[])proposed.Clone(),proposedOffset,allocation,retention);allocation=retention=null;
                if(proof.CurrentSavedSource())return true;proof.Dispose();proof=null;return false;
                }
            }
            catch{proof?.Dispose();proof=null;return false;}
            finally{allocation?.Dispose();retention?.Dispose();}
        }
        public void Dispose(){contribution=evidence=null;Body=null;retention?.Dispose();retention=null;allocation?.Dispose();allocation=null;}
    }
}
