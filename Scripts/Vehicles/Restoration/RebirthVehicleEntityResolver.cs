using System;
using UnityEngine;

// One resolution contract for entity service, operation and pickup persistence.
// true means recognized; a null assembly means authority/data is not ready and
// callers must fail closed rather than treating a known vehicle as untracked.
internal static class RebirthVehicleEntityResolver
{
    internal static bool Resolve(EntityVehicle entity,out RebirthVehicleAssembly assembly)
    {
        assembly=null;
        if(entity==null||entity.vehicle==null||!ThreadManager.IsMainThread())return false;
        if(RebirthVehicleAssemblyIndex.TryGetEntity(entity,out assembly)&&assembly!=null)return true;
        // Native item metadata carries pickup/load/replication state. It always
        // precedes empty migration and retains every existing installed stack.
        if(RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(entity,out assembly)&&assembly!=null)
        {RebirthVehicleAssemblyIndex.RegisterEntity(entity,assembly);return true;}
        var definition=EntityClass.GetEntityClass(entity.entityClass);
        var content=RebirthVehicleDefinitionRegistry.GetContentForEntity(definition?.entityClassName??string.Empty);
        if(content==null||content.Availability!=RebirthVehicleContentAvailability.Enabled)return false;
        var item=entity.vehicle.GetUpdatedItemValue();
        // Preserve malformed/unknown serialized evidence; do not overwrite it
        // with a fresh empty assembly or grant replacement component items.
        if(item==null||item.TryGetMetadata("RebirthVehicleAssembly",out string payload)&&!string.IsNullOrEmpty(payload))return true;
        if(entity.world==null||entity.world.IsRemote()||!ReferenceEquals(entity.world.GetEntity(entity.entityId),entity))return true;
        var native=entity.GetVehicle();
        var created=new RebirthVehicleAssembly{AssemblyId=Guid.NewGuid(),ContentId=content.ContentId,FamilyId=content.FamilyId,
            FuelPercent=native!=null&&native.GetMaxFuelLevel()>0?Mathf.Clamp01(native.GetFuelLevel()/native.GetMaxFuelLevel()):-1f};
        created.Performance=RebirthVehiclePerformanceCompiler.Compile(created);
        if(!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity,created))return true;
        RebirthVehicleAssemblyIndex.RegisterEntity(entity,created);assembly=created;return true;
    }
}
