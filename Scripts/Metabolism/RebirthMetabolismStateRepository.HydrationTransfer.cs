using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthMetabolismStateRepository
{
    // Only the actual private warm local grant and independently saved native
    // inventory leg admit this mutation. A receipt never recreates that grant.
    // Unknown publication retries save/readback only; no inventory compensation.
    internal static bool TrySettleLocalHydration(RebirthHydrationTransferGrant grant)
        =>QualifyLocalHydration(grant,false);
    internal static bool HasSavedLocalHydration(RebirthHydrationTransferGrant grant)
        =>QualifyLocalHydration(grant,true);
    private static bool QualifyLocalHydration(RebirthHydrationTransferGrant grant,bool observeOnly)
    {
        try
        {
            if(grant==null||!grant.Current()||!serverAuthority||
                !RebirthHydrationOwnerCheckpoint.HasSaved(grant.Player,grant.Original,RebirthGearOwnerReceipt.Applied)||
                !grant.Original.TryGetPlan(out var plan))return false;
            var original=grant.Original;var player=grant.Player;
            ItemStack after;
            if(plan.GearAfter.Count==0)after=ItemStack.Empty.Clone();
            else
            {
                if(plan.GearAfter.Count!=1||!RebirthNativeItemCodec.TryDecode(plan.GearAfter.ItemData,out var value)||
                    value==null||value.IsEmpty()||value.ItemClass==null)return false;
                after=new ItemStack(value,1);
            }
            int generation;string id,path;RebirthMetabolismState state;
            lock(Sync)
            {
                if(!grant.Current()||!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,player.world)||
                    !States.TryGetValue(id=GetStablePlayerId(player),out state))return false;
                generation=loadGeneration;path=scopePath;
                if(state.HydrationTransferReceipt?.Digest!=original.Digest)
                {
                    if(observeOnly)return false;
                    // Current grant independently qualified the exact Before.
                    // These two ordinary assignments have no native callbacks.
                    state.HydrationSlotItem=after;
                    state.HydrationTransferReceipt=original;
                    state.Touch();
                }
                if(!grant.Current())return false;
            }
            if(!observeOnly&&!TrySave("hydration-transfer-metabolism-leg",out _))return false;
            lock(Sync)
            {
                if(generation!=loadGeneration||path!=scopePath||!ScopeCurrentLocked()||!grant.Current()||
                    !States.TryGetValue(id,out var current)||!ReferenceEquals(current,state))return false;
                // Observe the actual final file, never a backup or cached dirty
                // flag. Both full receipt and native hydration item are from
                // this same strict, bounded repository decode.
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<1||stream.Length>MaximumStoreBytes)return false;
                    using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumStoreBytes}))
                    {
                        var saved=DecodeStore(XDocument.Load(reader));
                        if(!saved.TryGetValue(id,out var row)||row.HydrationTransferReceipt?.Digest!=original.Digest||
                            !RebirthHydrationTransferScope.MatchesState(original.Creation,row))return false;
                        var item=row.HydrationSlotItem;
                        int count=item==null||item.IsEmpty()?0:item.count;
                        string data=count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue);
                        if(count!=plan.GearAfter.Count||data!=plan.GearAfter.ItemData)return false;
                    }
                }
                return generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&grant.Current()&&
                    RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied);
            }
        }
        catch{return false;}
    }
}
