$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/UI/XUiC_RebirthMusicLibrary.cs')
$a=$source.IndexOf('    private string InspectionText(')
$b=$source.IndexOf('    private void Bind(string id,',$a)
if($a -lt 0 -or $b -lt 0){throw 'Inspection source seam missing'}
$actual=$source.Substring($a,$b-$a).Replace('private string InspectionText','public string InspectionText')
Add-Type -TypeDefinition (@"
using System;using System.Collections.Generic;
public class ItemClass {public string Key;public string GetItemDescriptionKey(){return Key;}}
public static class Localization {public static Dictionary<string,string> Values=new Dictionary<string,string>();public static bool Exists(string k){return Values.ContainsKey(k);}public static string Get(string k){return Values[k];}}
public class Actual {
 public struct Presentation {public ItemClass Item;public string Title;}
 public Presentation Value;
 private Presentation ResolvePresentation(string id){return Value;}
"@+$actual+@"
}
public class Checks {
 static void A(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
 var ui=new Actual();A(ui.InspectionText(null)==string.Empty,"empty fallback");
 ui.Value=new Actual.Presentation{Title="Lost Bytes"};A(ui.InspectionText("missing")=="Lost Bytes","missing definition");
 ui.Value=new Actual.Presentation{Title="Lost Bytes",Item=new ItemClass{Key="desc"}};
 A(ui.InspectionText("song")=="Lost Bytes","untranslated description leaked key");
 Localization.Values["desc"]="A recorded song.";A(ui.InspectionText("song")=="Lost Bytes\nA recorded song.","description absent");
 Localization.Values["desc"]="Lost Bytes";A(ui.InspectionText("song")=="Lost Bytes","duplicate title");
 Localization.Values["desc"]="";A(ui.InspectionText("song")=="Lost Bytes","empty description");
 Localization.Values["desc"]="[B58CFF]Translated[-]";A(ui.InspectionText("song").EndsWith("[B58CFF]Translated[-]"),"localization refresh/markup");
 Console.WriteLine("PASS actual inspection text: description, missing definition/key, empty/duplicate text, localization refresh; native adapters doubled.");
 }
}
"@)
[Checks]::Run()
[xml]$xml=Get-Content -Raw (Join-Path $PSScriptRoot '../../Config/XUi_InGame/windows.xml')
foreach($id in @('musicSelected','musicAvailableDetail')){
 $nodes=$xml.SelectNodes("//window[@name='rebirthMusicLibraryRoot']//label[@name='$id']")
 if($nodes.Count -ne 1){throw "Expected one $id"}
 $node=$nodes[0]
 if($node.overflow -ne 'resizeheight' -or $node.support_bb_code -ne 'true' -or $node.ParentNode.LocalName -ne 'scrollview' -or $node.ParentNode.ParentNode.controller -ne 'RebirthReadableText, RebirthUtils'){throw "Unreadable detail viewport $id"}
}
if($source -notmatch 'InspectionText\(RebirthMusicLibraryClient.Items\[selected\]\)' -or $source -notmatch 'InspectionText\(inspectedAvailable.Value.Name\)'){throw 'Both inspection routes required'}
'PASS XML detail viewport and both selection routes; not a visual/game test.'