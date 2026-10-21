using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static string coldNativeNoticeCursor="";
    // Terminal notice courier only. No parsed image becomes a native source,
    // queue/receipt setter, reward permission or retry of a native effect.
    private static bool TryReadColdNativeNotice(EntityPlayer viewer,Guid operation,out XElement primary,out XElement intent,
        out RebirthWorkstationCompletionOriginal confirmed,out IDisposable workspace)
    {
        primary=intent=null;confirmed=null;workspace=null;
        try
        {
            if(!viewer.world.IsRemote()||!ReferenceEquals(viewer.world.GetPrimaryPlayer(),viewer)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var actor))return false;
            string root=RebirthWorkstationCompletionOutbox.Root("client"),name=operation.ToString("N")+".xml";
            string original=Path.Combine(root,"intents","nativeOwners",name),active=Path.Combine(root,"intents",name),archive=Path.Combine(root,"intentArchive",name);
            long a=RebirthWorkstationCompletionPublication.PublishedLength(original);
            // Never use archive as recovery from an unknown staged active file.
            if(File.Exists(active+".tmp")||File.Exists(archive+".tmp"))return false;
            string delivered=File.Exists(active)?active:archive;
            long b=RebirthWorkstationCompletionPublication.PublishedLength(delivered);
            if(a<1||b<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*(a+b)+262144),out workspace))return false;
            primary=RebirthWorkstationCompletionPublication.ReadPrimary(original);intent=RebirthWorkstationCompletionPublication.ReadPrimary(delivered);
            string op=operation.ToString("N"),world=RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world),creation=RebirthCamperCharacterAccess.CurrentCreation(viewer);
            if((string)primary.Attribute("operation")!=op||(string)intent.Attribute("operation")!=op||
                (string)primary.Attribute("world")!=world||(string)intent.Attribute("world")!=world||
                !Guid.TryParseExact((string)primary.Attribute("nativeNonce"),"N",out var nonce)||nonce==Guid.Empty||
                string.IsNullOrEmpty((string)primary.Attribute("nativeSource"))||
                (string)primary.Attribute("nativeNonce")!=(string)intent.Attribute("nativeNonce")||
                (string)primary.Attribute("nativeSource")!=(string)intent.Attribute("nativeSource")||
                !RebirthWorkstationCompletionOriginal.TryRead(intent.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out confirmed)||
                confirmed.Operation!=op||confirmed.ViewerKey!=actor.StorageKey||confirmed.ViewerCreation!=creation||
                !confirmed.IsConfirmed||!ValidateDelivery(intent,confirmed))return false;
            return true;
        }
        catch{return false;}
    }
    private static void PumpColdNativeNotices(World world)
    {
        if(!world.IsRemote())return;var viewer=world.GetPrimaryPlayer();if(viewer==null)return;
        string folder=Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"intents","nativeOwners");
        try
        {
            RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);if(!Directory.Exists(folder))return;
            int count=0;bool selected=false;
            foreach(string path in Directory.EnumerateFiles(folder,"*.xml"))
            {
                if(++count>4096)break;
                string name=Path.GetFileNameWithoutExtension(path);
                if(string.CompareOrdinal(name,coldNativeNoticeCursor)<=0)continue;
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var operation)||nativeSources.ContainsKey(operation)||
                    File.Exists(path+".remote-owner-ack.xml"))continue;
                coldNativeNoticeCursor=name;selected=true;
                XElement primary=null,intent=null;RebirthWorkstationCompletionOriginal confirmed=null;IDisposable workspace=null;
                try
                {
                    if(!TryReadColdNativeNotice(viewer,operation,out primary,out intent,out confirmed,out workspace))continue;
                    Send(true,new XElement("nativeOwnerQuery",new XAttribute("nonce",(string)primary.Attribute("nativeNonce")),
                        new XAttribute("source",(string)primary.Attribute("nativeSource"))),operation,viewer.entityId);
                }
                finally{primary=intent=null;confirmed=null;workspace?.Dispose();}
                // Bound full reads and outgoing work per pump, independently
                // of the bounded metadata directory enumeration.
                break;
            }
            if(!selected)coldNativeNoticeCursor="";
        }
        catch{}
    }
    private static void AcceptColdNativeOwnerAck(EntityPlayer viewer,Guid operation,XElement ack)
    {
        XElement primary=null,intent=null;RebirthWorkstationCompletionOriginal confirmed=null;IDisposable workspace=null;
        try
        {
            if(!TryReadColdNativeNotice(viewer,operation,out primary,out intent,out confirmed,out workspace))return;
            var owner=primary.Element("originalQueueOwner");
            if((string)ack.Attribute("nativeNonce")!=(string)primary.Attribute("nativeNonce")||
                (string)ack.Attribute("nativeSource")!=(string)primary.Attribute("nativeSource")||
                (string)ack.Attribute("digest")!=(string)primary.Attribute("nativeSource")||
                (string)ack.Attribute("owner")!=(string)owner?.Attribute("owner")||
                (string)ack.Attribute("creation")!=(string)owner?.Attribute("creation"))return;
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"intents","nativeOwners",operation.ToString("N")+".xml.remote-owner-ack.xml");
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world)),new XElement(primary.Element("before")),
                new XElement("nativeOwnerTerminalAck",new XAttribute("historicalCourier",true),new XElement(ack),
                    new XElement("delivery",confirmed.Write().Element("delivery").Elements().Select(value=>new XElement(value)))));
            RebirthWorkstationCompletionPublication.Write(path,record);
        }
        catch{}
        finally{primary=intent=null;confirmed=null;workspace?.Dispose();}
    }
}
