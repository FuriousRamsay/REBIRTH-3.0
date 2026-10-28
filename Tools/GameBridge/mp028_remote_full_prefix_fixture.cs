// AUTHORED_NOT_RUN. Entire production unsent full-prefix supersession member.
// Native completion/current capability, saved XML and packet lease are adapters.
using System;using System.Linq;using System.Xml.Linq;
static class RebirthProductiveEarnedUiRemote{internal static byte[] ReadGrid(XElement value,string name)=>new byte[int.Parse(value.Element(name).Value)];}
static class RebirthWorkstationCompletionPipeline{
 class Confirmed{internal XElement Delivery=new XElement("nativeOwningDelivery",new XAttribute("sink","Inventory"),new XElement("extrasNative",1));internal XElement Write()=>new XElement("workstationCompletionOriginal",new XElement("delivery",new XElement(Delivery)));}
 class Observation{internal Guid Operation=Guid.NewGuid();internal string WorldGuid="world";}
 class Frame{internal Confirmed ConfirmedOriginal=new Confirmed();internal Observation Observation=new Observation();internal string Path="original";}
 class Prefix{internal int Count=2;internal bool Valid=true;internal bool Current()=>Valid;internal XElement CopyEvidence()=>new XElement("nativeOutputPrefix",new XAttribute("count",Count));}
 class ViewerClientStage{internal string Digest="request";internal bool Sent,Saved;internal int Releases;internal void ReleasePacket(){Releases++;}}
 class ViewerOutputPrefix{internal bool HeaderSent,SupersededByFinal;internal Prefix Prefix=new Prefix();internal ViewerClientStage Stage=new ViewerClientStage();}
 class NativeViewerClient{internal Frame Frame=new Frame();internal bool Valid=true;internal string Generation="generation";internal bool Current()=>Valid;}
 static bool Publish=true;static XElement Saved;static int checks;
 static bool SaveXml(string path,XElement image){if(!Publish)return false;Saved=new XElement(image);return true;}
 static void Need(bool value,string text){if(!value)throw new Exception(text);checks++;}
 // ACTUAL_SUPERSESSION
 static void Main(){
  var source=new NativeViewerClient();var prefix=new ViewerOutputPrefix();Need(TrySupersedeUnsentFullPrefix(source,prefix)&&prefix.SupersededByFinal&&prefix.Stage.Releases==1&&!prefix.Stage.Saved,"complete native callback permits unused packet release without fake Saved");
  Need(Saved.Element("before").Element("nativeViewerPrefixSupersededByFinal").Element("nativeOutputPrefix")!=null,"exact accepted evidence persisted before release");
  source=new NativeViewerClient();prefix=new ViewerOutputPrefix{HeaderSent=true};Need(!TrySupersedeUnsentFullPrefix(source,prefix)&&prefix.Stage.Releases==0,"header-sent prefix conserved");
  prefix=new ViewerOutputPrefix();prefix.Stage.Sent=true;Need(!TrySupersedeUnsentFullPrefix(source,prefix)&&prefix.Stage.Releases==0,"native-sent prefix conserved");
  prefix=new ViewerOutputPrefix();prefix.Prefix.Count=1;Need(!TrySupersedeUnsentFullPrefix(source,prefix),"partial accepted prefix conserved");
  prefix=new ViewerOutputPrefix();source.Frame.ConfirmedOriginal=null;Need(!TrySupersedeUnsentFullPrefix(source,prefix),"unknown callback cannot retire prefix");
  source=new NativeViewerClient{Valid=false};Need(!TrySupersedeUnsentFullPrefix(source,prefix),"stale native source refused");
  source=new NativeViewerClient();prefix.Prefix.Valid=false;Need(!TrySupersedeUnsentFullPrefix(source,prefix),"stale original prefix refused");
  prefix=new ViewerOutputPrefix();Publish=false;Need(!TrySupersedeUnsentFullPrefix(source,prefix)&&prefix.Stage.Releases==0&&!prefix.SupersededByFinal,"failed immutable receipt retains native packet");
  Publish=true;source.Frame.ConfirmedOriginal.Delivery.SetAttributeValue("sink","Output");Need(!TrySupersedeUnsentFullPrefix(source,prefix),"different sink refused");
  Console.WriteLine("PASS "+checks+" actual native full-prefix ordering assertions");
 }
}
