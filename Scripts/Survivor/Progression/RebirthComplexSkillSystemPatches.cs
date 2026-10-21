using HarmonyLib;

#nullable disable

[HarmonyPatch(typeof(GameManager), "explode")]
internal static class RebirthSurvivorExplosiveBlockSkillPatch
{
    private static void Prefix(int _entityId,ItemValue _itemValueExplosionSource,out RebirthExplosionWorkWitness.Scope __state)
        =>__state=RebirthExplosionWorkWitness.Begin(_entityId,_itemValueExplosionSource);
    private static void Postfix(RebirthExplosionWorkWitness.Scope __state,bool __runOriginal)
        =>RebirthExplosionWorkWitness.Finish(__state,__runOriginal);
    private static System.Exception Finalizer(System.Exception __exception,RebirthExplosionWorkWitness.Scope __state)
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
    internal sealed class Scope
    {
        internal Scope Previous;internal World World;internal EntityPlayer Player;
        internal RebirthStablePlayerIdentity Owner;internal RebirthWorldCharacterRecord Record;internal string Creation,Receipt;
        internal readonly System.Collections.Generic.Dictionary<Vector3i,BlockValue> Before=new System.Collections.Generic.Dictionary<Vector3i,BlockValue>();
        internal readonly System.Collections.Generic.Dictionary<Vector3i,BlockValue> After=new System.Collections.Generic.Dictionary<Vector3i,BlockValue>();
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
    internal static Scope Begin(int actor,ItemValue source)
    {
        var scope=new Scope{Previous=current};current=scope;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||source==null||
            RebirthProgressionRuntimeConfig.ClassifyCombat(source)!="skill.explosives")return scope;
        var player=world.GetEntity(actor) as EntityPlayer;
        if(!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record))return scope;
        scope.World=world;scope.Player=player;scope.Owner=owner;scope.Record=record;scope.Creation=record.Origin?.CreationId;scope.Receipt="explosion-block:"+System.Guid.NewGuid().ToString("N");
        return scope;
    }
    internal static void Capture(Explosion explosion)
    {
        var scope=current;if(scope?.Record==null||explosion==null||scope.Finished||explosion.ChangedBlockPositions==null)return;
        foreach(var pair in explosion.ChangedBlockPositions)
        {
            var before=scope.World.GetBlock(pair.Key);var after=pair.Value.blockValue;
            if(before.Equals(after))continue; // protected/zero-damage candidate
            scope.Before[pair.Key]=before;scope.After[pair.Key]=after;
        }
    }
    internal static void Finish(Scope scope,bool ran)
    {
        if(scope==null||scope.Finished)return;scope.Finished=true;
        if(scope.Record==null||!ran||!ReferenceEquals(GameManager.Instance?.World,scope.World)||scope.World.IsRemote())return;
        bool accepted=false;
        foreach(var pair in scope.After)
            if(!scope.Before[pair.Key].Equals(pair.Value)&&scope.World.GetBlock(pair.Key).Equals(pair.Value)){accepted=true;break;}
        if(accepted){Counts.GetOrCreateValue(scope.Player).Accepted++;Pending.Add(scope);Tick();}
    }
    internal static void Close(Scope scope){if(scope!=null&&ReferenceEquals(current,scope))current=scope.Previous;}
    internal static void Tick()
    {
        foreach(var scope in Pending.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,scope.World)||!ReferenceEquals(scope.World.GetEntity(scope.Player.entityId),scope.Player)||
                !RebirthSkillAwardService.TryGetEligible(scope.Player,out var owner,out var record)||owner.StorageKey!=scope.Owner.StorageKey||
                !ReferenceEquals(record,scope.Record)||!RebirthSurvivorRequestScope.Matches(scope.Creation,record.Origin?.CreationId))continue;
            if(RebirthSkillAwardService.TryAwardDurable(scope.Player,"skill.explosives",RebirthProgressionRuntimeConfig.ExplosivesBlockAward,
                "explosion:block",0f,scope.Receipt,out _,out _)){Pending.Remove(scope);Counts.GetOrCreateValue(scope.Player).Settled++;}
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
