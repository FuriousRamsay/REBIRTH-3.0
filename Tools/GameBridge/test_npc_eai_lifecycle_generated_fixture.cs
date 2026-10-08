using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class EAIRebirthSelectHostileTarget : EAIBase
{
    private const float MaximumRange=30f;
    private readonly List<Entity> candidates=new List<Entity>();
    private EntityRebirthNPC actor;
    private EntityAlive selected;
    private bool ownsTarget;
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=0.75f;
    }
    private bool Ready()
    {
        return actor!=null&&actor.world!=null&&!actor.world.IsRemote()&&!actor.IsDead()
            &&actor.RebirthRuntimeState!=null&&actor.RebirthRuntimeState.Presence==RebirthNpcPresenceState.Active
            &&!RebirthCompanionBehaviorService.IsAttackStopped(actor.RebirthRuntimeState.StableId);
    }
    private bool CanTarget(EntityAlive target)
    {
        if(!Ready()||target==null||target==actor||target.world!=actor.world||target.IsDead()||FactionManager.Instance==null)return false;
        if(!(target is EntityPlayer)&&!(target is EntityZombie)&&!(target is EntityRebirthNPC))return false;
        float sq=(target.position-actor.position).sqrMagnitude;
        float sense=actor.GetSeeDistance();
        if(float.IsNaN(sq)||float.IsInfinity(sq)||float.IsNaN(sense)||float.IsInfinity(sense)||sense<=0)return false;
        float range=Math.Min(MaximumRange,sense);
        if(sq>range*range)return false;
        var npc=target as EntityRebirthNPC;
        if(npc!=null&&RebirthNpcFriendlyFirePolicy.AreAllied(actor.RebirthRuntimeState,npc.RebirthRuntimeState))return false;
        var player=target as EntityPlayer;
        if(player!=null)
        {
            if(player.IsIgnoredByAI())return false;
            string owner=actor.RebirthRuntimeState.OwnerId;
            var data=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if(actor.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player)
            {
                if(string.IsNullOrWhiteSpace(owner)||data?.PrimaryId==null)return false;
                if(string.Equals(owner,data.PrimaryId.ToString(),StringComparison.OrdinalIgnoreCase))return false;
            }
            if(!actor.CanSeeStealth(Mathf.Sqrt(sq),player.Stealth.lightLevel))return false;
        }
        if(RebirthNpcFactionCombatResolver.Classify(FactionManager.Instance.GetRelationshipValue(actor,target))!=RebirthNpcCombatDisposition.Hostile)return false;
        return actor.CanSee(target);
    }
    public override bool CanExecute()
    {
        selected=null;if(!Ready())return false;
        float best=float.MaxValue;
        candidates.Clear();
        actor.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(actor.position,Vector3.one*(MaximumRange*2)),candidates);
        foreach(var entity in candidates)
        {
            var target=entity as EntityAlive;if(!CanTarget(target))continue;
            float sq=(target.position-actor.position).sqrMagnitude;
            if(sq<best||(sq==best&&selected!=null&&target.entityId<selected.entityId)){best=sq;selected=target;}
        }
        candidates.Clear();return selected!=null;
    }
    public override void Start()
    {
        if(!CanTarget(selected))return;
        actor.SetAttackTarget(selected,200);ownsTarget=true;
    }
    public override bool Continue(){return ownsTarget&&actor.GetAttackTarget()==selected&&CanTarget(selected);}
    public override void Update(){if(Continue())actor.SetAttackTarget(selected,200);}
    public override void Reset()
    {
        if(ownsTarget&&actor!=null&&actor.GetAttackTarget()==selected)actor.SetAttackTarget(null,0);
        ownsTarget=false;selected=null;candidates.Clear();
    }
}
public static class RebirthNpcFactionCombatResolver {    public static RebirthNpcCombatDisposition Classify(float relationship)
    {
        if(float.IsNaN(relationship)||float.IsInfinity(relationship))return RebirthNpcCombatDisposition.Unknown;
        // Installed native tier boundaries: Hate<200, Dislike<400, Neutral<600, Like>=600.
        if(relationship<400f)return RebirthNpcCombatDisposition.Hostile;
        if(relationship<600f)return RebirthNpcCombatDisposition.Neutral;
        return RebirthNpcCombatDisposition.Allied;
    }}
