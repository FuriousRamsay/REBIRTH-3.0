class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite=false;}
class Log {public static void Warning(string message){}}
public class NpcPublicationFixture {
 public static string Run(){int checks=0;string directory=Path.Combine(Path.GetTempPath(),"rebirth-npc-publication-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string path=Path.Combine(directory,"inventory.xml");
 Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
 try {
 var first=new XmlDocument();first.LoadXml("<inventory revision='1'><item metadata='a&amp;b'/></inventory>");
 var second=new XmlDocument();second.LoadXml("<inventory revision='2'><item metadata='quality5'/></inventory>");
 RebirthNpcPersistenceFile.SaveAtomic(path,first);var read=new XmlDocument();read.Load(path);check(read.DocumentElement.OuterXml==first.DocumentElement.OuterXml,"first exact write");
 RebirthNpcPersistenceFile.SaveAtomic(path,second);read.Load(path);check(read.DocumentElement.OuterXml==second.DocumentElement.OuterXml,"replacement exact write");read.Load(path+".bak");check(read.DocumentElement.OuterXml==first.DocumentElement.OuterXml,"previous final retained");
 bool refused=false;using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){try{RebirthNpcPersistenceFile.SaveAtomic(path,first);}catch(IOException){refused=true;}}check(refused,"locked destination refuses publication");read.Load(path);check(read.DocumentElement.OuterXml==second.DocumentElement.OuterXml,"refusal retains current final");check(!File.Exists(path+".tmp"),"failed candidate cleaned");
 RebirthNpcPersistenceFile.BlockWrite(path,"fixture rejection");refused=false;try{RebirthNpcPersistenceFile.SaveAtomic(path,first);}catch(InvalidDataException){refused=true;}check(refused,"rejected read blocks shared writer");read.Load(path);check(read.DocumentElement.OuterXml==second.DocumentElement.OuterXml,"blocked save preserves final");
 RebirthNpcPersistenceFile.VerifiedRead(path);RebirthNpcPersistenceFile.SaveAtomic(path,first);read.Load(path);check(read.DocumentElement.OuterXml==first.DocumentElement.OuterXml,"verified read releases guard");
 return "PASS "+checks+" whole NPC persistence/shared writer checks on real temporary files; checkpoint and logging adapters doubled";
 }finally{RebirthNpcPersistenceFile.VerifiedRead(path);Directory.Delete(directory,true);}
 }
}