using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedPhysicalHostAction
    {
        internal static CachedPhysicalHostAction FromRetainedIssue(CachedCreatorIssue issue,XElement prepare)
        {
            if(issue?.Current()!=true||issue.RetainedAction==Guid.Empty||!issue.Cache.RestoreAttempted||
                issue.Cache.Physical||issue.Cache.PhysicalObserved||!issue.Cache.NativeCurrent()||!issue.Cache.SavedCurrent()||
                !issue.TryCopyRetainedPrepare(out var saved)||!XNode.DeepEquals(saved,prepare))return null;
            string path=issue.Cache.Path+".remote-action."+issue.Epoch;
            var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            if(bytes==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,bytes)||!issue.Current())return null;
            return new CachedPhysicalHostAction(issue,issue.RetainedAction,(long)saved.Attribute("session"),path,bytes);
        }
        internal bool RestoreSavedBefore(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedViewerFile proof)
        {
            if(!Current()||Issue.RetainedAction!=Action||!Issue.TryCopyRetainedPrepare(out var prepare)||
                (long?)prepare.Attribute("session")!=Session||proof?.Source?.Cached!=this||proof.After||!proof.Current()||
                !proof.Source.BeforeInventoryCurrent()||!HasSavedCachedOwnerEvent(Issue.Cache)||
                !Issue.Cache.SavedCurrent()||!Issue.Cache.NativeCurrent())return false;
            string path=Path+".ready."+proof.Source.Generation;
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var ready=record?.Element("cachedPhysicalActionReady");
            if((string)record?.Attribute("operation")!=Operation.ToString("N")||(string)record.Attribute("world")!=WorldGuid||
                !XNode.DeepEquals(record.Element("before"),Issue.Cache.Original.Element("before"))||
                (string)ready?.Attribute("action")!=Action.ToString("N")||(string)ready.Attribute("ticket")!=Issue.Ticket.ToString("N")||
                (string)ready.Attribute("group")!=(string)prepare.Attribute("group")||(long?)ready.Attribute("session")!=Session||
                (string)ready.Attribute("original")!=SourceHash||(string)ready.Attribute("generation")!=proof.Source.Generation||
                (string)ready.Attribute("ownerNative")!=proof.NativePath||(string)ready.Attribute("ownerReceipt")!=proof.ReceiptPath||
                (string)ready.Attribute("ownerDigest")!=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(proof.Native)||
                (string)ready.Attribute("nativePublication")!=Issue.Cache.SavedPath||
                (string)ready.Attribute("nativePublicationDigest")!=HistoricalHash(Issue.Cache.SavedImage)||
                !XNode.DeepEquals(ready.Element("cachedViewer"),proof.Request))return false;
            var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            if(image==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,image)||!Current()||!proof.Current())return false;
            readyPath=path;readyImage=image;BeforeGeneration=proof.Source.Generation;SavedOwnerBeforeAction=true;return true;
        }
    }
}
