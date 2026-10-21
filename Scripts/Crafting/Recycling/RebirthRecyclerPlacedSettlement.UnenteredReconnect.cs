using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class RemoteHostOriginal
    {
        internal bool AdoptActualUnenteredOwner(RemoteLoadedOwner loaded)
        {
            if(ClosedAuthority!=null||FinancialReleased||AcceptedStation||AdmissionEntered||AdmissionUnknown||Publication!=null||
                OwnerCheckpoint!=null||OwnerBeforeCheckpoint!=null||PendingReentry!=null||
                !(Connection.IsDisconnected()||Peer.disconnecting)||!NativeLineageCurrent()||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,BeforeNative,BeforeOffset)||loaded?.Current()!=true||
                !ReferenceEquals(loaded.File.world,World)||loaded.Deaths!=Deaths||loaded.Creation!=Creation)return false;
            var peer=loaded.CurrentPeer();
            if(peer==null||ReferenceEquals(peer,Peer)||ReferenceEquals(peer.netConnection[0],Connection)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.StorageKey!=Owner||identity.CanonicalId!=Account||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerPlaced_"+Operation.ToString("N"),out float phase)||phase!=0||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerSlot_"+Operation.ToString("N"),out float count)||count!=0||
                !MatchesOwnerCheckpoint(loaded.File.bag,loaded.File.belt,0)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*loaded.File.native.Length+16L*Image.Length+65536),out var charge))return false;
            using(charge)
            {
                Guid generation=loaded.CaptureReconnectGeneration(Operation);if(generation==Guid.Empty)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Image))),new XElement("recyclerUnenteredOwnerReconnected",
                        new XAttribute("generation",generation.ToString("N")),new XAttribute("nativeMarkerNeverEntered",true),new XAttribute("ownerCheckpointNeverEntered",true),
                        new XAttribute("oldConnectionRetired",true),new XAttribute("actualNativeLoadToPlayer",true),new XElement("nativeOwner",Convert.ToBase64String(loaded.File.native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Operation.ToString("N")+".unentered-reconnect."+generation.ToString("N")+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !loaded.Current()||AdmissionEntered||AdmissionUnknown||Publication!=null||OwnerCheckpoint!=null||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,BeforeNative,BeforeOffset))return false;
                var previousPeer=Peer;var previousActor=Actor;var previousConnection=Connection;
                // Keep the immutable original nonce for its actual unused-close
                // metadata. Every native/owner request still requires this new
                // actual peer and connection; no old transport is reaccepted.
                Peer=peer;Actor=loaded.Actor;Connection=peer.netConnection[0];
                if(Current())
                {
                    // This retained source positively never crossed either
                    // native marker or owner checkpoint entry. The retired
                    // old connection cannot later perform that original.
                    if(RequestRemoteRecyclerUnused(this))PumpRemoteRecyclerUnused(this);
                    return true;
                }
                Peer=previousPeer;Actor=previousActor;Connection=previousConnection;return false;
            }
        }
    }
}
