using System;
using System.Collections.Generic;
using System.Threading;

#nullable disable

public enum RebirthNpcInteractionCommandKind : byte
{
    Inspect = 0,
    Dialogue = 1,
    Trade = 2,
    OpenInventory = 3,
    OpenEquipment = 4,
    IssueOrder = 5,
    Hire = 6,
    Dismiss = 7,
    AssignWork = 8,
    CancelWork = 9,
    Teach = 10,
    LessonPreview = 11,
    Learn = 12
}

public enum RebirthNpcInteractionCommandStatus : byte
{
    Accepted = 0,
    Rejected = 1,
    Duplicate = 2,
    HandlerUnavailable = 3,
    HandlerFailed = 4
}

public sealed class RebirthNpcInteractionCommand
{
    public string RequestId { get; set; }
    public string SessionKey { get; set; }
    public RebirthNpcInteractionCommandKind Kind { get; set; }
    public string Payload { get; set; }
    public uint ExpectedRevision { get; set; }
}

public sealed class RebirthNpcInteractionCommandResponse
{
    public RebirthNpcInteractionCommandStatus Status { get; internal set; }
    public RebirthNpcInteractionResult Authorization { get; internal set; }
    public string Detail { get; internal set; }
    public string ResponsePayload { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public string ActorId { get; internal set; }

    internal RebirthNpcInteractionCommandResponse Clone()
    {
        return (RebirthNpcInteractionCommandResponse)MemberwiseClone();
    }
}

public interface IRebirthNpcInteractionCommandHandler
{
    RebirthNpcInteractionCommandKind Kind { get; }
    int Priority { get; }
    RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
        RebirthNpcInteractionCommand command);
}

public static class RebirthNpcInteractionCommandRouter
{
    private const int MaxReplayRecords = 1024;
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcInteractionCommandKind,
        List<IRebirthNpcInteractionCommandHandler>> Handlers =
        new Dictionary<RebirthNpcInteractionCommandKind, List<IRebirthNpcInteractionCommandHandler>>();
    private static readonly Dictionary<string, RebirthNpcInteractionCommandResponse> Replay =
        new Dictionary<string, RebirthNpcInteractionCommandResponse>(StringComparer.Ordinal);
    private static readonly Queue<string> ReplayOrder = new Queue<string>();
    private static readonly Dictionary<string, string> ReplayFingerprint =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static long submitted, accepted, rejected, duplicate, unavailable, failed;

    public static void Register(IRebirthNpcInteractionCommandHandler handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        lock (Sync)
        {
            List<IRebirthNpcInteractionCommandHandler> list;
            if (!Handlers.TryGetValue(handler.Kind, out list))
            {
                list = new List<IRebirthNpcInteractionCommandHandler>();
                Handlers[handler.Kind] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public static void Unregister(IRebirthNpcInteractionCommandHandler handler)
    {
        if (handler == null) return;
        lock (Sync)
        {
            List<IRebirthNpcInteractionCommandHandler> list;
            if (!Handlers.TryGetValue(handler.Kind, out list)) return;
            list.Remove(handler);
            if (list.Count == 0) Handlers.Remove(handler.Kind);
        }
    }

    public static RebirthNpcInteractionCommandResponse Dispatch(RebirthNpcInteractionCommand command)
    {
        return Dispatch(command, null);
    }

    public static RebirthNpcInteractionCommandResponse Dispatch(RebirthNpcInteractionCommand command, string authenticatedActorId)
    {
        Interlocked.Increment(ref submitted);
        string actorKey = (authenticatedActorId ?? string.Empty).Trim();
        if (command == null || string.IsNullOrWhiteSpace(command.RequestId) ||
            string.IsNullOrWhiteSpace(command.SessionKey))
            return Record(command, actorKey, Reject(RebirthNpcInteractionCommandStatus.Rejected,
                RebirthNpcInteractionResult.InvalidRequest, "Request id and session key are required."));

        string replayKey = actorKey + "|" + command.RequestId.Trim();
        string fingerprint = BuildFingerprint(command);

        // Authorization always precedes replay. A request id is idempotent only for the
        // same authenticated actor/session/payload.
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult authorization = RebirthNpcInteractionSessionService.Authorize(
            command.SessionKey, actorKey, MapAction(command.Kind), out context);
        if (authorization != RebirthNpcInteractionResult.Allowed)
            return Record(command, actorKey, Reject(RebirthNpcInteractionCommandStatus.Rejected,
                authorization, "Interaction session authorization failed."));

        lock (Sync)
        {
            RebirthNpcInteractionCommandResponse prior;
            string priorFingerprint;
            if (Replay.TryGetValue(replayKey, out prior))
            {
                if (!ReplayFingerprint.TryGetValue(replayKey, out priorFingerprint) ||
                    !string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal))
                    return Reject(RebirthNpcInteractionCommandStatus.Rejected, RebirthNpcInteractionResult.InvalidRequest,
                        "Request id was already used for a different interaction payload.");
                Interlocked.Increment(ref duplicate);
                RebirthNpcInteractionCommandResponse clone = prior.Clone();
                clone.Status = RebirthNpcInteractionCommandStatus.Duplicate;
                return clone;
            }
        }
        if (command.ExpectedRevision != 0U && command.ExpectedRevision != context.RuntimeRevision)
            return Record(command, actorKey, Reject(RebirthNpcInteractionCommandStatus.Rejected,
                RebirthNpcInteractionResult.RevisionConflict,
                "The NPC runtime revision changed after the interaction was opened."));

        IRebirthNpcInteractionCommandHandler[] snapshot;
        lock (Sync)
        {
            List<IRebirthNpcInteractionCommandHandler> list;
            snapshot = Handlers.TryGetValue(command.Kind, out list)
                ? list.ToArray() : new IRebirthNpcInteractionCommandHandler[0];
        }
        if (snapshot.Length == 0)
        {
            Interlocked.Increment(ref unavailable);
            return Record(command, actorKey, new RebirthNpcInteractionCommandResponse
            {
                Status = RebirthNpcInteractionCommandStatus.HandlerUnavailable,
                Authorization = RebirthNpcInteractionResult.Allowed,
                Detail = "No authoritative handler is registered for " + command.Kind + ".",
                NpcId = context.NpcId,
                ActorId = context.ActorId
            });
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                RebirthNpcInteractionCommandResponse response = snapshot[i].Handle(context, command);
                if (response == null) continue;
                response.NpcId = context.NpcId;
                response.ActorId = context.ActorId;
                response.Authorization = RebirthNpcInteractionResult.Allowed;
                if (response.Status == RebirthNpcInteractionCommandStatus.Accepted)
                    Interlocked.Increment(ref accepted);
                else Interlocked.Increment(ref rejected);
                return Record(command, actorKey, response);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                Log.Warning("[REBIRTH NPC Interaction] Handler failed kind=" + command.Kind +
                    " handler=" + snapshot[i].GetType().Name + ": " + ex.Message);
            }
        }

        return Record(command, actorKey, new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.HandlerFailed,
            Authorization = RebirthNpcInteractionResult.Allowed,
            Detail = "All registered handlers declined or failed the request.",
            NpcId = context.NpcId,
            ActorId = context.ActorId
        });
    }

