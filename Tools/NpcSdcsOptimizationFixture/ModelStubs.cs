// Simulation boundary only. This is NOT a substitute for native runtime tests.
global using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine
{
    public class Object { public static void Destroy(object value) { } }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 zero => default; }
    public struct Quaternion { public static Quaternion identity => default; }
    public class GameObject
    {
        public string name,tag; public int layer; public Transform transform;
        public GameObject(string n=""){name=n;transform=new Transform(this);}
        public T AddComponent<T>() where T:new()=>new T();
        public T GetOrAddComponent<T>() where T:new()=>new T();
    }
    public class Transform
    {
        public GameObject gameObject; public Transform parent; public List<Transform> children=new();
        public string name {get=>gameObject.name;set=>gameObject.name=value;}
        public string tag {get=>gameObject.tag;set=>gameObject.tag=value;}
        public Transform(GameObject go){gameObject=go;}
        public void SetParent(Transform p,bool world){parent=p;p?.children.Add(this);}
        public void SetLocalPositionAndRotation(Vector3 p,Quaternion q){}
        public Transform FindInChildren(string n)=>FindRecursive(n);
        public Transform FindInChilds(string n)=>FindRecursive(n);
        public Transform FindRecursive(string n){foreach(var c in children){if(c.name==n)return c;var r=c.FindRecursive(n);if(r!=null)return r;}return null;}
    }
    public class CapsuleCollider { public Vector3 center; public float height,radius; }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo Method(Type t,string n,Type[] args)=>t.GetMethod(n,args);
        public static T MethodDelegate<T>(MethodInfo m,object obj,bool virt) where T:Delegate =>
            (T)(object)new Action<bool,bool>(((EModelBase)obj).FixtureSwitch);
    }
}
public class World{}
public class EModelInstanceAssets{}
public class Entity {public int entityClass,entityId;}
public class EntityAlive:Entity
{
    public Equipment equipment=new(); public bool IsMale=true,Dead; public int Reassignments;
    public Transform RootTransform=new GameObject("Root").transform;
    public Transform PhysicsTransform=new GameObject("Physics").transform;
    public bool IsDead()=>Dead;
    public void ReassignEquipmentTransforms(){Reassignments++;}
}
public class ItemClass{}
public class ItemValue { public ItemClass ItemClass; }
public class Equipment
{
    private Action changed;
    public int Subscribers;
    public event Action OnChanged {add{changed+=value;Subscribers++;}remove{changed-=value;Subscribers--;}}
    public ItemValue[] Items=new ItemValue[4]; public ItemClass[] Cosmetics=new ItemClass[4];
    public int GetSlotCount()=>Items.Length;
    public ItemValue GetSlotItem(int i)=>Items[i];
    public ItemClass GetCosmeticSlot(int i,bool temp)=>Cosmetics[i];
    public void Fire()=>changed?.Invoke();
}
public class Properties {public string GetString(string n)=>"Player";}
public class EntityClass
{
    public static EntityClass[] list={new EntityClass()}; public bool bIsMale=true,HasRagdoll=true;
    public float RagdollOnDeathChance; public Properties Properties=new();
}
public class EModelBase
{
    public Entity entity; public EModelInstanceAssets assets; public Transform transform=new GameObject("Model").transform;
    public GameObject gameObject=>transform.gameObject; public AvatarController avatarController;
    public Transform modelTransformParent,modelTransform,meshTransform,headTransform,bipedRootTransform,bipedPelvisTransform;
    public Transform neckTransform,neckParentTransform;
    public virtual Transform GetHeadTransform()=>headTransform??transform;
    public virtual Transform GetPelvisTransform()=>bipedPelvisTransform;
    public virtual Transform NeckTransform=>neckTransform;
    public bool bHasRagdoll,visible,IsFPV;public float ragdollChance;public string modelName;
    public int CommonInitializations;
    public static Transform FindModel(Transform t)=>t;
    public virtual void Init(World w,Entity e,EModelInstanceAssets a){}
    public virtual void SwitchModelAndView(bool f,bool m){}
    public void FixtureSwitch(bool f,bool m){modelTransform=baseRigForFixture();}
    private Transform baseRigForFixture()=>((EModelSDCS)this).baseRig?.transform;
    public virtual void SetSkinTexture(string n){}
    public virtual void Update(){}
    public virtual void PostInit(){}
    public virtual void SetVisible(bool value,bool keep){visible=value;}
    public void createAvatarController(EntityClass c){avatarController??=new AvatarController();}
    public void InitCommon(){CommonInitializations++;SwitchModelAndView(false,true);}
}
public class AvatarController{}
public class AvatarControllerDummy:AvatarController{}
public class EModelSDCS:EModelBase
{
    public EntityClass entityClass;public GameObject baseRig;public SDCSUtils.TransformCatalog boneCatalog;
    public Transform playerModelTransform,headT;public UpdateLightOnPlayers updateLightScript;public Archetype archetype;
    public bool BaseDestroyed;
    public void OnDestroy(){BaseDestroyed=true;}
    public void ClothSimInit(){}
}
public class AnimationEventBridge{}
public class UpdateLightOnPlayers {public bool IsDynamicObject;}
public static class GameManager {public static bool IsDedicatedServer;}
public static class Log {public static void Error(string s){}}
public static class GameUtils
{
    public static Transform FindTagInChilds(Transform t,string tag)
    {foreach(var c in t.children){if(c.tag==tag)return c;var r=FindTagInChilds(c,tag);if(r!=null)return r;}return null;}
}
public static class SDCSUtils
{
    public class TransformCatalog:Dictionary<string,Transform>{}
    public static int Calls;public static bool Fail,Throw,LastVisible;public static Action DuringBuild;
    public static void CreateVizTP(Archetype a,ref GameObject rig,ref TransformCatalog bones,EntityAlive e,bool fp)
    {
        Calls++; if(Throw)throw new InvalidOperationException("simulated mesh failure");
        if(Fail)return;
        rig??=new GameObject("Rig");bones??=new TransformCatalog();
        DuringBuild?.Invoke(); e.equipment.Fire();
    }
    public static void SetVisible(GameObject rig,bool value){LastVisible=value;}
}
public class Archetype
{
    public bool IsMale;public string Race,EyeColorName,Hair,HairColor,MustacheName,ChopsName,BeardName;public int Variant;
    public Archetype(string n,bool m,bool temp){IsMale=m;}
    public static Archetype GetArchetype(string n)=>new Archetype(n,true,true);
}
public static class SDCSDataUtils
{
    public enum HairTypes {Hair,Mustache,Chops,Beard}
    public class HairColorData {public string PrefabName="colour";}
    public static List<string> GetRaceList(bool m)=>new(){"white"};
    public static List<string> GetVariantList(bool m,string r)=>new(){"1"};
    public static List<string> GetEyeColorNames()=>new(){"Blue01"};
    public static List<string> GetHairNames(bool m,HairTypes t)=>new();
    public static List<HairColorData> GetHairColorNames()=>new();
}
public static class RebirthSdcsCatalogService {public static void EnsureVisualCatalog(){}public static string QualifyTextCatalog()=>"catalogue";}
public enum RebirthHumanNpcModelPipeline {Custom,SDCS}
public struct RebirthHumanNpcAppearanceDescriptor
{
    public bool IsValid;public RebirthHumanNpcModelPipeline Pipeline;public int Seed;public string Archetype;
    public RebirthNpcResolvedAppearance Resolved;
}
public class RebirthNpcResolvedAppearance
{
    public RebirthHumanNpcModelPipeline Pipeline;public bool IsMale;public int Variant;
    public string ModelId,GeneratorId,Race,HairColour,CatalogueId,Archetype,Eye,Hair,Mustache,Chops,Beard;
}
