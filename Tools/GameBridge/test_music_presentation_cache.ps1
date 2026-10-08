$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/UI/XUiC_RebirthMusicLibrary.cs')
$a=$source.IndexOf('    private struct Presentation')
$b=$source.IndexOf('    private readonly XUiController[] slots',$a)
if($a -lt 0 -or $b -lt 0){throw 'Presentation cache source seam missing'}
$actual=$source.Substring($a,$b-$a).Replace('private struct Presentation','public struct Presentation').Replace('private Presentation ResolvePresentation','public Presentation ResolvePresentation')
Add-Type -TypeDefinition (@'
using System;using System.Collections.Generic;
public class ItemClass {public string Name;public static int Calls;public static Dictionary<string,ItemClass> Items=new Dictionary<string,ItemClass>();public static ItemClass GetItemClass(string id,bool warning){Calls++;ItemClass c;Items.TryGetValue(id,out c);return c;}}
public static class RebirthLegacyMusicPlaybackService {public static int Calls;public static string Language="en";public static string GetSongName(ItemClass c,string fallback){Calls++;return Language+":"+(c==null?fallback:c.Name);}}
public class Actual {
'@+$actual+@'
 public void BeginRender(){presentation.Clear();}
}
public class Checks {
static void A(bool v,string n){if(!v)throw new Exception(n);}
public static void Run(){
 var ui=new Actual();var old=new ItemClass{Name="Song"};ItemClass.Items["cassette"]=old;
 var first=ui.ResolvePresentation("cassette");var again=ui.ResolvePresentation("cassette");
 A(ItemClass.Calls==1&&RebirthLegacyMusicPlaybackService.Calls==1&&Object.ReferenceEquals(first.Item,again.Item)&&first.Title==again.Title,"duplicate icon/title lookup not reused");
 ui.ResolvePresentation("missing");ui.ResolvePresentation("missing");A(ItemClass.Calls==2,"missing definition repeatedly resolved within render");
 RebirthLegacyMusicPlaybackService.Language="fr";var next=new ItemClass{Name="Changed"};ItemClass.Items["cassette"]=next;ItemClass.Items["missing"]=next;ui.BeginRender();
 var changed=ui.ResolvePresentation("cassette");A(Object.ReferenceEquals(changed.Item,next)&&changed.Title=="fr:Changed","definition/language stayed stale next render");
 A(ui.ResolvePresentation("missing").Item==next,"previous missing definition cached across refresh");
 int calls=ItemClass.Calls;A(ui.ResolvePresentation(null).Item==null&&ui.ResolvePresentation("").Item==null&&ItemClass.Calls==calls,"empty preview performed lookup");
 var other=new Actual();other.ResolvePresentation("cassette");A(ItemClass.Calls==calls+1,"cache leaked across controller identity");
 Console.WriteLine("PASS actual render-scoped presentation cache: once per ID within render, missing/empty IDs, definition and language refresh, isolated controllers; native registry/title adapters doubled.");
}}
'@)
[Checks]::Run()
if($source -notmatch 'private void Render\(\)\s*\{\s*presentation.Clear\(\);'){throw 'Render invalidation missing'}
if(([regex]::Matches($source,'presentation.Clear\(\);UnbindPressHandlers')).Count -ne 2){throw 'Init/Cleanup cache release missing'}
if($source -match 'ItemClass.GetItem\('){throw 'Allocating ItemValue lookup remains'}
