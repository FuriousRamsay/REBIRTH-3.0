using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Xml;
struct RebirthNpcStableId {public Guid Id;public bool IsEmpty{get{return Id==Guid.Empty;}}public static bool TryParse(string s,out RebirthNpcStableId v){Guid g;bool ok=Guid.TryParse(s,out g);v=new RebirthNpcStableId{Id=g};return ok&&!v.IsEmpty;}}
enum RebirthNpcCombatLifeState {Healthy} enum RebirthNpcInjurySeverity {None}
class RebirthNpcCombatStateRecord {public RebirthNpcStableId NpcId;public RebirthNpcCombatLifeState LifeState;public RebirthNpcInjurySeverity InjurySeverity;public int InjuryPoints,BleedPoints;public long IncapacitatedUtcTicks,DeathFinalizedUtcTicks;public string SettlementId,LastCause;public uint Revision;}
static class Log {public static void Warning(string s){}}static class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite;}
// FILEHELPER
class Check {const int FormatVersion=1;
// METHODS
 static string Wrap(string body){return "<rebirthNpcCombatState format='1'>"+body+"</rebirthNpcCombatState>";}
 static void Test(string xml,bool expected){bool valid;try{var d=new XmlDocument();d.LoadXml(xml);valid=ValidateDocument(d);}catch(InvalidDataException){valid=false;}if(valid!=expected)throw new Exception(xml);}
 static void Main(string[] args){
 string state="<state npc='11111111-1111-1111-1111-111111111111' life='0' severity='0' injury='0' bleed='0' incapacitated='0' death='0' revision='1'/>";
 Test(Wrap(""),true);Test(Wrap(state),true);Test(Wrap(state+state),false);Test(Wrap(state.Replace("life='0'","life='9'")),false);Test(Wrap(state.Replace("injury='0'","injury='-1'")),false);Test(Wrap("<deathTransaction id=''/ >".Replace("/ >","/>")),false);Test(Wrap("<deathTransaction id='one'/><deathTransaction id='one'/>"),false);Test(Wrap("<unexpected/>"),false);
 string folder=Path.GetFullPath(args[0]);if(!folder.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Temp required");string path=Path.Combine(folder,"combat.xml");
 File.WriteAllText(path,Wrap(state.Replace("life='0'","life='9'")));File.WriteAllText(path+".bak",Wrap(state));XmlDocument loaded;string source,error;
 if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error)||source!="backup"||File.Exists(path))throw new Exception("No backup fallback");
 Console.WriteLine("PASS: actual combat parser/validator rejects invalid range, duplicate identities/transactions and unknown nodes; actual disk loader selects valid backup and quarantines invalid primary. Isolated temporary files only.");
 }
}
