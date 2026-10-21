using System;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

public static partial class RebirthNpcInventoryTransactionService
{
    private static readonly object MetabolismRefusalIssuer=new object();
    internal sealed class MetabolismNotEntered
    {
        private readonly RebirthNpcMetabolismCustody.Original original;
        internal MetabolismNotEntered(RebirthNpcMetabolismCustody.Original value,object issuer){if(!ReferenceEquals(issuer,MetabolismRefusalIssuer))throw new InvalidOperationException();original=value;}
        internal bool IsFor(RebirthNpcMetabolismCustody.Original value)=>ReferenceEquals(original,value);
    }
    internal static bool ApplyMetabolismOriginal(RebirthNpcMetabolismCustody.Original original,string authority,out MetabolismNotEntered notEntered)
    {
        notEntered=null;
        if(!RebirthNpcMetabolismCustody.AllowsSourceMutation(original)||!RebirthNpcInventoryAuthorityService.Validate(authority,RebirthNpcInventoryAuthorityOperations.Mutate,original.Id)||
            !RebirthMetabolismStateRepository.NpcServingBeforeCurrent(original.Metabolism)||!RebirthNpcInventoryPersistenceStore.IsTransferLoaded){notEntered=new MetabolismNotEntered(original,MetabolismRefusalIssuer);return false;}
        lock(Sync)
        {
            var state=GetOrCreate(original.Id);var before=original.Before;
            if(state.Revision!=before.Revision||state.Revision==uint.MaxValue||state.AppliedTransactions.Contains(original.Operation)||state.Quantities.Count!=before.Quantities.Count||state.Reservations.Count!=before.Reservations.Count||
                state.Quantities.Any(pair=>!before.Quantities.TryGetValue(pair.Key,out int count)||count!=pair.Value)||state.Reservations.Any(pair=>!before.Reservations.TryGetValue(pair.Key,out int count)||count!=pair.Value)||
                (state.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)!=(before.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)||
                original.AfterNative==null||original.AfterNative.Owner!=original.Id.ToString()||original.AfterNative.Revision!=state.Revision+1||!original.AfterNative.FitsQuantities(original.AfterQuantities)){notEntered=new MetabolismNotEntered(original,MetabolismRefusalIssuer);return false;}
            if(!RebirthNpcMetabolismCustody.AllowsSourceMutation(original)){notEntered=new MetabolismNotEntered(original,MetabolismRefusalIssuer);return false;}
            RebirthNpcMetabolismCustody.ObserveSourceInvoked(original.Operation);
            generationEpoch=new object();state.Quantities.Clear();foreach(var pair in original.AfterQuantities)state.Quantities.Add(pair.Key,pair.Value);
            state.NativeStacks=original.AfterNative;state.Revision++;RememberTransaction(state,original.Operation);Interlocked.Increment(ref applied);generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();return true;
        }
    }
}
