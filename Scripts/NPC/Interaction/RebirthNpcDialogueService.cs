using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcDialogueTopicDefinition
{
    public string ProfileId { get; set; }
    public string TopicId { get; set; }
    public string Label { get; set; }
    public string ResponseText { get; set; }
    public RebirthNpcInteractionPermissions RequiredPermission { get; set; }
    public int Priority { get; set; }
}

public sealed class RebirthNpcDialogueTurn
{
    public int Index { get; internal set; }
    public string TopicId { get; internal set; }
    public string Text { get; internal set; }
    public long CreatedUtcTicks { get; internal set; }
}

public sealed class RebirthNpcConversationState
{
    public Guid ConversationId { get; internal set; }
    public string SessionKey { get; internal set; }
    public string ActorId { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public string ProfileId { get; internal set; }
    public long ExpiresUtcTicks { get; internal set; }
    public int Revision { get; internal set; }
    public List<RebirthNpcDialogueTurn> Turns { get; internal set; }
}

public static class RebirthNpcDialogueService
{
    private const int MaxConversations = 512;
    private const int MaxTurns = 16;
    private const int MaxFields = 16;
    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, RebirthNpcConversationState> Conversations =
        new Dictionary<Guid, RebirthNpcConversationState>();
    private static readonly RebirthNpcFairRoundRobin<Guid> ConversationOrder =
        new RebirthNpcFairRoundRobin<Guid>();
    private static readonly Dictionary<string, List<RebirthNpcDialogueTopicDefinition>> Topics =
        new Dictionary<string, List<RebirthNpcDialogueTopicDefinition>>(StringComparer.OrdinalIgnoreCase);
    private static bool registered;
    private static long opened, selected, ended, rejected, expired, socialEvents;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (registered) return;
            RegisterDefaults();
            RebirthNpcInteractionCommandRouter.Register(new DialogueCommandHandler());
            registered = true;
        }
    }

    public static void RegisterTopic(RebirthNpcDialogueTopicDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.TopicId) ||
            string.IsNullOrWhiteSpace(definition.ResponseText))
            throw new ArgumentException("Dialogue topic id and response are required.");
        string profile = NormalizeProfile(definition.ProfileId);
        lock (Sync)
        {
            List<RebirthNpcDialogueTopicDefinition> list;
            if (!Topics.TryGetValue(profile, out list))
                Topics[profile] = list = new List<RebirthNpcDialogueTopicDefinition>();
            list.RemoveAll(x => string.Equals(x.TopicId, definition.TopicId,
                StringComparison.OrdinalIgnoreCase));
            list.Add(definition);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public static string GetReport()
    {
        int conversations, topics = 0;
        lock (Sync)
        {
            CleanupExpired(DateTime.UtcNow.Ticks);
            conversations = Conversations.Count;
            foreach (List<RebirthNpcDialogueTopicDefinition> list in Topics.Values) topics += list.Count;
        }
        return "[REBIRTH NPC Dialogue] registered=" + registered +
            " topics=" + topics + " conversations=" + conversations +
            " opened=" + Interlocked.Read(ref opened) +
            " selected=" + Interlocked.Read(ref selected) +
            " ended=" + Interlocked.Read(ref ended) +
            " rejected=" + Interlocked.Read(ref rejected) +
            " expired=" + Interlocked.Read(ref expired) +
            " socialEvents=" + Interlocked.Read(ref socialEvents);
    }

    private static RebirthNpcInteractionCommandResponse HandleOpen(RebirthNpcInteractionContext context)
    {
        int entityId;
        RebirthNpcRuntimeState runtime;
        if (!TryRuntime(context.NpcId, out entityId, out runtime)) return Reject("NPC is no longer available.");
        EntityRebirthNPC entity = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcConversationState state = new RebirthNpcConversationState
        {
            ConversationId = Guid.NewGuid(), SessionKey = context.SessionKey,
            ActorId = context.ActorId, NpcId = context.NpcId, ProfileId = runtime.ProfileId,
            ExpiresUtcTicks = now + TimeSpan.FromMinutes(2).Ticks, Revision = 1,
            Turns = new List<RebirthNpcDialogueTurn>()
        };
        lock (Sync)
        {
            CleanupExpired(now);
            Conversations[state.ConversationId] = state;
            ConversationOrder.Add(state.ConversationId);
            TrimConversations();
        }
        Interlocked.Increment(ref opened);
        return Accept(Serialize(state, entity != null ? entity.EntityName : runtime.ProfileId,
            OpeningText(context.NpcId, context.ActorId), context.Permissions));
    }

    private static RebirthNpcInteractionCommandResponse HandleSelect(RebirthNpcInteractionContext context,
        Dictionary<string, string> fields)
    {
        Guid id;
        string rawId, topic;
        if (!fields.TryGetValue("conversation", out rawId) || !Guid.TryParse(rawId, out id) ||
            !fields.TryGetValue("topic", out topic) || string.IsNullOrWhiteSpace(topic))
            return Reject("Conversation id and topic are required.");
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcConversationState state;
        RebirthNpcDialogueTopicDefinition definition;
        lock (Sync)
        {
            CleanupExpired(now);
            if (!Conversations.TryGetValue(id, out state) ||
                !string.Equals(state.SessionKey, context.SessionKey, StringComparison.Ordinal) ||
                !string.Equals(state.ActorId, context.ActorId, StringComparison.OrdinalIgnoreCase) ||
                state.NpcId != context.NpcId)
                return Reject("Conversation is unavailable or does not belong to this interaction.");
            definition = ResolveTopic(state.ProfileId, topic.Trim(), context.Permissions);
            if (definition == null) return Reject("Dialogue topic is unavailable.");
            state.Revision++;
            state.ExpiresUtcTicks = now + TimeSpan.FromMinutes(2).Ticks;
            state.Turns.Add(new RebirthNpcDialogueTurn
            {
                Index = state.Revision - 1, TopicId = definition.TopicId,
                Text = definition.ResponseText, CreatedUtcTicks = now
            });
            while (state.Turns.Count > MaxTurns) state.Turns.RemoveAt(0);
        }
        Guid eventId = DeterministicGuid(id, state.Revision);
        if (RebirthNpcSocialService.ApplyEvent(eventId, context.NpcId, context.ActorId,
            RebirthNpcSocialEventKind.Conversation, 0.08f, 1f))
            Interlocked.Increment(ref socialEvents);
        Interlocked.Increment(ref selected);
        return Accept("conversation=" + id.ToString("N") + ";revision=" + state.Revision +
            ";topic=" + Escape(definition.TopicId) + ";text=" + Escape(definition.ResponseText) +
            ";disposition=" + Escape(Disposition(context.NpcId, context.ActorId)));
    }

    private static RebirthNpcInteractionCommandResponse HandleEnd(RebirthNpcInteractionContext context,
        Dictionary<string, string> fields)
    {
        Guid id; string raw;
        if (!fields.TryGetValue("conversation", out raw) || !Guid.TryParse(raw, out id))
            return Reject("Conversation id is required.");
        lock (Sync)
        {
            RebirthNpcConversationState state;
            if (!Conversations.TryGetValue(id, out state) || state.NpcId != context.NpcId ||
                !string.Equals(state.ActorId, context.ActorId, StringComparison.OrdinalIgnoreCase))
                return Reject("Conversation is unavailable.");
            Conversations.Remove(id);
            ConversationOrder.Remove(id);
        }
        Interlocked.Increment(ref ended);
        return Accept("conversation=" + id.ToString("N") + ";ended=1");
    }

    private static string Serialize(RebirthNpcConversationState state, string speaker, string opening,
        RebirthNpcInteractionPermissions permissions)
    {
        List<RebirthNpcDialogueTopicDefinition> available = AvailableTopics(state.ProfileId, permissions);
        StringBuilder b = new StringBuilder();
        b.Append("conversation=").Append(state.ConversationId.ToString("N"))
            .Append(";revision=").Append(state.Revision)
            .Append(";speaker=").Append(Escape(speaker))
            .Append(";opening=").Append(Escape(opening))
            .Append(";disposition=").Append(Escape(Disposition(state.NpcId, state.ActorId)))
            .Append(";topics=");
        for (int i = 0; i < available.Count; i++)
        {
            if (i > 0) b.Append(',');
            b.Append(Escape(available[i].TopicId)).Append(':').Append(Escape(available[i].Label));
        }
        return b.ToString();
    }

    private static List<RebirthNpcDialogueTopicDefinition> AvailableTopics(string profile,
        RebirthNpcInteractionPermissions permissions)
    {
        List<RebirthNpcDialogueTopicDefinition> result = new List<RebirthNpcDialogueTopicDefinition>();
        lock (Sync)
        {
            AddAvailable(result, "*", permissions);
            if (!string.IsNullOrWhiteSpace(profile)) AddAvailable(result, profile, permissions);
        }
        return result;
    }

    private static void AddAvailable(List<RebirthNpcDialogueTopicDefinition> result, string profile,
        RebirthNpcInteractionPermissions permissions)
    {
        List<RebirthNpcDialogueTopicDefinition> list;
        if (!Topics.TryGetValue(profile, out list)) return;
        for (int i = 0; i < list.Count; i++)
        {
            RebirthNpcDialogueTopicDefinition d = list[i];
            if ((d.RequiredPermission == RebirthNpcInteractionPermissions.None ||
                (permissions & d.RequiredPermission) == d.RequiredPermission ||
                (permissions & RebirthNpcInteractionPermissions.Admin) != 0) &&
                !result.Exists(x => string.Equals(x.TopicId, d.TopicId, StringComparison.OrdinalIgnoreCase)))
                result.Add(d);
        }
    }

    private static RebirthNpcDialogueTopicDefinition ResolveTopic(string profile, string topic,
        RebirthNpcInteractionPermissions permissions)
    {
        List<RebirthNpcDialogueTopicDefinition> list = AvailableTopics(profile, permissions);
        return list.Find(x => string.Equals(x.TopicId, topic, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryRuntime(RebirthNpcStableId npcId, out int entityId,
        out RebirthNpcRuntimeState runtime)
    {
        runtime = null;
        return RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) &&
            RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime);
    }

    private static string OpeningText(RebirthNpcStableId npc, string actor)
    {
        RebirthNpcRelationship r = RebirthNpcSocialService.GetRelationship(npc, actor);
        if (r == null || r.Familiarity < 0.15f) return "What do you need?";
        if (r.Hostility >= 0.65f) return "Make this quick.";
        if (r.Trust >= 0.7f) return "Good to see you. What do you need?";
        if (r.Suspicion >= 0.55f) return "I'm listening.";
        return "What can I do for you?";
    }

    private static string Disposition(RebirthNpcStableId npc, string actor)
    {
        RebirthNpcRelationship r = RebirthNpcSocialService.GetRelationship(npc, actor);
        if (r == null) return "unfamiliar";
        if (r.Hostility >= 0.65f) return "hostile";
        if (r.Fear >= 0.6f) return "fearful";
        if (r.Suspicion >= 0.55f) return "wary";
        if (r.Trust >= 0.7f) return "trusted";
        if (r.Familiarity >= 0.35f) return "familiar";
        return "neutral";
    }

    private static Dictionary<string, string> Parse(string payload)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(payload)) return result;
        List<string> parts = RebirthNpcInteractionPayloadCodec.SplitEscaped(payload, ';');
        int count = Math.Min(parts.Count, MaxFields);
        for (int i = 0; i < count; i++)
        {
            int equals = RebirthNpcInteractionPayloadCodec.IndexOfUnescaped(parts[i], '=');
            if (equals <= 0) continue;
            string key = RebirthNpcInteractionPayloadCodec.UnescapeBackslash(parts[i].Substring(0, equals)).Trim();
            string value = RebirthNpcInteractionPayloadCodec.UnescapeBackslash(parts[i].Substring(equals + 1)).Trim();
            if (key.Length > 32 || value.Length > 256) continue;
            result[key] = value;
        }
        return result;
    }

    private static void CleanupExpired(long now)
    {
        if (Conversations.Count == 0) return;
        List<Guid> remove = null;
        foreach (KeyValuePair<Guid, RebirthNpcConversationState> pair in Conversations)
            if (pair.Value.ExpiresUtcTicks <= now)
            {
                if (remove == null) remove = new List<Guid>();
                remove.Add(pair.Key);
            }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++)
        {
            Conversations.Remove(remove[i]);
            ConversationOrder.Remove(remove[i]);
        }
        Interlocked.Add(ref expired, remove.Count);
    }

    private static void TrimConversations()
    {
        while (Conversations.Count > MaxConversations && ConversationOrder.Count > 0)
        {
            Guid oldest;
            if(!ConversationOrder.TryTake(out oldest))break;
            Conversations.Remove(oldest);
        }
    }

    public static void ResetForWorldChange()
    {
        lock(Sync)
        {
            Conversations.Clear();
            ConversationOrder.Clear();
        }
    }

    private static string NormalizeProfile(string profile)
    {
        return string.IsNullOrWhiteSpace(profile) ? "*" : profile.Trim();
    }

    private static void RegisterDefaults()
    {
        RegisterDefault("status", "Status", "I'm managing. Ask if you need something specific.", RebirthNpcInteractionPermissions.Dialogue, 100);
        RegisterDefault("trade", "Trade", "Take a look at what I have available.", RebirthNpcInteractionPermissions.Trade, 90);
        RegisterDefault("hire", "Hire", "We can discuss terms if you have work for me.", RebirthNpcInteractionPermissions.Hire, 80);
        RegisterDefault("orders", "Orders", "Tell me where you need me and what order to follow.", RebirthNpcInteractionPermissions.Command, 70);
        RegisterDefault("work", "Work", "Give me an assignment and a valid target.", RebirthNpcInteractionPermissions.AssignWork, 60);
        RegisterDefault("dismiss", "Dismiss", "Understood. Confirm when you want me released.", RebirthNpcInteractionPermissions.Dismiss, 50);
    }

    private static void RegisterDefault(string id, string label, string text,
        RebirthNpcInteractionPermissions permission, int priority)
    {
        List<RebirthNpcDialogueTopicDefinition> list;
        if (!Topics.TryGetValue("*", out list)) Topics["*"] = list = new List<RebirthNpcDialogueTopicDefinition>();
        list.Add(new RebirthNpcDialogueTopicDefinition
        {
            ProfileId = "*", TopicId = id, Label = label, ResponseText = text,
            RequiredPermission = permission, Priority = priority
        });
    }

    private static Guid DeterministicGuid(Guid source, int revision)
    {
        byte[] bytes = source.ToByteArray();
        byte[] salt = BitConverter.GetBytes(revision);
        for (int i = 0; i < salt.Length; i++) bytes[(i + 5) % 16] ^= salt[i];
        return new Guid(bytes);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace(";", "\\;")
            .Replace(",", "\\,").Replace("=", "\\=").Replace(":", "\\:");
    }

    private static RebirthNpcInteractionCommandResponse Accept(string payload)
    {
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Accepted,
            Detail = "Dialogue operation completed.", ResponsePayload = payload ?? string.Empty
        };
    }

    private static RebirthNpcInteractionCommandResponse Reject(string detail)
    {
        Interlocked.Increment(ref rejected);
        return new RebirthNpcInteractionCommandResponse
        {
            Status = RebirthNpcInteractionCommandStatus.Rejected,
            Detail = detail ?? string.Empty, ResponsePayload = string.Empty
        };
    }

    private sealed class DialogueCommandHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Dialogue;
        public int Priority => 200;

        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            Dictionary<string, string> fields = Parse(command.Payload);
            string operation;
            if (!fields.TryGetValue("operation", out operation) || string.IsNullOrWhiteSpace(operation))
                operation = "open";
            if (string.Equals(operation, "open", StringComparison.OrdinalIgnoreCase)) return HandleOpen(context);
            if (string.Equals(operation, "select", StringComparison.OrdinalIgnoreCase)) return HandleSelect(context, fields);
            if (string.Equals(operation, "end", StringComparison.OrdinalIgnoreCase)) return HandleEnd(context, fields);
            return Reject("Unsupported dialogue operation.");
        }
    }
}
