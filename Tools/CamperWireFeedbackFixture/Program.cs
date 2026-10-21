using System;
namespace UnityEngine{
 public class Object{public static implicit operator bool(Object o)=>o!=null;public static GameObject Instantiate(GameObject o)=>new();public static void Destroy(Object o){}}
 public struct Vector3{public float x,y,z;public static Vector3 zero=>new();public static Vector3 operator +(Vector3 a,Vector3 b)=>new(){x=a.x+b.x};}
 public class GameObject:Object{public string name="wire_mesh";public bool activeInHierarchy=true;public T GetComponent<T>()where T:new()=>new T();}
 public class Transform:Object{public GameObject gameObject=new();public Vector3 position;public Transform FindInChilds(string n)=>new();}
 public static class Resources{public static bool Missing;public static Object Load(string p)=>Missing?null:new GameObject();}
}
public class EntityPlayerLocal:UnityEngine.Object{public bool RightArmAnimationUse;}
public class ItemActionConnectPower{public UnityEngine.Vector3 wireOffset;public UnityEngine.Transform GetHandTransform(EntityPlayerLocal p)=>new();}
public class WireNode:UnityEngine.Object{public UnityEngine.GameObject gameObject=new(),Source;public UnityEngine.Vector3 LocalPosition,localOffset,sourceOffset;public bool Pulse;public float Speed;public void TogglePulse(bool b)=>Pulse=b;public void SetPulseSpeed(float v)=>Speed=v;}
public class WireManager{public static WireManager Instance=new();public int Added,Removed;public WireNode Last;public void AddActiveWire(WireNode n){Added++;Last=n;}public void RemoveActiveWire(WireNode n){Removed++;}}
public static class Origin{public static UnityEngine.Vector3 position=new(){x=1000};}
namespace Audio{public static class Manager{public static int Calls;public static string Sound;public static float X;public static void BroadcastPlay(UnityEngine.Vector3 p,string s){Calls++;Sound=s;X=p.x;}}}
class Program{static int n;static void C(bool b){n++;if(!b)throw new Exception("assert"+n);}static void Main(){var f=new RebirthCamperWireFeedback();var a=new EntityPlayerLocal();var t=new ItemActionConnectPower();var e=new UnityEngine.Transform{position=new(){x=5}};C(f.Begin(a,t,e,true));C(WireManager.Instance.Added==1);C(!WireManager.Instance.Last.Pulse&&WireManager.Instance.Last.Speed==360);C(Audio.Manager.Sound=="wire_live_connect"&&Audio.Manager.X==1005);e.position=new(){x=7};C(f.Update(a,t));C(WireManager.Instance.Last.LocalPosition.x==7);C(Audio.Manager.Calls==1);C(!f.Update(a,new ItemActionConnectPower()));C(WireManager.Instance.Removed==1);C(f.Begin(a,t,e,false));C(Audio.Manager.Sound=="wire_dead_connect");e.gameObject.activeInHierarchy=false;C(!f.Update(a,t));C(WireManager.Instance.Removed==2);f.Clear();C(WireManager.Instance.Removed==2);UnityEngine.Resources.Missing=true;C(!f.Begin(a,t,e,true));C(WireManager.Instance.Added==2);Console.WriteLine("PASS "+n+" actual-source native wire feedback assertions");}}


