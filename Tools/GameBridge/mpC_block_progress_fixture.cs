using System;
using System.Linq;

// Authored only, NOT RUN. Supply an actual installed native action/data and
// independent captured progress vectors on a disposable world. No host
// permission, native owner receipt, callback or save witness is doubled here.
internal static class RemoteBlockProgressChecks
{
    internal static void Run(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target,
        string exactOriginal,bool expectedAuthoredReference)
    {
        string previous=RebirthRemoteResourceBlockProgress.Capture(action,data,target);
        if(!RebirthRemoteResourceBlockProgress.TryRead(previous,out var backup))throw new Exception("backup native progress");
        try
        {
            if(!RebirthRemoteResourceBlockProgress.TryRead(exactOriginal,out var frozen)||frozen.SameRepairList!=expectedAuthoredReference||
                !frozen.RestoreNative(action,data,target)||RebirthRemoteResourceBlockProgress.Capture(action,data,target)!=exactOriginal)
                throw new Exception("native progress/list branch roundtrip");
            var block=data.invData.world.GetBlock(target);var authored=block.Block.RepairItems;
            if(block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block))authored=block.Block.RepairItemsMeshDamage;
            if(ReferenceEquals(data.lastRepairItems,authored)!=expectedAuthoredReference&&authored!=null)
                throw new Exception("authored list pointer decision changed");
            if(frozen.Fractions!=null&&ReferenceEquals(frozen.Fractions,data.lastRepairItemsPercents))throw new Exception("mutable fraction alias");
            byte[] bytes=Convert.FromBase64String(exactOriginal);
            if(RebirthRemoteResourceBlockProgress.TryRead(Convert.ToBase64String(bytes.Concat(new byte[]{0}).ToArray()),out _))
                throw new Exception("trailing progress bytes accepted");
        }
        finally{if(!backup.RestoreNative(action,data,target))throw new Exception("fixture native progress restore");}
    }
    // Required vectors: null prior list; equal-by-value detached list; actual
    // authored normal/mesh list reference; fractional remaining costs; prior
    // target/type reset; upgrade threshold and started flag. Full execution
    // vectors must additionally refuse native2/Uncertain/captured candidates,
    // changed tool/action/index/hit/target/life, modal/range/foreign hold and
    // missing host permission; interrupt after callback and prove no replay.
}
