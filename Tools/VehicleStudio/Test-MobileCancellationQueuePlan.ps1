#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperPlan=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationCancellationQueuePlan.cs')).Replace('using System.Collections.Generic;','')
$camperNative=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Crafting/UI/RebirthStationNativeQueueEntry.cs'))
$camperMethod=[regex]::Match($camperNative,'(?s)    internal static bool TryBuildCancellation\(.*?\r?\n    }')
if(!$camperMethod.Success){throw 'Native cancellation planning method missing.'}
$camperFixture=@'
using System;using System.Collections.Generic;using System.Xml.Linq;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class Recipe { public string Job="job";public bool Marked=true; }
public class RecipeQueueItem { public Recipe Recipe=new Recipe();public int StartingEntityId=7,Multiplier=1,Quality=2,AmountToRepair;public object RepairItem;public float CraftingTimeLeft=3,OneItemCraftTime=4;public bool IsCrafting=true; }
public static class RebirthStationGridQueue { public static bool HasValidMultiplier(Recipe r,int n)=>n>=1&&n<=3;public static bool IsMarked(Recipe r)=>r!=null&&r.Marked;public static bool TryGetJobId(Recipe r,out string j){j=r.Job;return j!=null;} }
public class RebirthStationGridAdmission { public bool Mobile=true;public string JobId="job";public bool TryGetMobileAddress(out object a){a=new object();return Mobile;}public bool MatchesQueuedQuality(Recipe r,int n,int q)=>r.Job==JobId&&n>=1&&n<=3&&q==2; }
public class RebirthStationTerminalIntent {
 public int RemainingBatches=1;public bool IsCompletion;public int Actor=99;public string Job="job";
 public XElement Write()=>new XElement("choice",new XAttribute("actor",Actor),new XAttribute("job",Job),new XAttribute("remaining",RemainingBatches),new XAttribute("completion",IsCompletion));
 public static bool TryRead(XElement n,RebirthStationGridAdmission a,out RebirthStationTerminalIntent i){i=new RebirthStationTerminalIntent{RemainingBatches=(int)n.Attribute("remaining"),Actor=(int)n.Attribute("actor"),Job=(string)n.Attribute("job"),IsCompletion=(bool)n.Attribute("completion")};return i.Actor>0&&i.Job==a.JobId;}
 public static bool TryCreate(RebirthStationGridAdmission a,bool done,int actor,int remaining,out RebirthStationTerminalIntent i){i=new RebirthStationTerminalIntent{RemainingBatches=remaining,Actor=actor,Job=a.JobId,IsCompletion=done};return actor>0;}
}
public static class RebirthStationNativeQueueEntry {
NATIVE_METHOD
}
public static class CancellationPlanFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){var a=new RebirthStationGridAdmission();var choice=new RebirthStationTerminalIntent();var own=new RecipeQueueItem();var other=new RecipeQueueItem{Recipe=new Recipe{Job="other"}};var rows=new[]{own,other};
  Check(RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out var replacement)&&replacement[0].Recipe==null&&ReferenceEquals(replacement[1],other)&&ReferenceEquals(rows[0],own)&&choice.Actor==99&&own.StartingEntityId==7,"distinct requester, one detached removal, unrelated/current custody unchanged");
  Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,99,out _),"wrong saved actor refused");choice.IsCompletion=true;Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"completion choice refused");choice.IsCompletion=false;
  choice.Job="other";Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"different choice job refused");choice.Job="job";
  Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(new[]{own,own},a,choice,7,out _),"duplicate paid job refused");own.Quality=3;Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"paid quality preserved");own.Quality=2;
  own.Multiplier=2;Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"stale remaining choice denied");choice.RemainingBatches=2;Check(RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"exact partial remaining accepted");own.Multiplier=1;choice.RemainingBatches=1;a.Mobile=false;Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"terrain admission refused");a.Mobile=true;ThreadManager.Main=false;Check(!RebirthCamperStationCancellationQueuePlan.TryBuild(rows,a,choice,7,out _),"off-thread refused");
  Console.WriteLine("PASS actual mobile planner plus native removal builder: distinct current requester/original recipe actor, detached one-job removal, unchanged custody, wrong actor/choice/job/quality/duplicates/terrain/thread. Codecs/native adapters doubled; no removal/refund authority or gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture.Replace('NATIVE_METHOD',$camperMethod.Value)+"`n"+$camperPlan)
[CancellationPlanFixture]::Run()
