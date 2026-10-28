// AUTHORED_NOT_RUN. Durable source/native owner services are explicit adapters.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
class World{internal bool Remote;internal State worldState=new State();internal bool IsRemote()=>Remote;internal class State{internal string Guid="world";}}
class EntityPlayerLocal{internal World world=new World();internal int Died=2;}
class ItemStack{internal int count=3;internal string Rich="source";internal bool IsEmpty()=>count==0;}
class RebirthStablePlayerIdentity{internal string CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity value){value=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int limit)=>XElement.Parse(text);}
static class RebirthCamperInventorySnapshot
{
 internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join("|",items.Select(i=>i.count+":"+i.Rich)));
 internal static ItemStack[] Restore(byte[] bytes)=>Encoding.UTF8.GetString(bytes).Split('|').Where(s=>s.Length>0).Select(s=>new ItemStack{count=int.Parse(s.Split(':')[0]),Rich=s.Split(':')[1]}).ToArray();
}
enum RemoteResourceClientOperation{CookingPull}
static class RebirthRemoteResourceDurableOutcome{internal static bool IsRestoredCookingRefusal(RebirthRemoteResourceDurableJournal.Record record)=>false;}
static class RebirthCookingCatalogue{internal static bool IsIngredient(ItemStack item)=>item.Rich!="foreign";}
static class RebirthRemoteResourceDurableJournal
{
 internal sealed class Record{internal int Phase=2;internal string World="source-world",Binding="source-binding";internal Guid Operation=Guid.NewGuid();internal XElement Root;internal XElement Copy()=>new XElement(Root);}
 internal static string Digest(string image)=>"digest:"+image;
}
static class RebirthRemoteResourceDurableDispatch
{
 internal static RebirthRemoteResourceDurableJournal.Record Outcome;internal static int Resolves,Metadata,Replay;internal static bool Known,Resolved=true;internal static void Install(){}
 internal static bool TryReadCookingMetadata(EntityPlayerLocal player,long request,IList<ItemStack> needs,out RebirthRemoteResourceDurableJournal.Record result){Metadata++;result=Outcome;return Known;}
 internal static bool TryReadCookingReplay(EntityPlayerLocal player,long request,IList<ItemStack> needs,out RebirthRemoteResourceDurableJournal.Record result){Replay++;result=Outcome;return Known;}
 internal static bool TryReadCookingTerminal(EntityPlayerLocal player,long request,IList<ItemStack> needs,out RebirthRemoteResourceDurableJournal.Record result){result=Outcome;return Known&&needs.Count==1&&needs[0].Rich=="source";}
 internal static bool Resolve(EntityPlayerLocal player,ulong epoch,ulong request,RemoteResourceClientOperation kind,IList<ItemStack> needs,bool query,out RebirthRemoteResourceDurableJournal.Record result){if(epoch!=1||query)throw new Exception("wrong original source route");Resolves++;result=Outcome;return Resolved;}
}
// ACTUAL_LOCAL_ORIGINAL
public static partial class RebirthCookingPull
{
 private sealed class Original{internal EntityPlayerLocal Player=new EntityPlayerLocal();internal World World;internal string Owner,Creation;internal bool MetadataOnly;internal int Deaths=2;internal long Request=17;internal List<ItemStack> Needs=new List<ItemStack>{new ItemStack()};}
 private sealed class RequestRecord{internal string OwnerCreation="creation";internal byte[] Before,After;}
 private sealed class Journal{internal RequestRecord Record;internal RequestRecord Read(Guid operation)=>Record;}
 private static Journal requests;private static bool Scope=true;private static int Replies;private static string LastChild,LastHash,LastDisposition;private static bool LastTerminal,LastNoPayment;private static List<ItemStack> LastItems;
 private static bool CurrentPullSession(Original original)=>Scope;
 private static bool CurrentDeliverySession(Original original)=>Scope&&!original.MetadataOnly&&original.Player.Died==original.Deaths;
 private static Guid Operation(EntityPlayerLocal player,string owner,long request)=>Guid.Empty;
 private static Journal Requests()=>requests;
 private static void Receive(long id,List<ItemStack> items,string reason,string world,string operation,string binding,bool terminal,bool noPayment,string child,string hash,string proof,string disposition)
 {Replies++;LastItems=items;LastChild=child;LastHash=hash;LastTerminal=terminal;LastNoPayment=noPayment;LastDisposition=disposition;}
 private static Original Setup()
 {
  Scope=true;Replies=0;RebirthRemoteResourceDurableDispatch.Resolves=RebirthRemoteResourceDurableDispatch.Metadata=RebirthRemoteResourceDurableDispatch.Replay=0;RebirthRemoteResourceDurableDispatch.Known=false;RebirthRemoteResourceDurableDispatch.Resolved=true;
  var original=new Original();original.World=original.Player.world;
  var source=new XElement("source",new XAttribute("attempted",1));
  RebirthRemoteResourceDurableDispatch.Outcome=new RebirthRemoteResourceDurableJournal.Record{Root=new XElement("outcome",new XAttribute("outcome",1),new XAttribute("ownerProof","saved-owner"),new XAttribute("disposition","grid-owned"),new XElement("removed",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Needs.ToArray()))),new XElement("reason",""),new XElement("sources",source))};
  var root=new XElement("cookingPullOriginal",new XAttribute("request",17),new XAttribute("world","world"),new XAttribute("owner","owner"),new XElement("needs",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Needs.ToArray()))));
  byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));requests=new Journal{Record=new RequestRecord{Before=bytes,After=bytes}};return original;
 }
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 internal static void Exercise()
 {
  var original=Setup();SendLocalOriginal(original);Need(RebirthRemoteResourceDurableDispatch.Resolves==1&&Replies==1&&LastItems[0].count==3&&!LastTerminal,"local source result uses existing durable admission and typed receive");
  original=Setup();original.MetadataOnly=true;SendLocalOriginal(original);Need(RebirthRemoteResourceDurableDispatch.Metadata==1&&RebirthRemoteResourceDurableDispatch.Resolves==0&&Replies==0,"metadata-only local query cannot withdraw sources");
  original=Setup();original.Player.Died++;SendLocalOriginal(original);Need(RebirthRemoteResourceDurableDispatch.Resolves==0&&Replies==0,"ended life cannot start local source withdrawal");
  original=Setup();Scope=false;SendLocalOriginal(original);Need(RebirthRemoteResourceDurableDispatch.Resolves==0&&Replies==0,"stale owner scope cannot dispatch local original");
  original=Setup();original.Needs[0].Rich="foreign";SendLocalOriginal(original);Need(RebirthRemoteResourceDurableDispatch.Resolves==0&&Replies==0,"invalid cooking material cannot create source admission");
  original=Setup();RebirthRemoteResourceDurableDispatch.Known=true;var outcome=RebirthRemoteResourceDurableDispatch.Outcome;outcome.Phase=4;string child=Guid.NewGuid().ToString("N");var action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child),new XElement("original","immutable"));outcome.Root.Add(action);
  SendLocalOriginal(original);Need(Replies==1&&LastTerminal&&LastItems.Count==0&&LastChild==child&&LastDisposition=="grid-owned"&&LastHash==RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting)),"local terminal carries original child/body without a second item delivery");
  Need(MatchesLocalTerminal(original.Player,17,outcome.World,outcome.Operation.ToString("N"),outcome.Binding,child,LastHash,"saved-owner","grid-owned"),"actual saved host terminal independently qualifies local child");
  Need(!MatchesLocalTerminal(original.Player,17,outcome.World,outcome.Operation.ToString("N"),"foreign",child,LastHash,"saved-owner","grid-owned"),"changed source binding cannot qualify local terminal");
  outcome.Phase=2;Need(!MatchesLocalTerminal(original.Player,17,outcome.World,outcome.Operation.ToString("N"),outcome.Binding,child,LastHash,"saved-owner","grid-owned"),"nonterminal source cannot qualify local paid metadata");
  Console.WriteLine("PASS actual host-local cooking original dispatch/terminal: 9 assertions; native saved source service remains separately qualified");
 }
}
static class Program{static void Main()=>RebirthCookingPull.Exercise();}
