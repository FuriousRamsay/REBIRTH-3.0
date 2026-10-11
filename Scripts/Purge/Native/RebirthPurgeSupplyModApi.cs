using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthPurgeSupplyModApi:IModApi
{
    private static bool installed;
    private sealed class Hook
    {
        internal MethodInfo Target;
        internal HarmonyMethod Prefix,Postfix,Finalizer;
    }
    private static void Require(List<Hook> hooks,Type target,string name,Type[] arguments,Type handler,string callback,bool postfix=false)
    {
        var method=AccessTools.Method(target,name,arguments);
        var patch=AccessTools.Method(handler,callback);
        if(method==null||patch==null)throw new MissingMethodException("Native owned supply hook differs from qualified ABI: "+target.Name+"."+name);
        var hook=new Hook{Target=method};
        if(postfix)hook.Postfix=new HarmonyMethod(patch);else hook.Prefix=new HarmonyMethod(patch);
        hooks.Add(hook);
    }
    public void InitMod(Mod mod)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||installed)return;
        // Resolve the complete set before installing any hook.
        var hooks=new List<Hook>();
        Require(hooks,typeof(EntitySupplyCrate),nameof(EntitySupplyCrate.Write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)},typeof(RebirthPurgeSupplyCrateSerialization),nameof(RebirthPurgeSupplyCrateSerialization.AfterWrite),true);
        Require(hooks,typeof(EntitySupplyCrate),nameof(EntitySupplyCrate.Read),new[]{typeof(byte),typeof(PooledBinaryReader),typeof(StreamModeRead)},typeof(RebirthPurgeSupplyCrateSerialization),nameof(RebirthPurgeSupplyCrateSerialization.AfterRead),true);
        Require(hooks,typeof(EntityAlive),nameof(EntityAlive.OnLockRequestServer),new[]{typeof(int),typeof(PooledBinaryReader),typeof(ushort)},typeof(RebirthPurgeSupplyAccess),nameof(RebirthPurgeSupplyAccess.BeforeLock));
        Require(hooks,typeof(NetPackageBag),nameof(NetPackageBag.ProcessPackage),new[]{typeof(World),typeof(GameManager)},typeof(RebirthPurgeSupplyAccess),nameof(RebirthPurgeSupplyAccess.BeforeBag));
        Require(hooks,typeof(World),nameof(World.SpawnEntityInWorld),new[]{typeof(Entity)},typeof(RebirthPurgeSupplySpawnScope),nameof(RebirthPurgeSupplySpawnScope.BeforeSpawn));
        Require(hooks,typeof(AIDirectorAirDropComponent),nameof(AIDirectorAirDropComponent.SpawnAirDrop),Type.EmptyTypes,typeof(RebirthPurgeSupplyCoordinator),nameof(RebirthPurgeSupplyCoordinator.BeforeScheduled));
        Require(hooks,typeof(AIDirectorAirDropComponent),nameof(AIDirectorAirDropComponent.Tick),new[]{typeof(double)},typeof(RebirthPurgeSupplyCoordinator),nameof(RebirthPurgeSupplyCoordinator.BeforeDirectorTick));
        Require(hooks,typeof(EntitySupplyCrate),nameof(EntitySupplyCrate.GetActivationText),Type.EmptyTypes,typeof(RebirthPurgeSupplyAccess),nameof(RebirthPurgeSupplyAccess.AfterActivationText),true);
        Require(hooks,typeof(AIAirDrop),"CreateFlightPaths",Type.EmptyTypes,typeof(RebirthPurgeSupplyCoordinator),nameof(RebirthPurgeSupplyCoordinator.BeforeFlightPaths));
        Require(hooks,typeof(AIDirectorAirDropComponent),nameof(AIDirectorAirDropComponent.SpawnSupplyCrate),new[]{typeof(UnityEngine.Vector3),typeof(ChunkManager.ChunkObserver)},typeof(RebirthPurgeSupplyCoordinator),nameof(RebirthPurgeSupplyCoordinator.BeforeCrate));
        hooks[hooks.Count-1].Finalizer=new HarmonyMethod(AccessTools.Method(typeof(RebirthPurgeSupplyCoordinator),nameof(RebirthPurgeSupplyCoordinator.AfterCrate)));
        Require(hooks,typeof(EntityAlive),nameof(EntityAlive.GetLootList),Type.EmptyTypes,typeof(RebirthPurgeSupplyLoot),nameof(RebirthPurgeSupplyLoot.BeforeLootList));
        var harmony=new Harmony("rebirth.purge.supply.persistence");
        try
        {
            foreach(var hook in hooks)harmony.Patch(hook.Target,prefix:hook.Prefix,postfix:hook.Postfix,finalizer:hook.Finalizer);
            installed=true;
        }
        catch
        {
            // Remove only this installer's hooks; a failed partial installation
            // must not leave serialization and access enforcement inconsistent.
            harmony.UnpatchSelf();
            throw;
        }
    }
}