    public static void Reset()
    {
        lock (Sync)
        {
            Handlers.Clear();
            Replay.Clear();
            ReplayFingerprint.Clear();
            ReplayOrder.Clear();
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            int handlerCount = 0;
            foreach (KeyValuePair<RebirthNpcInteractionCommandKind,
                List<IRebirthNpcInteractionCommandHandler>> pair in Handlers)
                handlerCount += pair.Value.Count;
            return "[REBIRTH NPC Interaction Router] handlers=" + handlerCount +
                " replay=" + Replay.Count + " submitted=" + Interlocked.Read(ref submitted) +
                " accepted=" + Interlocked.Read(ref accepted) +
                " rejected=" + Interlocked.Read(ref rejected) +
                " duplicate=" + Interlocked.Read(ref duplicate) +
                " unavailable=" + Interlocked.Read(ref unavailable) +
                " failed=" + Interlocked.Read(ref failed);
        }
    }

    private static RebirthNpcInteractionCommandResponse Record(RebirthNpcInteractionCommand command, string actorId,
        RebirthNpcInteractionCommandResponse response)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.RequestId)) return response;
        string id = (actorId ?? string.Empty).Trim() + "|" + command.RequestId.Trim();
        lock (Sync)
        {
            if (!Replay.ContainsKey(id)) ReplayOrder.Enqueue(id);
            Replay[id] = response.Clone();
            ReplayFingerprint[id] = BuildFingerprint(command);
            while (ReplayOrder.Count > MaxReplayRecords)
            {
                string oldest = ReplayOrder.Dequeue();
                Replay.Remove(oldest);
                ReplayFingerprint.Remove(oldest);
            }
        }
        return response;
    }

    private static string BuildFingerprint(RebirthNpcInteractionCommand command)
    {
        if (command == null) return string.Empty;
        return (command.SessionKey ?? string.Empty).Trim() + "|" + (byte)command.Kind + "|" +
            command.ExpectedRevision + "|" + (command.Payload ?? string.Empty);
    }

    private static RebirthNpcInteractionCommandResponse Reject(
        RebirthNpcInteractionCommandStatus status, RebirthNpcInteractionResult authorization,
        string detail)
    {
        Interlocked.Increment(ref rejected);
        return new RebirthNpcInteractionCommandResponse
        {
            Status = status,
            Authorization = authorization,
            Detail = detail ?? string.Empty
        };
    }

    private static RebirthNpcInteractionAction MapAction(RebirthNpcInteractionCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcInteractionCommandKind.Inspect: return RebirthNpcInteractionAction.Inspect;
            case RebirthNpcInteractionCommandKind.Dialogue:
            case RebirthNpcInteractionCommandKind.LessonPreview:
            case RebirthNpcInteractionCommandKind.Learn: return RebirthNpcInteractionAction.Dialogue;
            case RebirthNpcInteractionCommandKind.Trade: return RebirthNpcInteractionAction.Trade;
            case RebirthNpcInteractionCommandKind.OpenInventory: return RebirthNpcInteractionAction.Inventory;
            case RebirthNpcInteractionCommandKind.OpenEquipment: return RebirthNpcInteractionAction.Equipment;
            case RebirthNpcInteractionCommandKind.Hire: return RebirthNpcInteractionAction.Hire;
            case RebirthNpcInteractionCommandKind.Dismiss: return RebirthNpcInteractionAction.Dismiss;
            case RebirthNpcInteractionCommandKind.AssignWork:
            case RebirthNpcInteractionCommandKind.CancelWork: return RebirthNpcInteractionAction.AssignWork;
            case RebirthNpcInteractionCommandKind.Teach: return RebirthNpcInteractionAction.Teach;
            default: return RebirthNpcInteractionAction.Command;
        }
    }
}
