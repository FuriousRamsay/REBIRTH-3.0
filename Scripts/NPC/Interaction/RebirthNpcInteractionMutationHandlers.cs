using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

/// <summary>
/// Registers the concrete authoritative interaction handlers that back the
/// contextual presentation entries. Payloads are bounded semicolon-delimited
/// key/value pairs; all mutations remain inside existing authoritative services.
/// </summary>
public static class RebirthNpcInteractionMutationHandlers
{
    private static readonly object Sync = new object();
    private static bool registered;
    private static long accepted, rejected, readModels, mutations;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (registered) return;
            RebirthNpcInteractionCommandRouter.Register(new InventoryHandler());
            RebirthNpcInteractionCommandRouter.Register(new EquipmentHandler());
            RebirthNpcInteractionCommandRouter.Register(new HireHandler());
            RebirthNpcInteractionCommandRouter.Register(new DismissHandler());
            RebirthNpcInteractionCommandRouter.Register(new OrderHandler());
            RebirthNpcInteractionCommandRouter.Register(new TeachHandler());
            RebirthNpcInteractionCommandRouter.Register(new AssignWorkHandler());
            RebirthNpcInteractionCommandRouter.Register(new CancelWorkHandler());
            registered = true;
        }
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Interaction Mutations] registered=" + registered +
            " accepted=" + Interlocked.Read(ref accepted) +
            " rejected=" + Interlocked.Read(ref rejected) +
            " readModels=" + Interlocked.Read(ref readModels) +
            " mutations=" + Interlocked.Read(ref mutations);
    }

    private static RebirthNpcInteractionCommandResponse Accepted(string detail, string payload, bool mutation)
    {
        Interlocked.Increment(ref accepted);
        if (mutation) Interlocked.Increment(ref mutations);
        else Interlocked.Increment(ref readModels);
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Accepted,
            Detail = detail ?? string.Empty,
            ResponsePayload = payload ?? string.Empty
        };
    }

    private static RebirthNpcInteractionCommandResponse Rejected(string detail)
    {
        Interlocked.Increment(ref rejected);
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Rejected,
            Detail = detail ?? string.Empty,
            ResponsePayload = string.Empty
        };
    }

    private static string FriendlyDenied(RebirthNpcInteractionResult result)
    {
        switch(result)
        {
            case RebirthNpcInteractionResult.AlreadyHired:return Localization.Get("xuiRebirthNpcAlreadyHired");
            case RebirthNpcInteractionResult.NotHired:return Localization.Get("xuiRebirthNpcNotHired");
            case RebirthNpcInteractionResult.OwnershipConflict:
            case RebirthNpcInteractionResult.PermissionDenied:return Localization.Get("xuiRebirthNpcNotYourCompanion");
            case RebirthNpcInteractionResult.TargetUnavailable:return Localization.Get("xuiRebirthNpcTargetUnavailable");
            case RebirthNpcInteractionResult.SessionExpired:
            case RebirthNpcInteractionResult.RevisionConflict:return Localization.Get("xuiRebirthNpcReopenAction");
            default:return Localization.Get("xuiRebirthNpcActionUnavailable");
        }
    }

    private static Dictionary<string, string> Parse(string payload)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(payload)) return values;
        string[] parts = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 32) return values;
        for (int i = 0; i < parts.Length; i++)
        {
            int equals = parts[i].IndexOf('=');
            if (equals <= 0) continue;
            string key = parts[i].Substring(0, equals).Trim();
            string value = parts[i].Substring(equals + 1).Trim();
            if (key.Length > 0 && key.Length <= 64 && value.Length <= 512) values[key] = value;
        }
        return values;
    }

    private static bool TryGet(Dictionary<string, string> values, string key, out string value)
    {
        return values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }

    internal static EntityPlayer FindActorPlayer(string actorId)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.Players == null || world.Players.list == null || string.IsNullOrWhiteSpace(actorId)) return null;
        for (int i = 0; i < world.Players.list.Count; ++i)
        {
            EntityPlayer player = world.Players.list[i]; if (player == null) continue;
            PersistentPlayerData data = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if (data?.PrimaryId != null && string.Equals(data.PrimaryId.ToString(), actorId, StringComparison.OrdinalIgnoreCase)) return player;
        }
        return null;
    }

    private sealed class InventoryHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.OpenInventory;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId);
            List<string> keys = new List<string>(snapshot.Quantities.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder payload = new StringBuilder();
            payload.Append("generation=").Append(RebirthNpcRecoveryGeneration.RequireCurrent()).Append(";revision=").Append(snapshot.Revision).Append(";items=");
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) payload.Append('|');
                payload.Append(keys[i]).Append(':').Append(snapshot.Quantities[keys[i]]);
            }
            return Accepted(Localization.Get("xuiRebirthNpcInventoryReady"), payload.ToString(), false);
        }
    }

    private sealed class EquipmentHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.OpenEquipment;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            RebirthNpcEquipmentSnapshot snapshot = RebirthNpcEquipmentService.GetSnapshot(context.NpcId);
            List<RebirthNpcEquipmentSlot> slots = new List<RebirthNpcEquipmentSlot>(snapshot.Slots.Keys);
            slots.Sort((a, b) => ((byte)a).CompareTo((byte)b));
            StringBuilder payload = new StringBuilder();
            payload.Append("revision=").Append(snapshot.Revision).Append(";slots=");
            for (int i = 0; i < slots.Count; i++)
            {
                if (i > 0) payload.Append('|');
                payload.Append(slots[i]).Append(':').Append(snapshot.Slots[slots[i]]);
            }
            return Accepted(Localization.Get("xuiRebirthNpcEquipmentReady"), payload.ToString(), false);
        }
    }

    private sealed class HireHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Hire;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            return RebirthSurvivorHirePayment.Handle(context, command);
        }
    }

    private sealed class DismissHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Dismiss;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            var values = Parse(command.Payload);
            if(RebirthNpcWorkNativeTransferOriginal.HasPending(context.NpcId))
                return Rejected(Localization.Get("xuiRbNpcEquipmentBusy"));
            if(RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out var nativeId)&&
                    GameManager.Instance?.World?.GetEntity(nativeId) is EntityRebirthHumanoidNPC human&&
                    human.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode)==1f)
                {
                    for(int slot=0;slot<10+(human.bag?.SlotCount??0);slot++)
                        if(!RebirthHumanEquipment.Get(human,slot).IsEmpty())return Rejected(Localization.Get("xuiRbNpcDismissEmptyEquipment"));
                    // Lockpicks use a separate logical slot; saved cargo still
                    // starts at 10. Check even an older non-lockpick stack that
                    // occupied the backing native toolbelt cell before upgrade.
                    if(!RebirthHumanEquipment.Get(human,RebirthHumanEquipment.LockpicksSlot).IsEmpty())
                        return Rejected(Localization.Get("xuiRbNpcDismissEmptyEquipment"));
                    // The reading hand is a native toolbelt cell outside the
                    // public equipment range; it must also be empty on dismiss.
                    if(human.inventory?.SlotCount>RebirthHumanEquipment.ReadingToolbeltIndex&&
                        human.inventory.GetItem(RebirthHumanEquipment.ReadingToolbeltIndex) is ItemStack readingHand&&
                        !readingHand.IsEmpty())
                        return Rejected(Localization.Get("xuiRbNpcDismissEmptyEquipment"));
                }
            if(values.TryGetValue("operation",out var operation)&&operation=="prepare")
            {
                // Return legacy belongings through native-owner logistics.
                // Keep ownership until its final authoritative empty check.
                RebirthNpcEquipmentService.ReleaseAll(context.NpcId);
                var inventory=RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId);
                var rows=new List<string>();
                foreach(var pair in inventory.Quantities)
                {
                    if(pair.Value<=0)continue;
                    var item=ItemClass.GetItem(pair.Key,false);
                    if(item==null||item.IsEmpty())return Rejected("A carried item cannot be resolved; dismissal retains your companion and belongings.");
                    rows.Add("-1|"+item.type+"|"+pair.Value+"|"+pair.Key);
                }
                if(rows.Count>0)return Accepted("Returning carried items before dismissal. Make room in your backpack.",
                    "return="+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("\n",rows))),false);
            }
            if(RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId).Quantities.Count>0)
                return Rejected("Make room in your backpack and unlock any retained items, then select Dismiss again. Your companion remains hired until all belongings have been returned.");
            RebirthNpcHireRecord record;
            RebirthNpcInteractionResult result = RebirthNpcHireService.Dismiss(command.SessionKey, out record);
            if (result != RebirthNpcInteractionResult.Allowed)
                return Rejected(FriendlyDenied(result));
            return Accepted(Localization.Get("xuiRebirthNpcDismissed"),
                "formerOwner=" + record.OwnerActorId + ";revision=" + record.Revision, true);
        }
    }

    private sealed class OrderHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.IssueOrder;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            Dictionary<string, string> values = Parse(command.Payload);
            string orderText;
            RebirthNpcOrderState order;
            if (!TryGet(values, "order", out orderText) ||
                !Enum.TryParse(orderText, true, out order) || !Enum.IsDefined(typeof(RebirthNpcOrderState), order))
                return Rejected("Choose a valid order.");

            if(RebirthNpcWorkReleaseGate.Hold(order))return Rejected(RebirthNpcWorkReleaseGate.Detail);

            int entityId;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) ||
                GameManager.Instance == null || GameManager.Instance.World == null)
                return Rejected("NPC unavailable.");
            EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null) return Rejected("NPC unavailable.");

            Vector3 position = Vector3.zero;
            bool hasPosition = TryVector(values, out position);
            if (order == RebirthNpcOrderState.Guard && !hasPosition)
                return Rejected("Choose a place to guard.");
            EntityPlayer actor = FindActorPlayer(context.ActorId);
            RebirthNpcCommandKind kind;
            if (actor == null || !Enum.TryParse(order.ToString(), out kind))
                return Rejected("Your character or the requested command is unavailable.");
            var result = RebirthNpcCommandGateway.SubmitAuthenticated(npc, context.ActorId,
                actor.entityId, kind, position, hasPosition, context.RuntimeRevision);
            if (!result.Accepted) return Rejected("Order rejected: " + result.Error + ".");
            return Accepted(Localization.Get("xuiRebirthNpcOrderAccepted"), "order=" + order +
                ";command=" + result.CommandId + (hasPosition ? ";position=" + Format(position) : string.Empty), true);
        }
    }

    private sealed class TeachHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Teach;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            EntityPlayer instructor = FindActorPlayer(context != null ? context.ActorId : string.Empty);
            if (instructor == null) return Rejected("Your character is unavailable.");
            Dictionary<string, string> values = Parse(command != null ? command.Payload : string.Empty);
            string preferredSkill = string.Empty;
            TryGet(values, "skill", out preferredSkill);
            string detail;
            string direction, reverseText;
            // "direction=npc-to-player" (or "reverse=1") makes the hired human the instructor.
            bool reverse = TryGet(values, "direction", out direction) &&
                string.Equals(direction, "npc-to-player", StringComparison.OrdinalIgnoreCase);
            if (!reverse && TryGet(values, "reverse", out reverseText))
                reverse = reverseText == "1" || string.Equals(reverseText, "true", StringComparison.OrdinalIgnoreCase);
            if (reverse)
            {
                if (!RebirthHumanTeaching.Begin(instructor, context.NpcId, preferredSkill, true, out detail))
                    return Rejected("Teaching rejected: " + detail);
            }
            else if (!RebirthTeachingService.TryTeachOwnedNpc(instructor, context.NpcId.ToString(), preferredSkill, out detail))
                return Rejected("Teaching rejected: " + detail);
            return Accepted(detail, "npc=" + context.NpcId + ";subject=" + (preferredSkill ?? string.Empty), true);
        }
    }

    private sealed class AssignWorkHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.AssignWork;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            Dictionary<string, string> values = Parse(command.Payload);
            string definition, target;
            if (!TryGet(values, "definition", out definition) || !TryGet(values, "target", out target))
                return Rejected("Work payload requires definition and target values.");
            int priority = 0;
            string priorityText;
            if (TryGet(values, "priority", out priorityText) && !int.TryParse(priorityText,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out priority))
                return Rejected("Work priority is invalid.");
            Vector3 position;
            bool hasPosition = TryVector(values, out position);
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult result = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = command.SessionKey,
                    DefinitionId = definition,
                    TargetKey = target,
                    TargetPosition = position,
                    HasTargetPosition = hasPosition,
                    Priority = priority,
                    ExpectedRuntimeRevision = command.ExpectedRevision
                }, out assignment, out error);
            if (result != RebirthNpcWorkAssignmentResult.Succeeded)
                return Rejected("Work assignment rejected: " + result + ". " + error);
            return Accepted(Localization.Get("xuiRebirthNpcWorkAssigned"), "assignment=" + assignment.AssignmentId +
                ";revision=" + assignment.Revision + ";status=" + assignment.Status, true);
        }
    }

    private sealed class CancelWorkHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.CancelWork;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            Dictionary<string, string> values = Parse(command.Payload);
            string idText, revisionText;
            ulong assignmentId;
            uint assignmentRevision = 0U;
            if (!TryGet(values, "assignment", out idText) ||
                !ulong.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out assignmentId))
                return Rejected("Cancellation payload requires a valid assignment id.");
            if (TryGet(values, "revision", out revisionText) &&
                !uint.TryParse(revisionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out assignmentRevision))
                return Rejected("Assignment revision is invalid.");
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult result = RebirthNpcWorkAssignmentService.Cancel(
                command.SessionKey, assignmentId, assignmentRevision, out assignment, out error);
            if (result != RebirthNpcWorkAssignmentResult.Succeeded)
                return Rejected("Work cancellation rejected: " + result + ". " + error);
            return Accepted(Localization.Get("xuiRebirthNpcWorkCancelled"), "assignment=" + assignment.AssignmentId +
                ";revision=" + assignment.Revision + ";status=" + assignment.Status, true);
        }
    }

    private static bool TryVector(Dictionary<string, string> values, out Vector3 position)
    {
        position = Vector3.zero;
        string xText, yText, zText;
        float x, y, z;
        if (!TryGet(values, "x", out xText) || !TryGet(values, "y", out yText) ||
            !TryGet(values, "z", out zText)) return false;
        if (!float.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
            !float.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
            !float.TryParse(zText, NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
        position = new Vector3(x, y, z);
        return true;
    }

    private static string Format(Vector3 value)
    {
        return value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.y.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.z.ToString("R", CultureInfo.InvariantCulture);
    }
}
