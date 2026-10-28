#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationCompletionWatch.cs'))
$start=$source.IndexOf('    internal static bool TryWatch(')
$tick=$source.IndexOf('    private static void Tick(',$start)
$end=$source.IndexOf('                if(entry.NativeRetired)',$tick)
if($start -lt 0 -or $tick -le $start -or $end -le $tick){throw 'Original watch registration/offline gate boundaries missing.'}
# The downstream reward/retirement body is deliberately excluded. This fixture
# compiles actual registration plus Tick's owner/request/deadline gate unchanged.
$body=$source.Substring($start,$end-$start)+"Passed++;}catch{throw;}}}`n"
$fixture=@'
using System;using System.Collections.Generic;
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public static class ModEvents {public struct SGameUpdateData {}}
public class Admission {public string JobId="job",CreationId="creation";}
public class Binding {public bool Online;public Admission Admission=new Admission();public object Player=new object(),Address=new object();public bool IsCurrent()=>Online;public bool TryRefreshOnlinePlayer()=>Online;}
public static class RebirthCamperStationCompletionCapture {public class SuccessfulOutput {public Binding Binding=new Binding();public bool Valid=true;public bool MatchesLiveOutput()=>Valid;}}
public static class RebirthStationLiveAccess {public static object Lease=new object();public static bool TryGetMobileLease(object p,object a,string c,out object state,out object lease){state=null;lease=Lease;return true;}}
public static class RebirthCamperStationSaveEvidence {public class Publication {}public class Request {public bool Closed;public int Disposals;public void Dispose(){Disposals++;Closed=true;}}public static int Begins;public static RebirthCamperStationCompletionCapture.SuccessfulOutput Last;public static Request BeginCompletion(RebirthCamperStationCompletionCapture.SuccessfulOutput completed){Begins++;Last=completed;return new Request();}}
public static class OfflineWatchFixture {
 private class Entry {public RebirthCamperStationCompletionCapture.SuccessfulOutput Completed;public RebirthCamperStationSaveEvidence.Request Request;public RebirthCamperStationSaveEvidence.Publication Proof=null;public object Lease;public float Deadline;public bool Releasing=false;}
 private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();private static bool installed=true;private static float next;private static int Passed;
 private static void Release(string job,Entry entry){throw new Exception("unexpected release");}
SOURCE
 private static void Check(bool b,string name){if(!b)throw new Exception(name);}
 private static void Poll(float time){UnityEngine.Time.realtimeSinceStartup=time;var d=new ModEvents.SGameUpdateData();Tick(ref d);}
 public static void Run(){var completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();Check(TryWatch(completed)&&RebirthCamperStationSaveEvidence.Begins==0&&ReferenceEquals(entries["job"].Completed,completed),"offline original event retained before native request");Poll(100);Check(RebirthCamperStationSaveEvidence.Begins==0&&Passed==0&&entries["job"].Deadline==130,"offline deadline advances without request/effects");Check(!TryWatch(new RebirthCamperStationCompletionCapture.SuccessfulOutput()),"different event cannot replace same job");completed.Binding.Online=true;Poll(101);Check(RebirthCamperStationSaveEvidence.Begins==1&&Passed==1&&ReferenceEquals(RebirthCamperStationSaveEvidence.Last,completed),"reconnect begins same original event request");var request=entries["job"].Request;completed.Binding.Online=false;Poll(500);Check(entries["job"].Deadline==530&&ReferenceEquals(request,entries["job"].Request)&&Passed==1,"disconnect midrequest pauses deadline and retains original request");request.Closed=true;completed.Binding.Online=true;Poll(501);Check(RebirthCamperStationSaveEvidence.Begins==2&&request.Disposals==1&&ReferenceEquals(RebirthCamperStationSaveEvidence.Last,completed)&&entries["job"].Deadline==531,"reconnect without proof restarts closed observation for same original event");request=entries["job"].Request;entries["job"].Proof=new RebirthCamperStationSaveEvidence.Publication();request.Closed=true;completed.Binding.Online=false;Poll(900);completed.Binding.Online=true;Poll(901);Check(RebirthCamperStationSaveEvidence.Begins==2&&ReferenceEquals(request,entries["job"].Request)&&entries["job"].Proof!=null,"reconnect with proof preserves acknowledgment without new request");ThreadManager.Main=false;int calls=Passed;Poll(1000);Check(Passed==calls,"off-thread denied");Console.WriteLine("PASS actual CompletionWatch registration/Tick offline gate: before request, midrequest deadline pause, same original event, reconnect proof/no-proof, off-thread. Downstream award/retirement body excluded; owner/native save adapters doubled.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('SOURCE',$body)
[OfflineWatchFixture]::Run()
