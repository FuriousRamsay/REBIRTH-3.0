using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// Constructed only by the completed original batch owner, after final-file witnesses.
internal sealed class RebirthPoiResetBatchCompletion
{
    public readonly object Scope;
    public readonly Guid World,Transaction;
    public readonly long GlobalRevision;
    public readonly IReadOnlyCollection<string> PoiKeys;
    public readonly IReadOnlyCollection<RebirthPoiIdentity> Identities;
    public readonly bool IsQuestBatch;
    internal RebirthPoiResetBatchCompletion(object scope,Guid world,Guid transaction,long revision,IEnumerable<RebirthPoiResetBatchEntry> entries)
    {Scope=scope;World=world;Transaction=transaction;GlobalRevision=revision;var list=entries.ToArray();PoiKeys=new ReadOnlyCollection<string>(list.Select(e=>e.Identity.Key).OrderBy(k=>k,StringComparer.Ordinal).ToArray());Identities=new ReadOnlyCollection<RebirthPoiIdentity>(list.Select(e=>e.Identity).ToArray());IsQuestBatch=list.All(e=>e.Plan.Caller==RebirthPoiResetCaller.Quest);}
}
