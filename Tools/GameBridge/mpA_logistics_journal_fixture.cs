// Actual production journal; disclosed world/creation/native item codec and
// durable-publication doubles. Does not certify engine disk scheduling/network.
public enum QuickStackRadialAction:byte {Deposit=0,PullDroneCredits=20}
public enum RemoteResourceSourceKind:byte {StaticContainer=1,WorkstationOutput=2,VehicleStorage=3,DroneStorage=4,NpcStorage=5}
public sealed class ItemValue {public int type=1;public ItemClass ItemClass=new ItemClass();public bool IsEmpty()=>type==0;}
public sealed class ItemClass {public string Name="ammo";public Number Stacknumber=new Number();public static ItemClass GetForId(int type)=>new ItemClass();}
public sealed class Number {public int Value=10;}
public sealed class ItemStack {public int count;public ItemValue itemValue=new ItemValue();public static ItemStack Empty=>new ItemStack();public bool IsEmpty()=>count==0;public ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue};public static ItemStack[] Clone(ItemStack[] items)=>System.Array.ConvertAll(items,item=>item.Clone());public bool CanStackPartlyWith(ItemStack other,out int amount){amount=System.Math.Min(10-count,other.count);return itemValue.type==other.itemValue.type&&amount>0;}}
public static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.type==b.itemValue.type;}
public static class RebirthCamperInventorySnapshot {
 public static byte[] Capture(ItemStack[] slots){using(var s=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(s)){w.Write(slots.Length);foreach(var item in slots)w.Write(item.count);w.Flush();return s.ToArray();}}
 public static ItemStack[] Restore(byte[] bytes){using(var s=new System.IO.MemoryStream(bytes))using(var r=new System.IO.BinaryReader(s)){int n=r.ReadInt32();if(n<0||n>256)throw new System.IO.InvalidDataException();var slots=new ItemStack[n];for(int i=0;i<n;i++)slots[i]=new ItemStack{count=r.ReadInt32()};if(s.Position!=s.Length)throw new System.IO.InvalidDataException();return slots;}}
}
public static class RebirthSurvivorRequestScope {public static bool TryNormalize(string value,out string normalized){normalized=value;return value=="creation";}}
public static class RebirthOriginalActionScope {public static bool Validate(string owner,string creation)=>creation=="creation";} // Native/survivor identity validation is a disclosed separate dependency.
public static class ThreadManager {public static bool IsMainThread()=>true;}
// Native original-effect transition admission is outside this journal cohort.
internal static class RebirthNpcOriginalEffectAdmission{internal sealed class Pending{internal string Owner;internal System.Guid Operation;}internal static Pending Logistics(RebirthLogisticsTransferJournal.Record original)=>throw new System.InvalidOperationException("Transition claim admission excluded from journal cohort");}
public sealed class World {public string Guid="11111111111111111111111111111111";public bool IsRemote()=>false;}
public enum EnumGamePrefs {GameGuidClient}
public static class GamePrefs {public static string GetString(EnumGamePrefs pref)=>"11111111111111111111111111111111";}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public static class GameIO {public static string Directory;public static string GetSaveGameDir()=>Directory;}
public static class RebirthDurableFileCommit {public static bool TryPublish(string a,string b,out string error){error=null;if(System.IO.File.Exists(b))System.IO.File.Replace(a,b,b+".bak");else System.IO.File.Move(a,b);return true;}}
public static class Log {public static void Warning(string text){System.Console.WriteLine(text);}}
// Native custody set parsing is outside this fixture; no NativeBefore is supplied.
internal sealed class RebirthNpcNativeStackSet {
 internal const int MaximumRecords=4096;internal string Owner;internal uint Revision;
 internal int GetQuantity(string key)=>0;internal bool FitsQuantities(System.Collections.Generic.Dictionary<string,int> values)=>false;
 internal System.Xml.Linq.XElement Write()=>null;
 internal static bool TryRead(System.Xml.Linq.XElement node,out RebirthNpcNativeStackSet value){value=null;return false;}
}
public static class MpALogisticsJournalFixture {
 static void Check(bool condition,string label){if(!condition)throw new System.Exception(label);}
 public static void Main(string[] args){
  GameIO.Directory=args[0];var record=new RebirthLogisticsTransferJournal.Record{Operation=System.Guid.NewGuid(),World="11111111111111111111111111111111",Owner=new string('a',64),Creation="creation",Epoch=7,Request=8,Deaths=0,Action=QuickStackRadialAction.Deposit,OriginalOwnerBag=new[]{new ItemStack{count=4}},OriginalOwnerBelt=new[]{ItemStack.Empty.Clone()},BeforeOwner=new[]{new ItemStack{count=4}},DebitedOwner=new[]{new ItemStack{count=2}},AfterOwner=new[]{new ItemStack{count=2}}};
  record.Legs.Add(new RebirthLogisticsTransferJournal.Leg{Id="S:1,2,3",NativeIdentity="original-native-entity",Kind=RemoteResourceSourceKind.StaticContainer,Before=new[]{new ItemStack{count=1}},After=new[]{new ItemStack{count=3}},Locks=new[]{false}});
  string original=RebirthLogisticsTransferJournal.Encode(record);RebirthLogisticsTransferJournal.Record decoded;
  Check(RebirthLogisticsTransferJournal.TryDecode(original,out decoded),"canonical roundtrip");
  Check(!RebirthLogisticsTransferJournal.TryDecode(original.Replace("version=\"2\"","version=\"3\""),out decoded),"version refusal");
  Check(!RebirthLogisticsTransferJournal.TryDecode(original.Replace("<target", "<unknown"),out decoded),"unknown schema refusal");
  record.Target="target\u001fsource";record.Selection="\u001eREBIRTH_LEGACY_OUTPUTS_ALL\u001e\u001fitem";
  Check(RebirthLogisticsTransferJournal.TryDecode(RebirthLogisticsTransferJournal.Encode(record),out decoded)&&decoded.Target==record.Target&&decoded.Selection==record.Selection,"native control separators canonical base64 roundtrip");
  record.Target=null;record.Selection=null;
  var legacy=System.Xml.Linq.XElement.Parse(original);legacy.SetAttributeValue("version",1);legacy.Element("target").Value="legacy";legacy.Element("selection").Value="safe";
  Check(RebirthLogisticsTransferJournal.TryDecode(legacy.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),out decoded)&&decoded.Target=="legacy"&&decoded.Selection=="safe","explicit legacy safe text reader");
  Check(!RebirthLogisticsTransferJournal.TryPublish(record,()=>false),"expired scope cannot publish");
  Check(RebirthLogisticsTransferJournal.TryPublish(record,()=>true),"original prepared intent published");
  record.AfterOwner[0].count=1;Check(!RebirthLogisticsTransferJournal.TryPublish(record,()=>true),"same operation never replans owner destinations");record.AfterOwner[0].count=2;
  record.Phase=2;Check(!RebirthLogisticsTransferJournal.TryPublish(record,()=>true),"cannot skip owner evidence phase");record.Phase=1;
  Check(RebirthLogisticsTransferJournal.TryPublish(record,()=>true),"next phase published");record.Phase=0;
  Check(!RebirthLogisticsTransferJournal.TryPublish(record,()=>true),"cannot regress attempted original receipt");
  Check(RebirthLogisticsTransferJournal.TryRead(record.Owner,record.Operation,out decoded)&&decoded.Phase==1,"final original record survived failed replacements");
  Check(RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(GameManager.Instance.World,out var pendingOriginals)&&pendingOriginals.Length==1,"cold native holds include primary original with validated native backup");
  decoded.OwnerDebitProof=new string('b',64);decoded.Legs[0].Attempted=1;decoded.Legs[0].Published=1;
  Check(RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"original native debit publication retained");
  decoded.Outcome=2;decoded.Phase=2;Check(!RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"abort needs every original native restoration");decoded.Outcome=0;decoded.Phase=1;
  decoded.Legs[0].Restored=true;Check(RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"original source restoration retained");
  decoded.Legs[0].Published=2;Check(!RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"restored source cannot resume credit");decoded.Legs[0].Published=1;
  decoded.Outcome=2;decoded.Phase=2;Check(RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"zero NPC effect abort outcome retained");
  decoded.Outcome=1;Check(!RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"abort cannot switch to commit");
  Check(RebirthLogisticsTransferJournal.TryRead(record.Owner,record.Operation,out decoded)&&decoded.Outcome==2&&decoded.BeforeOwner[0].count==4&&decoded.AfterOwner[0].count==2,"abort keeps original before and intended commit images immutable");
  decoded.Phase=3;Check(RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"owner restored saved phase");decoded.Phase=4;Check(RebirthLogisticsTransferJournal.TryPublish(decoded,()=>true),"original terminal archive");
  Check(RebirthLogisticsTransferJournal.TryRead(record.Owner,record.Operation,out decoded)&&decoded.Phase==4,"exact terminal lookup resolves archived original");
  Check(RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(GameManager.Instance.World,out pendingOriginals)&&pendingOriginals.Length==0,"terminal history does not consume future pending native budget");
  var unpaid=RebirthLogisticsTransferJournal.TryDecode(original,out var frozen)?frozen:null;Check(unpaid!=null,"unpaid original decode");
  unpaid.Operation=System.Guid.NewGuid();Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"unpaid original retained");unpaid.Phase=1;Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"unpaid original prepared permission boundary");
  unpaid.Rejecting=true;Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"original rejection survives restart before freeze request");
  unpaid.Rejecting=false;Check(!RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"rejected original cannot resume permission");unpaid.Rejecting=true;
  unpaid.Phase=2;unpaid.Outcome=3;
  Check(!RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"prepared phase alone never proves no debit");
  unpaid.OwnerNoEffectProof=new string('d',64);Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"positive frozen native phase5 proof admits no-effect outcome");
  unpaid.OwnerDebitProof=new string('e',64);Check(!RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"no-effect cannot also claim owner payment");unpaid.OwnerDebitProof=null;
  unpaid.Legs[0].Attempted=1;Check(!RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"no-effect forbids any source attempt");unpaid.Legs[0].Attempted=0;
  unpaid.OwnerNoEffectProof=new string('e',64);Check(!RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"frozen no-effect witness immutable");unpaid.OwnerNoEffectProof=new string('d',64);
  unpaid.Phase=3;Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"no-effect acknowledgement retained");unpaid.Phase=4;Check(RebirthLogisticsTransferJournal.TryPublish(unpaid,()=>true),"no-effect terminal archive preserves original");
  string rootPath=System.IO.Path.Combine(GameIO.Directory,"RebirthLogisticsTransfers",record.Owner,record.Operation.ToString("N")+".xml");
  System.IO.File.WriteAllText(rootPath,original);Check(!RebirthLogisticsTransferJournal.TryRead(record.Owner,record.Operation,out decoded),"conflicting root and archive refuse exact lookup");System.IO.File.Delete(rootPath);
  string orphan=rootPath+".tmp";System.IO.File.WriteAllText(orphan,original);Check(!RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(GameManager.Instance.World,out pendingOriginals),"unknown sidecar cannot release cold holds");System.IO.File.Delete(orphan);
  var plan=new RebirthLogisticsTransferPlan();
  var owner=plan.Capture("owner","original-owner",RemoteResourceSourceKind.StaticContainer,new[]{new ItemStack{count=7},new ItemStack{count=5}},new[]{true,false});
  var destination=plan.Capture("S:4,5,6","original-container",RemoteResourceSourceKind.StaticContainer,new[]{new ItemStack{count=9},new ItemStack{count=0}},new[]{true,true});
  Check(plan.Move(owner,destination,1,5,true)==1,"protected existing destination accepts topup; locked empty not used");
  Check(owner.After[0].count==7&&owner.After[1].count==4,"protected source untouched; exact source debit");
  var completed=new RebirthLogisticsTransferJournal.Record();plan.Complete(completed,owner);
  Check(completed.BeforeOwner[0].count==0&&completed.AfterOwner[0].count==0&&completed.BeforeOwner[1].count==5&&completed.AfterOwner[1].count==4,"unrelated owner slot removed from sparse intent");
  var logical=new RebirthLogisticsTransferPlan();
  var logicalOwner=logical.Capture("owner","owner",RemoteResourceSourceKind.StaticContainer,new[]{new ItemStack{count=1}},new[]{false});
  var sourceNpc=logical.CaptureNpc(new string('1',32),7,new System.Collections.Generic.Dictionary<string,int>{{"ammo",9}},new System.Collections.Generic.Dictionary<string,int>{{"ammo",6}},null);
  var targetNpc=logical.CaptureNpc(new string('2',32),8,new System.Collections.Generic.Dictionary<string,int>(),null,new[]{"ammo"});
  Check(logical.MoveNpcToNpc(sourceNpc,targetNpc,"ammo",20)==3,"logical source reservations protected and empty target gains canonical new key");
  Check(sourceNpc.Entries[0].After==6&&targetNpc.Entries[0].After==3,"logical conservation exact before-to-after quantities");
  Check(logical.MoveToNpc(logicalOwner,targetNpc,"ammo",1,1)==1&&logicalOwner.After[0].count==0,"native source stages exact logical quantity without callbacks");
  Check(logical.MoveFromNpc(targetNpc,logicalOwner,"ammo",new ItemStack{count=1},2,true)==2&&logicalOwner.After[0].count==2&&targetNpc.Entries[0].After==2,"logical source stages canonical native owner output within capacity");
  sourceNpc.Entries[0].Locked=true;
  Check(logical.MoveNpcToNpc(sourceNpc,targetNpc,"ammo",1)==0,"logical protected item cannot be used as source");sourceNpc.Entries[0].Locked=false;
  var logicalRecord=new RebirthLogisticsTransferJournal.Record();logical.Complete(logicalRecord,logicalOwner);
  Check(logicalRecord.Npcs.Count==2&&logicalRecord.Npcs[0].Entries[0].Reserved==6,"original logical revision and reservation evidence retained");
  record.Npcs.Add(logicalRecord.Npcs[0]);record.Npcs.Add(logicalRecord.Npcs[1]);record.Phase=0;record.AfterOwner[0].count=3;record.DebitedOwner[0].count=3;
  Check(RebirthLogisticsTransferJournal.TryDecode(RebirthLogisticsTransferJournal.Encode(record),out decoded)&&decoded.Npcs.Count==2,"logical journal canonical roundtrip");
  var hostile=System.Xml.Linq.XElement.Parse(RebirthLogisticsTransferJournal.Encode(record));
  foreach(var node in hostile.Elements("npc")){node.Element("nativeBefore").Value="<!DOCTYPE nativeStacks [<!ENTITY x 'x'>]><nativeStacks/>";break;}
  Check(!RebirthLogisticsTransferJournal.TryDecode(hostile.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),out decoded),"encoded native custody cannot introduce nested DTD/entity processing");
  record.Npcs[0].Entries[0].After=5;
  bool reservedRejected=false;try{RebirthLogisticsTransferJournal.Encode(record);}catch(System.IO.InvalidDataException){reservedRejected=true;}
  Check(reservedRejected,"journal rejects consuming logical reserved quantities");record.Npcs.Clear();record.AfterOwner[0].count=2;record.DebitedOwner[0].count=2;
  Check(completed.Legs.Count==1&&completed.Legs[0].After[0].count==10,"all endpoint plans retained before execution");
  var deposited=new ItemStack{count=10};var final=new ItemStack{count=7};
  Check(deposited.count+3>10,"counterexample: destination credit before owner debit duplicates on native death-drop");
  var deathContents=RebirthLogisticsTransferJournal.Debited(deposited,final);
  Check(deathContents.count+3==10,"saved original owner debit before destination credit conserves native death-drop contents");
  bool rejected=false;try{plan.Capture("S:4,5,6","replacement-container",RemoteResourceSourceKind.StaticContainer,new[]{new ItemStack{count=9},new ItemStack{count=0}},new[]{true,true});}catch(System.IO.InvalidDataException){rejected=true;}Check(rejected,"replaced native identity refused during planning");
  System.Console.WriteLine("PASS actual immutable logistics journal/planner with disclosed codec/world/publication doubles");
 }
}