namespace UnityEngine {
public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public float sqrMagnitude{get{return x*x+y*y+z*z;}} public static Vector3 one{get{return new Vector3(1,1,1);}} public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);} public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);} }
public struct Bounds { public Bounds(Vector3 a,Vector3 b){} }
public static class Mathf { public static float Sqrt(float value){return (float)System.Math.Sqrt(value);} }
}
namespace UnityEngine.Scripting { public sealed class PreserveAttribute:System.Attribute{} }
public enum RebirthNpcPresenceState{Active,Inactive}
public enum RebirthNpcOwnershipKind{None,Player}
public enum RebirthNpcCombatDisposition{Unknown,Allied,Neutral,Hostile}
public sealed class RebirthNpcRuntimeState {public int StableId;public string OwnerId;public RebirthNpcOwnershipKind OwnershipKind;public RebirthNpcPresenceState Presence;}
public class Entity {public int entityId;public UnityEngine.Vector3 position;}
public class EntityAlive:Entity {public World world;public bool Dead,Visible=true,StealthVisible=true;public float Sense=30;private EntityAlive target;public bool IsDead(){return Dead;}public float GetSeeDistance(){return Sense;} public bool CanSee(EntityAlive t){return t.Visible;}public bool CanSeeStealth(float d,float light){return StealthVisible;} public void SetAttackTarget(EntityAlive t,int ticks){target=t;} public EntityAlive GetAttackTarget(){return target;} }
public sealed class EntityRebirthNPC:EntityAlive {public RebirthNpcRuntimeState RebirthRuntimeState=new RebirthNpcRuntimeState();}
public sealed class EntityZombie:EntityAlive{}
public struct PlayerStealth{public float lightLevel;}
public sealed class EntityPlayer:EntityAlive{public PlayerStealth Stealth;public bool Ignored;public bool IsIgnoredByAI(){return Ignored;}}
public sealed class World{public bool Remote;public System.Collections.Generic.List<Entity> Entities=new System.Collections.Generic.List<Entity>();public bool IsRemote(){return Remote;}public System.Collections.Generic.List<Entity> GetEntitiesInBounds(System.Type type,UnityEngine.Bounds bounds,System.Collections.Generic.List<Entity> output){output.AddRange(Entities);return output;}}
public sealed class PersistentPlayerData{public object PrimaryId;}
public sealed class PlayerList{public System.Collections.Generic.Dictionary<int,PersistentPlayerData> Data=new System.Collections.Generic.Dictionary<int,PersistentPlayerData>();public PersistentPlayerData GetPlayerDataFromEntityID(int id){PersistentPlayerData result;Data.TryGetValue(id,out result);return result;}}
public sealed class GameManager {public static GameManager Instance=new GameManager();public PlayerList Players=new PlayerList();public PlayerList GetPersistentPlayerList(){return Players;}}
public sealed class FactionManager {public static FactionManager Instance=new FactionManager();public float Value=100;public float GetRelationshipValue(EntityAlive a,EntityAlive b){return Value;}}
public static class RebirthCompanionBehaviorService{public static bool Stopped;public static bool IsAttackStopped(int id){return Stopped;}}
public static class RebirthNpcFriendlyFirePolicy{public static bool Allies;public static bool AreAllied(RebirthNpcRuntimeState a,RebirthNpcRuntimeState b){return Allies;}}
public class EAIBase{public int MutexBits;public float executeDelay;public virtual void Init(EntityAlive e){}public virtual bool CanExecute(){return false;}public virtual void Start(){}public virtual bool Continue(){return false;}public virtual void Update(){}public virtual void Reset(){}}
public static class EaiLifecycleFixture {
private static void Check(bool pass,string name){if(!pass)throw new System.Exception(name);}
public static string Run(){
var world=new World();var actor=new EntityRebirthNPC{entityId=1,world=world};actor.RebirthRuntimeState.StableId=1;
var far=new EntityZombie{entityId=4,world=world,position=new UnityEngine.Vector3(10,0,0)};var near=new EntityZombie{entityId=3,world=world,position=new UnityEngine.Vector3(5,0,0)};world.Entities.Add(far);world.Entities.Add(near);
var task=new EAIRebirthSelectHostileTarget();task.Init(actor);Check(task.CanExecute(),"Acquisition");task.Start();Check(actor.GetAttackTarget()==near&&task.Continue(),"Nearest + start");
FactionManager.Instance.Value=450;Check(!task.Continue(),"Neutral invalidation");task.Reset();Check(actor.GetAttackTarget()==null,"Own target clear");FactionManager.Instance.Value=100;
Check(task.CanExecute(),"Reacquisition");task.Start();actor.SetAttackTarget(far,200);task.Reset();Check(actor.GetAttackTarget()==far,"Foreign target retained");actor.SetAttackTarget(null,0);
near.Visible=false;far.Visible=false;Check(!task.CanExecute(),"LOS rejection");near.Visible=true;far.Visible=true;
world.Remote=true;Check(!task.CanExecute(),"Client rejection");world.Remote=false;
RebirthCompanionBehaviorService.Stopped=true;Check(!task.CanExecute(),"Stop rejection");RebirthCompanionBehaviorService.Stopped=false;
FactionManager.Instance.Value=float.NaN;Check(!task.CanExecute(),"Unknown relation rejection");FactionManager.Instance.Value=100;
world.Entities.Clear();var owner=new EntityPlayer{entityId=2,world=world,position=new UnityEngine.Vector3(2,0,0)};world.Entities.Add(owner);actor.RebirthRuntimeState.OwnershipKind=RebirthNpcOwnershipKind.Player;actor.RebirthRuntimeState.OwnerId="owner";
Check(!task.CanExecute(),"Joining identity protection");GameManager.Instance.Players.Data[2]=new PersistentPlayerData{PrimaryId="OWNER"};Check(!task.CanExecute(),"Owner protection");GameManager.Instance.Players.Data[2].PrimaryId="other";Check(task.CanExecute(),"Hostile other player");task.Start();owner.Dead=true;Check(!task.Continue(),"Death invalidation");task.Reset();owner.Dead=false;
actor.StealthVisible=false;Check(!task.CanExecute(),"Stealth rejection");actor.StealthVisible=true;
owner.position=new UnityEngine.Vector3(float.NaN,0,0);Check(!task.CanExecute(),"Nonfinite distance rejection");
return "PASS: actual complete EAI task,16 lifecycle/perception/owner/authority cases with engine doubles.";
}
}