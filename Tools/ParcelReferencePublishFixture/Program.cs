using System;using System.IO;using System.Xml.Linq;using System.Linq;
static class GameIO {internal static string Root; internal static string GetSaveGameDir()=>Root;}
static class RebirthLogisticsTransferJournal {
 internal static bool Terminal=true; internal static int Reads;
 internal static bool TryRead(string owner,Guid operation,out Journal.Original r){Reads++;r=new Journal.Original{Owner=owner,Operation=operation,Phase=4};return Terminal;}
 internal static string ImmutablePlanDigest(Journal.Original r)=>new string('b',64);
}
partial class Journal {
 static Endpoint E()=>new Endpoint{Original=new Original{Owner=new string('a',64),Operation=Guid.NewGuid()},Death=new Death{OwnerEntity=1},Incarnation=Guid.NewGuid(),Identity=new string('c',64),Binding=new string('b',64)};
 static XElement Row(int old,int next,int state)=>new XElement("repair",new XAttribute("state",state),new XAttribute("old",old),new XAttribute("next",next),new XAttribute("nonce",Guid.NewGuid().ToString("N")),new XAttribute("before",new string('d',64)),new XAttribute("after",state==3||state==5?new string('e',64):""),new XAttribute("image",new string('f',64)),new XAttribute("marker",new string('a',64)),new XAttribute("timestamp",0),new XAttribute("x",0),new XAttribute("y",0),new XAttribute("z",0),new XAttribute("reserved",0),new XAttribute("tracking","untracked"),new XAttribute("witness",new string('b',64)));
 static void Save(string p,XElement x){Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,x.ToString(SaveOptions.DisableFormatting));}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Fresh(){GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-parcel-fixture",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Root);RebirthLogisticsTransferJournal.Terminal=true;}
 static void Main(){
 Fresh();var e=E();var before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);var next=new XElement(before);next.Add(Row(2,3,1));Check(Publish(e,before,next),"capacity archives current history then publication succeeds");Check(!File.Exists(PathFor(e))&&XNode.DeepEquals(Read(e),next),"one canonical archived copy remains");
 Fresh();e=E();before=Empty(e);before.Add(Row(1,2,3));Save(ArchivePath(e),before);next=new XElement(before);next.Add(Row(2,3,1));Check(Publish(e,before,next),"already archived endpoint accepts subsequent repair");Check(!File.Exists(PathFor(e))&&XNode.DeepEquals(Read(e),next),"archived repair does not recreate active twin");
 var attempted=new XElement(next);attempted.Elements().Last().SetAttributeValue("state",2);Check(Publish(e,next,attempted),"archived in-progress repair transition remains publishable");
 Fresh();e=E();before=Empty(e);next=new XElement(before);next.Add(Row(1,2,1));Check(Publish(e,before,next)&&File.Exists(PathFor(e)),"new active endpoint publication preserved");
 var stale=Empty(e);Check(!Publish(e,stale,next),"stale expected history rejected");
 Fresh();e=E();before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);Save(ArchivePath(e),before);bool rejected=false;try{Read(e);}catch(InvalidDataException){rejected=true;}Check(rejected,"conflicting active and archived copies rejected");
 Fresh();e=E();before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);ArchiveCompleted(e,Read(e));Check(Capacity(1)&&File.Exists(ArchivePath(e)),"addressed terminal archival retained");
 Fresh();RebirthLogisticsTransferJournal.Terminal=false;e=E();before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);Check(Capacity(1)&&File.Exists(PathFor(e)),"nonterminal history remains active");Check(!Capacity(33554432),"capacity byte bound retained");
 var malformed=Path.Combine(Path.GetDirectoryName(PathFor(e)),Guid.NewGuid().ToString("N")+".xml");File.WriteAllText(malformed,"broken");Check(Capacity(1),"unrelated malformed XML counted without global rejection");
var bad=E();File.Move(malformed,Path.Combine(Path.GetDirectoryName(PathFor(e)),bad.Incarnation.ToString("N")+".xml"));bad.Original=e.Original;
rejected=false;try{Read(bad);}catch{rejected=true;}Check(rejected,"addressed malformed history still rejected");
Fresh();RebirthLogisticsTransferJournal.Terminal=false;
for(int i=0;i<512;i++){var item=E();var h=Empty(item);h.Add(Row(1,2,3));Save(PathFor(item),h);}
RebirthLogisticsTransferJournal.Reads=0;Check(Capacity(1)&&RebirthLogisticsTransferJournal.Reads==0,"512 history quota scan reads zero transfer originals");
e=E();before=Empty(e);next=new XElement(before);next.Add(Row(1,2,1));Check(Publish(e,before,next)&&RebirthLogisticsTransferJournal.Reads==0,"publication unaffected by 512 unrelated history semantics");
File.WriteAllText(Path.Combine(Path.GetDirectoryName(PathFor(e)),Guid.NewGuid().ToString("N")+".xml"),new string('x',33554432));Check(!Capacity(1),"malformed bytes still enforce quota");
Fresh();e=E();var folder=Path.GetDirectoryName(PathFor(e));Directory.CreateDirectory(folder);
for(int i=0;i<4095;i++)File.WriteAllText(Path.Combine(folder,Guid.NewGuid().ToString("N")+".xml"),"x");
Check(Capacity(1),"4095 conservative file entries permitted");File.WriteAllText(Path.Combine(folder,Guid.NewGuid().ToString("N")+".xml"),"x");Check(!Capacity(1),"4096 conservative entries rejected");
Fresh();e=E();folder=Path.GetDirectoryName(PathFor(e));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"unexpected.xml"),"x");Check(!Capacity(1),"unexpected filename remains rejected");
Fresh();e=E();before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);RebirthLogisticsTransferJournal.Reads=0;
Check(Capacity(33554431)&&File.Exists(ArchivePath(e))&&!File.Exists(PathFor(e))&&RebirthLogisticsTransferJournal.Reads==1,"pressure-only legacy completed history reclaimed with proof");
Fresh();e=E();before=Empty(e);before.Add(Row(1,2,3));Save(PathFor(e),before);RebirthLogisticsTransferJournal.Terminal=false;
Check(!Capacity(33554431)&&File.Exists(PathFor(e)),"pressure does not reclaim nonterminal history");
Fresh();e=E();Directory.CreateDirectory(Path.GetDirectoryName(PathFor(e)));File.WriteAllText(PathFor(e),"malformed");
Check(!Capacity(33554431)&&File.ReadAllText(PathFor(e))=="malformed","pressure retains corrupt history and enforces actual bytes");
 }
}
