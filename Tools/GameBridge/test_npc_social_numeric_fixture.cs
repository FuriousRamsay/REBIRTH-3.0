using System;using System.IO;using System.Globalization;using System.Xml;using System.Collections.Generic;
static class Log {public static void Warning(string s){}}static class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite;}
// FILEHELPER
class Check {
// METHODS
static string Wrap(string value){return "<rebirthNpcSocialState version='2'><relationships><relationship key='test' trust='"+value+"'/></relationships></rebirthNpcSocialState>";}
static void Expect(string content,bool expected){var d=new XmlDocument();d.LoadXml("<rebirthNpcSocialState version='2'>"+content+"</rebirthNpcSocialState>");if(Validate(d)!=expected)throw new Exception(content);}
static void Main(string[] args){
string memory="<memory event='11111111111111111111111111111111' subject='22222222222222222222222222222222' kind='Trade'/>";
Expect("<memories>"+memory+"</memories>",true);
Expect("<memories>"+memory+memory+"</memories>",true);
Expect("<memories>"+memory+memory.Replace("22222222222222222222222222222222","33333333333333333333333333333333")+"</memories>",true);
Expect("<memories>"+memory.Replace("Trade","255")+"</memories>",false);
Expect("<relationships><relationship key='A'/><relationship key='a'/></relationships>",false);
Expect("<factions><standing faction='A' counterparty='B'/><standing faction=' a ' counterparty='b'/></factions>",false);
Expect("<memories/><memories/>",false);Expect("<unknown/>",false);
Expect("<appliedEvents><event id='00000000000000000000000000000000'/></appliedEvents>",false);

foreach(string bad in new[]{"NaN","Infinity","-Infinity","garbage",""}){var d=new XmlDocument();d.LoadXml(Wrap(bad));bool rejected=false;try{Validate(d);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Accepted "+bad);}
foreach(string xml in new[]{Wrap("0.25"),"<rebirthNpcSocialState version='1'><memories/></rebirthNpcSocialState>"}){var d=new XmlDocument();d.LoadXml(xml);if(!Validate(d))throw new Exception("Legacy/valid rejected");}
var e=new XmlDocument();e.LoadXml("<x revision='-1' changed='overflow'/>");bool revision=false,timestamp=false;try{PU(e.DocumentElement,"revision");}catch(InvalidDataException){revision=true;}try{PL(e.DocumentElement,"changed");}catch(InvalidDataException){timestamp=true;}if(!revision||!timestamp)throw new Exception("Invalid integers accepted");
string path=Path.Combine(args[0],"social.xml");File.WriteAllText(path,Wrap("NaN"));File.WriteAllText(path+".bak",Wrap("0.5"));XmlDocument loaded;string source,error;if(!RebirthNpcPersistenceFile.TryLoad(path,Validate,out loaded,out source,out error)||source!="backup")throw new Exception("Backup failed");Console.WriteLine("PASS: actual identity validation preserves repeated memory IDs, rejects duplicate relationship/faction/sections and invalid identities/enums; social numeric parsers reject malformed/nonfinite values, retain missing legacy defaults, and actual file loader selects valid backup. Runtime social restore not tested.");
}}
