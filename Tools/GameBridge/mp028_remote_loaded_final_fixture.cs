// AUTHORED_NOT_RUN. Actual loaded-final qualifier; native load, physical
// validator, owner projection and original admission are explicit adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class Recipe{internal string GetName()=>"recipe";}
static class RebirthWorkstationCompletionPipeline{
 internal class NativeHostOriginal{internal Recipe Recipe=new Recipe();internal bool Bound=true;internal bool IsRemoteOutputRestart(RemoteOutputHostSource source)=>Bound;}
 internal class Load{internal bool Valid=true;internal bool Current()=>Valid;}
 internal class RemoteOutputHostSource{internal bool FinalCallbackSaved=true,Valid=true;internal XElement DeferredMembership=new XElement("deferredNativeContribution"),Request;internal byte[] DeferredAddBody={1};internal int DeferredAddOffset=4;internal Load Native=new Load();internal bool Current()=>Valid;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal static bool Physical=true;internal static int PhysicalCalls;
 internal static bool ValidateRemoteNativePhysical(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement contribution,byte[] prior,int priorOffset,byte[] next,int nextOffset,int phase,out XElement physical){
  PhysicalCalls++;physical=new XElement("nativeOwningDelivery");return Physical&&phase==2&&ReferenceEquals(prior,next)&&priorOffset==nextOffset&&
   (int?)contribution.Attribute("phase")==2&&contribution.Element("activeBefore")!=null&&contribution.Element("physicalDelivery")!=null;
 }
 internal static partial class NativeViewerPds{
  class Stage{internal XElement Request,Frame;internal bool Valid=true;internal bool Current()=>Valid;}
  class ViewerSource{internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Original;internal XElement Active=new XElement("nativeOwningBefore"),Delivery=new XElement("nativeOwningDelivery"),DirectEffect;
   internal Stage Before,After;internal bool Valid=true,BeforeInventory=true,Inventory=true;internal bool Current()=>Valid;internal bool BeforeInventoryCurrent()=>BeforeInventory;internal bool InventoryCurrent()=>Inventory;}
  static Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();
  static string Key(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,string generation)=>generation;
  static bool ProjectionCounters(XElement frame,string recipe,out int total,out float crafted){total=(int)frame.Attribute("total");crafted=(float)frame.Attribute("crafted");return true;}
  static int checks;static RebirthWorkstationCompletionPipeline.NativeHostOriginal original;static RebirthWorkstationCompletionPipeline.RemoteOutputHostSource loaded;static ViewerSource source;
  static void Fresh(){sources.Clear();Physical=true;PhysicalCalls=0;original=new RebirthWorkstationCompletionPipeline.NativeHostOriginal();
   loaded=new RebirthWorkstationCompletionPipeline.RemoteOutputHostSource{Request=new XElement("nativeViewer",new XAttribute("generation","generation"))};
   source=new ViewerSource{Original=original,Before=new Stage{Frame=new XElement("nativeOwnerFrame",new XAttribute("total",4),new XAttribute("crafted",2))},After=new Stage{Request=new XElement(loaded.Request),Frame=new XElement("nativeOwnerFrame",new XAttribute("total",4),new XAttribute("crafted",2))}};sources.Add("generation",source);}
  static void Need(bool value){if(!value)throw new Exception("Loaded final qualifier "+checks);checks++;}
  internal static void Run(){
   Fresh();Need(ValidateLoadedDeferredFinal(original,loaded,out var physical)&&physical!=null&&PhysicalCalls==1);
   Fresh();source.After.Frame.SetAttributeValue("total",5);Need(!ValidateLoadedDeferredFinal(original,loaded,out _)&&PhysicalCalls==0);
   Fresh();source.After.Frame.SetAttributeValue("crafted",3);Need(!ValidateLoadedDeferredFinal(original,loaded,out _)&&PhysicalCalls==0);
   Fresh();source.DirectEffect=new XElement("effect");Need(!ValidateLoadedDeferredFinal(original,loaded,out _));
   Fresh();loaded.Native.Valid=false;Need(!ValidateLoadedDeferredFinal(original,loaded,out _));
   Fresh();loaded.DeferredMembership=null;Need(!ValidateLoadedDeferredFinal(original,loaded,out _));
   Fresh();original.Bound=false;Need(!ValidateLoadedDeferredFinal(original,loaded,out _));
   Fresh();Physical=false;Need(!ValidateLoadedDeferredFinal(original,loaded,out _)&&PhysicalCalls==1);
   Fresh();source.Inventory=false;Need(!ValidateLoadedDeferredFinal(original,loaded,out _)&&PhysicalCalls==0);
   Console.WriteLine("PASS "+checks+" actual loaded deferred final qualifier assertions; native adapters excluded");
  }
 }
 public static void Main()=>NativeViewerPds.Run();
}
// ACTUAL_LOADED_FINAL
