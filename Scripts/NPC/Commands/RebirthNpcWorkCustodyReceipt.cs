using System;
using UnityEngine;
#nullable disable

// Tools-only proposed identity data. A constructed view/scope is NOT authority:
// producers validate reference equality against their privately published receipt.
public enum RebirthNpcWorkCustodyDisposition
{
    HeldOriginalCurrentOwner,
    PendingSavedOriginalScope,
    QuarantinedForeignScope,
    QuarantinedUnknownOrigin
}

public sealed class RebirthNpcWorkCustodyView
{
    public ulong LeaseId { get; }
    public bool OriginalWorldScopeKnown { get; }
    public string OriginalSaveFilePath { get; }
    public long OriginalLoadGeneration { get; }
    public RebirthNpcStableId OriginalStableOwner { get; }
    public bool OriginalStableOwnerKnown { get; }
    public bool OriginalNativeActorKnown { get; }
    public RebirthNpcWorkCustodyDisposition Disposition { get; }
    internal RebirthNpcWorkCustodyView(ulong id, RebirthNpcExecutionWorkSaveScope scope,
        RebirthNpcStableId owner, bool actorKnown, RebirthNpcWorkCustodyDisposition disposition)
    {
        LeaseId=id; OriginalWorldScopeKnown=scope!=null;
        OriginalSaveFilePath=scope==null?string.Empty:scope.SaveFilePath;
        OriginalLoadGeneration=scope==null?0:scope.LoadGeneration;
        OriginalStableOwner=owner; OriginalStableOwnerKnown=!owner.IsEmpty;
        OriginalNativeActorKnown=actorKnown; Disposition=disposition;
    }
}

public sealed class RebirthNpcExecutionWorkSaveScope
{
    public string SaveFilePath { get; }
    public long LoadGeneration { get; }
    internal object NativeWorld { get; }
    internal string ValidatedLoadSource { get; }
    internal RebirthNpcExecutionWorkSaveScope(string path, object world, long generation, string source)
    { SaveFilePath=path; NativeWorld=world; LoadGeneration=generation; ValidatedLoadSource=source; }
}

internal static class RebirthNpcExecutionWorkRecordCopy
{
    // Full actual v2 execution record fields, including exact original owner/targets.
    internal static RebirthNpcPersistedExecution Copy(RebirthNpcPersistedExecution source)
    {
        return new RebirthNpcPersistedExecution {
            EntityId=source.EntityId, StableId=source.StableId, LeaseId=source.LeaseId,
            CommandId=source.CommandId, SubjectEntityId=source.SubjectEntityId,
            IssuerId=source.IssuerId, CommandKind=source.CommandKind, Order=source.Order,
            TargetPosition=source.TargetPosition, HasTargetPosition=source.HasTargetPosition,
            GuardPosition=source.GuardPosition, HasGuardPosition=source.HasGuardPosition,
            StartedUtcTicks=source.StartedUtcTicks
        };
    }
}
