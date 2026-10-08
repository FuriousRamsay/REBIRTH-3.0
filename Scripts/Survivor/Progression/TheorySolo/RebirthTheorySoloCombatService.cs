using System;using System.Collections.Generic;using System.Linq;using System.Runtime.CompilerServices;using UnityEngine;
internal static class RebirthTheorySoloCombatService
{
 internal sealed class Witness{internal World World;internal object State;internal ConnectionManager Manager;internal ClientInfo Peer;internal EntityPlayer Player;internal EntityAlive Target;internal DamageSource Source;internal ItemValue Item;internal RebirthStablePlayerIdentity Owner;internal RebirthWorldCharacterRecord Record;internal RebirthWorldProgressionState Progression;internal RebirthTheorySoloState Solo;internal string Creation,Subject,Family,Id,Generation;internal int Health,MaxHealth;internal float Difficulty;internal bool Consumed,Retiring,Preparing,PrepareRetirement,NativeKilled;internal long CaptureSequence;internal RebirthTheorySoloCombatLedger.Original Original;}
 private sealed class Life{internal long Epoch;internal bool Claimed;}
 private static readonly ConditionalWeakTable<EntityAlive,Life> Lives=new ConditionalWeakTable<EntityAlive,Life>();
 private static readonly Dictionary<Witness,long> CapturedEpochs=new Dictionary<Witness,long>();
 private static readonly List<Witness> Pending=new List<Witness>();
 internal sealed class KillCommit{internal Witness Witness;internal DamageSource Source;internal ItemValue Item;internal bool Finished;}
 private static long captureSequence;
 private static World world;private static object state;private static string generation;private static bool installed;private static float nextRetry;
 internal static Witness Before(EntityAlive target,DamageSource source)
 {
  if(!ThreadManager.IsMainThread()||target==null||target.world==null||target.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,target.world)||target is EntityPlayer||target.IsDead()||target.Health<=0||!target.EntityClass.bIsEnemyEntity||source==null||source.BuffClass!=null||source.AttackingItem==null||source.AttackingItem.IsEmpty()||RebirthComplexSkillSystemService.IsRemoteOwnedDeviceSource(target.world,source)||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return null;
  var player=target.world.GetEntity(source.getEntityId()) as EntityPlayer;
  if(player==null||!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return null;
  string subject=RebirthProgressionRuntimeConfig.ClassifyCombat(source.AttackingItem);
  if(!RebirthTheorySoloRegistry.TryGet(subject,out var rule)||rule.Family!="weapon_continuous"||!RebirthTheorySoloRegistry.TryCombatDifficulty(target.GetMaxHealth(),out var difficulty))return null;
  var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(manager==null||!manager.IsServer)return null;
  Context(target.world);var w=new Witness{World=target.world,State=target.world.worldState,Manager=manager,Peer=manager.Clients?.ForEntityId(player.entityId),Player=player,Target=target,Source=source,Item=source.AttackingItem,Owner=owner,Record=record,Progression=record.Progression,Solo=record.Progression.SoloTheory,Creation=record.Origin.CreationId,Subject=subject,Family=rule.Family,Id=Guid.NewGuid().ToString("N"),Generation=generation,Health=target.Health,MaxHealth=target.GetMaxHealth(),Difficulty=difficulty};
  if(!Current(w,false)||Pending.Count>=128||CapturedEpochs.Count>=128||captureSequence==long.MaxValue)return null;w.CaptureSequence=++captureSequence;
  var life=Lives.GetOrCreateValue(target);if(life.Claimed){life.Epoch++;life.Claimed=false;}CapturedEpochs[w]=life.Epoch;return w;
 }
 internal static void After(Witness w,EntityAlive target,DamageResponse response,bool ran)
 {
  if(w==null||w.Consumed)return;w.Consumed=true;
  if(!CapturedEpochs.TryGetValue(w,out var epoch))return;CapturedEpochs.Remove(w);
  if(!ran||!w.NativeKilled||!Current(w,false)||!ReferenceEquals(target,w.Target)||!ReferenceEquals(response.Source,w.Source)||!ReferenceEquals(w.Source.AttackingItem,w.Item)||w.Source.getEntityId()!=w.Player.entityId||RebirthProgressionRuntimeConfig.ClassifyCombat(w.Item)!=w.Subject||!target.bDead||!target.IsDead()||target.Health>0||w.Health<=target.Health)return;
  var life=Lives.GetOrCreateValue(target);if(life.Epoch!=epoch||life.Claimed)return;life.Claimed=true;
  lock(RebirthSkillKnowledgeService.SyncRoot)
  {
   if(!Current(w,false))return;var solo=w.Solo;if(solo==null){solo=new RebirthTheorySoloState{CreationId=w.Creation};w.Progression.SoloTheory=solo;w.Solo=solo;}
   if(solo.CreationId!=w.Creation)return;if(solo.CombatOriginal==null)solo.CombatOriginal=new RebirthTheorySoloCombatLedger();
   w.Preparing=true;Pending.Add(w);TrySave(w);
  }
 }
 internal static KillCommit BeforeKill(EntityAlive target,DamageResponse response)
 {
  if(target==null||target.bDead||target.Health>0||response.Source==null)return null;
  var w=CapturedEpochs.Keys.Where(x=>!x.Consumed&&ReferenceEquals(x.Target,target)&&ReferenceEquals(x.Source,response.Source)&&ReferenceEquals(x.Item,response.Source.AttackingItem)).OrderByDescending(x=>x.CaptureSequence).FirstOrDefault();
  if(w==null||!Current(w,false)||response.Source.getEntityId()!=w.Player.entityId)return null;
  return new KillCommit{Witness=w,Source=response.Source,Item=response.Source.AttackingItem};
 }
 internal static void AfterKill(KillCommit commit,bool ran)
 {
  if(commit==null||commit.Finished)return;commit.Finished=true;var w=commit.Witness;
  if(ran&&!w.Consumed&&Current(w,false)&&ReferenceEquals(w.Source,commit.Source)&&ReferenceEquals(w.Item,commit.Item)&&ReferenceEquals(commit.Source.AttackingItem,commit.Item)&&w.Target.bDead&&w.Target.IsDead()&&w.Target.Health<=0&&ReferenceEquals(w.Target.entityThatKilledMe,w.Player))w.NativeKilled=true;
 }
 internal static void UnknownKill(KillCommit commit,Exception failure){if(commit!=null&&failure!=null){commit.Finished=true;Unknown(commit.Witness,failure);}}
 internal static void Unknown(Witness w,Exception failure){if(w!=null&&failure!=null){w.Consumed=true;CapturedEpochs.Remove(w);}}
 private static void TrySave(Witness w)
 {
  if(!Current(w,true))return;
  lock(RebirthSkillKnowledgeService.SyncRoot)
  {
   if(!Current(w,true))return;if(w.Preparing&&!TryPrepare(w))return;var ledger=w.Solo.CombatOriginal;
   if(!w.Retiring){if(!ledger.MarkEvidence(w.Original.Ordinal,w.Id,out var already))return;if(!already){w.Solo.RecordAcknowledged(w.Subject,w.Family,"combat:"+w.Original.Ordinal+":"+w.Id,w.Difficulty,w.Record.Condition.ActivePlaySeconds);w.Record.Touch("solo-theory-combat-original-evidence");}}
   try
   {
    RebirthWorldCharacterRepository.SaveIfDirty(w.Owner,"solo-theory-combat-original-evidence");if(!Current(w,true)||!RebirthWorldCharacterRepository.HasSavedSoloCombatOriginal(w.Owner,w.Solo)||!Current(w,true))return;
    if(!w.Retiring){RetireWitnessedPredecessors(w);if(!ledger.Retire(w.Original.Ordinal,w.Id))return;w.Retiring=true;w.Record.Touch("solo-theory-combat-original-retired");}
    RebirthWorldCharacterRepository.SaveIfDirty(w.Owner,"solo-theory-combat-original-retired");if(Current(w,true)&&RebirthWorldCharacterRepository.HasSavedSoloCombatOriginal(w.Owner,w.Solo)&&Current(w,true)){Pending.Remove(w);RebirthSkillAwardService.QueueOwnerPublication(w.Player);}
   }
   catch{} // Same original outcome remains retained, including an uncertain retirement.
  }
 }
 private static bool TryPrepare(Witness w)
 {
  var ledger=w.Solo.CombatOriginal;
  try
  {
   if(!Current(w,true)||Pending.Any(other=>!ReferenceEquals(other,w)&&ReferenceEquals(other.Solo,w.Solo)&&other.PrepareRetirement))return false;
   if(ledger.Pending.Count>=128)
   {
    // A saturated loaded journal can be retired only as an exact saved acknowledgment set.
    if(ledger.Pending.Any(x=>!x.EvidenceAttempted||x.Generation==w.Generation||x.Creation!=w.Creation||x.Actor!=w.Player.entityId))return false;
    RebirthWorldCharacterRepository.SaveIfDirty(w.Owner,"solo-theory-combat-original-predecessors");
    if(!Current(w,true)||!RebirthWorldCharacterRepository.HasSavedSoloCombatOriginal(w.Owner,w.Solo)||!Current(w,true))return false;
    RetireWitnessedPredecessors(w);w.PrepareRetirement=true;w.Record.Touch("solo-theory-combat-original-predecessors-retired");
   }
   if(w.PrepareRetirement)
   {
    RebirthWorldCharacterRepository.SaveIfDirty(w.Owner,"solo-theory-combat-original-predecessors-retired");
    if(!Current(w,true)||!RebirthWorldCharacterRepository.HasSavedSoloCombatOriginal(w.Owner,w.Solo)||!Current(w,true))return false;
    w.PrepareRetirement=false;
   }
   if(!Current(w,true))return false;
   w.Original=ledger.Reserve(w.Id,w.Generation,w.Creation,w.Subject,w.Family,w.Player.entityId,w.Target.entityId,w.MaxHealth,w.Difficulty);
   if(w.Original==null)return false;w.Preparing=false;w.Record.Touch("solo-theory-combat-original-completed");return Current(w,true);
  }
  catch{return false;} // Keep the exact completed native witness; no new ordinal or repeated credit.
 }
 private static void RetireWitnessedPredecessors(Witness w)
 {
  // Called only after the exact whole character Solo snapshot is witnessed saved.
  // Old generation acknowledgments are retirement-only; they never create new evidence.
  foreach(var old in w.Solo.CombatOriginal.Pending.Where(x=>x.Generation!=w.Generation&&x.Creation==w.Creation&&x.Actor==w.Player.entityId&&x.EvidenceAttempted).ToArray())w.Solo.CombatOriginal.Retire(old.Ordinal,old.Id);
 }
 private static bool Current(Witness w,bool original)
 {
  if(w==null||!ThreadManager.IsMainThread()||!RebirthWorldCharacterRepository.IsServerAuthority||!Finite(Time.realtimeSinceStartup)||w.State==null||w.World.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,w.World)||!ReferenceEquals(w.World.worldState,w.State)||w.Generation!=generation||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,w.Manager)||!w.Manager.IsServer||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||w.Player.IsDead()||!ReferenceEquals(w.Player.world,w.World)||!ReferenceEquals(w.World.GetEntity(w.Player.entityId),w.Player)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(w.Record)||!ReferenceEquals(w.Record.Progression,w.Progression)||!ReferenceEquals(w.Progression.SoloTheory,w.Solo)||w.Record.Origin.CreationId!=w.Creation||(w.Solo!=null&&w.Solo.CreationId!=w.Creation)||!RebirthSkillAwardService.TryGetEligible(w.Player,out var owner,out var record)||!ReferenceEquals(record,w.Record)||owner.StorageKey!=w.Owner.StorageKey)return false;
  if(w.Peer!=null){if(w.Manager.Clients==null||!ReferenceEquals(w.Manager.Clients.ForEntityId(w.Player.entityId),w.Peer)||w.Peer.disconnecting||!w.Peer.loginDone||!w.Peer.bAttachedToEntity||w.Peer.entityId!=w.Player.entityId||w.Peer.InternalId?.CombinedString!=w.Owner.CanonicalId)return false;}else if(!ReferenceEquals(w.World.GetPrimaryPlayer(),w.Player))return false;
  if(!original)return ReferenceEquals(w.World.GetEntity(w.Target.entityId),w.Target)&&ReferenceEquals(w.Target.world,w.World);
  if(!Pending.Contains(w)||w.Solo?.CombatOriginal==null)return false;if(w.Preparing)return w.Consumed&&w.Original==null;if(w.Original==null)return false;
  return w.Retiring?w.Solo.CombatOriginal.Issued>=w.Original.Ordinal&&!w.Solo.CombatOriginal.TryGet(w.Original.Ordinal,w.Id,out _):w.Solo.CombatOriginal.TryGet(w.Original.Ordinal,w.Id,out var a)&&ReferenceEquals(a,w.Original);
 }
 private static void Context(World current){if(!ReferenceEquals(world,current)||!ReferenceEquals(state,current.worldState)){Pending.Clear();CapturedEpochs.Clear();world=current;state=current.worldState;generation=Guid.NewGuid().ToString("N");}if(!installed){ModEvents.GameUpdate.RegisterHandler(OnUpdate);installed=true;}}
 private static void OnUpdate(ref ModEvents.SGameUpdateData data){var current=GameManager.Instance?.World;if(current==null||current.IsRemote()||!ReferenceEquals(world,current)||!ReferenceEquals(state,current.worldState)){Pending.Clear();CapturedEpochs.Clear();return;}float now=Time.realtimeSinceStartup;if(!Finite(now)||now<nextRetry)return;nextRetry=now+1;foreach(var w in Pending.ToArray()){if(!Current(w,true)){Pending.Remove(w);continue;}TrySave(w);}}
 private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
}
