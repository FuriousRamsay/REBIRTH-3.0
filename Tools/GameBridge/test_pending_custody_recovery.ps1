$ErrorActionPreference='Stop'
$s=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs')
$a=$s.IndexOf('    public static bool TryGet(');$b=$s.IndexOf('    private static bool TryLoadValidatedRecord(', $a)
$methods=$s.Substring($a,$b-$a)
Add-Type -ReferencedAssemblies System.Xml.Linq,System.Xml -TypeDefinition (@"
using System;using System.Collections.Generic;using System.Xml.Linq;using File=FakeFile;
public static class FakeFile{public static bool Exists(string p){return true;}}
public class RebirthStablePlayerIdentity{public string StorageKey="owner";public string CanonicalId="owner";}
public class RebirthWorldCharacterRecord{public bool IsComplete=true;public void MarkPersisted(){}}
public static class RebirthAtomicXmlFile{public static int Writes;public static bool TryWrite(string p,XDocument d,out string e){Writes++;e="";return true;}}
public static class RepositoryChecks {
 static object Sync=new object();static bool serverAuthority=true;
 static Dictionary<string,RebirthWorldCharacterRecord> Cache=new Dictionary<string,RebirthWorldCharacterRecord>();
 static Dictionary<string,long> RetryAfterUtcTicks=new Dictionary<string,long>();
 static bool validFinal,custody;static int backups;static string issue;
 static string GetPath(string key){return "character.xml";}
 static XDocument Serialize(RebirthWorldCharacterRecord r){return new XDocument(new XElement("record"));}
 static void AddIssue(string key,string path,string reason,bool recovered){issue=reason;}
 static bool TryLoadValidatedRecord(string path,RebirthStablePlayerIdentity id,out RebirthWorldCharacterRecord r,out bool migrated,out string error,out bool pending){
 bool backup=path.EndsWith(".bak");if(backup)backups++;pending=!backup&&custody;migrated=false;error="invalid";r=new RebirthWorldCharacterRecord();return backup||validFinal;}
"@ + $methods + @"
 static void Reset(bool valid,bool pending){Cache.Clear();RetryAfterUtcTicks.Clear();validFinal=valid;custody=pending;backups=0;RebirthAtomicXmlFile.Writes=0;issue="";}
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
  Check(HasPendingItemCustody(XDocument.Parse("<record><support><music><pendingTransfer/></music></support></record>")),"music not detected");
  Check(HasPendingItemCustody(XDocument.Parse("<record><pendingGearTransfer/></record>")),"gear not detected");
  Check(!HasPendingItemCustody(XDocument.Parse("<record><support/></record>")),"ordinary record blocked");
  RebirthWorldCharacterRecord r;var id=new RebirthStablePlayerIdentity();
  Reset(false,true);Check(!TryGet(id,out r)&&r==null&&backups==0&&RebirthAtomicXmlFile.Writes==0&&issue.Contains("custody"),"custody overwritten by backup");
  Reset(false,false);Check(TryGet(id,out r)&&backups==1&&RebirthAtomicXmlFile.Writes==1,"ordinary recovery broken");
  Reset(true,true);Check(TryGet(id,out r)&&backups==0&&RebirthAtomicXmlFile.Writes==0,"valid pending save blocked");
 }
}
"@)
[RepositoryChecks]::Run()
Write-Output 'PASS: actual repository TryGet refuses backup replacement of failed pending custody; ordinary backup recovery and valid pending save load preserved (file/parse/save boundaries stubbed).'
