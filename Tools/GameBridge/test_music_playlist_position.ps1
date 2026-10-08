$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Progression/RebirthLegacyMusicPlaybackService.cs')
$start=$source.IndexOf('    public static void StepLibrary(')
$end=$source.IndexOf('    public static void TogglePause(', $start)
if($start -lt 0 -or $end -le $start){throw 'Production playlist helper missing'}
Add-Type -TypeDefinition ('using System;
public class EntityPlayerLocal {}
public static class RebirthMusicLibraryClient {
 public static bool Shuffle;
 public static System.Collections.Generic.List<string> Items=new System.Collections.Generic.List<string>();
 public static bool EnsureCurrent(EntityPlayerLocal p){return true;}
}
namespace UnityEngine {public static class Random {public static int Range(int a,int b){return a;}}}
public static class PlaylistChecks {
 private static int playlistIndex=-1,played=-1,currentCassetteIndex=-1;
 private static bool HasCurrentPlaybackOwner(EntityPlayerLocal player){return true;}
 private static string currentCassetteId="";
 private static void Stop(out string message){message="";played=-1;}
 private static void PlayLibrary(EntityPlayerLocal p,int index){played=index;}
' + $source.Substring($start,$end-$start) + @'
 public static void Run(){
  var items=new[]{"A","B","A","C"};
  if(ResolvePlayingIndex(items,2,"A")!=2)throw new Exception("duplicate jumped to first slot");
  int next=(ResolvePlayingIndex(items,2,"A")+1)%items.Length;
  if(items[next]!="C")throw new Exception("ordered playback skipped following song");
  if(ResolvePlayingIndex(new[]{"B","A","C"},2,"A")!=1)throw new Exception("shifted track not found");
  if(ResolvePlayingIndex(new[]{"B","C"},2,"A")!=-1)throw new Exception("removed track retained");
  if(ResolvePlayingIndex(new[]{"A"},9,"A")!=0)throw new Exception("outdated index not recovered");
  if(ResolvePlayingIndex(null,0,"A")!=-1||ResolvePlayingIndex(items,0,"")!=-1)throw new Exception("invalid input accepted");
  RebirthMusicLibraryClient.Items.AddRange(new[]{"C","B","A","D"});
  playlistIndex=0;currentCassetteId="A";
  StepLibrary(new EntityPlayerLocal(),1);
  if(playlistIndex!=2||played!=3)throw new Exception("immediate Next used pre-reorder position");
  RebirthMusicLibraryClient.Items.Clear();RebirthMusicLibraryClient.Items.AddRange(items);
  playlistIndex=2;currentCassetteId="A";StepLibrary(new EntityPlayerLocal(),1);
  if(played!=3)throw new Exception("Next lost duplicate position");
 }
}
'@)
[PlaylistChecks]::Run()
Write-Output 'PASS: duplicate playlist position, ordered successor, shifted/removed entries, invalid indices, actual StepLibrary immediate reorder and duplicate successor (native adapters doubled).'
