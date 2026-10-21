using System;
using System.Collections.Generic;

public static partial class RebirthDogLifecycleService
{
    // Compact readonly observation after typed load custody moves to a retained
    // settlement, or after its already-saved metadata terminal. Never a permit.
    private sealed class HireNativeAuditReceipt
    {
        public readonly string Operation;public readonly object Owner,Dog;
        internal HireNativeAuditReceipt(string operation,HireNativeOwnerLoad owner,HireNativeDogLoad dog)
        {
            Operation=operation;
            Owner=new{owner.Loaded,owner.ReadEntered,owner.ReadReturned,owner.Hydrated,owner.Owner,owner.Save,owner.Path};
            Dog=new{stable=dog.Stable.ToString(),dog.Owner,dog.Save};
        }
    }
    private static readonly Dictionary<string,HireNativeAuditReceipt> HireRetiredNativeReceipts=new Dictionary<string,HireNativeAuditReceipt>(StringComparer.Ordinal);
    private static void TransferHireNativeLoad(HireNativeOwnerLoad owner,HireNativeDogLoad dog,string operation)
    {
        if(!owner.Loaded||!owner.ReadReturned||!owner.Hydrated)return;
        lock(HireColdGate)
        {
            if(HireRetiredNativeReceipts.Count<64||HireRetiredNativeReceipts.ContainsKey(operation))HireRetiredNativeReceipts[operation]=new HireNativeAuditReceipt(operation,owner,dog);
            HireNativeOwners.Remove(owner.Data);HireNativeDogs.Remove(dog.Dog);
            owner.Budget?.Dispose();owner.Budget=null;owner.Native=null;owner.NativeBag=null;owner.NativeBelt=null;owner.NativeBuffs=null;
            owner.BeltGrid=null;owner.BagGrid=null;owner.BeltRefs=null;owner.BagRefs=null;owner.Actor=null;owner.Data=null;owner.World=null;owner.Spawn=null;
        }
    }
}
