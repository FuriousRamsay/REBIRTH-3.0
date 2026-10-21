using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class ClosedAuthorityCompletion
    {
        internal bool AdoptUnacceptedNativeOwner(RemoteLoadedOwner loaded)
        {
            if(Attached!=null||!(Connection.IsDisconnected()||Peer.disconnecting)||!NativeLineageCurrent()||
                !ReferenceEquals(Station.Queue?.LastOrDefault(),Row)||!ReferenceEquals(Row?.Recipe,Recipe)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,BeforeNative,BeforeOffset)||loaded?.Current()!=true||
                !ReferenceEquals(loaded.File.world,World)||loaded.Deaths!=Deaths||loaded.Creation!=Creation)return false;
            var peer=loaded.CurrentPeer();
            if(peer==null||ReferenceEquals(peer,Peer)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||
                identity.StorageKey!=Owner||identity.CanonicalId!=Account||ReferenceEquals(peer.netConnection[0],Connection)||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerPlaced_"+Operation.ToString("N"),out float phase)||phase!=0||
                !PhaseBytes(loaded.File.buffs,"rbRecyclerSlot_"+Operation.ToString("N"),out float count)||count!=0||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*loaded.File.native.Length+16L*Image.Length+65536),out var charge))return false;
            using(charge)
            {
                Guid generation=loaded.CaptureReconnectGeneration(Operation);if(generation==Guid.Empty)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Image))),
                    new XElement("recyclerClosedUnacceptedOwnerRejoined",new XAttribute("ticket",Ticket.ToString("N")),new XAttribute("generation",generation.ToString("N")),
                        new XAttribute("oldConnectionRetired",true),new XAttribute("actualNativeLoadToPlayer",true),new XAttribute("ownerOriginalNeverAccepted",true),
                        XElement.Parse(Encoding.UTF8.GetString(Image)),new XElement("nativeOwner",Convert.ToBase64String(loaded.File.native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Operation.ToString("N")+".closed-owner-rejoin."+generation.ToString("N")+".xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !loaded.Current()||Attached!=null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,BeforeNative,BeforeOffset))return false;
                var previousActor=Recipient;var previousPeer=Peer;var previousConnection=Connection;var previousGeneration=OwnerGeneration;
                Recipient=loaded.Actor;Peer=peer;Connection=peer.netConnection[0];OwnerGeneration=generation;
                if(Current())return true;
                Recipient=previousActor;Peer=previousPeer;Connection=previousConnection;OwnerGeneration=previousGeneration;return false;
            }
        }
    }
}
