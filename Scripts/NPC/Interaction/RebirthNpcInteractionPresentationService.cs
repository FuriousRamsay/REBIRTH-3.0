using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcInteractionMenuEntry
{
    public RebirthNpcInteractionCommandKind Kind { get; internal set; }
    public string Id { get; internal set; }
    public string Label { get; internal set; }
    public bool Enabled { get; internal set; }
    public string DisabledReason { get; internal set; }
}

public sealed class RebirthNpcInteractionProjection
{
    public string SessionKey { get; internal set; }
    public string ActorId { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public string DisplayName { get; internal set; }
    public string ProfileId { get; internal set; }
    public string StatusText { get; internal set; }
    public uint RuntimeRevision { get; internal set; }
    public long ExpiresClockTicks { get; internal set; }
    public RebirthNpcInteractionPermissions Permissions { get; internal set; }
    public RebirthNpcInteractionMenuEntry[] Entries { get; internal set; }
}

public sealed class RebirthNpcDialogueProjection
{
    public string SpeakerName { get; internal set; }
    public string OpeningText { get; internal set; }
    public string StateText { get; internal set; }
    public string[] Topics { get; internal set; }
}

/// <summary>
/// Default authority-side permission provider. It projects profile capabilities and
/// ownership into interaction permissions; presentation code never grants authority.
/// </summary>
public sealed class RebirthNpcDefaultInteractionPermissionProvider : IRebirthNpcInteractionPermissionProvider
{
    public RebirthNpcInteractionPermissions Resolve(string actorId, RebirthNpcStableId npcId,
        RebirthNpcRuntimeState runtime)
    {
        if (runtime == null) return RebirthNpcInteractionPermissions.None;
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile))
            return RebirthNpcInteractionPermissions.None;

        RebirthNpcInteractionPermissions result = RebirthNpcInteractionPermissions.Inspect;
        if ((profile.Capabilities & RebirthNpcCapabilities.Dialogue) != 0)
            result |= RebirthNpcInteractionPermissions.Dialogue;

        // Runtime/persistent NPC ownership is authoritative. RebirthNpcHireService is a
        // session-level registry used by generic interaction flows and is not guaranteed to
        // survive a reconstruction/reload. Treat an authoritative owned runtime as hired so
        // a valid companion can never regress to an Available for Hire interaction surface.
        RebirthNpcHireRecord hire;
        bool registryHired = RebirthNpcHireService.TryGet(npcId, out hire);
        bool runtimeHired = runtime.OwnershipKind != RebirthNpcOwnershipKind.None &&
            !string.IsNullOrWhiteSpace(runtime.OwnerId);
        bool hired = runtimeHired || registryHired;
        bool owner = runtimeHired
            ? string.Equals(runtime.OwnerId, actorId, StringComparison.OrdinalIgnoreCase)
            : registryHired && string.Equals(hire.OwnerActorId, actorId, StringComparison.OrdinalIgnoreCase);

        if (!hired && (profile.Capabilities & RebirthNpcCapabilities.Dialogue) != 0 &&
            (profile.Capabilities & RebirthNpcCapabilities.Inventory) != 0)
            result |= RebirthNpcInteractionPermissions.Trade;

        if (!hired && (profile.Capabilities & (RebirthNpcCapabilities.Hireable |
            RebirthNpcCapabilities.Ownership)) != 0)
            result |= RebirthNpcInteractionPermissions.Hire;

        if (owner)
        {
            result |= RebirthNpcInteractionPermissions.Dismiss | RebirthNpcInteractionPermissions.Teach;
            if ((profile.Capabilities & RebirthNpcCapabilities.Inventory) != 0)
                result |= RebirthNpcInteractionPermissions.Inventory;
            if ((profile.Capabilities & RebirthNpcCapabilities.Equipment) != 0)
                result |= RebirthNpcInteractionPermissions.Equipment;
            if ((profile.Capabilities & RebirthNpcCapabilities.Orders) != 0)
                result |= RebirthNpcInteractionPermissions.Command;
            if ((profile.Capabilities & (RebirthNpcCapabilities.WorkRepair |
                RebirthNpcCapabilities.WorkMining)) != 0)
                result |= RebirthNpcInteractionPermissions.AssignWork;
        }
        if(!RebirthNpcWorkReleaseGate.Enabled) result &= ~RebirthNpcInteractionPermissions.AssignWork;
        return result;
    }
}

