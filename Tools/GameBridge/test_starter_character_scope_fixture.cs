using System;
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class EntityPlayerLocal {public World world=new World();}
class Origin {public string CreationId="current",BackgroundId="chef";}
class RebirthWorldCharacterRecord {public bool IsComplete=true;public Origin Origin=new Origin();}
class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool TryGet(EntityPlayerLocal p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
class OwnerSnapshot {public bool RebirthModeEnabled=true,HasCharacter=true;public string CreationId="current",BackgroundId="chef";}
class RebirthSurvivorClientState {public static OwnerSnapshot Snapshot=new OwnerSnapshot();public static OwnerSnapshot GetOwnerStateSnapshot(){return Snapshot;}public static string Creation="current";public static string GetProjectedCreationId(EntityPlayerLocal p){return Creation;}}
class RebirthSurvivorRequestScope {public static bool Matches(string a,string b){return !string.IsNullOrEmpty(a)&&a==b;}}
class Check {
// METHODS
static void Assert(bool b){if(!b)throw new Exception("scope assertion");}
static void Main(){var p=new EntityPlayerLocal();Assert(IsCurrentCharacter(p,"current","chef"));Assert(!IsCurrentCharacter(p,"old","chef"));Assert(!IsCurrentCharacter(p,"current","other"));RebirthWorldCharacterService.Record.IsComplete=false;Assert(!IsCurrentCharacter(p,"current","chef"));p.world.Remote=true;Assert(IsCurrentCharacter(p,"current","chef"));Assert(!IsCurrentCharacter(p,"current","other"));RebirthSurvivorClientState.Snapshot.HasCharacter=false;Assert(!IsCurrentCharacter(p,"current","chef"));RebirthSurvivorClientState.Snapshot.HasCharacter=true;Assert(!IsCurrentCharacter(p,"old","chef"));RebirthSurvivorClientState.Creation="";Assert(!IsCurrentCharacter(p,"current","chef"));Assert(!IsCurrentCharacter(null,"current","chef"));p.world=null;Assert(!IsCurrentCharacter(p,"current","chef"));Console.WriteLine("PASS starter scope: current host/client accepted; old, unready, incomplete, wrong host background and missing world/player rejected");}
}
