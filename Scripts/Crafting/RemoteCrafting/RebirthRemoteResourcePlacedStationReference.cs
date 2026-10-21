using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Syntax and immutable full-artifact binding only. This descriptor is never
// owner permission/payment evidence and cannot reconstruct a missing artifact.
internal static class RebirthRemoteResourcePlacedStationReference
{
    internal const int MaximumBytes=4096;
    internal sealed class Image
    {
        internal readonly Guid Source,Job;internal readonly string World,Owner,Creation,Binding,Original,Artifact,Birth;
        internal readonly RebirthRemoteResourcePlacedStationScope.PaymentSource Payment;
        internal Image(XElement body)
        {Source=Guid.ParseExact((string)body.Attribute("source"),"N");Job=Guid.ParseExact((string)body.Attribute("job"),"N");
            World=(string)body.Attribute("world");Owner=(string)body.Attribute("owner");Creation=(string)body.Attribute("creation");
            Binding=(string)body.Attribute("binding");Original=(string)body.Attribute("original");Artifact=(string)body.Attribute("artifact");Birth=(string)body.Attribute("birth");
            Payment=(RebirthRemoteResourcePlacedStationScope.PaymentSource)checked((byte)(int)body.Attribute("payment"));}
        internal XElement Copy()=>new XElement("placedOwnerOriginal",new XAttribute("version",1),new XAttribute("source",Source.ToString("N")),new XAttribute("job",Job.ToString("N")),
            new XAttribute("world",World),new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("binding",Binding),
            new XAttribute("original",Original),new XAttribute("artifact",Artifact),new XAttribute("birth",Birth),new XAttribute("payment",(byte)Payment));
        internal string Encode()=>Copy().ToString(SaveOptions.DisableFormatting);
    }
    private static bool Hex(string text)=>text?.Length==64&&text.All(value=>value>='0'&&value<='9'||value>='a'&&value<='f');
    internal static bool TryRead(string text,out Image image)
    {
        image=null;
        try
        {
            if(text==null||Encoding.UTF8.GetByteCount(text)>MaximumBytes)return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumBytes);
            if(body.Name!="placedOwnerOriginal"||body.Attributes().Count()!=11||body.Nodes().Any()||(int?)body.Attribute("version")!=1||
                !Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||source==Guid.Empty||source.ToString("N")!=(string)body.Attribute("source")||
                !Guid.TryParseExact((string)body.Attribute("job"),"N",out var job)||job==Guid.Empty||job.ToString("N")!=(string)body.Attribute("job")||
                !Hex((string)body.Attribute("world"))||!Hex((string)body.Attribute("owner"))||!Hex((string)body.Attribute("binding"))||
                !Hex((string)body.Attribute("original"))||!Hex((string)body.Attribute("artifact"))||
                !Guid.TryParseExact((string)body.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||birth.ToString("N")!=(string)body.Attribute("birth")||
                !RebirthOriginalActionScope.Validate((string)body.Attribute("owner"),(string)body.Attribute("creation"))||
                (int?)body.Attribute("payment") is not int payment||payment!=(int)RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory&&payment!=(int)RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput)return false;
            var value=new Image(body);if(value.Encode()!=text)return false;image=value;return true;
        }
        catch{image=null;return false;}
    }
    internal static bool TryCapture(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation,out Image reference)
    {
        reference=null;
        try
        {
            if(root==null||original==null||allocation==null||allocation.Source!=root.Operation||allocation.Binding!=root.Binding||allocation.Original!=original.Digest||
                !RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var artifact,out string digest,allocation)||!artifact.HasCapturedAllocation||artifact.Original.Digest!=original.Digest||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out _,out _,out _,out _,out _,out _,out _,out _,out var payment,out _,out _))return false;
            var body=new XElement("placedOwnerOriginal",new XAttribute("version",1),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("job",original.Operation.ToString("N")),
                new XAttribute("world",root.World),new XAttribute("owner",root.Owner),new XAttribute("creation",root.Creation),new XAttribute("binding",root.Binding),
                new XAttribute("original",original.Digest),new XAttribute("artifact",digest),new XAttribute("birth",artifact.Birth),new XAttribute("payment",(byte)payment));
            return TryRead(body.ToString(SaveOptions.DisableFormatting),out reference);
        }
        catch{reference=null;return false;}
    }
    internal static bool TryResolve(RebirthRemoteResourceDurableJournal.Record root,string encoded,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation,out RebirthRemoteResourcePlacedStationArtifactReader.Image artifact)
    {
        artifact=null;
        if(!TryRead(encoded,out var reference)||root==null||reference.Source!=root.Operation||reference.Binding!=root.Binding||reference.World!=root.World||
            reference.Owner!=root.Owner||reference.Creation!=root.Creation||allocation==null||allocation.Source!=root.Operation||allocation.Binding!=root.Binding||allocation.Original!=reference.Original)return false;
        if(!RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var full,out string digest,allocation)||!full.HasCapturedAllocation||digest!=reference.Artifact||full.Birth!=reference.Birth||
            full.Original.Operation!=reference.Job||full.Original.Digest!=reference.Original||
            !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(full.Original,out _,out _,out _,out _,out _,out _,out _,out _,out var payment,out _,out _)||payment!=reference.Payment)return false;
        artifact=full;return true;
    }
}
