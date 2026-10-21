using System;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}}
namespace UnityEngine{public struct Vector3{public static Vector3 operator +(Vector3 a,Vector3 b)=>a;}public struct Vector2{public static Vector2 zero=>new();}public class Transform{public Vector3 position;}}
public static class Origin{public static UnityEngine.Vector3 position;}
public enum PassiveEffects{RoundRayCount}
public class ItemValue{public int Rays=1;}
public static class EffectManager{public static float GetValue(PassiveEffects p,ItemValue a,int d)=>a.Rays;}
public class Stack{public int count=5;}
public class PowerRangedTrap{public Stack[] Stacks={new Stack()};}
public class Tile{public object PowerItem=new PowerRangedTrap();}
namespace Audio{public static class Manager{public static int Calls;public static string Sound;public static void BroadcastPlay(UnityEngine.Vector3 p,string s){Calls++;Sound=s;}}}
public class AutoTurretFireController{public string muzzleFireParticle="flashCloud",muzzleSmokeParticle="smoke",fireSound="rifle";public int rayCount=1;public Tile TileEntity=new();public UnityEngine.Transform Muzzle=new();public Action During;public bool Spend=true,Throw;public void Fire(){During?.Invoke();if(Throw)throw new Exception("native");if(Spend)((PowerRangedTrap)TileEntity.PowerItem).Stacks[0].count--;}public UnityEngine.Vector2 getSpread(ItemValue v)=>new();}
class Program{static int n;static void C(bool b){n++;if(!b)throw new Exception("assert "+n);}static void Main(){var f=new AutoTurretFireController();f.During=()=>{C(f.muzzleFireParticle=="");C(f.muzzleSmokeParticle=="");C(RebirthCamperTurretManualShot.Precise(f,new ItemValue()));C(!RebirthCamperTurretManualShot.Precise(f,new ItemValue{Rays=8}));C(!RebirthCamperTurretManualShot.Precise(new(),new()));};RebirthCamperTurretManualShot.Fire(f,true);C(Audio.Manager.Calls==1&&Audio.Manager.Sound=="rifle");C(f.muzzleFireParticle=="flashCloud"&&f.muzzleSmokeParticle=="smoke");C(RebirthCamperTurretManualShot.Active==null);f.During=()=>{C(f.muzzleFireParticle=="flashCloud");C(!RebirthCamperTurretManualShot.Precise(f,new()));};RebirthCamperTurretManualShot.Fire(f,false);C(Audio.Manager.Calls==1);f.During=null;f.Spend=false;RebirthCamperTurretManualShot.Fire(f,true);C(Audio.Manager.Calls==1);f.Throw=true;try{RebirthCamperTurretManualShot.Fire(f,true);}catch{}C(RebirthCamperTurretManualShot.Active==null&&f.muzzleFireParticle=="flashCloud");Console.WriteLine("PASS "+n+" actual-source manual shot assertions");}}
