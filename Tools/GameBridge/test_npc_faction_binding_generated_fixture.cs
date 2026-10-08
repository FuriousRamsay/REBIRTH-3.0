using UnityEngine;

#nullable disable

/// <summary>Resolves live native factions without inventing faction identity from NPC category.</summary>
public static class RebirthNpcFactionCombatResolver
{
    public static RebirthNpcCombatDisposition Classify(float relationship)
    {
        if(float.IsNaN(relationship)||float.IsInfinity(relationship))return RebirthNpcCombatDisposition.Unknown;
        // Installed native tier boundaries: Hate<200, Dislike<400, Neutral<600, Like>=600.
        if(relationship<400f)return RebirthNpcCombatDisposition.Hostile;
        if(relationship<600f)return RebirthNpcCombatDisposition.Neutral;
        return RebirthNpcCombatDisposition.Allied;
    }
    public static bool TryResolve(RebirthNpcRuntimeState actor,RebirthNpcRuntimeState target,out RebirthNpcCombatDisposition disposition)
    {
        disposition=RebirthNpcCombatDisposition.Unknown;
        EntityAlive left,right;
        if(!TryBind(actor,out left)||!TryBind(target,out right)||left.world!=right.world||FactionManager.Instance==null)return false;
        disposition=Classify(FactionManager.Instance.GetRelationshipValue(left,right));
        // A live native relationship was sampled, even when its value is invalid.
        // Keep Unknown authoritative so callers cannot fall back to a hostile hint.
        return true;
    }
    private static bool TryBind(RebirthNpcRuntimeState state,out EntityAlive entity)
    {
        entity=null;int id;
        var world=GameManager.Instance?.World;
        if(state==null||world==null||state.Presence!=RebirthNpcPresenceState.Active||!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out id))return false;
        var npc=world.GetEntity(id) as EntityRebirthNPC;
        if(npc==null||npc.IsDead()||npc.world!=world||npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId!=state.StableId)return false;
        entity=npc;return true;
    }
}
namespace UnityEngine { public sealed class FixtureNamespaceMarker{} }
public enum RebirthNpcCombatDisposition:byte { Unknown,Allied,Neutral,Hostile }
public enum RebirthNpcPresenceState { Inactive,Active }
public sealed class RebirthNpcRuntimeState { public int StableId; public RebirthNpcPresenceState Presence=RebirthNpcPresenceState.Active; }
public class EntityAlive { public World world; }
public sealed class EntityRebirthNPC:EntityAlive { public RebirthNpcRuntimeState RebirthRuntimeState; public bool Dead; public bool IsDead(){return Dead;} }
public sealed class World { public readonly System.Collections.Generic.Dictionary<int,EntityAlive> Entities=new System.Collections.Generic.Dictionary<int,EntityAlive>(); public EntityAlive GetEntity(int id){EntityAlive result;Entities.TryGetValue(id,out result);return result;} }
public sealed class GameManager { public static GameManager Instance=new GameManager(); public World World=new World(); }
public static class RebirthNpcRuntimeRegistry { public static bool TryGetEntityId(int stable,out int id){id=stable;return true;} }
public sealed class FactionManager { public static FactionManager Instance=new FactionManager(); public float Value; public float GetRelationshipValue(EntityAlive a,EntityAlive b){return Value;} }
public static class FactionBindingFixture {
public static string Run(){
var world=GameManager.Instance.World;
var a=new RebirthNpcRuntimeState{StableId=1};var b=new RebirthNpcRuntimeState{StableId=2};
var left=new EntityRebirthNPC{world=world,RebirthRuntimeState=a};var right=new EntityRebirthNPC{world=world,RebirthRuntimeState=b};world.Entities[1]=left;world.Entities[2]=right;
RebirthNpcCombatDisposition disposition;
FactionManager.Instance.Value=float.NaN;if(!RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition)||disposition!=RebirthNpcCombatDisposition.Unknown)throw new System.Exception("Invalid native value must be sampled Unknown, without fallback");
FactionManager.Instance.Value=100;if(!RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition)||disposition!=RebirthNpcCombatDisposition.Hostile)throw new System.Exception("Live hostile not resolved");
right.Dead=true;if(RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition))throw new System.Exception("Dead entity bound");right.Dead=false;
right.world=new World();if(RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition))throw new System.Exception("Different world bound");right.world=world;
right.RebirthRuntimeState=new RebirthNpcRuntimeState{StableId=9};if(RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition))throw new System.Exception("Stale entity ID bound");right.RebirthRuntimeState=b;
b.Presence=RebirthNpcPresenceState.Inactive;if(RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition))throw new System.Exception("Inactive target bound");b.Presence=RebirthNpcPresenceState.Active;
world.Entities.Remove(2);if(RebirthNpcFactionCombatResolver.TryResolve(a,b,out disposition))throw new System.Exception("Missing target bound");
return "PASS: actual full resolver, seven binding/nonfinite/native relationship cases.";
}
}