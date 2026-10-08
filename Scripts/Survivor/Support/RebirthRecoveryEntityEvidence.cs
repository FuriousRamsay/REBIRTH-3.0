using System;
using System.Collections.Generic;

// Complete declared entity section, not complete chunk/world persistence proof.
public static class RebirthRecoveryEntityEvidence
{
    public static bool TryMatch(byte[] payload,int offset,int count,RebirthGearTransferState pending,Guid publicationId,string ownerKey,int backpackClassId,int itemClassId,byte staticSource,int[] nativeSpecialIds,int itemsStart,Func<int,int> itemKind,out int endOffset)
    {
        endOffset=0;
        if(payload==null||offset<0||offset>payload.Length||count<0||count>4096||pending==null||backpackClassId==itemClassId||nativeSpecialIds==null||nativeSpecialIds.Length!=7||
            !pending.TryGetRecoveryAttempt(publicationId,out var attempt)||attempt.OriginalOwnerEntityId<=0)return false;
        foreach(int id in nativeSpecialIds)if(id==backpackClassId||id==itemClassId)return false;
        var entities=new HashSet<int>();int cursor=offset,matches=0;
        for(int i=0;i<count;i++)
        {
            if(cursor>payload.Length-9||payload[cursor]!=38)return false;
            int type=BitConverter.ToInt32(payload,cursor+1),entity=BitConverter.ToInt32(payload,cursor+5);
            if(!entities.Add(entity)||!RebirthRecoveryForeignRecord.TrySkip(payload,cursor,nativeSpecialIds,itemsStart,itemKind,out int next)||next<=cursor)return false;
            if(type==backpackClassId||type==itemClassId)
            {
                if(!RebirthRecoveryEntityRecord.TryRead(payload,cursor,type,out var record)||record.EndOffset!=next||
                    !RebirthRecoveryEntityMetadata.TryRead(payload,record,staticSource,type==backpackClassId,out var metadata))return false;
                if(metadata.Identity.PublicationId==publicationId)
                {
                    if(++matches!=1||!RebirthRecoveryPublicationEvidence.TryMatch(payload,cursor,pending,publicationId,ownerKey,attempt.OriginalOwnerEntityId,backpackClassId,itemClassId,staticSource,out int matchedEnd)||matchedEnd!=next)return false;
                }
            }
            cursor=next;
        }
        if(matches!=1)return false;endOffset=cursor;return true;
    }
}