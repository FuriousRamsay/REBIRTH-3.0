// AUTHORED_NOT_RUN. Production attachment lifetime and aggregate supersession.
using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
static partial class RebirthWorkstationCompletionPipeline{
 internal class NativeHostOriginal{internal bool IsRemoteOutputRestart(RemoteOutputHostSource source)=>true;}
 internal class Native{internal bool Current()=>true;}
 internal class RemoteOutputHostSource{internal bool FinalCallbackSaved;internal Native Native=new Native();internal bool Current()=>true;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal class HostContribution:IDisposable{internal object World,Station;internal Guid Operation,SupersededBy;internal XElement ImmutableMembership,NativePhysical;internal int NativePhase=1;internal bool Disposed;internal RebirthWorkstationCompletionPipeline.NativeHostOriginal NativeOriginal=new RebirthWorkstationCompletionPipeline.NativeHostOriginal();public void Dispose(){Disposed=true;if(host.TryGetValue(Operation,out var value)&&ReferenceEquals(value,this))host.Remove(Operation);}}
 static class NativeViewerPds{internal static bool ValidateLoadedDeferredFinal(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,out XElement physical){physical=new XElement("physical");return true;}}
 static readonly Dictionary<Guid,HostContribution> host=new Dictionary<Guid,HostContribution>();
 static Guid ReadGuid(XElement node,string name)=>Guid.ParseExact((string)node.Attribute(name),"N");
 // ACTUAL_ATTACHMENT_LIFETIME
 // ACTUAL_AGGREGATE_SUPERSESSION
 static int checks;static void Need(bool value){if(!value)throw new Exception("loaded routing assertion "+checks);checks++;}
 static HostContribution Make(object world,object station){var value=new HostContribution{World=world,Station=station,Operation=Guid.NewGuid()};host.Add(value.Operation,value);return value;}
 public static void Main(){
  var world=new object();var station=new object();var prior=Make(world,station);var otherWorld=Make(new object(),station);var otherStation=Make(world,new object());var unrelated=Make(world,station);var current=Make(world,station);
  current.ImmutableMembership=new XElement("contribution",new XElement("members",new[]{prior,otherWorld,otherStation,current}.Select(value=>new XElement("member",new XAttribute("operation",value.Operation.ToString("N"))))));
  using(var attachment=new LoadedNativeContribution(current)){Need(prior.SupersededBy==Guid.Empty);}
  Need(current.Disposed&&!host.ContainsKey(current.Operation));Need(prior.SupersededBy==Guid.Empty);
  current.Disposed=false;host.Add(current.Operation,current);var transaction=new LoadedNativeContribution(current);transaction.Commit();
  Need(prior.SupersededBy==current.Operation);Need(otherWorld.SupersededBy==Guid.Empty);Need(otherStation.SupersededBy==Guid.Empty);Need(unrelated.SupersededBy==Guid.Empty);Need(current.SupersededBy==Guid.Empty);
  transaction.Commit();transaction.Dispose();Need(!current.Disposed&&host[current.Operation]==current);Need(prior.SupersededBy==current.Operation);
  var abandoned=new LoadedNativeContribution(current);abandoned.Dispose();bool refused=false;try{abandoned.Commit();}catch(ObjectDisposedException){refused=true;}Need(refused);
  Console.WriteLine("PASS "+checks+" loaded aggregate routing assertions; native source producers excluded");
 }
}
