using System;
using System.IO;
class Test {
 // SOURCE
 static void Main(){
 string dir=Path.Combine(Path.GetTempPath(),"rebirth-journal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
 try {
 string path=Path.Combine(dir,"journal.xml"),temp=path+".tmp";
 File.WriteAllText(temp,"first");CommitJournalFile(temp,path);
 if(File.ReadAllText(path)!="first"||File.Exists(temp))throw new Exception("initial commit");
 File.WriteAllText(temp,"second");CommitJournalFile(temp,path);
 if(File.ReadAllText(path)!="second"||File.ReadAllText(path+".bak")!="first"||File.Exists(temp))throw new Exception("replace commit");
 bool failed=false;try{CommitJournalFile(temp,path);}catch(IOException){failed=true;}
 if(!failed||File.ReadAllText(path)!="second")throw new Exception("failed commit damaged primary");
 Console.WriteLine("PASS: initial commit, replacement backup, failed replacement preserves primary");
 } finally {Directory.Delete(dir,true);}
 }
}
