using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool PublishRemoteRecyclerCompleted(RemoteHostOriginal source)
    {
        if(!source.FinancialReleased||source.Access?.Ready()!=true||source.Terminal==null||source.OwnerBeforeCheckpoint?.CurrentSaved()!=true||
            source.OwnerCheckpoint?.Phase!=3||source.OwnerCheckpoint.Completed!=source.Slots.Length||!source.OwnerCheckpoint.CurrentSaved()||
            !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(source.TerminalPath,source.Terminal)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(source.Image.Length+source.Terminal.Length)+65536),out var memory))return false;
        using(memory)
        {
            var cleanup=source.Publication.CopyCleanupSaved();if(cleanup==null)return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRemoteCompleted",
                    new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("completed",source.Slots.Length),
                    new XAttribute("terminal",RebirthStationPublicationRecord.Digest(source.Terminal)),new XAttribute("terminalLength",source.Terminal.Length),cleanup));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-completed.xml");
            return RebirthWorkstationCompletionPublication.Write(path,record)&&XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))&&
                source.Access.Ready()&&source.Publication.CleanupSaved()&&source.OwnerCheckpoint.CurrentSaved()&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(source.TerminalPath,source.Terminal);
        }
    }
}
