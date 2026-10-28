#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperService=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationTrainingService.cs'))
$camperService=[regex]::Replace($camperService,'(?m)^using [^;]+;\r?\n','')
# Native owner saves, original event authority and award service are explicit doubles.
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Xml.Linq;
public static class ThreadManager{public static bool IsMainThread()=>true;}
public class FixtureAdmission{public string JobId="job";}
public class FixtureProgression{public Dictionary<string,RebirthCamperStationTrainingRecord> CamperStationTrainingPlans=new Dictionary<string,RebirthCamperStationTrainingRecord>();}
public class FixtureBinding{public FixtureProgression Progression=new FixtureProgression();public FixtureAdmission Admission=new FixtureAdmission();public object Identity,Publication,Owner,Player;}
public class RebirthCamperStationTrainingPlan{public FixtureBinding Binding=new FixtureBinding();public object Publication,Model;public string Skill="skill",Source="craft:skill:recipe",Receipt="receipt";public int Cycles=2;public float PreparationMultiplier=1;public bool HasTraining=true,Current=true;public bool IsCurrent()=>Current;}
public class RebirthCamperStationTrainingRecord{public string Receipt="receipt";public XElement Write()=>new XElement("training",new XAttribute("receipt",Receipt));public RebirthCamperStationTrainingRecord Clone()=>new RebirthCamperStationTrainingRecord{Receipt=Receipt};public static bool TryCreate(RebirthCamperStationTrainingPlan p,out RebirthCamperStationTrainingRecord r){r=new RebirthCamperStationTrainingRecord();return p.Current;}}
public static class RebirthWorldCharacterService{public static bool Flush=true;public static void MarkDirty(object o,string s){}public static bool FlushPlayer(object p,string s)=>Flush;}
public static class RebirthWorldCharacterRepository{public static bool Saved=true,AwardSaved=true;public static bool HasSavedCamperStationTrainingPlan(object i,FixtureAdmission a,object q,object c,RebirthCamperStationTrainingRecord r,bool requireAwardReceipt=false)=>Saved&&(!requireAwardReceipt||AwardSaved);}
public static class RebirthSkillAwardService{public static int Calls;public static bool Result=true;public static bool TryAwardCraft(object p,string s,object m,int c,float f,string source,string receipt){Calls++;return Result;}}
public static class MobileTrainingServiceFixture{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){var p=new RebirthCamperStationTrainingPlan();
 RebirthWorldCharacterService.Flush=false;Check(!RebirthCamperStationTrainingService.TryGrant(p)&&RebirthSkillAwardService.Calls==0,"flush failure blocks grant");
 RebirthWorldCharacterService.Flush=true;RebirthWorldCharacterRepository.Saved=false;Check(!RebirthCamperStationTrainingService.TryGrant(p)&&RebirthSkillAwardService.Calls==0,"primary acknowledgement required");
 RebirthWorldCharacterRepository.Saved=true;RebirthWorldCharacterRepository.AwardSaved=false;Check(!RebirthCamperStationTrainingService.TryGrant(p),"award primary receipt required");
 RebirthWorldCharacterRepository.AwardSaved=true;Check(RebirthCamperStationTrainingService.TryGrant(p),"successful retained retry");
 p.HasTraining=false;int before=RebirthSkillAwardService.Calls;Check(RebirthCamperStationTrainingService.TryGrant(p)&&before==RebirthSkillAwardService.Calls,"unclassified no-training decision");
 p.Current=false;Check(!RebirthCamperStationTrainingService.TryGrant(p)&&before==RebirthSkillAwardService.Calls,"stale original authority");
 Console.WriteLine("PASS actual mobile training service save/grant ordering. Owner save/event/award doubles; does not prove native duplicate suppression or gameplay.");}
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperService)
[MobileTrainingServiceFixture]::Run()
