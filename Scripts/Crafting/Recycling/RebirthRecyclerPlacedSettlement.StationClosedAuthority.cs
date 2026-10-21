using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class StationPublication
    {
        // The original native runtime already planned this closed station's
        // final row. The remote owner supplies only its actual owner leg; it
        // cannot upload a substitute station or choose another station plan.
        internal bool ApplyClosedAuthorityAfter(RemoteHostOriginal source)
        {
            var closedSource=source?.ClosedAuthority;
            var owner=source?.OwnerBeforeCheckpoint;
            if(closedSource==null||!ReferenceEquals(Original,source)||!ReferenceEquals(closedSource.Attached,source)||
                attempted||unknown||after||source.AcceptedStation||source.UnusedRequested||!source.HasOwnerAdmission()||
                owner?.Phase!=1||owner.Completed!=0||!owner.CurrentSaved()||owner.AcceptedHead==null||
                !owner.AcceptedHead.TryGetTarget(out var acceptedHead)||!ReferenceEquals(acceptedHead,source.Peer.latestPlayerData)||
                beforeReceipt==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(beforePath,beforeReceipt)||!Current())return false;
            lock(chunk)
            {
                if(chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
                chunk.InProgressSaving=true;chunkOwned=true;
            }
            try
            {
                var intent=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerClosedAuthorityStationAttempted",
                        new XAttribute("ticket",closedSource.Ticket.ToString("N")),owner.CopySavedDescriptor(),
                        new XElement("savedNativeBefore",Convert.ToBase64String(beforeReceipt))));
                string target=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".closed-station-attempted.xml");
                if(!RebirthWorkstationCompletionPublication.Write(target,intent)||!XNode.DeepEquals(intent,RebirthWorkstationCompletionPublication.ReadPrimary(target))||
                    !Current()||!source.HasOwnerAdmission()||!owner.CurrentSaved()||!ReferenceEquals(acceptedHead,source.Peer.latestPlayerData))
                {ReleaseChunk();return false;}
                attempted=true;watch?.Dispose();watch=null;
                var station=source.Station;station.SetDisableModifiedCheck(true);
                station.Input=closedSource.Input;station.Output=closedSource.Output;station.Tools=closedSource.Tools;station.Queue=closedSource.Queue;
                station.cycleRecipeQueue();station.ResetTickTime();
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.AfterNative,source.AfterOffset))
                {unknown=true;return false;}
                source.AcceptedStation=true;after=true;cold=null;
                station.SetDisableModifiedCheck(false);ReleaseChunk();station.setModified();
                watch=RebirthRemoteResourceNativePublication.Begin(binding,Current,markerKey);
                return true; // SavedAfter remains a separate actual native-save gate.
            }
            catch{unknown=true;return false;}
        }
    }
}
