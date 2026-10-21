using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    // Historical status only. No Registration, native action, wear, timer,
    // callback or queue-transition capability is recreated by this reader.
    private static bool TryQueryLoadedDeferredTerminal(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,
        Guid operation,Guid nonce,string hash,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(!ThreadManager.IsMainThread()||operation==Guid.Empty||nonce==Guid.Empty||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity))return false;
            string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarnedUi","remote",identity.StorageKey,operation.ToString("N"),"original.uio");
            string proofPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",operation.ToString("N")+".loaded-uio-owner.xml");
            long a=new FileInfo(path).Length,b=new FileInfo(path+".TerminalSaved").Length,c=RebirthWorkstationCompletionPublication.PublishedLength(proofPath);
            if(a<1||b<1||c<1||a>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||b>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||c>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*(a+b+c)+65536),out var memory))return false;
            using(memory)
            {
                var bytes=RebirthProductiveEarnedUiDeferredTransport.LoadedUiMember.ReadExact(path,a);
                var receiptBytes=RebirthProductiveEarnedUiDeferredTransport.LoadedUiMember.ReadExact(path+".TerminalSaved",b);
                var proofBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(proofPath);
                if(bytes==null||receiptBytes==null||proofBytes?.LongLength!=c||Hash(bytes)!=hash)return false;
                var frame=RebirthProductiveEarnedUiOriginal.ReadUiArtifact(bytes);
                var receipt=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(receiptBytes),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(proofPath);var saved=record?.Element("before")?.Element("loadedUiOwnerTerminal");
                var member=saved?.Element("member");var group=saved?.Element("nativeOwnerGroup");
                if(frame.Operation!=operation||frame.Owner!=identity.StorageKey||frame.Creation!=creation||frame.Deaths!=actor.Died||
                    frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                    !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out var birth)||birth!=frame.Birth.ToString("N")||
                    receipt.Name!="uiNativeDeferredTerminalSaved"||receipt.HasElements||receipt.Attributes().Count()!=4||
                    (string)receipt.Attribute("operation")!=operation.ToString("N")||(string)receipt.Attribute("nonce")!=nonce.ToString("N")||
                    (string)receipt.Attribute("birth")!=birth||(string)receipt.Attribute("uio")!=hash||
                    (string)record?.Attribute("operation")!=operation.ToString("N")||(string)record.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world)||
                    saved==null||saved.Attributes().Count()!=6||saved.Elements().Count()!=2||
                    (string)saved.Attribute("uio")!=hash||(string)saved.Attribute("nonce")!=nonce.ToString("N")||(string)saved.Attribute("birth")!=birth||
                    (string)saved.Attribute("owner")!=identity.StorageKey||(string)saved.Attribute("creation")!=creation||(int?)saved.Attribute("deaths")!=actor.Died||
                    (string)member?.Attribute("kind")!="uio"||(string)member.Attribute("operation")!=operation.ToString("N")||
                    (string)member.Attribute("uio")!=hash||(string)member.Attribute("nonce")!=nonce.ToString("N")||(string)member.Attribute("source")!=frame.OwnerPurpose||
                    group?.Element("membership")?.Elements().SelectMany(node=>node.Element("members")?.Elements("member")??Enumerable.Empty<XElement>()).Count(node=>XNode.DeepEquals(node,member))!=1)return false;
                string terminalPath=(string)group.Attribute("terminalPath");long length=RebirthWorkstationCompletionPublication.PublishedLength(terminalPath);
                if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*length+65536),out var terminalMemory))return false;
                using(terminalMemory)
                {
                    var terminalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(terminalPath);
                    var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(terminalPath)?.Element("before")?.Element("uiDeferredNativeOwnerSaved");
                    if(terminalBytes?.LongLength!=length||Hash(terminalBytes)!=(string)group.Attribute("terminalDigest")||terminal==null||
                        !XNode.DeepEquals(terminal.Element("membership"),group.Element("membership"))||
                        (string)terminal.Attribute("owner")!=(string)group.Attribute("recipient"))return false;
                    foreach(string field in new[]{"creation","beforePlayerFile","beforePlayerDigest","afterPlayerFile","afterPlayerDigest"})
                        if((string)terminal.Attribute(field)!=(string)group.Attribute(field))return false;
                    if(!RebirthProductiveEarnedArtifact.FileMatches(path,bytes)||!RebirthProductiveEarnedArtifact.FileMatches(path+".TerminalSaved",receiptBytes)||
                        !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(proofPath,proofBytes)||
                        !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalBytes)||
                        !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation))return false;
                    reply=new RebirthProductiveEarnedWire.Reply{Operation=operation,Nonce=nonce,Birth=frame.Birth,World=frame.World,Owner=frame.Owner,
                        Creation=frame.Creation,Deaths=frame.Deaths,Position=station.ToWorldPos(),Digest=hash,QueueOwner=frame.OwnerPurpose,State=RebirthProductiveEarnedWire.Status.UiTerminalSaved};
                    return true;
                }
            }
        }
        catch{return false;}
    }
}
