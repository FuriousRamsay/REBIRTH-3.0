using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Reflection;
// Actual cold scanner; native scope/original codec and closed-chain authority
// are explicit adapters. This cohort certifies no production financial grant.
struct RebirthNpcStableId
{
 internal string Value;public static bool operator ==(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Value==b.Value;
 public static bool operator !=(RebirthNpcStableId a,RebirthNpcStableId b)=>!(a==b);
 public override bool Equals(object o)=>o is RebirthNpcStableId id&&this==id;public override int GetHashCode()=>Value?.GetHashCode()??0;
}
class World{internal State worldState=new State();internal bool IsRemote()=>false;internal class State{internal string Guid=System.Guid.NewGuid().ToString();}}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class RebirthNpcSaveQuarantineScope{internal World World;internal string Directory;}
static class RebirthNpcSaveQuarantineIds{internal static void Add(HashSet<RebirthNpcStableId> ids,RebirthNpcStableId npc){ids.Add(npc);}}
// ACTUAL_QUARANTINE_DOMAIN
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
class RebirthNpcTradeOriginal
{
 internal Guid Operation;internal string World,Account,Digest="digest",Image;internal RebirthNpcStableId Npc;
 internal Quote CopyQuote()=>new Quote{NpcId=Npc};internal class Quote{internal RebirthNpcStableId NpcId;}
 internal static RebirthNpcTradeOriginal Read(string value){var p=value.Split('|');return new RebirthNpcTradeOriginal{Operation=Guid.Parse(p[0]),World=p[1],Account=p[2],Npc=new RebirthNpcStableId{Value=p[3]},Image=value};}
}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string value)=>new string(value=="account"?'a':'b',64);}
class RebirthNpcTradeClosedHistory
{
 internal Guid Operation;
 internal static bool IsClosedPreparation(string path,RebirthNpcTradeOriginal o,string image)=>File.ReadAllText(path+".closed")=="positive";
}
static class RebirthNpcTradeService{internal class HostClosed{internal bool CanRetireFromQuote;internal Prepared Prepared;}internal class Prepared{internal RebirthNpcTradeOriginal Original;}}
// ACTUAL_COLD_FENCE
static class Program
{
 static int checks,saveSequence;static string root;
 static RebirthNpcStableId npc=new RebirthNpcStableId{Value="npc"},other=new RebirthNpcStableId{Value="other"};
 static void Check(bool value){checks++;if(!value)throw new Exception("Cold fence source assertion "+checks);}
 static void Reset()
 {
  var type=typeof(RebirthNpcTradeColdFence);var flags=BindingFlags.Static|BindingFlags.NonPublic;
  foreach(string name in new[]{"boundWorld","boundDirectory"})type.GetField(name,flags).SetValue(null,null);
  foreach(string name in new[]{"scanned","unknown"})type.GetField(name,flags).SetValue(null,false);
  ((Dictionary<Guid,RebirthNpcStableId>)type.GetField("pending",flags).GetValue(null)).Clear();
  GameIO.Root=Path.Combine(root,(++saveSequence).ToString());Directory.CreateDirectory(GameIO.Root);GameManager.Instance.World=new World();
 }
 static string Write(Guid operation,string body=null)
 {
  string world=GameManager.Instance.World.worldState.Guid;
  string directory=Path.Combine(GameIO.Root,"RebirthData","NpcTradeOriginals",Guid.Parse(world).ToString("N"),new string('a',64));Directory.CreateDirectory(directory);
  string image=operation.ToString("N")+"|"+world+"|account|npc";
  string value=body??new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest","digest"),new XElement("original",image),new XElement("savedNpcBefore","before")).ToString(SaveOptions.DisableFormatting);
  string path=Path.Combine(directory,operation.ToString("N")+".xml");File.WriteAllText(path,value,new UTF8Encoding(false));return path;
 }
 static void Main()
 {
  root=Path.Combine(Path.GetTempPath(),"rh-cold-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try
  {
   Reset();Check(!RebirthNpcTradeColdFence.Blocks(npc));
   Reset();var id=Guid.NewGuid();Write(id);Check(RebirthNpcTradeColdFence.Blocks(npc));Check(!RebirthNpcTradeColdFence.Blocks(other));
   GameManager.Instance.World=new World();Check(RebirthNpcTradeColdFence.Blocks(other));
   Reset();string path=Write(Guid.NewGuid());File.WriteAllText(path+".closed","positive");Check(!RebirthNpcTradeColdFence.Blocks(npc));
   Reset();path=Write(Guid.NewGuid());File.WriteAllText(path+".closed","partial");Check(RebirthNpcTradeColdFence.Blocks(npc));
   Reset();Write(Guid.NewGuid(),"partial");Check(RebirthNpcTradeColdFence.Blocks(other));
   Reset();for(int i=0;i<5;i++)Write(Guid.NewGuid());Check(RebirthNpcTradeColdFence.Blocks(other));
   Reset();path=Write(Guid.NewGuid());File.WriteAllText(path+".closed",new string('x',4097));Check(RebirthNpcTradeColdFence.Blocks(other));
   Reset();id=Guid.NewGuid();Write(id);Check(RebirthNpcTradeColdFence.Blocks(npc));
   RebirthNpcTradeColdFence.ObserveClosed(new RebirthNpcTradeService.HostClosed{CanRetireFromQuote=false,Prepared=new RebirthNpcTradeService.Prepared{Original=new RebirthNpcTradeOriginal{Operation=id}}});Check(RebirthNpcTradeColdFence.Blocks(npc));
   RebirthNpcTradeColdFence.ObserveClosed(new RebirthNpcTradeService.HostClosed{CanRetireFromQuote=true,Prepared=new RebirthNpcTradeService.Prepared{Original=new RebirthNpcTradeOriginal{Operation=id}}});Check(!RebirthNpcTradeColdFence.Blocks(npc));
   Reset();Write(Guid.NewGuid());Check(RebirthNpcTradeColdFence.Blocks(npc));
   var ids=new HashSet<RebirthNpcStableId>();RebirthNpcTradeColdFence.AppendQuarantineIds(ids);Check(ids.Contains(npc));
   Check(!RebirthNpcTradeColdFence.CanBeginFreshGeneration(GameManager.Instance.World));
   var domain=RebirthNpcTradeColdFence.PrepareQuarantine(new RebirthNpcSaveQuarantineScope{World=GameManager.Instance.World,Directory=GameIO.Root});
   Check(domain.IsCurrent);domain.Detach();Check(domain.IsDetached&&!domain.IsCurrent);domain.Rollback();Check(domain.IsCurrent&&RebirthNpcTradeColdFence.Blocks(npc));
   Console.WriteLine(checks+" actual cold scanner assertions passed; native/closed-chain adapters NOT certified");
  }
  finally{Directory.Delete(root,true);}
 }
}
