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
    // Only populated after bootstrap has validated the receipt-to-vehicle mapping.
    // Unattributable bootstrap/publication failures still fence the whole index.
    private readonly HashSet<Guid> failedVehicles=new HashSet<Guid>();
    private readonly Dictionary<Guid,Guid> knownVehicles=new Dictionary<Guid,Guid>();
    private readonly HashSet<string> unresolvedCandidates=new HashSet<string>(StringComparer.Ordinal);
    private readonly Func<DateTime> utcNow;
    private DateTime retryAfter;
    private bool loaded,failed,disposed;
    private int publishing;
    internal RebirthCamperTransferPendingIndex(RebirthCamperTransferJournal journal,Func<DateTime> utcNow=null)
    {
        this.journal=journal??throw new ArgumentNullException(nameof(journal));
        this.utcNow=utcNow??(()=>DateTime.UtcNow);
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
            if(publishing<=0){FailGlobal();return;}
            publishing--;
            if(!success)
            {
                if(record==null||knownVehicles.TryGetValue(record.Operation,out var prior)&&prior!=record.Vehicle)FailGlobal();
                else {knownVehicles[record.Operation]=record.Vehicle;FailVehicle(record.Vehicle);}
                return;
            }
            if(loaded&&!failed)Track(record);
        }
    }
    private void Track(RebirthCamperTransferJournal.Record record)
    {
        if(knownVehicles.TryGetValue(record.Operation,out var known)&&known!=Guid.Empty&&known!=record.Vehicle)
        {FailGlobal();throw new InvalidDataException("Journal vehicle identity changed.");}
        knownVehicles[record.Operation]=record.Vehicle;
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
            Load();
            AssertVehicleAvailable(vehicle);
            try
            {
                if(!pending.TryGetValue(vehicle,out var operations))return null;
                if(operations.Count!=1)throw new InvalidDataException("Multiple pending transfers require reconciliation.");
                foreach(var operation in operations)
                {
                    var record=journal.Read(operation);
                    if(record==null||record.Vehicle!=vehicle||
                        record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain)
                    {FailGlobal();throw new InvalidDataException("Pending transfer changed outside journal publication.");}
                    return record;
                }
                throw new InvalidDataException("Pending transfer index inconsistent.");
            }
            catch{FailVehicle(vehicle);throw;}
        }
    }
    private void FailGlobal(){failed=true;retryAfter=utcNow().AddSeconds(30);}
    private void FailVehicle(Guid vehicle){failedVehicles.Add(vehicle);retryAfter=utcNow().AddSeconds(30);}
    private void AssertVehicleAvailable(Guid vehicle)
    {
        if(!failedVehicles.Contains(vehicle))return;
        if(publishing==0&&utcNow()>=retryAfter)Rebuild();
        if(failedVehicles.Contains(vehicle))throw new InvalidOperationException("Vehicle journal unavailable; retain custody.");
    }
    private void Load()
    {
        if(disposed||publishing!=0)throw new InvalidOperationException("Journal index unavailable; retain custody.");
        if(failed&&utcNow()<retryAfter)throw new InvalidOperationException("Journal index unavailable; retain custody.");
        if(loaded&&!failed)return;
        Rebuild();
    }
    private void Rebuild()
    {
        loaded=false;pending.Clear();applied.Clear();rejected.Clear();failedVehicles.Clear();
        try
        {
            foreach(var record in journal.ReadForIndex(knownVehicles,unresolvedCandidates,vehicle=>failedVehicles.Add(vehicle)))Track(record);
            failed=false;loaded=true;retryAfter=utcNow().AddSeconds(30);
        }
        catch
        {
            // No caller may observe a partial bootstrap. Known identities survive
            // only as conservative attribution evidence for the next bounded retry.
            pending.Clear();applied.Clear();rejected.Clear();failedVehicles.Clear();FailGlobal();throw;
        }
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
            AssertVehicleAvailable(vehicle);
            if(!terminal.TryGetValue(vehicle,out var owners)||!owners.TryGetValue(owner,out var receipts))yield break;
            operations=new Guid[receipts.Count];receipts.CopyTo(operations);
        }
        foreach(var operation in operations)
        {
            RebirthCamperTransferJournal.Record record;
            lock(gate)
            {
                Load();
                AssertVehicleAvailable(vehicle);
                try
                {
                    record=journal.Read(operation);
                    if(record==null||record.Vehicle!=vehicle||record.OwnerCreation!=owner||record.State!=phase)
                    {FailGlobal();throw new InvalidDataException("Applied receipt changed outside journal publication.");}
                }
                catch{FailVehicle(vehicle);throw;}
            }
            yield return record;
        }
    }
    public void Dispose()
    {
        lock(gate){disposed=true;pending.Clear();applied.Clear();rejected.Clear();failedVehicles.Clear();knownVehicles.Clear();unresolvedCandidates.Clear();}
        RebirthCamperTransferJournal.PublicationStarting-=Starting;
        RebirthCamperTransferJournal.PublicationFinished-=Finished;
    }
}
