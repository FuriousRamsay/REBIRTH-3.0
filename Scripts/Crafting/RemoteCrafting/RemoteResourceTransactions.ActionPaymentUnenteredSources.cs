using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    // Only the positive target-before-saved head may enter this route. Its
    // complete causal history precedes paying and every native financial intent.
    // The actual same-life owner and native target still qualify admission;
    // today's full source must retain its original identity and entire Before.
    private static ActionLeg TryRestoreUnenteredActionSource(ActionTargetNativeOriginal target,ActionHostOwnerLoad owner,XElement row,
        ItemStack[] before,ItemStack[] after,bool[] locks,int[] slots,ItemStack[] paid,List<IRemoteResourceSource> available)
    {
        if(!target.BeforeFirstGrant||available==null||owner.Actor.Died!=(int)ReadRemoteActionOriginal(target.Original).Attribute("deaths"))return null;
        string id=(string)row.Attribute("id");var kind=(RemoteResourceSourceKind)(int)row.Attribute("kind");
        var matches=available.Where(value=>value.StableId==id&&value.Kind==kind).ToArray();if(matches.Length!=1)return null;
        var source=matches[0];
        if(!RemoteResourceEligibility.CanUse(source,owner.Actor,true,false,out _)||source.Revision!=(int)row.Attribute("revision")||
            !SameActionItems(source.Slots,before)||!ActionLocks(source,before.Length).SequenceEqual(locks)||RebirthRemoteResourceSourceCustody.BlocksSource(source))return null;
        object endpoint;ItemStackGrid grid=null;RebirthRemoteResourceNativeBinding binding=null;
        if(source is RemoteNpcResourceSource npc)
        {
            endpoint=npc.NativeActor;
            if(npc.NativeIdentity.ToString()!=(string)row.Attribute("incarnation")||ActionBlocksNpc(npc.NativeIdentity)||RebirthNpcMetabolismCustody.Blocks(npc.NativeIdentity))return null;
        }
        else
        {
            if(!RebirthRemoteResourceNativeBinding.TryResolve(target.World,id,kind,(string)row.Attribute("incarnation"),out binding)||!binding.Current()||!binding.IsAuthorized(owner.Actor,out _))return null;
            endpoint=binding.Endpoint;grid=binding.Grid;
        }
        if(endpoint==null||ActionBlocksNative(endpoint)||grid!=null&&ActionBlocksNative(grid))return null;
        var leg=new ActionLeg(id,source,before,after,locks,slots,paid,endpoint,grid,binding,false,false,source.Revision,
            Guid.ParseExact((string)row.Attribute("debit"),"N"),Guid.ParseExact((string)row.Attribute("return"),"N"))
            {State=ActionLegState.Before,BeforeSaved=true};
        if(leg.Npc!=null)
        {
            if(!leg.Npc.IsDogStorage)
            {
                leg.NpcBefore=RebirthNpcInventoryTransactionService.GetSnapshot(leg.Npc.NativeIdentity);
                if(!XNode.DeepEquals(ActionNpcSnapshot(leg.NpcBefore),row.Element("logicalInventory")))return null;
                leg.NpcAfter=ActionExpectedNpc(leg,true,false);
            }
            var native=row.Element("nativeBody");if(native==null||(string)native.Attribute("stable")!=leg.Npc.NativeIdentity.ToString()||(int?)native.Attribute("class")!=leg.Npc.NativeActor.entityClass)return null;
            leg.NpcBodyBefore=ActionNativeBase64(native,2*1024*1024);
            using(var body=new ActionNpcBodyStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(body);leg.Npc.NativeActor.Write(writer,StreamModeWrite.Persistency);writer.Flush();
                if(!leg.Npc.NativeActor.NativeIdentityReadable||!body.ToArray().SequenceEqual(leg.NpcBodyBefore))return null;
            }
        }
        return leg;
    }
}
