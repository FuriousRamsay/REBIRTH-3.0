using System;
namespace UnityEngine {public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}}
class World{public bool Remote;public object Entity;public bool IsRemote(){return Remote;}public object GetEntity(int id){return Entity;}}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
class RebirthNpcRuntimeState{public int StableId=1;public uint Revision=2;public string ProfileId="specialist.medic";}
class EntityRebirthNPC{public World world;public RebirthNpcRuntimeState RebirthRuntimeState;public UnityEngine.Vector3 position,rotation;}
class RebirthNpcTransformRecord{public UnityEngine.Vector3 WorldPosition;public float RotationYaw;public uint TransformRevision;}
class RebirthNpcRuntimeRegistry{public static bool Found=true;public static bool TryGetEntityId(int id,out int entityId){entityId=7;return Found;}}
public class TransformFixture{
 public static string Run(){int checks=0;var w=new World();GameManager.Instance.World=w;var state=new RebirthNpcRuntimeState();var npc=new EntityRebirthNPC{world=w,RebirthRuntimeState=new RebirthNpcRuntimeState(),position=new UnityEngine.Vector3(14,45,99),rotation=new UnityEngine.Vector3(0,123,0)};w.Entity=npc;
 var target=new RebirthNpcTransformRecord{WorldPosition=new UnityEngine.Vector3(1,2,3)};
 Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
 check(RebirthNpcAggregateTransformCapture.TryCapture(w,state,target)&&target.WorldPosition.x==14&&target.WorldPosition.z==99&&target.RotationYaw==123,"actual position and yaw");
 Action<string> refused=label=>{target.WorldPosition=new UnityEngine.Vector3(1,2,3);target.TransformRevision=19;target.RotationYaw=27;check(!RebirthNpcAggregateTransformCapture.TryCapture(w,state,target)&&target.WorldPosition.x==1&&target.TransformRevision==19&&target.RotationYaw==27,label);};
 w.Entity=null;refused("unpublished preserves saved position");w.Entity=npc;
 npc.RebirthRuntimeState.StableId=8;refused("reused id");npc.RebirthRuntimeState.StableId=1;
 npc.RebirthRuntimeState.Revision=3;refused("stale runtime snapshot");npc.RebirthRuntimeState.Revision=2;
 npc.RebirthRuntimeState.ProfileId="other";refused("profile mismatch");npc.RebirthRuntimeState.ProfileId=state.ProfileId;
 npc.position.x=float.NaN;refused("nonfinite position");npc.position.x=14;
 npc.rotation.y=float.PositiveInfinity;refused("nonfinite yaw");npc.rotation.y=123;
 w.Remote=true;refused("remote world");w.Remote=false;
 GameManager.Instance.World=new World();refused("old world");GameManager.Instance.World=w;
 RebirthNpcRuntimeRegistry.Found=false;refused("missing embodiment");
 return "PASS "+checks+" complete actual transform capture helper checks; native world/registry/vector types doubled";
 }
}