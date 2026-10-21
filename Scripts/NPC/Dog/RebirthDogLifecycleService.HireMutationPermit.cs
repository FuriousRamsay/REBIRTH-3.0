using System;
using UnityEngine;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireMutationPermit
    {
        internal HireSettlement Original;internal bool Owner,Consumed,Guard;internal string OwnerId;
        internal RebirthNpcOwnershipKind Ownership;internal RebirthNpcOrderState Order;internal Vector3 Position;
    }
    [ThreadStatic]private static HireMutationPermit HireNativeMutation;
    internal static bool AllowsHireOwnerMutation(EntityRebirthNPC entity,RebirthNpcOwnershipKind kind,string owner)
    {
        var dog=entity as EntityRebirthDogCompanion;if(dog?.RebirthRuntimeState==null||!IsHireCustodyHeld(dog.RebirthRuntimeState.StableId))return true;
        var permit=HireNativeMutation;
        if(permit==null||permit.Consumed||!permit.Owner||permit.Ownership!=kind||(permit.OwnerId??string.Empty)!=(owner??string.Empty)||
            !ReferenceEquals(permit.Original.Dog,dog)||!ReferenceEquals(CurrentHireMutation,permit.Original)||!permit.Original.Busy||!HireCurrent(permit.Original))return false;
        permit.Consumed=true;return true;
    }
    internal static bool AllowsHireOrderMutation(EntityRebirthNPC entity,RebirthNpcOrderState order,Vector3 position,bool guard)
    {
        var dog=entity as EntityRebirthDogCompanion;if(dog?.RebirthRuntimeState==null||!IsHireCustodyHeld(dog.RebirthRuntimeState.StableId))return true;
        var permit=HireNativeMutation;
        if(permit==null||permit.Consumed||permit.Owner||permit.Order!=order||permit.Guard!=guard||permit.Position!=position||
            !ReferenceEquals(permit.Original.Dog,dog)||!ReferenceEquals(CurrentHireMutation,permit.Original)||!permit.Original.Busy||!HireCurrent(permit.Original))return false;
        permit.Consumed=true;return true;
    }
    private static RebirthNpcTransactionResult ApplyHireOwner(HireSettlement original,RebirthNpcOwnershipKind kind,string owner)
    {
        var prior=HireNativeMutation;HireNativeMutation=new HireMutationPermit{Original=original,Owner=true,Ownership=kind,OwnerId=owner};
        try{return original.Dog.SetRebirthOwner(kind,owner);}finally{HireNativeMutation=prior;}
    }
    private static RebirthNpcTransactionResult ApplyHireOrder(HireSettlement original,RebirthNpcOrderState order,Vector3 position,bool guard)
    {
        var prior=HireNativeMutation;HireNativeMutation=new HireMutationPermit{Original=original,Order=order,Position=position,Guard=guard};
        try{return original.Dog.SetRebirthOrder(order,position,guard);}finally{HireNativeMutation=prior;}
    }
}
