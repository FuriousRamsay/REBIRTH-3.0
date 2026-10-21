using System;
using System.Collections.Generic;
using System.IO;

// World-scoped metadata only. Receipts remain the authority for recovery.
internal sealed class RebirthCamperTransferPendingIndex : IDisposable
{
    private readonly RebirthCamperTransferJournal journal;
    private readonly Dictionary<Guid,HashSet<Guid>> pending=new Dictionary<Guid,HashSet<Guid>>();
    private readonly Dictionary<Guid,Dictionary<string,HashSet<Guid>>> applied=new Dictionary<Guid,Dictionary<string,HashSet<Guid>>>();
    private readonly Dictionary<Guid,Dictionary<string,HashSet<Guid>>> rejected=new Dictionary<Guid,Dictionary<string,HashSet<Guid>>>();
    private readonly object gate=new object();
    private bool loaded,failed,disposed;
    private int publishing;
    internal RebirthCamperTransferPendingIndex(RebirthCamperTransferJournal journal)
    {
        this.journal=journal??throw new ArgumentNullException(nameof(journal));
        RebirthCamperTransferJournal.PublicationStarting+=Starting;
        RebirthCamperTransferJournal.PublicationFinished+=Finished;
    }
    private bool Matches(string directory)=>string.Equals(directory,journal.DirectoryPath,StringComparison.OrdinalIgnoreCase);
    private void Starting(string directory)
    {if(Matches(directory))lock(gate){if(!disposed)publishing++;}}
    private void Finished(string directory,RebirthCamperTransferJournal.Record record,bool success)
    {
        if(!Matches(directory))return;
        lock(gate)
        {
            if(disposed)return;
            if(publishing<=0){failed=true;return;}
            publishing--;
            if(!success){failed=true;return;}
            if(loaded&&!failed)Track(record);
        }
    }
    private void Track(RebirthCamperTransferJournal.Record record)
    {
        if(record.State==RebirthCamperTransferJournal.Phase.Applied||record.State==RebirthCamperTransferJournal.Phase.Rejected)
        {
            var terminal=record.State==RebirthCamperTransferJournal.Phase.Applied?applied:rejected;
            if(!terminal.TryGetValue(record.Vehicle,out var owners))terminal.Add(record.Vehicle,owners=new Dictionary<string,HashSet<Guid>>(StringComparer.Ordinal));
            if(!owners.TryGetValue(record.OwnerCreation,out var receipts))owners.Add(record.OwnerCreation,receipts=new HashSet<Guid>());
            receipts.Add(record.Operation);
        }
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain)
        {
            if(!pending.TryGetValue(record.Vehicle,out var operations))pending.Add(record.Vehicle,operations=new HashSet<Guid>());
            operations.Add(record.Operation);
        }
        else if(pending.TryGetValue(record.Vehicle,out var operations))
        {operations.Remove(record.Operation);if(operations.Count==0)pending.Remove(record.Vehicle);}
    }
    internal RebirthCamperTransferJournal.Record FindPending(Guid vehicle)
    {
        lock(gate)
        {
            if(disposed||failed||publishing!=0)throw new InvalidOperationException("Journal index unavailable; retain custody.");
            try
            {
                Load();
                if(!pending.TryGetValue(vehicle,out var operations))return null;
                if(operations.Count!=1)throw new InvalidDataException("Multiple pending transfers require reconciliation.");
                foreach(var operation in operations)
                {
                    var record=journal.Read(operation);
                    if(record==null||record.Vehicle!=vehicle||
                        record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain)
                        throw new InvalidDataException("Pending transfer changed outside journal publication.");
                    return record;
                }
                throw new InvalidDataException("Pending transfer index inconsistent.");
            }
            catch{failed=true;throw;}
        }
    }
    private void Load()
    {
        if(disposed||failed||publishing!=0)throw new InvalidOperationException("Journal index unavailable; retain custody.");
        if(loaded)return;
        try{foreach(var record in journal.ReadForIndex())Track(record);loaded=true;}
        catch{failed=true;throw;}
    }
    internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid vehicle,string owner)
        =>FindTerminal(vehicle,owner,applied,RebirthCamperTransferJournal.Phase.Applied);
    internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid vehicle,string owner)
        =>FindTerminal(vehicle,owner,rejected,RebirthCamperTransferJournal.Phase.Rejected);
    private IEnumerable<RebirthCamperTransferJournal.Record> FindTerminal(Guid vehicle,string owner,Dictionary<Guid,Dictionary<string,HashSet<Guid>>> terminal,RebirthCamperTransferJournal.Phase phase)
    {
        Guid[] operations;
        lock(gate)
        {
            Load();
            if(!terminal.TryGetValue(vehicle,out var owners)||!owners.TryGetValue(owner,out var receipts))yield break;
            operations=new Guid[receipts.Count];receipts.CopyTo(operations);
        }
        foreach(var operation in operations)
        {
            RebirthCamperTransferJournal.Record record;
            lock(gate)
            {
                Load();
                try
                {
                    record=journal.Read(operation);
                    if(record==null||record.Vehicle!=vehicle||record.OwnerCreation!=owner||record.State!=phase)
                        throw new InvalidDataException("Applied receipt changed outside journal publication.");
                }
                catch{failed=true;throw;}
            }
            yield return record;
        }
    }
    public void Dispose()
    {
        lock(gate){disposed=true;pending.Clear();applied.Clear();rejected.Clear();}
        RebirthCamperTransferJournal.PublicationStarting-=Starting;
        RebirthCamperTransferJournal.PublicationFinished-=Finished;
    }
}
