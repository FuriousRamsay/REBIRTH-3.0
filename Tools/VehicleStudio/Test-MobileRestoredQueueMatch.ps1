#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperQueuedPublicationMatch.cs'))
$camperFixture=@'
using System;
using System.IO;
using System.Xml.Linq;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public static class GameIO { public static string GetSaveGameDir()=>"C:/fixture/save"; }
public class RebirthCamperStationAddress { }
public class Recipe { public string Job="job";public bool Marked=true,Matches=true; }
public class RecipeQueueItem { public Recipe Recipe=new Recipe();public int Multiplier=1,StartingEntityId=7,AmountToRepair,Quality=2;public object RepairItem;public float CraftingTimeLeft=3,OneItemCraftTime=3; }
public class Block { public string GetBlockName()=>"workbench"; }
public class Installed { public Block Block=new Block(); }
public class RebirthCamperStationState { public object InstallationId;public RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};public string[] MaterialNames=new string[0];public object Input=new object();public int InputSlotCount=1;public Installed InstalledBlock=new Installed(); }
public static class RebirthCamperStationSaveEvidence { public class Publication { public RebirthCamperStationAddress Address;public string SaveDirectory="C:/fixture/save";public RebirthCamperStationState State;public RebirthCamperStationState RestoreStationSnapshot()=>State; } }
public static class RebirthStationInputLayout { public static bool Valid=true;public static bool TryReadPhysical(object i,int slots,int materials,out object input){input=i;return Valid;} }
public static class RebirthStationGridQueue { public static bool IsMarked(Recipe r)=>r!=null&&r.Marked;public static bool TryGetJobId(Recipe r,out string job){job=r.Job;return job!=null;} }
public static class RebirthStationMaterialInputCodec {public static bool TryWrite(object s,int p,out XElement image){image=null;return false;}public static bool TryRead(XElement image,out object s,out int p){s=null;p=0;return false;}}
public class RebirthStationGridAdmission { public bool IsMaterialPayment=false;
 public bool IsPublicationAttempted=true,InputMatches=true;public string JobId="job",CreationId="creation";public RebirthCamperStationAddress Address=new RebirthCamperStationAddress();
 public bool TryGetMobileAddress(out RebirthCamperStationAddress a){a=Address;return true;}
 public bool MatchesQueuedQuality(Recipe r,int multiplier,int quality)=>r.Matches&&quality==2;
 public bool MatchesQueuedRecipe(Recipe r,int multiplier)=>r.Matches;
 public bool MatchesMobileQueuedObservation(string creation,RebirthCamperStationAddress a,string block,object input,Recipe r,int multiplier)=>InputMatches&&r.Matches;
 public XElement Write()=>new XElement("admission",new XAttribute("block","workbench"));
}
public static class RestoredQueueFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
  var a=new RebirthStationGridAdmission();var s=new RebirthCamperStationState{InstallationId=a.Address.Installation};var proof=new RebirthCamperStationSaveEvidence.Publication{Address=a.Address,State=s};
  Check(RebirthCamperQueuedPublicationMatch.Matches(proof,a,7),"original matching payment image");a.InputMatches=false;
  Check(!RebirthCamperQueuedPublicationMatch.Matches(proof,a,7)&&RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"legitimate later input differs only in recovery correlation");
  s.Queue[0].Recipe.Matches=false;Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"paid recipe fragments still checked");s.Queue[0].Recipe.Matches=true;
  s.Queue[0].StartingEntityId=8;Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"wrong actor denied");s.Queue[0].StartingEntityId=7;
  s.Queue=new[]{s.Queue[0],s.Queue[0]};Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"duplicate job denied");s.Queue=new[]{s.Queue[0]};
  s.Queue[0].CraftingTimeLeft=float.NaN;Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"invalid countdown denied");s.Queue[0].CraftingTimeLeft=1;
  RebirthStationInputLayout.Valid=false;Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"malformed physical input denied");RebirthStationInputLayout.Valid=true;
  proof.SaveDirectory="C:/fixture/other";Check(!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,a,7),"different save denied");
  Console.WriteLine("PASS actual restored queue correlation: later input edits allowed; original payment image strict; paid recipe/actor/uniqueness/countdown/input/save checks. Adapters doubled; no restored authority or gameplay proof.");
 }
}
'@
$camperFixture=$camperFixture.Replace('public class RebirthCamperStationAddress { }','public class RebirthCamperStationAddress { public object Installation=new object(); }')
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource.Replace('using System;','').Replace('using System.IO;',''))
[RestoredQueueFixture]::Run()
