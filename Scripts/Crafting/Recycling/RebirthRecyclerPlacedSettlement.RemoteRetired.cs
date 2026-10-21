using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool ReplyPlacedRetired(NetPackageRebirthRecyclerPlacedRequestV1 packet,World world,XElement request,Guid operation)
    {
        if((string)request.Attribute("command")!="ack"&&(string)request.Attribute("command")!="query"||
            !RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity))return false;
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),path=Path.Combine(root,operation.ToString("N")+".remote-received.xml");
        bool awaitingReceipt=false;
        if(!new[]{path,path+".tmp",path+".bak"}.Any(ArtifactNodeExists))
        {path=Path.Combine(root,operation.ToString("N")+".remote-completed.xml");awaitingReceipt=true;}
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
        using(memory)
        {
            var receipt=RebirthWorkstationCompletionPublication.ReadPrimary(path);var before=receipt?.Element("before")?.Elements().SingleOrDefault();var received=receipt?.Element(awaitingReceipt?"recyclerRemoteCompleted":"recyclerRemoteReceived");
            if(receipt?.Name!="workstationCompletionIntent"||(string)receipt.Attribute("operation")!=operation.ToString("N")||before?.Name!="placedRecyclerOriginal"||received==null||
                (string)before.Attribute("operation")!=operation.ToString("N")||(string)before.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                (string)before.Attribute("owner")!=identity.StorageKey||(string)before.Attribute("account")!=identity.CanonicalId||
                ((string)received.Attribute("nonce")!=(string)request.Attribute("nonce")&&
                    !(awaitingReceipt&&(bool?)received.Attribute("actualLoadedCompletion")==true&&Guid.TryParseExact((string)request.Attribute("nonce"),"N",out var loadedNonce)&&loadedNonce!=Guid.Empty))||
                (int?)received.Attribute("completed") is not int completed||completed<0||
                completed>((string)before.Attribute("bagLocks")??string.Empty).Length+((string)before.Attribute("beltLocks")??string.Empty).Length)return false;
            string image=before.ToString(SaveOptions.DisableFormatting);
            if(RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(image))!=(string)request.Attribute("original"))return false;
            string terminal=Path.Combine(root,operation.ToString("N")+".terminal.xml");
            if(RecyclerFileDigest(terminal)!=(string)received.Attribute("terminal"))return false;
            if(awaitingReceipt)
            {
                if(received.Element("recyclerCleanupSaved")==null||RebirthWorkstationCompletionPublication.PublishedLength(terminal)!=(long?)received.Attribute("terminalLength"))return false;
                if((string)request.Attribute("command")=="ack")
                {
                    var acknowledgement=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",(string)before.Attribute("world")),
                        new XElement("before",new XElement(before)),new XElement("recyclerRemoteReceived",new XAttribute("nonce",(string)request.Attribute("nonce")),
                            new XAttribute("completed",(int)received.Attribute("completed")),new XAttribute("terminal",(string)received.Attribute("terminal"))));
                    string acknowledged=Path.Combine(root,operation.ToString("N")+".remote-received.xml");
                    if(!RebirthWorkstationCompletionPublication.Write(acknowledged,acknowledgement)||!XNode.DeepEquals(acknowledgement,RebirthWorkstationCompletionPublication.ReadPrimary(acknowledged)))return false;
                    awaitingReceipt=false;
                }
            }
            // Historical metadata only: this cannot create a Source, native
            // upload allowance, destination plan or owner slot permit.
            var reply=new XElement("recyclerRemoteStatus",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("nonce",(string)request.Attribute("nonce")),
                new XAttribute("original",(string)request.Attribute("original")),new XAttribute("stage",awaitingReceipt?5:6),new XAttribute("phase",3),new XAttribute("completed",completed),new XAttribute("ownerSaved",true),
                new XAttribute("world",(string)before.Attribute("world")),new XAttribute("owner",identity.StorageKey),new XAttribute("creation",(string)before.Attribute("creation")),new XAttribute("deaths",(int)before.Attribute("deaths")));
            packet.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRecyclerPlacedReplyV1>().Setup(reply.ToString(SaveOptions.DisableFormatting)));return true;
        }
    }
}
