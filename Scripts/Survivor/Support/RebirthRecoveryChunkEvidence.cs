using System;

// Prefix coordinates and complete entity list only; not a world custody receipt.
public static class RebirthRecoveryChunkEvidence
{
    public static bool TryMatch(byte[] payload,uint version,int expectedX,int expectedY,int expectedZ,RebirthGearTransferState pending,Guid publicationId,string ownerKey,int backpackClassId,int itemClassId,byte staticSource,int[] nativeSpecialIds,int itemsStart,Func<int,int> itemKind,out int entityEnd)
    {
        entityEnd=0;
        if(!RebirthRecoveryEntitySection.TryLocate(payload,version,out int x,out int y,out int z,out int count,out int offset)||x!=expectedX||y!=expectedY||z!=expectedZ)return false;
        return RebirthRecoveryEntityEvidence.TryMatch(payload,offset,count,pending,publicationId,ownerKey,backpackClassId,itemClassId,staticSource,nativeSpecialIds,itemsStart,itemKind,out entityEnd);
    }
}