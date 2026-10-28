#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSourceRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperPreparation=[IO.File]::ReadAllText((Join-Path $camperSourceRoot 'Scripts/Crafting/UI/RebirthStationPreparationService.cs'))
$camperSealStart=$camperPreparation.IndexOf('    private static bool HasNoPublication(')
$camperSealEnd=$camperPreparation.IndexOf('    private static bool TryPhysicalInput(', $camperSealStart)
if($camperSealStart -lt 0 -or $camperSealEnd -le $camperSealStart){throw 'Current retry gate boundary missing'}
$camperSeal=$camperPreparation.Substring($camperSealStart,$camperSealEnd-$camperSealStart).Replace('private static bool HasNoPublication','public static bool HasNoPublication')
$camperFixture=@'
using System;using System.Collections.Generic;
public class Progression {public Dictionary<string,object> StationPublications=new Dictionary<string,object>(),CamperStationPublications=new Dictionary<string,object>();}
public class RebirthWorldCharacterRecord{public Progression Progression=new Progression();}
public class Recipe{public bool Marked;public string Job;}
public class RecipeQueueItem{public Recipe Recipe;}
public class TileEntityWorkstation{public RecipeQueueItem[] Queue=new RecipeQueueItem[0];}
public static class RebirthStationGridQueue{public static bool IsMarked(Recipe r)=>r.Marked;public static bool TryGetJobId(Recipe r,out string job){job=r.Job;return Guid.TryParseExact(job,"N",out _);}}
public static class Subject {
METHOD
}
public static class RetrySealFixture{
static void Check(bool result,string label){if(!result)throw new Exception(label);}
public static void Run(){var job=Guid.NewGuid();var owner=new RebirthWorldCharacterRecord();var station=new TileEntityWorkstation();
Check(Subject.HasNoPublication(owner,station,job),"empty retry preflight");
owner.Progression.CamperStationPublications.Add(job.ToString("N"),new object());Check(!Subject.HasNoPublication(owner,station,job),"mobile publication blocks retry");owner.Progression.CamperStationPublications.Clear();
owner.Progression.StationPublications.Add(job.ToString("N"),new object());Check(!Subject.HasNoPublication(owner,station,job),"stationary publication blocks retry");owner.Progression.StationPublications.Clear();
station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Marked=true,Job=job.ToString("N")}}};Check(!Subject.HasNoPublication(owner,station,job),"existing paid job blocks retry");
station.Queue[0].Recipe.Job=Guid.NewGuid().ToString("N");Check(Subject.HasNoPublication(owner,station,job),"unrelated valid job");
station.Queue[0].Recipe.Job="bad";Check(!Subject.HasNoPublication(owner,station,job),"malformed marked queue refuses retry");
station.Queue=null;Check(!Subject.HasNoPublication(owner,station,job),"missing queue");
Console.WriteLine("PASS current production retry gate: mobile/static receipts, duplicate job, malformed queue and missing queue refused. Gate visibility changed for test; owner/queue adapters doubled.");}}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD',$camperSeal)
[RetrySealFixture]::Run()