public static class RebirthNpcInteractionPresentationService
{
    private static readonly object Sync = new object();
    private static readonly RebirthNpcDefaultInteractionPermissionProvider DefaultPermissions =
        new RebirthNpcDefaultInteractionPermissionProvider();
    private static bool registered;
    private static long opened, projected, rejected, dialogueBuilt;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (registered) return;
            RebirthNpcInteractionPermissionService.Register(DefaultPermissions);
            RebirthNpcInteractionCommandRouter.Register(new InspectHandler());
            RebirthNpcInteractionCommandRouter.Register(new DialogueHandler());
            RebirthNpcInteractionCommandRouter.Register(new LessonPreviewHandler());
            RebirthNpcInteractionCommandRouter.Register(new LearnHandler());
            RebirthNpcInteractionMutationHandlers.EnsureRegistered();
            RebirthNpcTradeService.EnsureRegistered();
            RebirthNpcDialogueService.EnsureRegistered();
            registered = true;
        }
    }

    public static RebirthNpcInteractionResult Open(string actorId, RebirthNpcStableId npcId,
        out RebirthNpcInteractionProjection projection)
    {
        EnsureRegistered();
        projection = null;
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult result = RebirthNpcInteractionSessionService.Open(
            actorId, npcId, TimeSpan.FromSeconds(45), out context);
        if (result != RebirthNpcInteractionResult.Allowed)
        {
            Interlocked.Increment(ref rejected);
            return result;
        }
        Interlocked.Increment(ref opened);
        if (!TryBuild(context, out projection))
        {
            RebirthNpcInteractionSessionService.Close(context.SessionKey);
            Interlocked.Increment(ref rejected);
            return RebirthNpcInteractionResult.TargetUnavailable;
        }
        return RebirthNpcInteractionResult.Allowed;
    }

    public static bool TryRefresh(string sessionKey, out RebirthNpcInteractionProjection projection)
    {
        return TryRefresh(sessionKey, null, out projection);
    }

    public static bool TryRefresh(string sessionKey, string authenticatedActorId, out RebirthNpcInteractionProjection projection)
    {
        EnsureRegistered();
        projection = null;
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult result = RebirthNpcInteractionSessionService.Authorize(
            sessionKey, authenticatedActorId, RebirthNpcInteractionAction.Inspect, out context);
        return result == RebirthNpcInteractionResult.Allowed && TryBuild(context, out projection);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Interaction Presentation] registered=" + registered +
            " opened=" + Interlocked.Read(ref opened) +
            " projected=" + Interlocked.Read(ref projected) +
            " dialogue=" + Interlocked.Read(ref dialogueBuilt) +
            " rejected=" + Interlocked.Read(ref rejected);
    }

    private static bool TryBuild(RebirthNpcInteractionContext context,
        out RebirthNpcInteractionProjection projection)
    {
        projection = null;
        int entityId;
        RebirthNpcRuntimeState runtime;
        if (context == null || !RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime)) return false;

        EntityRebirthNPC entity = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
        string displayName = entity != null ? entity.EntityName : runtime.ProfileId;
        List<RebirthNpcInteractionMenuEntry> entries = new List<RebirthNpcInteractionMenuEntry>();
        Add(entries, context, RebirthNpcInteractionCommandKind.Inspect, "inspect", "Inspect",
            RebirthNpcInteractionPermissions.Inspect);
        Add(entries, context, RebirthNpcInteractionCommandKind.Dialogue, "dialogue", "Talk",
            RebirthNpcInteractionPermissions.Dialogue);
        Add(entries, context, RebirthNpcInteractionCommandKind.Trade, "trade", "Trade",
            RebirthNpcInteractionPermissions.Trade);
        Add(entries, context, RebirthNpcInteractionCommandKind.OpenInventory, "inventory", "Inventory",
            RebirthNpcInteractionPermissions.Inventory);
        Add(entries, context, RebirthNpcInteractionCommandKind.OpenEquipment, "equipment", "Equipment",
            RebirthNpcInteractionPermissions.Equipment);
        Add(entries, context, RebirthNpcInteractionCommandKind.IssueOrder, "orders", "Orders",
            RebirthNpcInteractionPermissions.Command);
        if(RebirthNpcWorkReleaseGate.Enabled) Add(entries, context, RebirthNpcInteractionCommandKind.AssignWork, "work", "Assign Work",
            RebirthNpcInteractionPermissions.AssignWork);
        Add(entries, context, RebirthNpcInteractionCommandKind.Teach, "teach", "Teach",
            RebirthNpcInteractionPermissions.Teach);
        Add(entries, context, RebirthNpcInteractionCommandKind.Hire, "hire", "Hire",
            RebirthNpcInteractionPermissions.Hire);
        Add(entries, context, RebirthNpcInteractionCommandKind.Dismiss, "dismiss", "Dismiss",
            RebirthNpcInteractionPermissions.Dismiss);

        if(RebirthTheorySpecialistRegistry.GetSubjects(runtime.ProfileId).Length>0)
        {
            Add(entries,context,RebirthNpcInteractionCommandKind.LessonPreview,"lessonpreview",
                Localization.Get("xuiRebirthNpcLessonPreview"),RebirthNpcInteractionPermissions.Dialogue);
            Add(entries,context,RebirthNpcInteractionCommandKind.Learn,"learn",
                Localization.Get("xuiRebirthNpcLearn"),RebirthNpcInteractionPermissions.Dialogue);
        }
        projection = new RebirthNpcInteractionProjection
        {
            SessionKey = context.SessionKey,
            ActorId = context.ActorId,
            NpcId = context.NpcId,
            DisplayName = displayName ?? string.Empty,
            ProfileId = runtime.ProfileId,
            StatusText = BuildStatus(runtime),
            RuntimeRevision = runtime.Revision,
            ExpiresClockTicks = context.ExpiresClockTicks,
            Permissions = context.Permissions,
            Entries = entries.ToArray()
        };
        Interlocked.Increment(ref projected);
        return true;
    }

    private static void Add(List<RebirthNpcInteractionMenuEntry> entries,
        RebirthNpcInteractionContext context, RebirthNpcInteractionCommandKind kind,
        string id, string label, RebirthNpcInteractionPermissions required)
    {
        bool enabled = (context.Permissions & required) == required ||
            (context.Permissions & RebirthNpcInteractionPermissions.Admin) != 0;
        entries.Add(new RebirthNpcInteractionMenuEntry
        {
            Kind = kind,
            Id = id,
            Label = label,
            Enabled = enabled,
            DisabledReason = enabled ? string.Empty : "Not available for this NPC or actor."
        });
    }

    private static string BuildStatus(RebirthNpcRuntimeState runtime)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(runtime.Presence).Append(" | ").Append(runtime.Order);
        if (runtime.OwnershipKind != RebirthNpcOwnershipKind.None)
            builder.Append(" | ").Append(runtime.OwnershipKind);
        return builder.ToString();
    }

    private sealed class InspectHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Inspect;
        public int Priority => 100;

        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            RebirthNpcInteractionProjection projection;
            if (!TryBuild(context, out projection))
                return Failed("NPC is no longer available.");
            return Accepted(SerializeProjection(projection));
        }
    }

    private sealed class DialogueHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Dialogue;
        public int Priority => 100;

        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            int entityId;
            RebirthNpcRuntimeState runtime;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) ||
                !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
                return Failed("NPC is no longer available.");
            EntityRebirthNPC entity = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
            RebirthNpcDialogueProjection dialogue = new RebirthNpcDialogueProjection
            {
                SpeakerName = entity != null ? entity.EntityName : runtime.ProfileId,
                OpeningText = "What do you need?",
                StateText = BuildStatus(runtime),
                Topics = BuildTopics(context.Permissions)
            };
            Interlocked.Increment(ref dialogueBuilt);
            return Accepted(SerializeDialogue(dialogue));
        }
    }

    private sealed class LearnHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind=>RebirthNpcInteractionCommandKind.Learn;
        public int Priority=>100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,RebirthNpcInteractionCommand command)
        {
            string reason;
            if(!RebirthTheorySpecialistLessonService.TryBegin(context,command.Payload,out reason))return Failed(reason);
            var response=Accepted(string.Empty);response.Detail=reason;return response;
        }
    }
    private sealed class LessonPreviewHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.LessonPreview;
        public int Priority => 100;
        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            int entityId; int page;
            if(context==null||!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out entityId))
                return Failed("NPC is no longer available.");
            if(!int.TryParse(command.Payload,System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,out page)||page<0)
                return Failed("Lesson preview page is invalid.");
            EntityPlayer student=RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
            string[] subjects;
            if(!RebirthTheorySpecialistResolver.TryResolveSubjects(student,entityId,out subjects))
                return Failed("No live specialist subjects are available.");
            int pages=subjects.Length; page%=pages;
            float transfer;double cooldown;string reason;
            bool eligible=RebirthTheoryProgressionService.TryPreviewNpcInstruction(student,entityId,subjects[page],
                out transfer,out cooldown,out reason);
            RebirthSkillDefinition definition;
            string name=RebirthSurvivorDefinitionRegistry.TryGetSkill(subjects[page],out definition)&&definition!=null
                ?Localization.Get(definition.NameKey):Localization.Get("xuiRebirthStudySubject");
            string summary=eligible
                ?string.Format(Localization.Get("xuiRebirthStudyPreviewReady"),name,
                    transfer.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),
                    RebirthTeachingService.SessionDurationSeconds.ToString("0",System.Globalization.CultureInfo.InvariantCulture),
                    Math.Ceiling(RebirthTheoryProgressionService.SpecialistInstructionCooldownSeconds/60d).ToString("0",System.Globalization.CultureInfo.InvariantCulture))
                :cooldown>0d?string.Format(Localization.Get("xuiRebirthStudyPreviewCooldown"),name,
                    Math.Ceiling(cooldown).ToString("0",System.Globalization.CultureInfo.InvariantCulture)):name+": "+reason;
            // Empty selection still displays the explanation, but cannot expose Learn.
            var response=Accepted((eligible?subjects[page]:string.Empty)+"\n"+summary);
            response.Detail=string.Format(Localization.Get("xuiRebirthStudyPreviewPage"),page+1,pages);
            return response;
        }
    }
    private static string[] BuildTopics(RebirthNpcInteractionPermissions permissions)
    {
        List<string> topics = new List<string>();
        topics.Add("status");
        if ((permissions & RebirthNpcInteractionPermissions.Trade) != 0) topics.Add("trade");
        if ((permissions & RebirthNpcInteractionPermissions.Hire) != 0) topics.Add("hire");
        if ((permissions & RebirthNpcInteractionPermissions.Command) != 0) topics.Add("orders");
        if (RebirthNpcWorkReleaseGate.Enabled && (permissions & RebirthNpcInteractionPermissions.AssignWork) != 0) topics.Add("work");
        if ((permissions & RebirthNpcInteractionPermissions.Dismiss) != 0) topics.Add("dismiss");
        return topics.ToArray();
    }

    private static string SerializeProjection(RebirthNpcInteractionProjection value)
    {
        StringBuilder b = new StringBuilder();
        b.Append("npc=").Append(value.NpcId).Append(";name=").Append(Escape(value.DisplayName))
            .Append(";profile=").Append(Escape(value.ProfileId)).Append(";revision=")
            .Append(value.RuntimeRevision).Append(";status=").Append(Escape(value.StatusText))
            .Append(";entries=");
        for (int i = 0; i < value.Entries.Length; i++)
        {
            if (i > 0) b.Append(',');
            b.Append(value.Entries[i].Id).Append(':').Append(value.Entries[i].Enabled ? '1' : '0');
        }
        return b.ToString();
    }

    private static string SerializeDialogue(RebirthNpcDialogueProjection value)
    {
        return "speaker=" + Escape(value.SpeakerName) + ";opening=" + Escape(value.OpeningText) +
            ";state=" + Escape(value.StateText) + ";topics=" + string.Join(",", value.Topics);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace(";", "\\;")
            .Replace(",", "\\,").Replace("=", "\\=");
    }

    private static RebirthNpcInteractionCommandResponse Accepted(string payload)
    {
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Accepted,
            Detail = "Presentation projection created.",
            ResponsePayload = payload ?? string.Empty
        };
    }

    private static RebirthNpcInteractionCommandResponse Failed(string detail)
    {
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Rejected,
            Detail = detail ?? string.Empty,
            ResponsePayload = string.Empty
        };
    }
}
