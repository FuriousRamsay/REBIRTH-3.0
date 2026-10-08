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
    internal bool CanTarget(EntityAlive target)
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
public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public float magnitude{get{return (float)System.Math.Sqrt(sqrMagnitude);}} public Vector3 normalized{get{float m=magnitude;return m>0?new Vector3(x/m,y/m,z/m):new Vector3(0,0,0);}} public static Vector3 up{get{return new Vector3(0,1,0);}} public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;} public float sqrMagnitude{get{return x*x+y*y+z*z;}} public static Vector3 one{get{return new Vector3(1,1,1);}} public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);} public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);} public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);} }
public struct Ray{public Vector3 origin;public Ray(Vector3 value){origin=value;}} public static class Time{public static float time;} public struct Bounds { public Bounds(Vector3 a,Vector3 b){} }
public static class Mathf { public static float Sqrt(float value){return (float)System.Math.Sqrt(value);} }
}
namespace UnityEngine.Scripting { public sealed class PreserveAttribute:System.Attribute{} }
public enum RebirthNpcPresenceState{Active,Inactive}
public enum RebirthNpcOwnershipKind{None,Player}
public enum RebirthNpcCombatDisposition{Unknown,Allied,Neutral,Hostile}
public sealed class RebirthNpcRuntimeState {public int StableId;public string OwnerId;public RebirthNpcOwnershipKind OwnershipKind;public RebirthNpcPresenceState Presence;}
public class Entity {public int entityId;public UnityEngine.Vector3 position;}
public class EntityAlive:Entity {public Inventory inventory=new Inventory();public object bag=new object();public MoveHelper moveHelper=new MoveHelper();public int Presses,Paths;public bool AcceptAttack=true,LastCanBreak;public UnityEngine.Vector3 Look=new UnityEngine.Vector3(1,0,0);public void RotateTo(EntityAlive entity,float yaw,float pitch){} public float GetEyeHeight(){return 1;} public UnityEngine.Ray GetLookRay(){return new UnityEngine.Ray(position+UnityEngine.Vector3.up);} public UnityEngine.Vector3 GetLookVector(){return Look;} public bool IsAttackValid(){return true;}public bool Attack(bool release){if(!release)Presses++;return AcceptAttack;}public float GetMoveSpeedAggro(){return 2;}public void FindPath(UnityEngine.Vector3 p,float speed,bool canBreak,EAIBase task){Paths++;LastCanBreak=canBreak;}public World world;public bool Dead,Visible=true,StealthVisible=true;public float Sense=30;private EntityAlive target;public bool IsDead(){return Dead;}public float GetSeeDistance(){return Sense;} public bool CanSee(EntityAlive t){return t.Visible;}public bool CanSeeStealth(float d,float light){return StealthVisible;} public void SetAttackTarget(EntityAlive t,int ticks){target=t;} public EntityAlive GetAttackTarget(){return target;} }
public sealed class EntityRebirthNPC:EntityAlive {public RebirthNpcRuntimeState RebirthRuntimeState=new RebirthNpcRuntimeState();}
public sealed class EntityZombie:EntityAlive{}
public struct PlayerStealth{public float lightLevel;}
public sealed class EntityPlayer:EntityAlive{public PlayerStealth Stealth;public bool Ignored;public bool IsIgnoredByAI(){return Ignored;}}
public sealed class World{public bool Remote;public System.Collections.Generic.List<Entity> Entities=new System.Collections.Generic.List<Entity>();public bool IsRemote(){return Remote;}public System.Collections.Generic.List<Entity> GetEntitiesInBounds(System.Type type,UnityEngine.Bounds bounds,System.Collections.Generic.List<Entity> output){output.AddRange(Entities);return output;}}
public sealed class PersistentPlayerData{public object PrimaryId;}
public sealed class PlayerList{public System.Collections.Generic.Dictionary<int,PersistentPlayerData> Data=new System.Collections.Generic.Dictionary<int,PersistentPlayerData>();public PersistentPlayerData GetPlayerDataFromEntityID(int id){PersistentPlayerData result;Data.TryGetValue(id,out result);return result;}}
public sealed class GameManager {public static GameManager Instance=new GameManager();public PlayerList Players=new PlayerList();public int ReloadRequests;public void ItemReloadServer(int id){ReloadRequests++;}public PlayerList GetPersistentPlayerList(){return Players;}}
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

