using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

internal enum RebirthPoiPriorActorDisposition { Survived=1, NativeRemoved=2, RetainedUnresolved=3 }
// Producer assertion: the native adapter must prove actual survival or original
// native removal. RetainedUnresolved conservatively carries the original saved obligation
// without claiming present native life. Missing lookup/time is never removal evidence.
internal sealed class RebirthPoiResetActorOutcome
{
    public readonly Guid Token,SourceGeneration,Receipt;
    public readonly int EntityId;
    public readonly long SourceEpoch;
    public readonly string Descriptor,ActorDigest;
    public readonly RebirthPoiPriorActorDisposition Disposition;
    public RebirthPoiResetActorOutcome(Guid token,int entityId,long sourceEpoch,Guid sourceGeneration,string descriptor,string actorDigest,RebirthPoiPriorActorDisposition disposition,Guid receipt)
    {
        if(token==Guid.Empty||entityId<0||sourceEpoch<0||sourceGeneration==Guid.Empty||receipt==Guid.Empty||!Enum.IsDefined(typeof(RebirthPoiPriorActorDisposition),disposition)||!Digest(descriptor)||!Digest(actorDigest))throw new ArgumentException("Invalid original prior-actor outcome.");
        Token=token;EntityId=entityId;SourceEpoch=sourceEpoch;SourceGeneration=sourceGeneration;Descriptor=descriptor;ActorDigest=actorDigest;Disposition=disposition;Receipt=receipt;
    }
    private static bool Digest(string value){return value!=null&&value.Length==64&&!value.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'));}
    public bool Matches(RebirthPoiPartialObservation original,RebirthPoiVolumeObservation volume,RebirthPoiActorObservation actor)
    {return original!=null&&volume!=null&&actor!=null&&!actor.Dead&&original.Epoch==SourceEpoch&&original.EffectiveGeneration(volume)==SourceGeneration&&volume.Descriptor==Descriptor&&actor.Token==Token&&actor.EntityId==EntityId&&actor.CausalDigest==ActorDigest;}
    internal XElement Write(){return new XElement("priorActor",new XAttribute("token",Token.ToString("N")),new XAttribute("entity",EntityId),new XAttribute("epoch",SourceEpoch),new XAttribute("generation",SourceGeneration.ToString("N")),new XAttribute("descriptor",Descriptor),new XAttribute("actor",ActorDigest),new XAttribute("disposition",(int)Disposition),new XAttribute("receipt",Receipt.ToString("N")));}
    internal static RebirthPoiResetActorOutcome Read(XElement node)
    {
        RebirthPoiClearanceCodec.Shape(node,"priorActor","token,entity,epoch,generation,descriptor,actor,disposition,receipt","");
        return new RebirthPoiResetActorOutcome(RebirthPoiClearanceCodec.Id(node,"token"),RebirthPoiClearanceCodec.Int(node,"entity"),RebirthPoiClearanceCodec.Number(node,"epoch"),RebirthPoiClearanceCodec.Id(node,"generation"),RebirthPoiClearanceCodec.Text(node,"descriptor"),RebirthPoiClearanceCodec.Text(node,"actor"),(RebirthPoiPriorActorDisposition)RebirthPoiClearanceCodec.Int(node,"disposition"),RebirthPoiClearanceCodec.Id(node,"receipt"));
    }
}
internal sealed class RebirthPoiActorInheritance
{
    public readonly long OriginEpoch;
    public readonly bool Unresolved;
    public readonly Guid OriginGeneration,ResetTransaction,SurvivalReceipt;
    public RebirthPoiActorInheritance(long originEpoch,Guid originGeneration,Guid resetTransaction,Guid survivalReceipt,bool unresolved=false)
    {if(originEpoch<0||originGeneration==Guid.Empty||resetTransaction==Guid.Empty||survivalReceipt==Guid.Empty)throw new ArgumentException("Invalid inherited actor origin.");Unresolved=unresolved;OriginEpoch=originEpoch;OriginGeneration=originGeneration;ResetTransaction=resetTransaction;SurvivalReceipt=survivalReceipt;}
    public string Canonical {get{return OriginEpoch.ToString(CultureInfo.InvariantCulture)+":"+OriginGeneration.ToString("N")+":"+ResetTransaction.ToString("N")+":"+SurvivalReceipt.ToString("N")+(Unresolved?":unresolved":string.Empty);} }
    internal XElement Write(){return new XElement("inherit",new XAttribute("epoch",OriginEpoch),new XAttribute("generation",OriginGeneration.ToString("N")),new XAttribute("reset",ResetTransaction.ToString("N")),new XAttribute("receipt",SurvivalReceipt.ToString("N")),Unresolved?new XAttribute("unresolved",true):null);}
    internal static RebirthPoiActorInheritance Read(XElement node)
    {RebirthPoiClearanceCodec.Shape(node,"inherit",node.Attribute("unresolved")==null?"epoch,generation,reset,receipt":"epoch,generation,reset,receipt,unresolved","");return new RebirthPoiActorInheritance(RebirthPoiClearanceCodec.Number(node,"epoch"),RebirthPoiClearanceCodec.Id(node,"generation"),RebirthPoiClearanceCodec.Id(node,"reset"),RebirthPoiClearanceCodec.Id(node,"receipt"),node.Attribute("unresolved")!=null&&System.Xml.XmlConvert.ToBoolean(RebirthPoiClearanceCodec.Text(node,"unresolved")));}
}