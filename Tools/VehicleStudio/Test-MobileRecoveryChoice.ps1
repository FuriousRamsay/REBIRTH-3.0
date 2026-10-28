#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationTrainingRecovery.cs'))
$camperMatch=[regex]::Match($camperSource,'(?s)    private bool MatchesCompletionIntent\(\).*?\r?\n    }')
if(!$camperMatch.Success){throw 'Recovery choice method missing.'}
$camperCodec=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Survivor/Persistence/RebirthStationTerminalIntent.cs'))
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
CODEC_BODY
public class RebirthStationGridAdmission
{
 public bool IsPerBatch=false;public int OriginalBatches=1;public string JobId="job",CreationId="creation";public bool IsPublicationAttempted=true;
 public XElement Write()=>new XElement("admission",new XAttribute("job",JobId),new XAttribute("creation",CreationId));
}
public class Progression { public Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new Dictionary<string,RebirthStationTerminalIntent>(); }
public class Owner { public Progression Progression=new Progression(); }
public static class RebirthWorldCharacterRepository
{
 public static bool Saved;public static bool HasSavedStationTerminalIntent(object identity,RebirthStationGridAdmission a,RebirthStationTerminalIntent i)=>Saved;
}
public class ChoiceFixture
{
 private Owner owner=new Owner();private object identity=new object();private RebirthStationGridAdmission admission=new RebirthStationGridAdmission();
 private RebirthStationTerminalIntent completionIntent;
 private bool intentPending;
MATCH_BODY
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Run()
 {
  var f=new ChoiceFixture();Check(f.MatchesCompletionIntent(),"unfinished no-choice recovery preserved");
  Check(RebirthStationTerminalIntent.TryCreate(f.admission,true,7,out var complete),"native completion choice codec");
  f.owner.Progression.StationTerminalIntents.Add("job",complete);
  Check(!f.MatchesCompletionIntent(),"choice added after capture invalidates recovery");
  f.completionIntent=complete.Clone();Check(!f.MatchesCompletionIntent(),"unsaved choice is not authority");
  f.intentPending=true;Check(f.MatchesCompletionIntent(),"original locally staged choice permits save retry");f.intentPending=false;
  RebirthWorldCharacterRepository.Saved=true;Check(f.MatchesCompletionIntent(),"exact saved completion accepted");
  RebirthStationTerminalIntent.TryCreate(f.admission,false,7,out var cancel);f.owner.Progression.StationTerminalIntents["job"]=cancel;
  Check(!f.MatchesCompletionIntent(),"changed cancellation refused");f.completionIntent=cancel.Clone();
  Check(!f.MatchesCompletionIntent(),"captured cancellation refused");f.completionIntent=complete.Clone();
  RebirthStationTerminalIntent.TryCreate(f.admission,true,8,out var actorChanged);f.owner.Progression.StationTerminalIntents["job"]=actorChanged;
  Check(!f.MatchesCompletionIntent(),"changed actor choice refused");f.owner.Progression.StationTerminalIntents["job"]=complete;
  f.admission.CreationId="other";Check(!f.MatchesCompletionIntent(),"changed admission binding refused");
  Console.WriteLine("PASS actual recovery completion-choice filter and terminal-intent codec: exact saved completion, absent/changed/cancellation/actor/admission cases. Primary-file witness doubled; no reload gameplay proof.");
 }
}
'@
$camperFixture=$camperFixture.Replace('CODEC_BODY',[regex]::Replace($camperCodec,'(?m)^using [^;]+;\r?\n','')).Replace('MATCH_BODY',$camperMatch.Value)
Add-Type -TypeDefinition $camperFixture
[ChoiceFixture]::Run()
