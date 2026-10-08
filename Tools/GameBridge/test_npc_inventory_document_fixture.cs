using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Xml;
struct RebirthNpcStableId {public Guid Id;public bool IsEmpty{get{return Id==Guid.Empty;}}public static bool TryParse(string s,out RebirthNpcStableId v){Guid g;bool ok=Guid.TryParse(s,out g);v=new RebirthNpcStableId{Id=g};return ok&&!v.IsEmpty;}}
class RebirthNpcInventoryPersistentRecord {public RebirthNpcStableId NpcId;public uint Revision;public Dictionary<string,int> Quantities,Reservations;public Guid[] ReplayJournal;}
class RebirthNpcInventoryTransactionService {public static bool ValidatePersistentRecord(RebirthNpcInventoryPersistentRecord r,out string e){return Check.ValidatePersistentRecord(r,out e);}}
static class Log {public static void Warning(string s){}}
static class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite; }
// FILEHELPER
class Check {const int FormatVersion=1,MaxReplayTransactionsPerInventory=256;
// METHODS
 static void AssertDoc(string children,bool expected){var doc=new XmlDocument();doc.LoadXml("<rebirthNpcInventoryState format='1'>"+children+"</rebirthNpcInventoryState>");if(ValidateDocument(doc)!=expected)throw new Exception(children);}
 static string Record(string contents){return "<inventory stableId='11111111-1111-1111-1111-111111111111' revision='9'>"+contents+"</inventory>";}
 static void Main(string[] args){
 AssertDoc("",true);string item="<item key='resourceWood' quantity='5'/>";AssertDoc(Record(item),true);
 AssertDoc(Record(item+"<reservation key='RESOURCEWOOD' quantity='5'/>"),true);
 AssertDoc(Record(item+"<reservation key='resourceWood' quantity='6'/>"),false);
 AssertDoc(Record("<item key='wood' quantity='-1'/>"),false);
 AssertDoc(Record(item+"<item key='RESOURCEWOOD' quantity='1'/>"),false);
 AssertDoc(Record(item)+Record(item),false);AssertDoc("<unexpected/>",false);
 AssertDoc(Record("<replay id='00000000000000000000000000000000'/>"),false);
 string replay="<replay id='22222222222222222222222222222222'/>";AssertDoc(Record(replay+replay),false);
 AssertDoc(Record("<unexpected/>"),false);
 string folder=Path.GetFullPath(args[0]);
 if(!folder.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Fixture must run under temp");
 string path=Path.Combine(folder,"inventory.xml");
 string valid="<rebirthNpcInventoryState format='1'>"+Record(item)+"</rebirthNpcInventoryState>";
 string invalid="<rebirthNpcInventoryState format='1'>"+Record("<item key='wood' quantity='-1'/>")+"</rebirthNpcInventoryState>";
 if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))throw new Exception("New store rejected");
 File.WriteAllText(path,invalid);File.WriteAllText(path+".bak",valid);
 XmlDocument loaded;string source,error;
 if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error)||source!="backup"||File.Exists(path)||!ValidateDocument(loaded))throw new Exception("Backup recovery failed");
 RebirthNpcPersistenceFile.AssertWritable(path);
 File.WriteAllText(path,invalid);File.WriteAllText(path+".bak",invalid);
 if(RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error))throw new Exception("Accepted invalid copies");
 bool blocked=false;try{RebirthNpcPersistenceFile.AssertWritable(path);}catch(InvalidDataException){blocked=true;}if(!blocked)throw new Exception("Invalid copies did not block write");
 File.Delete(path+".bak");
 if(RebirthNpcPersistenceFile.CanInitializeEmpty(path))throw new Exception("Quarantined store treated as new");
 // Clear the process-local record to model a fresh process while preserving on-disk quarantine evidence.
 RebirthNpcPersistenceFile.VerifiedRead(path);
 if(RebirthNpcPersistenceFile.CanInitializeEmpty(path))throw new Exception("Quarantine forgotten after memory reset");
 if(RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error))throw new Exception("Missing copies loaded after memory reset");
 blocked=false;try{RebirthNpcPersistenceFile.AssertWritable(path);}catch(InvalidDataException){blocked=true;}if(!blocked)throw new Exception("Quarantine did not reestablish write guard");

 File.WriteAllText(path,valid);
 if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error)||source!="primary")throw new Exception("Valid primary did not recover");
 RebirthNpcPersistenceFile.AssertWritable(path);
 Console.WriteLine("PASS: actual disk loader recovered backup from invalid primary, quarantined primary, blocked writes for two invalid copies, then cleared block after verified primary recovery. Only isolated temporary files used.");
 Console.WriteLine("PASS: actual inventory document reader/validator rejects invalid quantities, over-reservations, duplicate item keys/stable IDs/replay IDs and unknown records; accepts empty and valid format1. No runtime state mutation methods compiled into fixture.");
 }
}
