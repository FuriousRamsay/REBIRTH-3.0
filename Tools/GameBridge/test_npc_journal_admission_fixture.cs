using System;
using System.IO;
class GameIO {public static string DirectoryPath;public static string GetSaveGameDir(){return DirectoryPath;}}
class RebirthNpcPersistenceFile {public static bool Blocked;public static void AssertWritable(string path){if(Blocked)throw new IOException("blocked");}}
class Test {
 static long journalGeneration=1;
 static string loadedJournalDirectory="A";const string JournalFileName="journal.xml";
 // SOURCE
 static void Fails(string expected){bool failed=false;try{AssertReplayAdmission(expected,1);}catch(Exception){failed=true;}if(!failed)throw new Exception("unsafe admission");}
 static void Main(){GameIO.DirectoryPath="A";AssertReplayAdmission("A",1);Fails("");GameIO.DirectoryPath="B";Fails("A");GameIO.DirectoryPath="A";loadedJournalDirectory="";Fails("A");loadedJournalDirectory="A";RebirthNpcPersistenceFile.Blocked=true;Fails("A");RebirthNpcPersistenceFile.Blocked=false;journalGeneration=2;Fails("A");AssertReplayAdmission("A",2);Console.WriteLine("PASS: loaded same-world admission; empty, switched, reset and write-blocked and same-path new-session history rejected");}
}
