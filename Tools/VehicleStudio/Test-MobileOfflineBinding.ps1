#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueuedAuthority.cs'))
$start=$source.IndexOf('        internal bool IsProcessingCurrent()')
$end=$source.IndexOf('        private bool MatchesCompletionIntent()',$start)
if($start -lt 0 -or $end -le $start){throw 'Processing binding predicates missing.'}
$fixture=@'
using System;using System.Collections.Generic;using System.Xml.Linq;
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public class World {public bool Remote;public Dictionary<int,object> Entities=new Dictionary<int,object>();public PlayerList Players=new PlayerList();public bool IsRemote()=>Remote;public object GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;}
public class PlayerList {public List<EntityPlayer> list=new List<EntityPlayer>();}
public class EntityPlayer {public int entityId=7;public World world;public bool Dead;public bool IsDead()=>Dead;}
public class EntityVehicle {public int entityId=9;public World world;}
public class GameManager {public static GameManager Instance=new GameManager();public World World;}
public static class GameIO {public static string GetSaveGameDir()=>"E:/_Haven/shared/REBIRTHTemp/offline-binding";}
public class Identity {public string StorageKey="owner",CanonicalId="canonical";}
public class Origin {public string CreationId="creation";}
public class OwnerRecord {public Progression Progression=new Progression();public Origin Origin=new Origin();}
public class Admission {public string JobId="job",CreationId="creation";public XElement Write()=>new XElement("admission");public XElement WritePaymentImage()=>new XElement("payment",new XAttribute("block","forge"));}
public class Publication {public bool Scope=true;public XElement Write()=>new XElement("publication");public bool MatchesSaveScope(string path)=>Scope;}
public class Progression {public Dictionary<string,object> StationCancellationAttempts=new Dictionary<string,object>(),StationCancellationRefunds=new Dictionary<string,object>(),StationPublications=new Dictionary<string,object>();public Dictionary<string,Admission> StationPreparations=new Dictionary<string,Admission>();public Dictionary<string,Publication> CamperStationPublications=new Dictionary<string,Publication>();}
public class Address {public int Vehicle=1;public string Installation="installation";}
public class Block {public string GetBlockName()=>"forge";}
public class BlockValue {public Block Block=new Block();}
public class State {public EntityVehicle BoundVehicle;public BlockValue InstalledBlock=new BlockValue();}
public static class RebirthWorldCharacterRepository {public static bool Cached=true;public static bool IsCurrentCachedRecord(OwnerRecord owner)=>Cached;}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b)=>a==b;}
public static class RebirthWorldCharacterService {public static OwnerRecord Owner;public static EntityPlayer Authenticated;public static Identity Identity=new Identity();public static bool TryGetIdentity(EntityPlayer p,out Identity id){id=Identity;return ReferenceEquals(p,Authenticated);}public static bool TryGet(EntityPlayer p,out OwnerRecord owner){owner=Owner;return ReferenceEquals(p,Authenticated);}}
public class RebirthCamperModuleRuntime {public bool TransferPending;public int Identity=1;public Manifest Stations=new Manifest();public static RebirthCamperModuleRuntime Current=new RebirthCamperModuleRuntime();public static RebirthCamperModuleRuntime For(EntityVehicle v)=>Current;public class Installed {public State State;}public class Manifest {public Installed Installation;public Installed Find(string key)=>key=="installation"?Installation:null;}}
public static class RebirthCamperVehicleIdentity {public static bool Unique=true;public static bool IsUniqueCurrent(EntityVehicle v,int id)=>Unique;}
public class BindingFixture {
 public EntityVehicle Vehicle;public EntityPlayer Player;public World World;public OwnerRecord Owner;public Progression Progression;public Identity Identity=new Identity();public State Station;public Address Address=new Address();public Admission Admission=new Admission();public Publication Publication=new Publication();public string SaveRoot=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());public bool Intent;
 private bool MatchesCompletionIntent()=>!Intent;
 public bool IsCurrent()=>IsOwnerCurrent()&&MatchesCompletionIntent()&&!Progression.StationCancellationAttempts.ContainsKey(Admission.JobId)&&!Progression.StationCancellationRefunds.ContainsKey(Admission.JobId);
SOURCE
 private static void Check(bool b,string name){if(!b)throw new Exception(name);}
 public static void Run(){var w=new World();var p=new EntityPlayer{world=w};var v=new EntityVehicle{world=w};var owner=new OwnerRecord();var state=new State{BoundVehicle=v};var b=new BindingFixture{World=w,Player=p,Vehicle=v,Owner=owner,Progression=owner.Progression,Station=state};w.Entities[p.entityId]=p;w.Entities[v.entityId]=v;w.Players.list.Add(p);GameManager.Instance.World=w;RebirthWorldCharacterService.Owner=owner;RebirthWorldCharacterService.Authenticated=p;owner.Progression.StationPreparations.Add("job",b.Admission);owner.Progression.CamperStationPublications.Add("job",b.Publication);RebirthCamperModuleRuntime.Current.Stations.Installation=new RebirthCamperModuleRuntime.Installed{State=state};
  Check(b.IsCurrent()&&b.IsProcessingCurrent(),"original live processing and effects");p.Dead=true;Check(!b.IsCurrent()&&b.IsProcessingCurrent(),"dead owner preserves paid processing but denies effects");w.Entities.Remove(p.entityId);w.Players.list.Clear();RebirthWorldCharacterService.Authenticated=null;Check(b.IsProcessingCurrent()&&!b.TryRefreshOnlinePlayer(),"offline processing without reward authority");
  var foreign=new EntityPlayer{entityId=8,world=w};w.Entities[8]=foreign;w.Players.list.Add(foreign);Check(!b.TryRefreshPlayer(foreign)&&ReferenceEquals(b.Player,p),"unauthenticated replacement denied");var returning=new EntityPlayer{entityId=10,world=w};w.Entities[10]=returning;w.Players.list.Add(returning);RebirthWorldCharacterService.Authenticated=returning;Check(b.TryRefreshOnlinePlayer()&&ReferenceEquals(b.Player,returning)&&b.IsCurrent(),"same stable owner reconnect refreshes effect destination");
  owner.Progression.StationCancellationAttempts.Add("job",new object());Check(!b.IsProcessingCurrent(),"cancellation excludes processing");owner.Progression.StationCancellationAttempts.Clear();b.Intent=true;Check(!b.IsProcessingCurrent(),"terminal choice excludes processing");b.Intent=false;RebirthWorldCharacterRepository.Cached=false;Check(!b.IsProcessingCurrent(),"retained owner cache scope required");RebirthWorldCharacterRepository.Cached=true;RebirthCamperModuleRuntime.Current.TransferPending=true;Check(!b.IsProcessingCurrent(),"transfer custody excludes processing");RebirthCamperModuleRuntime.Current.TransferPending=false;RebirthCamperModuleRuntime.Current.Stations.Installation.State=new State();Check(!b.IsProcessingCurrent(),"station instance substitution denied");RebirthCamperModuleRuntime.Current.Stations.Installation.State=state;ThreadManager.Main=false;Check(!b.IsProcessingCurrent()&&!b.TryRefreshOnlinePlayer(),"off-thread denied");
  Console.WriteLine("PASS actual processing/owner predicates and reconnect methods: dead/offline paid authority, no offline effects, exact authenticated owner refresh, cancellation/terminal/cache/transfer/instance/thread denial. Native/service/cache adapters doubled; no offline gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $fixture.Replace('SOURCE',$source.Substring($start,$end-$start))
[BindingFixture]::Run()
