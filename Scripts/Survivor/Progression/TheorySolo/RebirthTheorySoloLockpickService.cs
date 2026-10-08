using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class RebirthTheorySoloLockpickService
{
    internal sealed class Binding
    {
        internal World World;internal object State;internal ConnectionManager Manager;internal ClientInfo Peer;internal EntityPlayer Player;
        internal RebirthStablePlayerIdentity Owner;internal RebirthWorldCharacterRecord Record;internal RebirthWorldProgressionState Progression;internal RebirthTheorySoloState Solo;
        internal TEFeatureLockPickable Feature;internal ushort Channel;internal RebirthTheorySoloLockpickLedger.Original Original;
        internal TimerEventData Timer;internal string Creation;internal float Earliest,Expires;internal bool Released,Completed,Held,Retiring;
    }
    internal sealed class Transition { internal Binding binding;internal object source;internal bool remote;internal bool finished;internal bool applied;internal PlatformUserIdentifierAbs user;internal List<BlockChangeInfo> changes;internal Transition(Binding b,object s,bool r){binding=b;source=s;remote=r;} }
    internal sealed class TimerSetup { internal Binding Binding,Previous;internal bool Finished; }
    [ThreadStatic] private static Binding activeTimerSetup;
    [ThreadStatic] private static Transition activeTransition;
    private static readonly Dictionary<int,Binding> Bindings=new Dictionary<int,Binding>();
    private static World currentWorld;private static object currentState;private static string generation;private static bool installed;private static float nextRetry;
    internal static void Grant(TEFeatureLockPickable feature,EntityPlayer player,ushort channel,float nativeSeconds,float effectiveSeconds,float remaining)
    {
        if(!ThreadManager.IsMainThread()||player?.world==null||player.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,player.world)||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||feature==null||!RebirthTheorySoloRegistry.TryLockpickDifficulty(nativeSeconds,out var difficulty)||!Finite(effectiveSeconds)||effectiveSeconds<=0||!Finite(remaining))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var lockManager=LockManager.Instance;
        if(manager==null||!manager.IsServer||feature.IsSharedLock(channel)||lockManager==null||!lockManager.singleLocks.TryGetByValue(new LockManager.LockEntry(feature,channel),out var actor)||actor!=player.entityId||!ReferenceEquals(player.world.GetTileEntity(feature.ToWorldPos()),feature.Parent)||!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return;
        Context(player.world);if(Bindings.Count>=128&&!Bindings.ContainsKey(player.entityId))return;
        var peer=manager.Clients?.ForEntityId(player.entityId);
        if(peer==null&&!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return;
        float now=Time.realtimeSinceStartup;if(!Finite(now))return;
        var position=feature.ToWorldPos();var before=player.world.GetBlock(position);var after=feature.lockpickDowngradeBlock;after.rotation=before.rotation;after.meta=before.meta;
        if(before.isair||after.isair||before.rawData==after.rawData)return;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            if(Bindings.TryGetValue(player.entityId,out var prior)&&prior.Retiring){TrySaveCompleted(prior);if(Bindings.TryGetValue(player.entityId,out var pending)&&ReferenceEquals(pending,prior))return;}
            var p=record.Progression;var solo=p.SoloTheory;
            var b=new Binding{World=player.world,State=player.world.worldState,Manager=manager,Peer=peer,Player=player,Owner=owner,Record=record,Progression=p,Solo=solo,Creation=record.Origin.CreationId,Feature=feature,Channel=channel,Earliest=now+Mathf.Max(.25f,Mathf.Min(Mathf.Max(.25f,effectiveSeconds),Mathf.Max(.25f,remaining))-.75f),Expires=now+Mathf.Max(20,Mathf.Max(.25f,remaining)+30)};
            if(!Current(b,false))return;
            if(solo==null){solo=new RebirthTheorySoloState{CreationId=b.Creation};p.SoloTheory=solo;b.Solo=solo;}
            if(solo.CreationId!=b.Creation)return;
            if(solo.LockpickOriginal==null)solo.LockpickOriginal=new RebirthTheorySoloLockpickLedger();var ledger=solo.LockpickOriginal;
            if(ledger.Active!=null)
            {
                // A saved attempted outcome cannot be discarded under save uncertainty.
                if(ledger.Active.EvidenceAttempted&&!RebirthWorldCharacterRepository.HasSavedSoloLockpickOriginal(owner,solo))return;
                // The native accepted new grant proves the preceding player lock is no longer active.
                ledger.Retire(ledger.Active.Ordinal,ledger.Active.Lease,ledger.Active.Generation);
            }
            b.Original=ledger.Reserve(Guid.NewGuid().ToString("N"),generation,solo.CreationId,player.entityId,position.x,position.y,position.z,before.rawData,after.rawData,before.damage,after.damage,nativeSeconds,difficulty);
            if(b.Original==null)return;
            record.Touch("solo-theory-lockpick-original-grant");Bindings[player.entityId]=b;
            try{RebirthWorldCharacterRepository.SaveIfDirty(owner,"solo-theory-lockpick-original-grant");}catch{}
        }
    }
    internal static void Release(TEFeatureAbs feature,int actor,ushort channel,bool ran)
    {
        if(!ran||!Bindings.TryGetValue(actor,out var b)||!Current(b,true)||!ReferenceEquals(feature,b.Feature)||b.Channel!=channel)return;
        b.Released=true;
    }
    internal static Transition BeforeRemote(NetPackageSetBlock package,World world,GameManager callbacks)
    {
        if(package?.Sender==null||!ReferenceEquals(callbacks,GameManager.Instance)||!ReferenceEquals(world,currentWorld)||!Bindings.TryGetValue(package.localPlayerThatChanged,out var b)||!Current(b,true)||b.Peer==null||!ReferenceEquals(package.Sender,b.Peer)||!package.ValidEntityIdForSender(b.Player.entityId)||!package.ValidUserIdForSender(package.persistentPlayerId)||!b.Released||package.blockChanges==null||package.blockChanges.Count<1||package.blockChanges.Count>128)return null;
        var position=Position(b);BlockChangeInfo matching=null;
        foreach(var row in package.blockChanges)
        {
            if(row==null)return null;
            if(row.blockValueRef.TryGetBlockPos(out var pos)&&pos.Equals(position))
            {if(matching!=null||!row.bChangeBlockValue||row.blockValue.rawData!=b.Original.After||row.blockValue.damage!=b.Original.AfterDamage)return null;matching=row;}
        }
        return matching!=null&&BeforeImage(b)?Open(b,package,true):null;
    }
    internal static TimerSetup BeforeShowUi(TEFeatureLockPickable feature,bool granted)
    {
        if(!granted)return null;
        foreach(var b in Bindings.Values)
        {
            if(b.Peer!=null||!ReferenceEquals(b.Feature,feature)||!Current(b,true)||b.Held||b.Completed)continue;
            var locks=LockManager.Instance;if(locks==null||!locks.singleLocks.TryGetByValue(new LockManager.LockEntry(feature,b.Channel),out var owner)||owner!=b.Player.entityId)return null;
            var setup=new TimerSetup{Binding=b,Previous=activeTimerSetup};activeTimerSetup=b;return setup;
        }
        return null;
    }
    internal static void CaptureTimer(XUiC_Timer controller,TimerEventData timer,float seconds,bool ran)
    {
        var b=activeTimerSetup;if(!ran||b==null||!Current(b,true)||b.Peer!=null||controller==null||timer==null||!ReferenceEquals(controller.eventData,timer)||!ReferenceEquals(timer.Data,b.Player)||!Finite(seconds)||seconds<=0)return;
        b.Timer=timer;
    }
    internal static void AfterShowUi(TimerSetup setup,bool ran)
    {
        if(setup==null||setup.Finished)return;setup.Finished=true;
        if(ReferenceEquals(activeTimerSetup,setup.Binding))activeTimerSetup=setup.Previous;
        if(!ran)setup.Binding.Held=true;
    }
    internal static void UnknownShowUi(TimerSetup setup,Exception failure)
    {if(setup!=null&&failure!=null){setup.Binding.Held=true;AfterShowUi(setup,false);}}
    internal static Transition BeforeLocal(TEFeatureLockPickable feature,TimerEventData timer)
    {
        if(timer?.Data is not EntityPlayerLocal player||!Bindings.TryGetValue(player.entityId,out var b)||!Current(b,true)||b.Peer!=null||!ReferenceEquals(timer.Data,b.Player)||!ReferenceEquals(timer,b.Timer)||!ReferenceEquals(feature,b.Feature)||!Finite(timer.Completion)||timer.Completion<1||!BeforeImage(b))return null;
        return Open(b,timer,false);
    }
    internal static void After(Transition transition,object source,bool ran)
    {
        if(transition==null||transition.finished||!ReferenceEquals(transition.source,source))return;transition.finished=true;
        if(ReferenceEquals(activeTransition,transition))activeTransition=null;
        var b=transition.binding;
        if(!ran||!transition.applied||!Current(b,true)||!b.Released)return;
        var after=b.World.GetBlock(Position(b));if(after.rawData!=b.Original.After||after.damage!=b.Original.AfterDamage)return;
        b.Completed=true;TrySaveCompleted(b);
    }
    internal static void Unknown(Transition transition,Exception failure)
    {if(transition!=null&&failure!=null&&!transition.binding.Completed){transition.binding.Held=true;transition.finished=true;if(ReferenceEquals(activeTransition,transition))activeTransition=null;}}
    private static Transition Open(Binding b,object source,bool remote)
    {
        if(activeTransition!=null)return null;var t=new Transition(b,source,remote);
        if(remote){var package=(NetPackageSetBlock)source;t.user=package.persistentPlayerId;t.changes=package.blockChanges;}
        activeTransition=t;return t;
    }
    internal static Transition BeforeCommit(GameManager callbacks,PlatformUserIdentifierAbs user,List<BlockChangeInfo> changes)
    {
        var t=activeTransition;if(t==null||t.finished||t.applied||!ReferenceEquals(callbacks,GameManager.Instance)||!Current(t.binding,true)||!BeforeImage(t.binding)||changes==null||changes.Count<1||changes.Count>128)return null;
        if(t.remote&&(!ReferenceEquals(changes,t.changes)||!object.Equals(user,t.user)))return null;
        if(!t.remote&&(t.source is not TimerEventData timer||!ReferenceEquals(timer.Data,t.binding.Player)))return null;
        BlockChangeInfo matching=null;
        foreach(var row in changes){if(row==null)return null;if(row.blockValueRef.TryGetBlockPos(out var pos)&&pos.Equals(Position(t.binding))){if(matching!=null||!row.bChangeBlockValue||row.blockValue.rawData!=t.binding.Original.After||row.blockValue.damage!=t.binding.Original.AfterDamage)return null;matching=row;}}
        return matching==null?null:t;
    }
    internal static void AfterCommit(Transition transition,bool ran)
    {
        if(transition==null||!ran||!ReferenceEquals(activeTransition,transition)||transition.finished||!Current(transition.binding,true))return;
        var after=transition.binding.World.GetBlock(Position(transition.binding));
        // A queued/later change has no matching post-image here and cannot qualify this original callback.
        if(after.rawData==transition.binding.Original.After&&after.damage==transition.binding.Original.AfterDamage)transition.applied=true;
    }
    private static bool BeforeImage(Binding b)
    {
        float now=Time.realtimeSinceStartup;
        if(b.Completed||b.Held||!Finite(now)||now<b.Earliest||now>b.Expires||(b.Player.position-Position(b).ToVector3()).sqrMagnitude>100||!ReferenceEquals(b.World.GetTileEntity(Position(b)),b.Feature.Parent))return false;
        var before=b.World.GetBlock(Position(b));if(before.rawData!=b.Original.Before||before.damage!=b.Original.BeforeDamage)return false;
        try{RebirthWorldCharacterRepository.SaveIfDirty(b.Owner,"solo-theory-lockpick-original-prepare");return Current(b,true)&&RebirthWorldCharacterRepository.HasSavedSoloLockpickOriginal(b.Owner,b.Solo)&&Current(b,true);}catch{return false;}
    }
    private static void TrySaveCompleted(Binding b)
    {
        if(!b.Completed||b.Held||!Current(b,true))return;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            if(!Current(b,true))return;var ledger=b.Solo.LockpickOriginal;
            if(b.Retiring){TrySaveRetirement(b);return;}
            if(!ledger.MarkEvidence(b.Original.Ordinal,b.Original.Lease,b.Original.Generation,out var already))return;
            if(!already)
            {
                b.Solo.RecordAcknowledged("skill.lockpicking","discrete_job","lockpick:"+b.Original.Ordinal+":"+b.Original.Lease,b.Original.Difficulty,b.Record.Condition.ActivePlaySeconds);
                b.Record.Touch("solo-theory-lockpick-original-completed");
            }
            try
            {
                RebirthWorldCharacterRepository.SaveIfDirty(b.Owner,"solo-theory-lockpick-original-completed");
                if(!Current(b,true)||!RebirthWorldCharacterRepository.HasSavedSoloLockpickOriginal(b.Owner,b.Solo)||!Current(b,true))return;
                if(!ledger.Retire(b.Original.Ordinal,b.Original.Lease,b.Original.Generation))return;b.Retiring=true;b.Record.Touch("solo-theory-lockpick-original-retired");TrySaveRetirement(b);
            }
            catch{} // Exact attempted evidence/retirement remains; do not invent another outcome.
        }
    }
    private static void TrySaveRetirement(Binding b)
    {
        if(!b.Retiring||!Current(b,true))return;
        try
        {
            RebirthWorldCharacterRepository.SaveIfDirty(b.Owner,"solo-theory-lockpick-original-retired");
            if(Current(b,true)&&RebirthWorldCharacterRepository.HasSavedSoloLockpickOriginal(b.Owner,b.Solo)&&Current(b,true))
            {Bindings.Remove(b.Player.entityId);RebirthSkillAwardService.QueueOwnerPublication(b.Player);}
        }
        catch{} // Retain the same original retirement; never enter evidence recording again.
    }
    private static bool Current(Binding b,bool requireOriginal)
    {
        if(b==null||b.State==null||!Finite(Time.realtimeSinceStartup)||!ThreadManager.IsMainThread()||b.World==null||b.World.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,b.World)||!ReferenceEquals(b.World.worldState,b.State)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,b.Manager)||b.Manager==null||!b.Manager.IsServer||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||b.Player==null||b.Player.IsDead()||!ReferenceEquals(b.Player.world,b.World)||!ReferenceEquals(b.World.GetEntity(b.Player.entityId),b.Player)||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(b.Record)||!ReferenceEquals(b.Record.Progression,b.Progression)||!ReferenceEquals(b.Progression.SoloTheory,b.Solo)||b.Record.Origin?.CreationId!=b.Creation||(b.Solo!=null&&b.Solo.CreationId!=b.Creation)||!RebirthSkillAwardService.TryGetEligible(b.Player,out var owner,out var record)||!ReferenceEquals(record,b.Record)||owner.StorageKey!=b.Owner.StorageKey)return false;
        if(b.Peer!=null){if(b.Manager.Clients==null||!ReferenceEquals(b.Manager.Clients.ForEntityId(b.Player.entityId),b.Peer)||b.Peer.disconnecting||!b.Peer.loginDone||!b.Peer.bAttachedToEntity||b.Peer.entityId!=b.Player.entityId||b.Peer.InternalId?.CombinedString!=b.Owner.CanonicalId)return false;}
        else if(!ReferenceEquals(b.World.GetPrimaryPlayer(),b.Player))return false;
        return !requireOriginal||(b.Original!=null&&b.Original.Generation==generation&&b.Original.Creation==b.Solo.CreationId&&Bindings.TryGetValue(b.Player.entityId,out var current)&&ReferenceEquals(current,b)&&b.Solo.LockpickOriginal!=null&&(b.Retiring?(b.Solo.LockpickOriginal.Active==null&&b.Solo.LockpickOriginal.Issued==b.Original.Ordinal):b.Solo.LockpickOriginal.TryGet(b.Original.Ordinal,b.Original.Lease,b.Original.Generation,out var original)&&ReferenceEquals(original,b.Original)));
    }
    private static void Context(World world)
    {
        if(!ReferenceEquals(world,currentWorld)||!ReferenceEquals(world.worldState,currentState)){Bindings.Clear();activeTransition=null;activeTimerSetup=null;currentWorld=world;currentState=world.worldState;generation=Guid.NewGuid().ToString("N");}
        if(installed)return;ModEvents.GameUpdate.RegisterHandler(OnUpdate);installed=true;
    }
    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;if(world==null||world.IsRemote()||!ReferenceEquals(world,currentWorld)||!ReferenceEquals(world.worldState,currentState)){Bindings.Clear();activeTransition=null;activeTimerSetup=null;currentWorld=null;currentState=null;return;}
        float now=Time.realtimeSinceStartup;if(!Finite(now)||now<nextRetry)return;nextRetry=now+1;
        foreach(var pair in Bindings.ToArray()){var b=pair.Value;if(!Current(b,true)){Bindings.Remove(pair.Key);continue;}if(b.Completed)TrySaveCompleted(b);else if(now>b.Expires)Bindings.Remove(pair.Key);}
    }
    private static Vector3i Position(Binding b)=>new Vector3i(b.Original.X,b.Original.Y,b.Original.Z);
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
}