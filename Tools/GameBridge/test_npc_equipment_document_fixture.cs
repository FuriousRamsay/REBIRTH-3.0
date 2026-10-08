using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Xml;
struct RebirthNpcStableId {public Guid Id;public bool IsEmpty{get{return Id==Guid.Empty;}}public static bool TryParse(string s,out RebirthNpcStableId v){Guid g;bool ok=Guid.TryParse(s,out g);v=new RebirthNpcStableId{Id=g};return ok&&!v.IsEmpty;}}
enum RebirthNpcEquipmentSlot:byte {Head,Face,Chest,Hands,Legs,Feet,PrimaryWeapon,SecondaryWeapon,Utility}
class RebirthNpcEquipmentPersistentRecord {public RebirthNpcStableId NpcId;public uint Revision;public Dictionary<RebirthNpcEquipmentSlot,string> Slots;public Guid[] ReplayJournal;}
class RebirthNpcEquipmentService {public static bool ValidatePersistentRecord(RebirthNpcEquipmentPersistentRecord r,out string e){return Check.ValidatePersistentRecord(r,out e);}}
static class Log {public static void Warning(string s){}}static class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite;}
// FILEHELPER
class Check {const int FormatVersion=1,MaxJournal=128;
// METHODS
 static string Wrap(string s){return "<rebirthNpcEquipmentState format='1'>"+s+"</rebirthNpcEquipmentState>";}
 static void Test(string s,bool expected){var d=new XmlDocument();d.LoadXml(Wrap(s));if(Validate(d)!=expected)throw new Exception(s);}
 static void Main(string[] args){string row="<loadout stableId='11111111-1111-1111-1111-111111111111' revision='1'><slot id='0' item='helmet'/></loadout>";
 Test("",true);Test(row,true);Test(row+row,false);Test(row.Replace("id='0'","id='99'"),false);Test(row.Replace("item='helmet'","item=''"),false);Test("<unknown/>",false);
 string replay="<replay id='22222222222222222222222222222222'/>";Test(row.Replace("</loadout>",replay+replay+"</loadout>"),false);
 string folder=Path.GetFullPath(args[0]);if(!folder.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Temp required");string path=Path.Combine(folder,"equipment.xml");File.WriteAllText(path,Wrap(row+row));File.WriteAllText(path+".bak",Wrap(row));XmlDocument loaded;string source,error;if(!RebirthNpcPersistenceFile.TryLoad(path,Validate,out loaded,out source,out error)||source!="backup")throw new Exception("Backup failed");
 Console.WriteLine("PASS: actual equipment reader/preflight/document checks and disk-loader backup fallback; rejects duplicate identities/replays, invalid slots and empty items. Temporary files only; native runtime restore not executed.");}
}
