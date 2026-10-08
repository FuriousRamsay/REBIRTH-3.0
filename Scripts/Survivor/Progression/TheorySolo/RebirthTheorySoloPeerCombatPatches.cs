using System;using HarmonyLib;using UnityEngine;
// Read-only original native packet and nested native core brackets; never suppress or alter damage.
internal static class RebirthTheorySoloPeerCombatService
{
 internal sealed class Scope{internal NetPackageDamageEntity Package;internal World World;internal object State;internal ConnectionManager Manager;internal ClientInfo Peer;internal EntityPlayer Player;internal EntityAlive Target;internal RebirthStablePlayerIdentity Owner;internal RebirthWorldCharacterRecord Record;internal RebirthWorldProgressionState Progression;internal string Creation;internal ItemValue Held,PacketItem;internal Scope Previous;internal readonly System.Collections.Generic.List<Core> Cores=new System.Collections.Generic.List<Core>();internal bool Finished;}
 internal sealed class Core{internal Scope Scope;internal EntityAlive Target;internal DamageResponse Response;internal RebirthTheorySoloCombatService.Witness Witness;internal bool Finished;}
 [ThreadStatic]private static Scope active;
 internal static Scope BeforePackage(NetPackageDamageEntity package,World world,GameManager callbacks)
 {
  try
  {
   var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
   if(!ThreadManager.IsMainThread()||package?.Sender==null||world==null||world.IsRemote()||!ReferenceEquals(callbacks,GameManager.Instance)||!ReferenceEquals(callbacks.World,world)||manager==null||!manager.IsServer||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!package.ValidEntityIdForSender(package.attackerEntityId)||package.strength==0||(package.flags&~2047u)!=0||(package.flags&32u)!=0||package.attackingItem==null||package.attackingItem.IsEmpty())return null;
   var player=world.GetEntity(package.attackerEntityId) as EntityPlayer;var target=world.GetEntity(package.entityId) as EntityAlive;
   if(player==null||target==null||target is EntityPlayer||target.IsDead()||target.Health<=0||!target.EntityClass.bIsEnemyEntity||!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||!RebirthTheorySoloRegistry.TryGet(RebirthProgressionRuntimeConfig.ClassifyCombat(package.attackingItem),out var rule)||rule.Family!="weapon_continuous")return null;
   var held=player.inventory?.holdingItemItemValue;if(held==null||held.IsEmpty()||!held.EqualsExceptUseTimesAndMeta(package.attackingItem))return null;
   var scope=new Scope{Package=package,World=world,State=world.worldState,Manager=manager,Peer=package.Sender,Player=player,Target=target,Owner=owner,Record=record,Progression=record.Progression,Creation=record.Origin.CreationId,Held=held.Clone(),PacketItem=package.attackingItem.Clone(),Previous=active};
   if(!Current(scope))return null;active=scope;return scope;
  }
  catch{return null;} // Invalid observation must never change native packet delivery.
 }
 internal static Core BeforeCore(EntityAlive target,DamageResponse response)
 {
  var scope=active;
  try
  {
   if(scope==null||scope.Finished||!Current(scope)||!ReferenceEquals(scope.Target,target)||response.Source==null||response.Source.getEntityId()!=scope.Player.entityId||!ReferenceEquals(response.Source.AttackingItem,scope.Package.attackingItem)||!scope.PacketItem.EqualsExceptUseTimesAndMeta(response.Source.AttackingItem)||response.Source.BuffClass!=null)return null;
   if(scope.Cores.Count>=8)return null;var witness=RebirthTheorySoloCombatService.Before(target,response.Source);if(witness==null)return null;var core=new Core{Scope=scope,Target=target,Response=response,Witness=witness};scope.Cores.Add(core);return core;
  }
  catch{return null;}
 }
 internal static void AfterCore(Core core,bool ran)
 {if(core==null||core.Finished)return;core.Finished=true;if(!ran||!ReferenceEquals(active,core.Scope)||!Current(core.Scope)){RebirthTheorySoloCombatService.Unknown(core.Witness,new InvalidOperationException("Original peer scope unavailable"));return;}}
 internal static void UnknownCore(Core core,Exception failure){if(core!=null&&failure!=null){core.Finished=true;RebirthTheorySoloCombatService.Unknown(core.Witness,failure);}}
 internal static void AfterPackage(Scope scope,bool ran){if(scope==null||scope.Finished)return;bool current=ran&&ReferenceEquals(active,scope)&&Current(scope);scope.Finished=true;if(ReferenceEquals(active,scope))active=scope.Previous;foreach(var core in scope.Cores){if(current&&core.Finished)RebirthTheorySoloCombatService.After(core.Witness,core.Target,core.Response,true);else RebirthTheorySoloCombatService.Unknown(core.Witness,new InvalidOperationException("Original native peer packet did not complete"));}}
 internal static void UnknownPackage(Scope scope,Exception failure){if(failure!=null)AfterPackage(scope,false);}
 private static bool Current(Scope s)
 {
  if(s==null||!ThreadManager.IsMainThread()||float.IsNaN(Time.realtimeSinceStartup)||float.IsInfinity(Time.realtimeSinceStartup)||!RebirthWorldCharacterRepository.IsServerAuthority||s.State==null||s.World.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,s.World)||!ReferenceEquals(s.World.worldState,s.State)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,s.Manager)||!s.Manager.IsServer||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||s.Player.IsDead()||!ReferenceEquals(s.Player.world,s.World)||!ReferenceEquals(s.World.GetEntity(s.Player.entityId),s.Player)||!ReferenceEquals(s.Target.world,s.World)||!ReferenceEquals(s.World.GetEntity(s.Target.entityId),s.Target)||s.Manager.Clients==null||!ReferenceEquals(s.Manager.Clients.ForEntityId(s.Player.entityId),s.Peer)||!ReferenceEquals(s.Package.Sender,s.Peer)||s.Peer.disconnecting||!s.Peer.loginDone||!s.Peer.bAttachedToEntity||s.Peer.entityId!=s.Player.entityId||s.Peer.InternalId?.CombinedString!=s.Owner.CanonicalId||s.Package.attackerEntityId!=s.Player.entityId||s.Package.entityId!=s.Target.entityId||!s.Package.ValidEntityIdForSender(s.Player.entityId)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(s.Record)||!ReferenceEquals(s.Record.Progression,s.Progression)||s.Record.Origin.CreationId!=s.Creation||!RebirthSkillAwardService.TryGetEligible(s.Player,out var owner,out var record)||!ReferenceEquals(record,s.Record)||owner.StorageKey!=s.Owner.StorageKey)return false;
  var held=s.Player.inventory?.holdingItemItemValue;return held!=null&&s.Held.EqualsExceptUseTimesAndMeta(held)&&s.PacketItem.EqualsExceptUseTimesAndMeta(s.Package.attackingItem)&&s.Held.EqualsExceptUseTimesAndMeta(s.PacketItem);
 }
}
[HarmonyPatch(typeof(NetPackageDamageEntity),nameof(NetPackageDamageEntity.ProcessPackage))]
internal static class RebirthTheorySoloPeerCombatPacketPatch
{
 private static void Prefix(NetPackageDamageEntity __instance,World __0,GameManager __1,out RebirthTheorySoloPeerCombatService.Scope __state){__state=null;try{__state=RebirthTheorySoloPeerCombatService.BeforePackage(__instance,__0,__1);}catch{}}
 private static void Postfix(RebirthTheorySoloPeerCombatService.Scope __state,bool __runOriginal){try{RebirthTheorySoloPeerCombatService.AfterPackage(__state,__runOriginal);}catch(Exception observer){try{RebirthTheorySoloPeerCombatService.UnknownPackage(__state,observer);}catch{}}}
 private static Exception Finalizer(Exception __exception,RebirthTheorySoloPeerCombatService.Scope __state){try{RebirthTheorySoloPeerCombatService.UnknownPackage(__state,__exception);}catch{}return __exception;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.ProcessDamageResponse),new Type[]{typeof(DamageResponse)})]
