public struct RebirthNpcStableId
{
 public string Text;public bool IsEmpty=>string.IsNullOrEmpty(Text);public override string ToString()=>Text;public static bool TryParse(string text,out RebirthNpcStableId id){id=new RebirthNpcStableId{Text=text};return text?.Length==32;}
}
public static class RebirthSurvivorRequestScope{public static bool TryNormalize(string text,out string normalized){normalized=text;return text=="creation";}}
public static class RebirthOriginalActionScope{public static bool Validate(string owner,string creation)=>creation=="creation";} // Identity codec is explicitly doubled.
public sealed class ItemStack{public int count,Quality;}
internal sealed class RebirthNpcNativeStackRecord
{
 internal Guid Id;internal int Count,Quality;
 internal static bool TryCapture(Guid id,RebirthNpcStableId owner,ItemStack stack,out RebirthNpcNativeStackRecord value){value=new RebirthNpcNativeStackRecord{Id=id,Count=stack.count,Quality=stack.Quality};return true;}
 internal static bool TryRead(XElement node,out RebirthNpcNativeStackRecord value){value=new RebirthNpcNativeStackRecord{Id=Guid.Parse((string)node.Attribute("id")),Count=(int)node.Attribute("count"),Quality=(int)node.Attribute("quality")};return true;}
 internal XElement Write()=>new XElement("stack",new XAttribute("id",Id),new XAttribute("count",Count),new XAttribute("quality",Quality));
}
internal sealed class RebirthNpcNativeStackSet
{
 internal string Owner;internal uint Revision;internal List<RebirthNpcNativeStackRecord> Records=new List<RebirthNpcNativeStackRecord>();
 internal bool FitsQuantities(Dictionary<string,int> quantities)=>true;
 internal int GetQuantity(string key)=>0;
 internal XElement Write()=>new XElement("native",new XAttribute("revision",Revision),Records.Select(value=>value.Write()));
 internal RebirthNpcNativeStackSet WithRevision(uint revision)=>new RebirthNpcNativeStackSet{Revision=revision,Records=new List<RebirthNpcNativeStackRecord>(Records)};
 internal static bool TryCreate(RebirthNpcStableId id,uint revision,IEnumerable<RebirthNpcNativeStackRecord> records,out RebirthNpcNativeStackSet value){value=new RebirthNpcNativeStackSet{Revision=revision,Records=records.ToList()};return true;}
 internal static bool TryRead(XElement node,out RebirthNpcNativeStackSet value){value=new RebirthNpcNativeStackSet{Revision=(uint)node.Attribute("revision")};foreach(var child in node.Elements()){RebirthNpcNativeStackRecord.TryRead(child,out var item);value.Records.Add(item);}return true;}
}
internal static class RebirthLogisticsTransferJournal
{
 internal sealed class NpcEntry{internal string Key;internal int Before,After,PendingNative;}
 internal sealed class NpcCredit{internal Guid StackId;internal ItemStack Stack;}
 internal sealed class NpcLeg{internal string StableId,NativeBefore;internal uint Revision;internal Guid DebitId,CreditId;internal List<NpcEntry> Entries=new List<NpcEntry>();internal List<NpcCredit> NativeCredits=new List<NpcCredit>();}
 internal sealed class Record{internal Guid Operation;internal string World=new string('1',32),Owner=new string('a',64),Creation="creation";internal int Deaths,Phase;internal List<NpcLeg> Npcs=new List<NpcLeg>();}
 internal static Record Saved;internal static string ImmutablePlanDigest(Record record)=>new string('b',64);
 internal static bool TryRead(string owner,Guid operation,out Record record){record=Saved;return record!=null&&record.Operation==operation&&record.Owner==owner;}
}
internal sealed class RebirthRemoteResourceNpcEffectPlan
{
 internal Guid Operation,MutationId;internal RebirthNpcStableId NpcId;internal string World=new string('c',64),Owner=new string('a',64),Creation="creation",Binding=new string('d',64),BeforeNative="",AfterNative="";
 internal int Deaths;internal uint Revision;internal bool DogStorage;internal IReadOnlyDictionary<string,int> BeforeQuantities=new Dictionary<string,int>(),AfterQuantities=new Dictionary<string,int>();
}
public static class Fixture
{
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Main()
 {
  var id=new RebirthNpcStableId{Text=new string('2',32)};var record=new RebirthLogisticsTransferJournal.Record{Operation=Guid.NewGuid()};
  var leg=new RebirthLogisticsTransferJournal.NpcLeg{StableId=id.ToString(),Revision=7,DebitId=Guid.NewGuid(),CreditId=Guid.NewGuid()};leg.Entries.Add(new RebirthLogisticsTransferJournal.NpcEntry{Key="Ammo",Before=5,After=6,PendingNative=3});leg.NativeCredits.Add(new RebirthLogisticsTransferJournal.NpcCredit{StackId=Guid.NewGuid(),Stack=new ItemStack{count=3,Quality=7}});record.Npcs.Add(leg);
  string debit=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,false),credit=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,true);
  Check(RebirthLogisticsNpcEffectReceipt.TryRead(debit,out var d)&&d.Phase==1&&d.Revision==8&&d.Transaction==leg.DebitId,"exact typed original debit");
  Check(RebirthLogisticsNpcEffectReceipt.TryRead(credit,out var c)&&c.Phase==2&&c.Revision==9&&c.NativeAfter!=c.NativeBefore,"credit native custody bound");
  string continued=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,true,14);
  Check(RebirthLogisticsNpcEffectReceipt.MatchesLogistics(record,leg,true,continued)&&RebirthLogisticsNpcEffectReceipt.TryRead(continued,out var continuedEffect)&&continuedEffect.Revision==14&&continuedEffect.Before==c.Before&&continuedEffect.After==c.After&&continuedEffect.NativeAfter==c.NativeAfter,"continued actual revision retains exact original positive delta binding");
  bool oldRevisionRefused=false;try{RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,true,8);}catch(InvalidDataException){oldRevisionRefused=true;}Check(oldRevisionRefused,"continued credit cannot precede original paid debit");
  Check(!RebirthLogisticsNpcEffectReceipt.MatchesLogistics(record,leg,false,continued),"continued credit is never a debit receipt");
  leg.NativeCredits[0].Stack.Quality=8;Check(RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,true)!=credit,"native quality changes receipt");leg.NativeCredits[0].Stack.Quality=7;
  Check(!RebirthLogisticsNpcEffectReceipt.TryRead(debit.Replace("domain=\"logistics\"","domain=\"arbitrary\""),out _),"domain allowlist");
  Check(!RebirthLogisticsNpcEffectReceipt.TryRead("<!DOCTYPE x [<!ENTITY e 'x'>]>"+debit,out _),"DTD refused");
  Check(!RebirthLogisticsNpcEffectReceipt.TryRead(debit.Replace("version=\"1\"","version=\"2\""),out _),"unknown schema refused");
  var effects=new Dictionary<Guid,string>{{leg.DebitId,debit},{leg.CreditId,credit}};
  Check(RebirthLogisticsNpcEffectReceipt.Validate(id,14,effects),"actual original effect survives later revision");
  Check(!RebirthLogisticsNpcEffectReceipt.Validate(id,8,effects),"future effect cannot precede applied revision");
  Check(RebirthLogisticsNpcEffectReceipt.Validate(id,0,null),"legacy absent effect collection");
  Check(!RebirthLogisticsNpcEffectReceipt.Validate(new RebirthNpcStableId{Text=new string('3',32)},14,effects),"receipt owner NPC bound");
  record.Phase=3;RebirthLogisticsTransferJournal.Saved=record;Check(!RebirthLogisticsNpcEffectReceipt.IsSavedTerminalLogistics(debit),"volatile unfinished outcome cannot prune");record.Phase=4;Check(RebirthLogisticsNpcEffectReceipt.IsSavedTerminalLogistics(debit),"positive exact terminal original may prune");
  Check(RebirthLogisticsNpcEffectReceipt.IsSavedTerminalLogistics(continued),"saved terminal original recognizes exact continued delta receipt");
  var remote=new RebirthRemoteResourceNpcEffectPlan{Operation=Guid.NewGuid(),MutationId=Guid.NewGuid(),NpcId=id,Revision=10};
  string fetch=RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(remote);Check(RebirthLogisticsNpcEffectReceipt.TryRead(fetch,out var f)&&f.Domain=="remoteResource"&&f.World.Length==64,"fetch separate native world identity contract");
  remote.World=new string('1',32);bool refused=false;try{RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(remote);}catch(InvalidDataException){refused=true;}Check(refused,"fetch world64 never confused with logistics GUID32");
  Console.WriteLine("PASS actual known-domain NPC effect receipt codec (native custody/journal-reader doubles)");
 }
}
