using System;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
// ENUM
class RebirthNpcPersistenceFile {
 public static bool CanInitializeEmpty(string path){return !File.Exists(path+".bak");}
 public static void VerifiedRead(string path){}
}
class Test {
 // SOURCE
 // READ
 static void MustFail(string path){bool failed=false;try{ReadReplayDocument(path);}catch(Exception){failed=true;}if(!failed)throw new Exception("unsafe history accepted");}
 static void Main(){
 string dir=Path.Combine(Path.GetTempPath(),"rebirth-npc-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
 try {
 string path=Path.Combine(dir,"history.xml"),valid="<rebirthNpcExternalTransactions format='1'/>";
 if(ReadReplayDocument(path)!=null)throw new Exception("fresh not empty");
 File.WriteAllText(path+".bak",valid);MustFail(path);
 File.WriteAllText(path,"<broken>");MustFail(path);
 if(File.ReadAllText(path)!="<broken>")throw new Exception("corrupt evidence changed");
 File.WriteAllText(path,valid);if(ReadReplayDocument(path)==null)throw new Exception("valid failed");
 File.Delete(path);File.Delete(path+".bak");File.WriteAllText(path+".tmp",valid);MustFail(path);
 Console.WriteLine("PASS: fresh store, valid primary, missing/corrupt primary with valid older backup rejected, evidence retained, orphan temp rejected");
 }finally{Directory.Delete(dir,true);}
 }
}
