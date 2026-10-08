$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/UI/XUiC_RebirthMapLists.cs')
$start=$source.IndexOf('    private sealed class NameState')
$end=$source.IndexOf('[Preserve]', $start)
if($start -lt 0 -or $end -le $start){throw 'Production name helper not found'}
$methods=$source.Substring($start,$end-$start)
Add-Type -TypeDefinition (@'
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
public class XUiV_Label {public string Text;}
public class TextValue {public string Text;}
public class Waypoint {public bool bIsAutoWaypoint,bUsingLocalizationId;public TextValue name=new TextValue();}
public static class Localization {public static string Language="en";public static string Get(string s){return Language+":"+s;}}
public static class GeneratedTextManager {
 public enum TextFilteringMode {FilterWithSafeString}
 public static List<Action<string>> Pending=new List<Action<string>>();
 public static void GetDisplayText(TextValue v,Action<string> callback,bool a,bool b,TextFilteringMode mode){Pending.Add(callback);}
}
public static class MapNameChecks {
'@ + $methods + @'
public static class Check {
 public static void Run(){
  var label=new XUiV_Label();var w=new Waypoint();w.name.Text="old";
  MapNameChecks.Name(w,label,()=>true);
  MapNameChecks.Name(w,label,()=>true);
  if(GeneratedTextManager.Pending.Count!=1)throw new Exception("unchanged text refiltered");
  w.name.Text="new";MapNameChecks.Name(w,label,()=>true);
  GeneratedTextManager.Pending[1]("new filtered");GeneratedTextManager.Pending[0]("old filtered");
  if(label.Text!="new filtered")throw new Exception("out-of-order callback overwrote rename");
  w.name.Text="newer";GeneratedTextManager.Pending[1]("new filtered again");
  if(label.Text!="new filtered")throw new Exception("unrefreshed mutation accepted stale callback");
  w.bUsingLocalizationId=true;MapNameChecks.Name(w,label,()=>true);
  if(label.Text!="en:newer")throw new Exception("localized label missing");
  Localization.Language="fr";MapNameChecks.Name(w,label,()=>true);
  if(label.Text!="fr:newer")throw new Exception("language change ignored");
  var other=new Waypoint();other.name.Text="other";
  MapNameChecks.Name(other,label,()=>false);
  GeneratedTextManager.Pending[2]("wrong row");
  if(label.Text!="")throw new Exception("pooled row callback accepted");
  MapNameChecks.Name(other,label,()=>true);
  if(GeneratedTextManager.Pending.Count!=4)throw new Exception("discarded callback prevented row retry");
  GeneratedTextManager.Pending[3]("other filtered");
  if(label.Text!="other filtered")throw new Exception("returned row stayed blank");
  MapNameChecks.Name(other,label,()=>true);
  if(GeneratedTextManager.Pending.Count!=4)throw new Exception("successful retry not cached");
 }
}
'@)
[Check]::Run()
Write-Output 'PASS: production map-name helper caches unchanged names, follows renames/language and rejects stale or reassigned callbacks.'