internal static class RebirthTheorySoloPeerCombatCorePatch
{
 private static void Prefix(EntityAlive __instance,DamageResponse __0,out RebirthTheorySoloPeerCombatService.Core __state){__state=null;try{__state=RebirthTheorySoloPeerCombatService.BeforeCore(__instance,__0);}catch{}}
 private static void Postfix(RebirthTheorySoloPeerCombatService.Core __state,bool __runOriginal){try{RebirthTheorySoloPeerCombatService.AfterCore(__state,__runOriginal);}catch(Exception observer){try{RebirthTheorySoloPeerCombatService.UnknownCore(__state,observer);}catch{}}}
 private static Exception Finalizer(Exception __exception,RebirthTheorySoloPeerCombatService.Core __state){try{RebirthTheorySoloPeerCombatService.UnknownCore(__state,__exception);}catch{}return __exception;}
}

[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.Kill),new Type[]{typeof(DamageResponse)})]
internal static class RebirthTheorySoloCombatKillPatch
{
 private static void Prefix(EntityAlive __instance,DamageResponse __0,out RebirthTheorySoloCombatService.KillCommit __state){__state=null;try{__state=RebirthTheorySoloCombatService.BeforeKill(__instance,__0);}catch{}}
 private static void Postfix(RebirthTheorySoloCombatService.KillCommit __state,bool __runOriginal){try{RebirthTheorySoloCombatService.AfterKill(__state,__runOriginal);}catch(Exception observer){try{RebirthTheorySoloCombatService.UnknownKill(__state,observer);}catch{}}}
 private static Exception Finalizer(Exception __exception,RebirthTheorySoloCombatService.KillCommit __state){try{RebirthTheorySoloCombatService.UnknownKill(__state,__exception);}catch{}return __exception;}
}