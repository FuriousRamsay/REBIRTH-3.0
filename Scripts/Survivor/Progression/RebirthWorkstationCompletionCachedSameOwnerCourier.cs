using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedPhysicalSource
    {
        internal bool SameOwnerCourierRetired;
        internal bool RequiresSameOwnerCourier=>(string)Before.Attribute("startingOwnerKey")==Grant.Owner&&
            (string)Before.Attribute("startingOwnerCreation")==Grant.Creation;
        internal bool EnsureSameOwnerCourier()
        {
            if(!RequiresSameOwnerCourier)return true;
            if(Frame?.NativeAccepted!=true||Frame.ConfirmedOriginal?.IsConfirmed!=true||!CourierAcknowledged)return false;
            string path=Path+".same-owner-courier";
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length>0)
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*length+65536),out var readMemory))return false;
                using(readMemory)
                {
                    var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path);var evidence=saved?.Element("cachedSameOwnerCourier");
                    if((string)evidence?.Attribute("original")!=Frame.ConfirmedOriginal.Digest||(string)evidence.Attribute("nonce")!=Grant.Nonce.ToString("N")||
                        (string)evidence.Attribute("source")!=Grant.SourceHash)return false;
                    var retired=RebirthWorkstationCompletionPublication.ReadPrimary(path+".retired");
                    if(XNode.DeepEquals(retired?.Element("before")?.Elements().SingleOrDefault(),saved))SameOwnerCourierRetired=true;
                    return true;
                }
            }
            long bound=checked(16*MeasureIntent(Frame.Intent)+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(bound,out var memory))return false;
            using(memory)
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(Before)),new XElement("cachedSameOwnerCourier",
                        new XAttribute("nonce",Grant.Nonce.ToString("N")),new XAttribute("source",Grant.SourceHash),
                        new XAttribute("owner",Grant.Owner),new XAttribute("creation",Grant.Creation),new XAttribute("original",Frame.ConfirmedOriginal.Digest),
                        new XElement("confirmedOriginal",Frame.ConfirmedOriginal.Write()),new XElement("nativePhysicalIntent",new XElement(Frame.Intent))));
                return RebirthWorkstationCompletionPublication.Write(path,record)&&XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path));
            }
        }
    }
    private static float nextCachedSameOwnerCourier;
    private static bool ReadCachedSameOwnerCourier(string path,EntityPlayer actor,out XElement record,out RebirthWorkstationCompletionOriginal original)
    {
        record=null;original=null;
        if(actor==null||!ReferenceEquals(actor.world?.GetPrimaryPlayer(),actor)||!actor.world.IsRemote()||actor.IsDead()||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
        record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var courier=record?.Element("cachedSameOwnerCourier");
        if(courier==null||(string)record.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world)||
            (string)courier.Attribute("owner")!=owner.StorageKey||(string)courier.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            !RebirthWorkstationCompletionOriginal.TryRead(courier.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out original)||
            original.Operation!=(string)record.Attribute("operation")||original.Digest!=(string)courier.Attribute("original")||
            original.ViewerKey!=owner.StorageKey||original.OwnerKey!=owner.StorageKey||original.Creation!=(string)courier.Attribute("creation")||
            !ValidateDelivery(courier.Element("nativePhysicalIntent")?.Elements().SingleOrDefault(),original)||
            !RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _)||
            !Guid.TryParseExact((string)courier.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||
            !RebirthWorkstationCompletionOriginal.ValidKey((string)courier.Attribute("source")))return false;
        return true;
    }
    private static void PumpCachedSameOwnerCouriers(World world)
    {
        if(!world.IsRemote()||UnityEngine.Time.realtimeSinceStartup<nextCachedSameOwnerCourier)return;
        nextCachedSameOwnerCourier=UnityEngine.Time.realtimeSinceStartup+2f;string folder=Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedPhysical");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);if(!Directory.Exists(folder))return;
        int count=0;
        foreach(string path in Directory.EnumerateFiles(folder,"*.same-owner-courier"))
        {
            if(++count>4096)return;long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*length+65536),out var memory))continue;
            using(memory)try
            {
                if(!ReadCachedSameOwnerCourier(path,world.GetPrimaryPlayer(),out var record,out var original))continue;
                var courier=record.Element("cachedSameOwnerCourier");
                var retired=RebirthWorkstationCompletionPublication.ReadPrimary(path+".retired");
                if(retired!=null&&XNode.DeepEquals(retired.Element("before")?.Elements().SingleOrDefault(),record))continue;
                var acknowledged=RebirthWorkstationCompletionPublication.ReadPrimary(path+".ack");
                var ack=acknowledged?.Element("cachedSameOwnerAck")?.Element("ack");
                bool received=ack!=null&&XNode.DeepEquals(acknowledged.Element("before")?.Elements().SingleOrDefault(),record)&&
                    (string)ack.Attribute("nativeNonce")== (string)courier.Attribute("nonce")&&
                    (string)ack.Attribute("nativeSource")== (string)courier.Attribute("source");
                var request=new XElement("nativeSameOwnerQuery",new XAttribute("stage",received?"received":"query"),
                    new XAttribute("nonce",(string)courier.Attribute("nonce")),new XAttribute("source",(string)courier.Attribute("source")));
                if(received)request.Add(new XAttribute("terminal",(string)ack.Attribute("digest")));
                Send(true,request,Guid.ParseExact(original.Operation,"N"),world.GetPrimaryPlayer().entityId);
            }
            catch{/* Uncertain courier files never recreate a physical action. */}
        }
    }
    private static bool AcceptCachedSameOwnerTerminal(EntityPlayer actor,Guid operation,XElement ack)
    {
        string folder=Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedPhysical");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);if(!Directory.Exists(folder))return false;
        var files=Directory.EnumerateFiles(folder,operation.ToString("N")+".*.same-owner-courier").Take(5).ToArray();
        if(files.Length==0)return false;if(files.Length!=1)return true;string path=files[0];long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*length+65536),out var memory))return true;
        using(memory)try
        {
            if(!ReadCachedSameOwnerCourier(path,actor,out var record,out var original))return true;
            var courier=record.Element("cachedSameOwnerCourier");
            if((string)ack.Attribute("nativeNonce")!=(string)courier.Attribute("nonce")||(string)ack.Attribute("nativeSource")!=(string)courier.Attribute("source")||
                (string)ack.Attribute("owner")!=original.OwnerKey||(string)ack.Attribute("creation")!=original.Creation||
                !RebirthWorkstationCompletionOriginal.ValidKey((string)ack.Attribute("digest")))return true;
            string stage=(string)ack.Attribute("stage"),suffix=stage=="nativeSameOwnerRetired"?".retired":".ack";
            if(stage=="nativeSameOwnerRetired")
            {
                var prior=RebirthWorkstationCompletionPublication.ReadPrimary(path+".ack");
                if(!XNode.DeepEquals(prior?.Element("before")?.Elements().SingleOrDefault(),record)||
                    (string)prior?.Element("cachedSameOwnerAck")?.Element("ack")?.Attribute("digest")!=(string)ack.Attribute("digest"))return true;
            }
            var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",original.WorldGuid),
                new XElement("before",record),new XElement("cachedSameOwnerAck",new XElement(ack)));
            if(!RebirthWorkstationCompletionPublication.Write(path+suffix,receipt)||!XNode.DeepEquals(receipt,RebirthWorkstationCompletionPublication.ReadPrimary(path+suffix)))return true;
            if(stage=="nativeSameOwnerRetired"&&cachedPhysicalSources.TryGetValue(operation,out var source)&&source.Path+".same-owner-courier"==path)
                source.SameOwnerCourierRetired=true;
            return true;
        }
        catch{return true;}
    }
}
