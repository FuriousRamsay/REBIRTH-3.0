// NOT RUN. Actual witness class with explicit native spawn/Harmony doubles.
// This does not execute the installed native drop, patching, physics or saves.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace HarmonyLib
{
    class Harmony {internal Harmony(string id){}}
    class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string method,params Type[] args){}}
}
namespace UnityEngine
{
    struct Vector3 {public static Vector3 up=>new Vector3();public static Vector3 zero=>new Vector3();public static Vector3 operator+(Vector3 a,Vector3 b)=>a;}
}
class ItemStack {internal int count;internal string Data="full-native-item";internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack{count=count,Data=Data};}
class Entity {internal World world;internal int entityId,belongsPlayerId;internal bool Marked;internal bool IsMarkedForUnload()=>Marked;}
class EntityItem:Entity {internal ItemStack itemStack;}
class EntityPlayer:Entity {internal Vector3 position;}
class World
{
    internal readonly Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();
    internal bool IsRemote()=>false;
    internal Entity GetEntity(int id)=>Entities.TryGetValue(id,out var entity)?entity:null;
    internal void SpawnEntityInWorld(Entity entity)
    {Entities.Add(entity.entityId,entity);RebirthHarmonyBootstrap.Observer.Invoke(null,new object[]{this,entity,GameManager.Mode!=6});}
}
class GameManager
{
    internal static GameManager Instance=new GameManager();internal static int Mode;internal World World;
    internal void ItemDropServer(ItemStack stack,Vector3 point,Vector3 offset,int owner,float life,bool relative)
    {
        if(Mode==1)return;
        var item=new EntityItem{world=World,entityId=20,belongsPlayerId=owner,itemStack=stack.Clone(),Marked=Mode==4};
        if(Mode==2)item.itemStack.Data="altered";
        World.SpawnEntityInWorld(item);
        if(Mode==3)World.SpawnEntityInWorld(new EntityItem{world=World,entityId=21,belongsPlayerId=owner,itemStack=stack.Clone()});
        if(Mode==5)throw new Exception("after native spawn");
    }
}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthHarmonyBootstrap
{
    internal static MethodInfo Observer;
    internal static void PatchClassOnce(Harmony harmony,Type type)=>Observer=type.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
}
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&a.Data==b.Data;}
static class RebirthVehicleRefundDropFixture
{
    static EntityPlayer Owner(){var world=new World();var player=new EntityPlayer{world=world,entityId=1};world.Entities.Add(1,player);GameManager.Instance.World=world;return player;}
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        var stack=new ItemStack{count=3};
        foreach(int mode in new[]{0,1,2,3,4,6})
        {GameManager.Mode=mode;Need(RebirthVehicleRefundDropWitness.TryDrop(Owner(),stack)==(mode==0),"Native drop witness accepted missing/altered/duplicate/unloading/skipped spawn: "+mode);}
        GameManager.Mode=5;bool threw=false;try{RebirthVehicleRefundDropWitness.TryDrop(Owner(),stack);}catch{threw=true;}
        Need(threw,"Unknown after-spawn exception was swallowed");
        GameManager.Mode=0;Need(RebirthVehicleRefundDropWitness.TryDrop(Owner(),stack),"Invocation scope leaked after failure");
    }
}