#nullable disable

[Preserve]
public sealed class EAIRebirthRangedCombat : EAIBase
{
    private EntityRebirthNPC actor;
    private EAIRebirthSelectHostileTarget gate;
    private float nextPath,nextShot,nextReload;
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=11;executeDelay=0.05f;
        gate=new EAIRebirthSelectHostileTarget();gate.Init(entity);
    }
    private bool TryWeapon(out ItemActionRanged action,out ItemActionRanged.ItemActionDataRanged data,out ItemValue item)
    {
        action=null;data=null;item=actor?.inventory?.holdingItemItemValue;
        var actions=item?.ItemClass?.Actions;
        if(actions==null||actions.Length==0||actions[0] is ItemActionCatapult||actions[0] is ItemActionLauncher)return false;
        action=actions[0] as ItemActionRanged;
        var actionData=actor.inventory.holdingItemData?.actionData;
        if(action==null||actionData==null||actionData.Count==0)return false;
        data=actionData[0] as ItemActionRanged.ItemActionDataRanged;
        if(data==null||!ReferenceEquals(data.invData,actor.inventory.holdingItemData)||data.invData?.holdingEntity!=actor||data.invData.itemValue==null)return false;
        // Playerlike NPCs use actual rounds, never a native infinite-ammunition override.
        return !action.HasInfiniteAmmo(data);
    }
    public override bool CanExecute()
    {ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;return actor!=null&&gate.CanTarget(actor.GetAttackTarget())&&TryWeapon(out action,out data,out item);}
    public override bool Continue(){return CanExecute();}
    public override void Start(){nextPath=nextShot=nextReload=Time.time;}
    public override void Update()
    {
        if(!Continue())return;
        ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;
        if(!TryWeapon(out action,out data,out item))return;
        var target=actor.GetAttackTarget();actor.RotateTo(target,30f,30f);
        if(ItemActionRanged.Reloading(data)){actor.moveHelper.Stop();return;}
        if(item.Meta<=0)
        {
            actor.moveHelper.Stop();
            if(Time.time<nextReload||actor.bag==null||action.MagazineItemNames==null||item.SelectedAmmoTypeIndex>=action.MagazineItemNames.Length)return;
            nextReload=Time.time+1f;
            if(action.CanReload(data))GameManager.Instance.ItemReloadServer(actor.entityId);
            return;
        }
        float range=action.Range;
        if(range==0f)range=EffectManager.GetItemValue(PassiveEffects.MaxRange,item);
        if(float.IsNaN(range)||float.IsInfinity(range)||range<=0f){actor.moveHelper.Stop();return;}
        range=Math.Min(range,25f);
        float distance=(target.position-actor.position).magnitude;
        if(distance>range)
        {
            if(Time.time>=nextPath){actor.FindPath(target.position,actor.GetMoveSpeedAggro(),false,this);nextPath=Time.time+0.75f;}
            return;
        }
        actor.moveHelper.Stop();
        float delay=data.Delay;
        if(float.IsNaN(delay)||float.IsInfinity(delay)||delay<=0f||Time.time<nextShot||!actor.IsAttackValid())return;
        // Rotation acceptance must precede a native shot. Rig/ray convergence needs native qualification.
        Vector3 direction=(target.position+Vector3.up*target.GetEyeHeight()-actor.GetLookRay().origin).normalized;
        float alignment=Vector3.Dot(actor.GetLookVector(),direction);
        if(float.IsNaN(alignment)||float.IsInfinity(alignment)||alignment<0.995f)return;
        if(!actor.Attack(false))return;
        actor.Attack(true);
        nextShot=Time.time+delay;
    }
    public override void Reset(){if(actor!=null)actor.moveHelper.Stop();}
}
public sealed class MoveHelper{public int Stops;public void Stop(){Stops++;}}
public class ItemAction{public float Range=20;}
public class ItemActionRanged:ItemAction{public bool Infinite,AmmoAvailable=true;public string[] MagazineItemNames=new[]{"ammo"};public bool HasInfiniteAmmo(ItemActionData data){return Infinite;}public bool CanReload(ItemActionData data){return AmmoAvailable;}public static bool Reloading(ItemActionDataRanged data){return data.Reloading;}public sealed class ItemActionDataRanged:ItemActionData{public float Delay=0.5f;public bool Reloading;}}
public sealed class ItemActionCatapult:ItemActionRanged{}
public sealed class ItemActionLauncher:ItemActionRanged{}
public sealed class ItemClass{public ItemAction[] Actions;}
public sealed class ItemValue{public ItemClass ItemClass;public int Meta;public byte SelectedAmmoTypeIndex;}
public class ItemActionData{public ItemInventoryData invData;}
public sealed class ItemInventoryData{public EntityAlive holdingEntity;public ItemValue itemValue;public System.Collections.Generic.List<ItemActionData> actionData=new System.Collections.Generic.List<ItemActionData>();}
public sealed class Inventory{public ItemValue holdingItemItemValue;public ItemInventoryData holdingItemData;}
public enum PassiveEffects{MaxRange}
public static class EffectManager{public static float GetItemValue(PassiveEffects effect,ItemValue item){return 20;}}
public static class RangedLifecycleFixture{
private static void Check(bool pass,string name){if(!pass)throw new System.Exception(name);}
public static string Run(){
var world=new World();var actor=new EntityRebirthNPC{entityId=11,world=world};actor.RebirthRuntimeState.StableId=11;var target=new EntityZombie{entityId=12,world=world,position=new UnityEngine.Vector3(10,0,0)};actor.SetAttackTarget(target,200);
var action=new ItemActionRanged();var item=new ItemValue{Meta=3,ItemClass=new ItemClass{Actions=new ItemAction[]{action}}};var inv=new ItemInventoryData{holdingEntity=actor,itemValue=item};var data=new ItemActionRanged.ItemActionDataRanged{invData=inv};inv.actionData.Add(data);actor.inventory.holdingItemItemValue=item;actor.inventory.holdingItemData=inv;
var task=new EAIRebirthRangedCombat();task.Init(actor);Check(task.CanExecute(),"Firearm admission");task.Start();task.Update();Check(actor.Presses==1,"Native press");UnityEngine.Time.time=0.1f;task.Update();Check(actor.Presses==1,"Native delay");
item.Meta=0;UnityEngine.Time.time=1;task.Update();Check(GameManager.Instance.ReloadRequests==1&&item.Meta==0,"Reload requested without fabricated magazine");task.Update();Check(GameManager.Instance.ReloadRequests==1,"Reload request throttled");
data.Reloading=true;UnityEngine.Time.time=3;task.Update();Check(GameManager.Instance.ReloadRequests==1&&actor.Presses==1,"Reloading blocks action");data.Reloading=false;action.AmmoAvailable=false;task.Update();Check(GameManager.Instance.ReloadRequests==1,"No-ammo no reload");action.AmmoAvailable=true;
action.Infinite=true;Check(!task.CanExecute(),"Infinite ammo excluded");action.Infinite=false;
item.Meta=2;actor.Look=new UnityEngine.Vector3(float.NaN,0,0);task.Update();Check(actor.Presses==1,"Nonfinite aim no shot");actor.Look=new UnityEngine.Vector3(0,0,1);task.Update();Check(actor.Presses==1,"Unaligned aim no shot");actor.Look=new UnityEngine.Vector3(1,0,0);
data.invData=new ItemInventoryData{holdingEntity=actor,itemValue=item};Check(!task.CanExecute(),"Stale holding data rejected");data.invData=inv;
target.position=new UnityEngine.Vector3(24,0,0);action.Range=15;task.Update();Check(actor.Paths==1&&!actor.LastCanBreak,"Native path cannot break blocks");
item.ItemClass.Actions[0]=new ItemActionCatapult();Check(!task.CanExecute(),"Bow excluded pending lifecycle");item.ItemClass.Actions[0]=new ItemActionLauncher();Check(!task.CanExecute(),"Explosive excluded pending safety");
return "PASS: complete actual ranged task,14 lifecycle/ammo/aim/identity/admission checks with engine doubles.";
}
}