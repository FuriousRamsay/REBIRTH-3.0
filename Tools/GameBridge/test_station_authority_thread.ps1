#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthStationObservationDispatcher.cs'))
$start=$source.IndexOf('    internal static bool IsCurrentAuthorityThread(')
$end=$source.IndexOf('    public static void Install()',$start)
if($start -lt 0 -or $end -lt $start){throw 'Current authority method boundary missing'}
$method=$source.Substring($start,$end-$start)
$doubles=@"
public class World{public bool Remote;public bool IsRemote(){return Remote;}}
public class GameManager{public static GameManager Instance=new GameManager();public World World;}
public static class RebirthWorldCharacterRepository{public static bool IsServerAuthority=true;}
public static class RebirthSurvivorMode{public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
public static class StationThreadFixture{
private static int authorityThreadId;private static World authorityWorld;
$method
static int checks;static void Check(bool ok,string name){checks++;if(!ok)throw new System.Exception(name);}
public static string Run(){
var world=new World();GameManager.Instance.World=world;
Check(!IsCurrentAuthorityThread(world),"unbound refused");
authorityThreadId=System.Threading.Thread.CurrentThread.ManagedThreadId;authorityWorld=world;
Check(IsCurrentAuthorityThread(world),"current captured authority accepted");
bool worker=true;var thread=new System.Threading.Thread(()=>worker=IsCurrentAuthorityThread(world));thread.Start();thread.Join();Check(!worker,"worker refused");
Check(!IsCurrentAuthorityThread(new World()),"foreign world refused");
GameManager.Instance.World=new World();Check(!IsCurrentAuthorityThread(world),"world transition refused");GameManager.Instance.World=world;
world.Remote=true;Check(!IsCurrentAuthorityThread(world),"remote refused");world.Remote=false;
RebirthWorldCharacterRepository.IsServerAuthority=false;Check(!IsCurrentAuthorityThread(world),"client refused");RebirthWorldCharacterRepository.IsServerAuthority=true;
RebirthSurvivorMode.Enabled=false;Check(!IsCurrentAuthorityThread(world),"disabled mode refused");RebirthSurvivorMode.Enabled=true;
authorityWorld=null;authorityThreadId=0;Check(!IsCurrentAuthorityThread(world),"cleared lifecycle refused");
return "PASS "+checks+" actual authority method checks with world/mode/authority doubles; not native lifecycle qualification";
}}
"@
Add-Type -TypeDefinition $doubles
[StationThreadFixture]::Run()