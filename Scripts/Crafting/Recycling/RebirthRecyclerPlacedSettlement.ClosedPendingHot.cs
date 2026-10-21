using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class ClosedAuthorityCompletion
    {
        internal bool RebindActualPendingLoad(LoadedStation loaded,RemoteLoadedOwner owner=null)
        {
            if(Attached!=null||Pending?.HistoricalSaved()!=true||!NativeOriginCurrent()||loaded?.Current()!=true||
                loaded.Adopted||loaded.Operation!=Operation||loaded.Stage!="before"||!ReferenceEquals(loaded.World,World)||
                !loaded.MatchesOriginal(Image)||ReferenceEquals(loaded.Station,Station)||loaded.Station.ToWorldPos()!=Station.ToWorldPos()||
                loaded.Station.block.GetBlockName()!=Station.block.GetBlockName()||loaded.Station.Queue?.LastOrDefault()?.Recipe==null)return false;
            ClientInfo nextPeer=null;Guid nextGeneration=Guid.Empty;
            if(!RecipientCurrent()&&!QualifyPendingReconnectedPair(loaded,owner,out nextPeer,out nextGeneration))return false;
            var previousStation=Station;var previousRow=Row;var previousRecipe=Recipe;var previousPending=Pending;
            var previousActor=Recipient;var previousPeer=Peer;var previousConnection=Connection;var previousGeneration=OwnerGeneration;
            ClosedPendingPublication next=null;bool committed=false;
            try
            {
                Station=loaded.Station;Row=Station.Queue.Last();Recipe=Row.Recipe;
                if(nextPeer!=null){Recipient=owner.Actor;Peer=nextPeer;Connection=nextPeer.netConnection[0];OwnerGeneration=nextGeneration;}
                if(!Current())return false;
                next=ClosedPendingPublication.FromActualPendingLoad(this,loaded);if(next==null)return false;
                Pending=next;if(Access?.RebindPendingClosedLoad(this,loaded)!=true)return false;
                loaded.Adopted=true;committed=true;previousPending.Dispose();return true;
            }
            finally
            {
                if(!committed)
                {
                    Station=previousStation;Row=previousRow;Recipe=previousRecipe;Pending=previousPending;
                    Recipient=previousActor;Peer=previousPeer;Connection=previousConnection;OwnerGeneration=previousGeneration;
                    next?.PreserveUnadoptedLoad(loaded);next?.Dispose();
                }
            }
        }
        private bool QualifyPendingReconnectedPair(LoadedStation loaded,RemoteLoadedOwner owner,out ClientInfo peer,out Guid generation)
        {
            peer=null;generation=Guid.Empty;
            if(!(Connection.IsDisconnected()||Peer.disconnecting)||owner?.Current()!=true||!ReferenceEquals(owner.File.world,World)||owner.Creation!=Creation||owner.Deaths!=Deaths)return false;
            var candidate=owner.CurrentPeer();
            if(candidate==null||ReferenceEquals(candidate,Peer)||ReferenceEquals(candidate.netConnection[0],Connection)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(candidate,out var identity)||identity.StorageKey!=Owner||identity.CanonicalId!=Account||
                !PhaseBytes(owner.File.buffs,"rbRecyclerPlaced_"+Operation.ToString("N"),out float phase)||phase!=0||
                !PhaseBytes(owner.File.buffs,"rbRecyclerSlot_"+Operation.ToString("N"),out float count)||count!=0||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*owner.File.native.Length+16L*Image.Length+65536),out var memory))return false;
            using(memory)
            {
                Guid next=owner.CaptureReconnectGeneration(Operation);if(next==Guid.Empty)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Image))),
                    new XElement("recyclerClosedPendingPairLoaded",new XAttribute("ticket",Ticket.ToString("N")),new XAttribute("generation",next.ToString("N")),
                        new XAttribute("actualNativeTileLoad",true),new XAttribute("actualOwnerLoadToPlayer",true),new XAttribute("oldConnectionRetired",true),
                        new XAttribute("nativeReceiptPath",loaded.ReceiptPath),new XAttribute("nativeReceiptLength",RebirthWorkstationCompletionPublication.PublishedLength(loaded.ReceiptPath)),
                        new XAttribute("nativeReceiptDigest",RecyclerFileDigest(loaded.ReceiptPath)),XElement.Parse(Encoding.UTF8.GetString(Image)),
                        new XElement("nativeOwner",Convert.ToBase64String(owner.File.native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Operation.ToString("N")+".closed-pending-pair."+next.ToString("N")+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !owner.Current()||!loaded.Current()||!Pending.HistoricalSaved())return false;
                peer=candidate;generation=next;return true;
            }
        }
    }
    private static void RecoverClosedPendingHot(ClosedAuthorityCompletion source)
    {
        LoadedStation[] candidates;lock(coldStationGate)candidates=loadedRecyclerStations.Where(value=>!value.Adopted&&value.Operation==source.Operation).ToArray();
        foreach(var loaded in candidates)
        {
            try
            {
                if(source.RebindActualPendingLoad(loaded))return;
                foreach(var owner in remoteLoadedRecyclerOwners.ToArray())if(source.RebindActualPendingLoad(loaded,owner))return;
            }
            catch{/* No native setter is performed by this retained rebind. */}
        }
    }
}
