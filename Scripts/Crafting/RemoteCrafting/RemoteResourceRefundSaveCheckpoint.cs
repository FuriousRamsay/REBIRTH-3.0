using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Registration checkpoint only. Producer authenticates world/owner and exact removal
// entitlement; successful persistence does not authorize a native item credit.
internal static class RemoteResourceRefundSaveCheckpoint
{
    private static readonly object Gate=new object();
    private static readonly HashSet<RebirthWorldCharacterRecord> Active=new HashSet<RebirthWorldCharacterRecord>();
    internal static bool TryPersist(RebirthWorldCharacterRecord record,RebirthStablePlayerIdentity identity,
        string authenticatedWorldKey,RemoteResourceRefundRecord refund,Func<bool> authenticatedCurrent,out bool pending)
    {
        pending=false;
        if(record==null||identity==null||refund==null||authenticatedCurrent==null)return false;
        lock(Gate)
        {
            if(!Active.Add(record))return false;
            try
            {
                Func<bool> current=()=>authenticatedCurrent()&&RebirthWorldCharacterRepository.IsServerAuthority&&
                    RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&record.IsComplete&&record.Support!=null&&
                    record.StablePlayerKey==identity.StorageKey&&record.StablePlayerId==identity.CanonicalId&&
                    RebirthSurvivorRequestScope.Matches(refund.CreationId,record.Origin?.CreationId);
                if(!current()||!RemoteResourceRefundRecord.TryRead(refund.ToXml(),authenticatedWorldKey,identity.StorageKey,
                    record.Origin.CreationId,out var validated))return false;
                var expected=record.Support.RemoteResourceRefundJournalImage;
                RemoteResourceRefundJournal journal;
                if(expected==null)
                {if(!RemoteResourceRefundJournal.TryCreate(authenticatedWorldKey,identity.StorageKey,record.Origin.CreationId,out journal))return false;}
                else if(!RemoteResourceRefundJournal.TryRead(expected,authenticatedWorldKey,identity.StorageKey,record.Origin.CreationId,out journal))return false;
                Func<XElement,bool> persist=image=>
                {
                    if(!current()||!ReferenceEquals(expected,record.Support.RemoteResourceRefundJournalImage))return false;
                    var staged=new XElement(image);expected=new XElement(staged);record.Support.RemoteResourceRefundJournalImage=expected;
                    // Never revert an uncertain persisted candidate to an older image.
                    // It remains pending/dirty for final-file reconciliation and retry.
                    RebirthWorldCharacterService.MarkDirty(record,"remote-resource-refund-registration");
                    if(!current()||!ReferenceEquals(expected,record.Support.RemoteResourceRefundJournalImage)||!XNode.DeepEquals(staged,expected))return false;
                    try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-resource-refund-registration");}
                    catch{}
                    return current()&&ReferenceEquals(expected,record.Support.RemoteResourceRefundJournalImage)&&
                        XNode.DeepEquals(staged,expected)&&RebirthWorldCharacterRepository.HasSavedRemoteResourceRefunds(identity,validated.CreationId,staged)&&current()&&XNode.DeepEquals(staged,expected);
                };
                if(!journal.TryRegister(validated,persist,current))return false;
                if(!current()||!ReferenceEquals(expected,record.Support.RemoteResourceRefundJournalImage))return false;
                // An exact retry may be retained in memory after a failed write, so
                // journal idempotency alone cannot establish native final persistence.
                if(!RebirthWorldCharacterRepository.HasSavedRemoteResourceRefunds(identity,validated.CreationId,expected)&&
                    !persist(journal.ToXml()))return false;
                if(!current()||!ReferenceEquals(expected,record.Support.RemoteResourceRefundJournalImage))return false;
                pending=Array.Exists(journal.Pending(),r=>r.SessionEpoch==validated.SessionEpoch&&r.RequestId==validated.RequestId);
                return true;
            }
            catch{return false;}
            finally{Active.Remove(record);}
        }
    }
}