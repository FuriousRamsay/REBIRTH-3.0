$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../..'
$source=Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs')
$scope=Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs')
$start=$source.IndexOf('    private static bool MatchesCharacter(')
$end=$source.IndexOf('    private static void OnStart(', $start)
if($start -lt 0 -or $end -le $start){throw 'Production lifecycle methods missing'}
$methods=$source.Substring($start,$end-$start).Replace('record.Origin?.CreationId','(record.Origin==null?null:record.Origin.CreationId)')
Add-Type -TypeDefinition ($scope + @'
public class Origin {public string CreationId;}
public class RebirthWorldCharacterRecord {public bool IsComplete=true;public Origin Origin=new Origin();}
public class EntityPlayer {public bool Dead,Held;public int Cleared;public RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public bool IsDead(){return Dead;}}
public static class RebirthCharacterCreationHoldService {public static bool IsHeld(EntityPlayer p){return p.Held;}}
public static class RebirthWorldCharacterService {public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=p.Record;return r!=null;}}
public class World {public bool Remote;public System.Collections.Generic.Dictionary<int,EntityPlayer> Players=new System.Collections.Generic.Dictionary<int,EntityPlayer>();public bool IsRemote(){return Remote;}public EntityPlayer GetEntity(int id){EntityPlayer p;return Players.TryGetValue(id,out p)?p:null;}}
public class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public static class Time {public static float realtimeSinceStartup;}
public static class ModEvents {public struct SGameUpdateData {}}
public static class RageLifecycleChecks {
 private class ActiveState {public string CreationId;public float Until;}
 private static System.Collections.Generic.Dictionary<int,ActiveState> Active=new System.Collections.Generic.Dictionary<int,ActiveState>();
 private static int[] updateIds=new int[1];
 private static void SetMarkers(EntityPlayer p,ActiveState s){p.Cleared++;}
'@ + $methods + @'
 public static void Run(){
  var world=GameManager.Instance.World;string id=Guid.NewGuid().ToString("N");
  for(int i=0;i<7;i++){var p=new EntityPlayer();p.Record.Origin.CreationId=id;world.Players[i]=p;Active[i]=new ActiveState{CreationId=id,Until=20};}
  world.Players[1].Dead=true;world.Players[2].Held=true;world.Players[3].Record.Origin.CreationId=Guid.NewGuid().ToString("N");
  world.Players[4].Record.IsComplete=false;world.Players.Remove(5);Active[6].Until=0;
  var data=new ModEvents.SGameUpdateData();OnUpdate(ref data);
  if(Active.Count!=1||!Active.ContainsKey(0))throw new Exception("invalid states not removed or valid state lost");
  foreach(int i in new[]{1,2,3,4,6})if(world.Players[i].Cleared!=1)throw new Exception("markers not cleared exactly once");
  if(world.Players[0].Cleared!=0)throw new Exception("valid markers cleared");
  world.Remote=true;Time.realtimeSinceStartup=30;OnUpdate(ref data);
  if(Active.Count!=1)throw new Exception("client mutated authoritative state");
  world.Remote=false;OnUpdate(ref data);OnUpdate(ref data);
  if(Active.Count!=0||world.Players[0].Cleared!=1)throw new Exception("expiry/empty tick incorrect");
 }
}
'@)
[RageLifecycleChecks]::Run()
Write-Output 'PASS: production Rage cleanup handles valid/dead/held/replaced/incomplete/missing/expired players, array growth, remote and empty ticks. Native game types stubbed.'
