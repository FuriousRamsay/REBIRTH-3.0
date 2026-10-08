using System;
using System.Collections.Generic;
public enum RebirthCompanionTargetKind { Npc, Drone }
public class RebirthCompanionCommandEntry { public string Text; public bool Enabled; }
public class RebirthCompanionListEntry {
 public string Id; public RebirthCompanionTargetKind Kind; public int EntityId;
 public bool DroneLockedToPlayer,DroneAccessLocked,DroneLightAttached,DroneLightOn,DroneHealAttached,DroneHealingAllies,DroneQuiet;
}
public class RebirthCompanionSnapshot { public string SelectedId; public List<RebirthCompanionListEntry> Companions=new List<RebirthCompanionListEntry>(); public List<RebirthCompanionCommandEntry> Commands=new List<RebirthCompanionCommandEntry>(); }
public class EntityPlayerLocal { public int entityId=1; }
public class EntityDrone { public int entityId=2,belongsPlayerId=1; public bool IsFlashlightAttached,IsFlashlightOn,IsHealModAttached,IsHealingAllies; public bool IsLocked(){return false;} }
public class EntityActivationCommand { public string commandId; public bool enabled; }
public class World { public EntityPlayerLocal Player=new EntityPlayerLocal(); public EntityDrone Drone; public EntityPlayerLocal GetPrimaryPlayer(){return Player;} public object GetEntity(int id){return Drone;} }
public class GameManager { public static GameManager Instance=new GameManager(); public World World=new World(); }
public static class RebirthDogNavigationMarkerService { public static int Calls; public static void SynchronizeSnapshot(List<RebirthCompanionListEntry> c){Calls++;} }
public static class RebirthDroneLockToPlayerService { public static bool IsLockedToPlayer(int id){return true;} }
public static class Subject {
 public static int Builds;
 private static bool TryGetLocalDroneCommands(EntityDrone d,EntityPlayerLocal p,out EntityActivationCommand[] c){ c=new[]{new EntityActivationCommand {commandId="drone_silent_off",enabled=true}};return true;}
 private static void BuildCommands(World w,EntityPlayerLocal p,RebirthCompanionListEntry e,List<RebirthCompanionCommandEntry> output){Builds++;output.Add(new RebirthCompanionCommandEntry {Text="local drone"});}
 // PRODUCTION_METHOD
}
class Check {
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static RebirthCompanionSnapshot Make(RebirthCompanionTargetKind kind){var s=new RebirthCompanionSnapshot {SelectedId="selected"}; s.Companions.Add(new RebirthCompanionListEntry {Id="selected",Kind=kind,EntityId=2}); s.Commands.Add(new RebirthCompanionCommandEntry {Text="server enabled",Enabled=true});s.Commands.Add(new RebirthCompanionCommandEntry {Text="server disabled",Enabled=false});return s;}
 static void Main(){
 var npc=Make(RebirthCompanionTargetKind.Npc);var enabled=npc.Commands[0];var disabled=npc.Commands[1];Subject.AugmentLocalSnapshot(npc);
 Assert(Subject.Builds==0&&npc.Commands.Count==2&&object.ReferenceEquals(npc.Commands[0],enabled)&&object.ReferenceEquals(npc.Commands[1],disabled)&&!disabled.Enabled,"NPC server gates changed without local entity");
 Assert(RebirthDogNavigationMarkerService.Calls==1,"navigation projection lost");
 var drone=Make(RebirthCompanionTargetKind.Drone);GameManager.Instance.World.Drone=new EntityDrone();Subject.AugmentLocalSnapshot(drone);Assert(Subject.Builds==1&&drone.Commands.Count==1&&drone.Commands[0].Text=="local drone"&&drone.Companions[0].DroneQuiet&&drone.Companions[0].DroneLockedToPlayer,"native drone refresh lost");
 var missing=Make(RebirthCompanionTargetKind.Npc);missing.SelectedId="absent";Subject.AugmentLocalSnapshot(missing);Assert(Subject.Builds==1&&missing.Commands.Count==2,"unmatched selection changed");
 GameManager.Instance.World.Player=null;Subject.AugmentLocalSnapshot(npc);Assert(Subject.Builds==1,"missing player refresh");Subject.AugmentLocalSnapshot(null);
 Console.WriteLine("PASS: NPC authoritative command identity/enabled states, absent local entity, marker projection, native drone refresh, unmatched selection and missing player.");
 }
}
