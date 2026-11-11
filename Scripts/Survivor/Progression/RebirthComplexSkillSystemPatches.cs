using HarmonyLib;

#nullable disable

[HarmonyPatch(typeof(GameManager), "explode")]
internal static class RebirthSurvivorExplosiveBlockSkillPatch
{
    private static void Prefix(int _entityId,ItemValue _itemValueExplosionSource,out RebirthExplosionWorkWitness.Frame __state)
        =>RebirthExplosionWorkWitness.Begin(_entityId,_itemValueExplosionSource,out __state);
    private static void Postfix(RebirthExplosionWorkWitness.Frame __state,bool __runOriginal)
        =>RebirthExplosionWorkWitness.Finish(__state.Witness,__runOriginal);
    private static System.Exception Finalizer(System.Exception __exception,RebirthExplosionWorkWitness.Frame __state)
    {
        RebirthExplosionWorkWitness.Close(__state);return __exception;
    }
}

[HarmonyPatch(typeof(Explosion),nameof(Explosion.AttackBlocks))]
internal static class RebirthSurvivorExplosionPlannedBlocksPatch
{
    private static void Postfix(Explosion __instance)=>RebirthExplosionWorkWitness.Capture(__instance);
}

internal static class RebirthExplosionWorkWitness
{
    internal struct Frame
    {
        internal Scope Previous,Witness;
        internal bool Open;
    }
    internal sealed class Scope
    {
        internal World World;internal EntityPlayer Player;internal int Deaths;
        internal RebirthStablePlayerIdentity Owner;internal RebirthWorldCharacterRecord Record;internal string Creation,Receipt;
        internal System.Collections.Generic.Dictionary<Vector3i,BlockValue> ChangedBlocks;
        internal bool Finished;
    }
    [System.ThreadStatic]private static Scope current;
    private static readonly System.Collections.Generic.List<Scope> Pending=new System.Collections.Generic.List<Scope>();
    private sealed class ExplosionCounts{internal long Accepted,Settled;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,ExplosionCounts> Counts=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,ExplosionCounts>();
    internal static bool IsPendingReceipt(string receipt)=>Pending.Exists(scope=>scope.Receipt==receipt);
    internal static System.Collections.Generic.ISet<string> ProtectReceipts(System.Collections.Generic.HashSet<string> receipts)
    {
        if(!ThreadManager.IsMainThread())return new System.Collections.Generic.HashSet<string>(receipts,System.StringComparer.Ordinal);
        var result=new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);foreach(var scope in Pending)result.Add(scope.Receipt);return result;
    }
    internal static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer player=null)
    {
        player=player??GameManager.Instance?.World?.GetPrimaryPlayer();ExplosionCounts counts=null;if(player!=null)Counts.TryGetValue(player,out counts);
        return new Newtonsoft.Json.Linq.JObject{["actor"]=player?.entityId??-1,["accepted"]=counts?.Accepted??0,["settled"]=counts?.Settled??0,["pending"]=Pending.FindAll(scope=>ReferenceEquals(scope.Player,player)).Count};
    }
    internal static void Begin(int actor,ItemValue source,out Frame frame)
    {
        // An unqualified nested explosion still masks the outer witness.
        frame=new Frame{Previous=current,Open=true};
        current=null;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||source==null||
            RebirthProgressionRuntimeConfig.ClassifyCombat(source)!="skill.explosives")return;
        var player=world.GetEntity(actor) as EntityPlayer;
        if(player==null||player.IsDead()||!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record))return;
        frame.Witness=current=new Scope{World=world,Player=player,Deaths=player.Died,Owner=owner,Record=record,
            Creation=record.Origin?.CreationId};
    }
    internal static void Capture(Explosion explosion)
    {
        var scope=current;if(scope?.Record==null||explosion==null||scope.Finished||explosion.ChangedBlockPositions==null)return;
        foreach(var pair in explosion.ChangedBlockPositions)
        {
            var before=scope.World.GetBlock(pair.Key);var after=pair.Value.blockValue;
            if(before.Equals(after))continue; // protected/zero-damage candidate
            (scope.ChangedBlocks??=new System.Collections.Generic.Dictionary<Vector3i,BlockValue>())[pair.Key]=after;
        }
    }
    internal static void Finish(Scope scope,bool ran)
    {
        if(scope==null||scope.Finished)return;scope.Finished=true;
        var changed=scope.ChangedBlocks;
        scope.ChangedBlocks=null;
        if(!ran||changed==null||!ReferenceEquals(GameManager.Instance?.World,scope.World)||scope.World.IsRemote())return;
        foreach(var pair in changed)
        {
            if(!scope.World.GetBlock(pair.Key).Equals(pair.Value))continue;
            scope.Receipt="explosion-block:"+System.Guid.NewGuid().ToString("N");
            Counts.GetOrCreateValue(scope.Player).Accepted++;
            Pending.Add(scope);
            Tick();
            break;
        }
    }
    internal static void Close(Frame frame)
    {
        if(frame.Open&&ReferenceEquals(current,frame.Witness))current=frame.Previous;
    }
    internal static void Tick()
    {
        if(Pending.Count==0||!ThreadManager.IsMainThread())return;
        foreach(var scope in Pending.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,scope.World)||!ReferenceEquals(scope.World.GetEntity(scope.Player.entityId),scope.Player)||
                scope.Player.IsDead()||scope.Player.Died!=scope.Deaths)
            {
                Pending.Remove(scope);
                continue;
            }
            if(!RebirthSkillAwardService.TryGetEligible(scope.Player,out var owner,out var record))continue;
            if(owner.StorageKey!=scope.Owner.StorageKey||!RebirthSurvivorRequestScope.Matches(scope.Creation,record.Origin?.CreationId))
            {
                Pending.Remove(scope);
                continue;
            }
            if(!ReferenceEquals(record,scope.Record))continue;
            if(RebirthSkillAwardService.TryAwardDurable(scope.Player,"skill.explosives",RebirthProgressionRuntimeConfig.ExplosivesBlockAward,
                "explosion:block",0f,scope.Receipt,out _,out _)&&Pending.Remove(scope))
                Counts.GetOrCreateValue(scope.Player).Settled++;
        }
    }
}

[HarmonyPatch(typeof(DroneWeapons.StunBeamWeapon), nameof(DroneWeapons.StunBeamWeapon.Fire))]
internal static class RebirthSurvivorDroneStunSkillPatch
{
    private static void Postfix(DroneWeapons.StunBeamWeapon __instance, EntityAlive _target)
    {
        RebirthComplexSkillSystemService.OnDroneShockCompleted(__instance, _target);
    }
}
