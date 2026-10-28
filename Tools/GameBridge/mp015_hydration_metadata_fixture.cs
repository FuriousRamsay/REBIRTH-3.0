// AUTHORED_NOT_RUN. Pure production metadata codec only; native identities,
// readers, send queues, grants and gameplay are intentionally NOT modeled.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Security.Cryptography;
static class RebirthWorkstationCompletionPublication {internal const int MaximumBytes=24*1024*1024;}
static class NetPackageRebirthUiDeferredHydrationV1 {internal const int Maximum=32*1024*1024,NativeEnvelopeMaximum=5*1024*1024;}
static class RebirthProductiveEarnedObservationBudget
{
 internal sealed class Lease:IDisposable{public void Dispose(){}}
 internal static bool Deny;internal static long Last;
 internal static bool TryResizeDetached(IDisposable lease,long bytes){Last=bytes;return !Deny;}
}
static class MetadataFixture
{
 private const int ExpandedHydrationMetadataMaximum=4*RebirthWorkstationCompletionPublication.MaximumBytes+65536;
 private const int PackedHydrationMetadataMaximum=NetPackageRebirthUiDeferredHydrationV1.Maximum-NetPackageRebirthUiDeferredHydrationV1.NativeEnvelopeMaximum;
 private static readonly XNamespace hydrationBlobNamespace="urn:rebirth:completion:hydration:metadata:1";
 private sealed class HydrationDispatch {internal Guid Group;internal bool Deny;internal bool ReserveMetadata(long bytes)=>!Deny;}
 // PRODUCTION_COUNTER
 // PRODUCTION_MEASURE
 // PRODUCTION_HASH
 // PRODUCTION_GUID
 // PRODUCTION_PACK
 // PRODUCTION_XML
 // PRODUCTION_EXPAND
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Refuses(Action action,string message){try{action();}catch(InvalidDataException){return;}throw new Exception(message);}
 public static void Main()
 {
  var source=new HydrationDispatch{Group=Guid.NewGuid()};var seed=Enumerable.Range(0,196608).Select(value=>(byte)(value*37+value/253)).ToArray();
  string rich=Convert.ToBase64String(seed);var header=new XElement("deferredNativeHydration",new XAttribute("version",1),new XAttribute("group",source.Group.ToString("N")),
   new XElement("cachedPrimaries",new XElement("pending",new XAttribute("operation",Guid.NewGuid().ToString("N")),new XElement("cachedCreatorGrant",
    new XElement("before",new XElement("nativeBefore",rich)),new XElement("full",new XElement("nativeBefore",rich),new XElement("nativeAfter",rich),new XElement("primaryNative",rich))))));
  var unchanged=new XElement(header);byte[] selector,metadata;Check(PackHydrationMetadata(source,header,out selector,out metadata),"rich metadata pack refused");
  Check(metadata!=null&&metadata.Length<Encoding.UTF8.GetByteCount(header.ToString(SaveOptions.DisableFormatting))/2,"repeated immutable images not deduplicated");
  Check(XNode.DeepEquals(header,unchanged),"codec mutated original evidence");
  using(var budget=new RebirthProductiveEarnedObservationBudget.Lease())
  {
   int expanded;string digest;var restored=ExpandHydrationMetadata(metadata,source.Group,budget,out expanded,out digest);
   Check(XNode.DeepEquals(restored,header)&&expanded==Encoding.UTF8.GetByteCount(header.ToString(SaveOptions.DisableFormatting)),"rich full proof changed on expansion");
   var selection=XElement.Parse(Encoding.UTF8.GetString(selector));Check((string)selection.Attribute("digest")==HydrationHash(metadata)&&(string)selection.Attribute("header")==digest,"selector did not bind exact component");
   Refuses(()=>ExpandHydrationMetadata(metadata,Guid.NewGuid(),budget,out expanded,out digest),"foreign selected group accepted");
   var unknown=XElement.Parse(Encoding.UTF8.GetString(metadata));unknown.Element("blobs").Elements().First().Remove();
   Refuses(()=>ExpandHydrationMetadata(Encoding.UTF8.GetBytes(unknown.ToString(SaveOptions.DisableFormatting)),source.Group,budget,out expanded,out digest),"unknown blob accepted");
   var duplicate=XElement.Parse(Encoding.UTF8.GetString(metadata));duplicate.Element("blobs").Add(new XElement(duplicate.Element("blobs").Elements().First()));
   Refuses(()=>ExpandHydrationMetadata(Encoding.UTF8.GetBytes(duplicate.ToString(SaveOptions.DisableFormatting)),source.Group,budget,out expanded,out digest),"duplicate blob accepted");
   var oversized=XElement.Parse(Encoding.UTF8.GetString(metadata));oversized.SetAttributeValue("expanded",ExpandedHydrationMetadataMaximum+1);RebirthProductiveEarnedObservationBudget.Last=0;
   Refuses(()=>ExpandHydrationMetadata(Encoding.UTF8.GetBytes(oversized.ToString(SaveOptions.DisableFormatting)),source.Group,budget,out expanded,out digest),"unbounded expansion accepted");
   Check(RebirthProductiveEarnedObservationBudget.Last==0,"invalid expansion requested a reservation");
   RebirthProductiveEarnedObservationBudget.Deny=true;Refuses(()=>ExpandHydrationMetadata(metadata,source.Group,budget,out expanded,out digest),"denied combined budget still expanded");RebirthProductiveEarnedObservationBudget.Deny=false;
  }
  source.Deny=true;Check(!PackHydrationMetadata(source,header,out selector,out metadata),"denied producer reservation still cloned proof");source.Deny=false;
  var small=new XElement("deferredNativeHydration",new XAttribute("group",source.Group.ToString("N")));Check(PackHydrationMetadata(source,small,out selector,out metadata)&&metadata==null,"small original unnecessarily required side metadata");
  Console.WriteLine("PASS actual production metadata pack/expand; native/queue/identity/source qualification NOT covered");
 }
}
