using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Xml;
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero{get{return new Vector3();}}}
public enum RebirthNpcCommandKind {Follow,Guard}public enum RebirthNpcOrderState {None,Follow,Guard,Patrol,Work,Travel,Mission}
static class Log {public static void Warning(string s){}}static class RebirthNpcPersistenceCoordinator {public static bool IsCheckpointWrite;}
// FILEHELPER
// METHODS
 static string Wrap(string row,int version=2){return "<rebirthNpcExecutionState format='"+version+"'>"+row+"</rebirthNpcExecutionState>";}
 static void Test(string xml,bool expected){bool valid;try{var d=new XmlDocument();d.LoadXml(xml);valid=ValidateDocument(d);}catch(InvalidDataException){valid=false;}if(valid!=expected)throw new Exception(xml);}
 static void Main(string[] args){string row="<execution entity='1' subject='2' lease='1' command='1' started='0' commandKind='Follow' order='Follow' hasTarget='false' hasGuard='false'/>";
 Test(Wrap(row,1),true);Test(Wrap(row),true);Test(Wrap(row.Replace("entity='1'","stableId='00000000000000000000000000000000' entity='1'")),true);
 Test(Wrap(row+row),false);Test(Wrap(row.Replace("entity='1'","stableId='bad' entity='1'")),false);Test(Wrap(row.Replace("order='Follow'","order='99'")),false);
 Test(Wrap(row.Replace("hasTarget='false'","hasTarget='true' targetX='NaN' targetY='0' targetZ='0'")),false);Test(Wrap("<unexpected/>"),false);
 string folder=Path.GetFullPath(args[0]);if(!folder.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Temp required");string path=Path.Combine(folder,"execution.xml");File.WriteAllText(path,Wrap(row+row));File.WriteAllText(path+".bak",Wrap(row));XmlDocument loaded;string source,error;if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out loaded,out source,out error)||source!="backup")throw new Exception("Backup failed");
 Console.WriteLine("PASS: actual execution parser and stable ID type preserve legacy empty/zero identity; reject duplicate identities, malformed IDs, undefined order and nonfinite targets; actual temporary-file backup fallback succeeds. Command enums/vector types substituted; no runtime lease restore.");}
}
