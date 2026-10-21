using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class RemoteHostOriginal
    {
        // An actual new native primary Load -> ToPlayer renews only the remote
        // owner leg. The original held native station publication is retained;
        // neither a current TE getter nor a receipt stamp supplies that custody.
        internal bool AdoptActualReconnectedOwner(RemoteLoadedOwner loaded)
        {
            if(FinancialReleased||AdmissionUnknown||PendingReentry!=null||OwnerRejoined&&ColdNeedsOwner||
                !(Connection?.IsDisconnected()==true||Peer.disconnecting)||Publication?.RetainedSavedCustody(this)!=true||
                (OwnerCheckpoint==null?AcceptedStation||OwnerBeforeCheckpoint!=null:!OwnerCheckpoint.CurrentSaved()||OwnerCheckpoint.Workers!=0)||loaded?.Current()!=true||
                !ReferenceEquals(loaded.File.world,World)||ReferenceEquals(loaded.Actor,Actor)||loaded.Deaths!=Deaths||loaded.Creation!=Creation)return false;
            var peer=loaded.CurrentPeer();
            if(peer==null||ReferenceEquals(peer,Peer)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||
                identity.StorageKey!=Owner||identity.CanonicalId!=Account||ReferenceEquals(peer.netConnection?[0],Connection)||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerPlaced_"+Operation.ToString("N"),out float phase)||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerSlot_"+Operation.ToString("N"),out float completed)||
                phase!=(OwnerCheckpoint?.Phase??0)||completed!=(OwnerCheckpoint?.Completed??0)||!MatchesOwnerCheckpoint(loaded.File.bag,loaded.File.belt,(int)completed)||
                OwnerCheckpoint!=null&&!OwnerCheckpoint.MatchesSavedNative(loaded.File.native)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*loaded.File.native.Length+16L*Image.Length+65536),out var charge))return false;
            using(charge)
            {
                // Nonce is issued once per actual loaded owner/station source;
                // budget or metadata retry cannot manufacture new operations.
                Guid nonce=loaded.CaptureReconnectGeneration(Operation);if(nonce==Guid.Empty)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Image))),new XElement("recyclerRemoteOwnerReconnected",
                        new XAttribute("nonce",nonce.ToString("N")),new XAttribute("phase",(int)phase),new XAttribute("completed",(int)completed),
                        new XAttribute("actualPrimaryLoad",true),new XAttribute("actualToPlayer",true),new XAttribute("oldConnectionRetired",true),
                        Publication.CopyRetainedSavedCustody(this),OwnerCheckpoint?.CopySavedDescriptor(),new XElement("native",Convert.ToBase64String(loaded.File.native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Operation.ToString("N")+".remote-owner-reconnect."+nonce.ToString("N")+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !loaded.Current()||Publication.RetainedSavedCustody(this)!=true||OwnerCheckpoint!=null&&!OwnerCheckpoint.CurrentSaved())return false;
                var old=OwnerCheckpoint;
                // From this boundary stale old-peer headers/native envelopes
                // fail exact registry peer/connection/nonce admission.
                Peer=peer;Actor=loaded.Actor;Connection=peer.netConnection[0];Nonce=nonce;
                OwnerCheckpoint=null;if(!ReferenceEquals(old,OwnerBeforeCheckpoint))old?.Dispose();
                ColdNeedsOwner=true;ColdPhase=(int)phase;ColdCompleted=(int)completed;OwnerRejoined=true;AutomaticOwnerSequence=0;
                // A fresh real owning snapshot/upload/save must establish the
                // new receive head before any remaining owner setter is allowed.
                return Current();
            }
        }
    }
    private static void RecoverRemoteRecyclerReconnect(World world)
    {
        if(world?.IsRemote()!=false)return;
        foreach(var source in remoteRecyclerOriginals.Values.ToArray())foreach(var loaded in remoteLoadedRecyclerOwners.ToArray())
        {
            if(!source.AdoptActualUnenteredOwner(loaded)&&!source.AdoptActualReconnectedOwner(loaded))continue;
            remoteLoadedRecyclerOwners.Remove(loaded);loaded.File.Dispose();break;
        }
    }
}
