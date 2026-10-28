#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$leaseSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationSaveEvidence.cs'))
$leaseStart=$leaseSource.IndexOf('    internal static Request BeginRecovery(')
$leaseEnd=$leaseSource.IndexOf('    internal static Request BeginCompletion(',$leaseStart)
if($leaseStart -lt 0 -or $leaseEnd -le $leaseStart){throw 'Actual recovery lease method missing.'}
$leaseFixture=@'
using System;
class EntityPlayer{}
struct RebirthCamperStationAddress{public int Id;}
class RebirthCamperStationState{}
class Origin{public string CreationId="creation";}
class Owner{public Origin Origin=new Origin();}
static class RebirthWorldCharacterService{public static Owner Owner=new Owner();public static bool TryGet(EntityPlayer player,out Owner owner){owner=Owner;return player!=null;}}
static class RebirthStationLiveAccess{
 public static object Lease=new object();public static RebirthCamperStationState State=new RebirthCamperStationState();
 public static bool TryGetMobileLease(EntityPlayer player,RebirthCamperStationAddress address,string creation,out RebirthCamperStationState state,out object lease){state=State;lease=Lease;return player!=null&&lease!=null&&address.Id==1&&creation==RebirthWorldCharacterService.Owner.Origin.CreationId;}
}
static class RebirthCamperStationSaveEvidence{
 public class Request{public Func<bool> Current;}
 public static int Begins;
 private static Request BeginCore(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current){if(!current())throw new Exception("stale");Begins++;return new Request{Current=current};}
METHOD_BODY
}
public static class MobileRecoveryLeaseFixture{
 private static void Check(bool value,string label){if(!value)throw new Exception(label);}
 public static void Run(){
  var player=new EntityPlayer();var state=RebirthStationLiveAccess.State;var address=new RebirthCamperStationAddress{Id=1};
  var request=RebirthCamperStationSaveEvidence.BeginRecovery(player,state,address,()=>true);
  Check(request.Current()&&RebirthCamperStationSaveEvidence.Begins==1,"actual method accepts exact generalized authenticated remote lease");
  RebirthStationLiveAccess.Lease=new object();Check(!request.Current(),"replacement lease invalidates retained native request");
  var request2=RebirthCamperStationSaveEvidence.BeginRecovery(player,state,address,()=>true);
  RebirthWorldCharacterService.Owner.Origin.CreationId="replacement";Check(!request2.Current(),"creation change invalidates captured lease scope");
  RebirthStationLiveAccess.Lease=null;bool refused=false;try{RebirthCamperStationSaveEvidence.BeginRecovery(player,state,address,()=>true);}catch(InvalidOperationException){refused=true;}
  Check(refused&&RebirthCamperStationSaveEvidence.Begins==2,"missing lease refuses before native save effects");
  Console.WriteLine("PASS actual native recovery generalized lease method fixture");
 }
}
'@
Add-Type -TypeDefinition $leaseFixture.Replace('METHOD_BODY',$leaseSource.Substring($leaseStart,$leaseEnd-$leaseStart))
[MobileRecoveryLeaseFixture]::Run()
