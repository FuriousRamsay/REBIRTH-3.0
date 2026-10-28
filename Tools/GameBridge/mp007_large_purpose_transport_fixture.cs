using System;using System.Linq;using System.Collections.Generic;using System.IO;using System.Text;using System.Xml;using System.Xml.Linq;using System.Security.Cryptography;
// Native dequeue/session/world/UI are adapters. Financial purpose decode and
// actual native source/effect/save lifecycle are deliberately not simulated.
class NetPackage{}
static class RebirthProductiveEarnedObservationBudget{private class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long size,out IDisposable memory){memory=new Lease();return true;}}
class GameManager{}
class INetConnection{internal string Key=Guid.NewGuid().ToString("N");}
class EntityPlayer{internal int entityId=7;}
class WorldState{internal string Guid="world";}
class World{internal WorldState worldState=new WorldState();internal EntityPlayer Player=new EntityPlayer();internal EntityPlayer GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>id==Player.entityId?Player:null;}
static class RebirthRemoteResourceNetworkScope{internal static bool Live=true;internal static INetConnection Connection=new INetConnection();internal static bool Receive(NetPackage p,World w,GameManager g,bool server,out INetConnection connection){connection=Connection;return Live;}internal static string SessionKey(INetConnection connection)=>connection.Key;}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int maximum){using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum}))return XElement.Load(reader);}}
// ACTUAL_ASSEMBLER
internal static partial class RebirthConsumptionDurableDelivery
{
 private const int MaximumInventoryPurpose=16*1024*1024+65536;
 // ACTUAL_TRANSPORT
 internal static string Wrap(Guid op,RebirthRemoteResourceFragments.Body body,int part,bool refusal=false)=>PurposeFragment(op,body,part,refusal);
 internal static bool Receive(World world,Guid op,byte phase,string text,bool refusal,out string complete)=>TryReceiveInventoryPurposeFragment(new NetPackage(),world,new GameManager(),7,phase,text,refusal,out complete);
}
static class Program
{
 static int assertions;static World world=new World();static Guid operation;static string text;static RebirthRemoteResourceFragments.Body body;
 static void Require(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Fresh(){operation=Guid.NewGuid();text=new XElement("playerOwnerConsumption",new XAttribute("operation",operation.ToString("N")),new XElement("probe",new string('x',60000))).ToString(SaveOptions.DisableFormatting);body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(text));RebirthRemoteResourceFragments.ClearTransient();RebirthRemoteResourceNetworkScope.Live=true;RebirthRemoteResourceNetworkScope.Connection=new INetConnection();world.Player.entityId=7;}
 static string Send(int part,byte phase=0,bool refusal=false,string changed=null){RebirthConsumptionDurableDelivery.Receive(world,operation,phase,changed??RebirthConsumptionDurableDelivery.Wrap(operation,body,part,refusal),refusal,out var complete);return complete;}
 static void Main()
 {
  Fresh();string complete=null;for(int index=0;index<body.Count;index++)complete=Send(index)??complete;Require(text.Length>48000&&complete==text,"Actual purpose larger than native envelope reassembles unchanged through shared fragment codec");
  Require(Enumerable.Range(0,body.Count).All(index=>Encoding.UTF8.GetByteCount(RebirthConsumptionDurableDelivery.Wrap(operation,body,index))<48000),"Each actual native envelope remains below its unchanged48k wire bound");
  Fresh();RebirthRemoteResourceNetworkScope.Live=false;Require(Enumerable.Range(0,body.Count).All(index=>Send(index)==null),"Stale native receive scope cannot assemble purpose");
  Fresh();Send(0);RebirthRemoteResourceNetworkScope.Connection=new INetConnection();Require(Enumerable.Range(1,body.Count-1).All(index=>Send(index)==null),"Native connection replacement cannot join an earlier fragment prefix");
  Fresh();Send(0);Require(Enumerable.Range(1,body.Count-1).All(index=>Send(index,1)==null),"Different purpose phases cannot join fragment prefixes");
  Fresh();world.Player.entityId=8;Require(Enumerable.Range(0,body.Count).All(index=>Send(index)==null),"Wrong actual owning primary cannot assemble owner purpose");
  Fresh();var image=XElement.Parse(RebirthConsumptionDurableDelivery.Wrap(operation,body,0));image.SetAttributeValue("version",2);Require(Send(0,changed:image.ToString())==null,"Unsupported envelope version has no financial decoder output");
  Fresh();image=XElement.Parse(RebirthConsumptionDurableDelivery.Wrap(operation,body,0));image.SetAttributeValue("total",32*1024*1024);Require(Send(0,changed:image.ToString())==null,"Purpose cannot exceed admitted full original domain");
  Fresh();var other=Guid.NewGuid();complete=null;for(int index=0;index<body.Count;index++){image=XElement.Parse(RebirthConsumptionDurableDelivery.Wrap(operation,body,index));image.SetAttributeValue("operation",other.ToString("N"));complete=Send(index,changed:image.ToString())??complete;}Require(complete==null,"Complete payload operation must equal fragment operation");
  Fresh();complete=null;for(int index=0;index<body.Count;index++){image=XElement.Parse(RebirthConsumptionDurableDelivery.Wrap(operation,body,index));image.SetAttributeValue("hash",new string('0',64));complete=Send(index,changed:image.ToString())??complete;}Require(complete==null,"Changed whole body digest cannot produce purpose");
  Fresh();Require(!RebirthConsumptionDurableDelivery.Receive(world,operation,0,"<unrelated/>",false,out _),"Unrelated purpose retains original envelope dispatcher");
  Fresh();complete=null;for(int index=0;index<body.Count;index++)complete=Send(index,refusal:true)??complete;Require(complete==text,"Bound no-source refusal can carry the same complete rich original without wire truncation");
  Console.WriteLine("Actual large player purpose transport: "+assertions+" assertions passed; native scope/financial decoder/lifecycle remain separate evidence.");
 }
}
