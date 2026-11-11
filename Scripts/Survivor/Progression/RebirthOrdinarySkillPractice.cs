using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Owning-player gains are calculated at the native action. The checkpoint moves
// only accumulated results into the existing authoritative Survivor record.
internal static class RebirthOrdinarySkillPractice
{
    internal const double CheckpointSeconds = 60;
    // One shared envelope for ordinary practice. Stock simultaneous movement
    // routes are below 1 point/active second even with learning bonuses.
    private const double MaximumGainPerActiveSecond = 1;
    private const double GainGraceSeconds = 3;
    private const double MaximumBankedGain = (2 * CheckpointSeconds + GainGraceSeconds) * MaximumGainPerActiveSecond;
    private const float MaximumAttributeEquipmentFactor = 2f;

    internal readonly struct Gain
    {
        internal readonly double Skill;
        internal readonly double Attribute;

        internal Gain(double skill, double attribute)
        {
            Skill = skill;
            Attribute = attribute;
        }
    }

    private sealed class Peer
    {
        internal World World;
        internal string Creation;
        internal double LastActive;
        internal double AvailableGain;
        internal uint Sequence;
    }

    private static readonly ConditionalWeakTable<ClientInfo, Peer> Peers = new ConditionalWeakTable<ClientInfo, Peer>();
    private static Dictionary<string, Gain> pending = new Dictionary<string, Gain>(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, Gain> inFlight = new Dictionary<string, Gain>(StringComparer.OrdinalIgnoreCase);
    private static World ownerWorld;
    private static INetConnection ownerConnection;
    private static string creation;
    private static uint sequence;
    private static uint inFlightSequence;
    private static double sendAt;

    internal static bool IsReady(EntityPlayerLocal player) => TryBind(player, out _, out _);

    private static bool TryBind(EntityPlayerLocal player, out RebirthSurvivorOwnerStateSnapshot state, out float elapsed)
    {
        state = null;
        elapsed = 0;
        var world = player?.world;
        if (world == null || !world.IsRemote() || !ReferenceEquals(world.GetPrimaryPlayer(), player)) return false;

        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var connections = manager?.GetConnectionToServer();
        var transport = connections != null && connections.Length > 0 ? connections[0] : null;
        if (!ReferenceEquals(ownerWorld, world) || !ReferenceEquals(ownerConnection, transport))
        {
            ResetClient();
            ownerWorld = world;
            ownerConnection = transport;
        }
        if (manager?.IsClient != true || transport == null || transport.IsDisconnected() ||
            !RebirthSurvivorClientState.TryReadOrdinaryProgressBase(player, out state, out elapsed)) return false;
        if (!string.Equals(creation, state.CreationId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Clear();
            inFlight.Clear();
            sequence = inFlightSequence = 0;
            creation = state.CreationId;
        }
        if (inFlightSequence != 0 && state.OrdinaryPracticeSequence >= inFlightSequence)
        {
            inFlight.Clear();
            inFlightSequence = 0;
        }
        return true;
    }

    internal static bool TryAward(EntityPlayerLocal player, RebirthSkillTrainingEvidence evidence,
        out RebirthSkillTrainingComputation computation, out float appliedSkill, out float appliedAttribute)
    {
        computation = null;
        appliedSkill = appliedAttribute = 0;
        if (!ThreadManager.IsMainThread() || !TryBind(player, out var state, out float elapsed) ||
            !RebirthSurvivorDefinitionRegistry.TryGetSkill(evidence.SkillId, out var definition) ||
            !TryGetSkill(state, definition, out float value, out float progress, out _) ||
            value >= definition.Max ||
            !RebirthSkillTrainingCalculator.TryCalculate(evidence, value + progress, out computation, ownerLocalPractice: true)) return false;

        float learning = RebirthArmorBonusSpecials.SkillGainFactor(player, definition.Id);
        foreach (var lesson in state.OrdinaryPracticeLessons)
            if (string.Equals(lesson.SkillId, definition.Id, StringComparison.OrdinalIgnoreCase) &&
                lesson.RemainingActiveSeconds > elapsed)
            {
                learning *= lesson.Multiplier;
                break;
            }
        float raw = computation.RawBeforeLearningModifiers *
            RebirthTraitSupportService.GetProjectedSkillGainMultiplier(state, value, elapsed) *
            RebirthTraitGameplayModifierService.GetSkillGainMultiplier(state.TraitIds, definition.Id, value) * learning;
        if (!RebirthSkillAwardService.TryAddSkillProgress(definition, value, progress, raw, out float next, out float fraction))
            return false;
        appliedSkill = ProgressDifference(value, progress, next, fraction);
        if (appliedSkill <= 0) return false;

        string attributeId = definition.PrimaryAttributeId;
        if (!string.IsNullOrEmpty(attributeId))
        {
            var attributeDefinition = RebirthAttributeProgressionService.FindDefinition(attributeId);
            var attribute = state.Attributes.Find(row => string.Equals(row.Id, attributeId, StringComparison.OrdinalIgnoreCase));
            if (attributeDefinition == null || attribute == null)
            {
                appliedSkill = 0;
                return false;
            }
            float current = ProjectAttribute(state, attribute, attributeDefinition);
            float rawAttribute = appliedSkill * RebirthProgressionRuntimeConfig.SkillProgressToAttribute *
                RebirthArmorBonusSpecials.AttributeGainFactor(player);
            float after = RebirthAttributeProgressionService.ProjectTraining(attributeDefinition, current, attribute.Potential, rawAttribute);
            appliedAttribute = after - current;
            if (!Finite(appliedAttribute) || appliedAttribute < 0)
            {
                appliedSkill = appliedAttribute = 0;
                return false;
            }
        }

        if (pending.Count == 0) sendAt = Time.realtimeSinceStartupAsDouble + CheckpointSeconds;
        pending.TryGetValue(definition.Id, out var previous);
        pending[definition.Id] = new Gain(previous.Skill + appliedSkill, previous.Attribute + appliedAttribute);
        computation.ExpectedFinalGain = appliedSkill;
        RebirthSurvivorClientState.MarkOrdinaryProgressChanged();
        RebirthDebug.Log("survivor", $"[REBIRTH Practice] Local {definition.Id} skill={appliedSkill:F6} attribute={appliedAttribute:F6}");
        return true;
    }

    private static Gain TotalGain(RebirthSurvivorOwnerStateSnapshot state, string skillId)
    {
        pending.TryGetValue(skillId, out var gain);
        if (inFlightSequence == 0 || state.OrdinaryPracticeSequence >= inFlightSequence ||
            !inFlight.TryGetValue(skillId, out var sent)) return gain;
        return new Gain(gain.Skill + sent.Skill, gain.Attribute + sent.Attribute);
    }

    private static float ProgressDifference(float value, float progress, float next, float fraction) =>
        (next - value) + (fraction - progress);

    private static bool TryGetSkill(RebirthSurvivorOwnerStateSnapshot state, RebirthSkillDefinition definition,
        out float value, out float progress, out float credited)
    {
        value = progress = credited = 0;
        var skill = state.Skills.Find(row => string.Equals(row.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
        if (skill == null) return false;
        return TryProjectSkill(skill, definition, TotalGain(state, definition.Id), out value, out progress, out credited);
    }

    private static bool TryProjectSkill(RebirthSurvivorOwnerSkillSnapshot skill, RebirthSkillDefinition definition,
        Gain gain, out float value, out float progress, out float credited)
    {
        credited = 0;
        if (gain.Skill == 0)
        {
            value = skill.Value;
            progress = skill.Progress;
            return true;
        }
        if (!RebirthSkillAwardService.TryAddSkillProgress(definition, skill.Value, skill.Progress, gain.Skill, out value, out progress))
            return false;
        credited = ProgressDifference(skill.Value, skill.Progress, value, progress);
        return true;
    }

    private static float ProjectAttribute(RebirthSurvivorOwnerStateSnapshot state, RebirthSurvivorOwnerAttributeSnapshot attribute,
        RebirthAttributeDefinition definition)
    {
        double total = attribute.Current;
        foreach (var pair in pending)
            total += AttributeContribution(state, attribute.Id, pair.Key, TotalGain(state, pair.Key));
        if (inFlightSequence != 0 && state.OrdinaryPracticeSequence < inFlightSequence)
            foreach (var pair in inFlight)
                if (!pending.ContainsKey(pair.Key))
                    total += AttributeContribution(state, attribute.Id, pair.Key, pair.Value);
        return (float)Math.Min(definition.Max, total);
    }

    private static double AttributeContribution(RebirthSurvivorOwnerStateSnapshot state, string attributeId, string skillId, Gain gain)
    {
        if (gain.Attribute <= 0 || !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out var definition) ||
            !string.Equals(definition.PrimaryAttributeId, attributeId, StringComparison.OrdinalIgnoreCase)) return 0;
        var skill = state.Skills.Find(row => string.Equals(row.Id, skillId, StringComparison.OrdinalIgnoreCase));
        return skill != null && TryProjectSkill(skill, definition, gain, out _, out _, out float credited)
            ? gain.Attribute * Math.Max(0, Math.Min(1, credited / gain.Skill))
            : 0;
    }

    // Only progression windows call this, on their own detached display snapshot.
    internal static void ApplyDisplay(EntityPlayerLocal player, RebirthSurvivorOwnerStateSnapshot display)
    {
        if (!ReferenceEquals(ownerWorld, player?.world) || !string.Equals(creation, display.CreationId, StringComparison.OrdinalIgnoreCase) ||
            pending.Count == 0 && inFlightSequence == 0) return;
        foreach (var attribute in display.Attributes)
        {
            var definition = RebirthAttributeProgressionService.FindDefinition(attribute.Id);
            if (definition != null) attribute.Current = ProjectAttribute(display, attribute, definition);
        }
        foreach (var skill in display.Skills)
        {
            var gain = TotalGain(display, skill.Id);
            if (gain.Skill == 0 || !RebirthSurvivorDefinitionRegistry.TryGetSkill(skill.Id, out var definition) ||
                !TryProjectSkill(skill, definition, gain, out float value, out float progress, out _)) continue;
            skill.Value = value;
            skill.Progress = progress;
        }
    }

    internal static void Tick(World world, EntityPlayerLocal player)
    {
        if (pending.Count > 0 && Time.realtimeSinceStartupAsDouble >= sendAt)
            Flush(world, player, disconnect: false);
    }

    private static void Flush(World world, EntityPlayerLocal player, bool disconnect)
    {
        if (pending.Count == 0 || !ReferenceEquals(world, player?.world) || !TryBind(player, out _, out _) ||
            pending.Count == 0 || !disconnect && inFlightSequence != 0) return;
        uint next = sequence + 1;
        try
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthOrdinarySkillPractice>().Setup(player.entityId, creation, next, pending),
                _flush: disconnect);
            sequence = next;
            if (!disconnect || inFlightSequence == 0)
            {
                (pending, inFlight) = (inFlight, pending);
                inFlightSequence = next;
            }
            pending.Clear();
        }
        catch (Exception error)
        {
            sendAt = Time.realtimeSinceStartupAsDouble + 5;
            RebirthDebug.Log("survivor", "[REBIRTH Practice] Checkpoint send failed: " + error.Message);
        }
    }

    internal static void ResetClient()
    {
        pending.Clear();
        inFlight.Clear();
        ownerWorld = null;
        ownerConnection = null;
        creation = null;
        sequence = inFlightSequence = 0;
    }

    internal static uint ObserveOwner(World world, ClientInfo sender, RebirthWorldCharacterRecord record)
    {
        if (world == null || sender == null || record?.Origin == null) return 0;
        var peer = Peers.GetValue(sender, _ => new Peer());
        if (!ReferenceEquals(peer.World, world) || peer.Creation != record.Origin.CreationId)
        {
            peer.World = world;
            peer.Creation = record.Origin.CreationId;
            peer.LastActive = record.Condition?.ActivePlaySeconds ?? 0;
            peer.AvailableGain = GainGraceSeconds * MaximumGainPerActiveSecond;
            peer.Sequence = 0;
        }
        return peer.Sequence;
    }

    internal static void Receive(World world, ClientInfo sender, EntityPlayer player, string requestedCreation,
        uint receivedSequence, KeyValuePair<string, Gain>[] gains)
    {
        if (!Peers.TryGetValue(sender, out var peer) || !ReferenceEquals(peer.World, world) ||
            receivedSequence <= peer.Sequence || !RebirthSurvivorRequestScope.Matches(requestedCreation, peer.Creation) ||
            !RebirthSkillAwardService.TryGetEligible(player, out var identity, out var record) ||
            !RebirthSurvivorRequestScope.Matches(requestedCreation, record.Origin?.CreationId)) return;

        double total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double active = record.Condition?.ActivePlaySeconds ?? 0;
        double elapsed = Math.Max(0, active - peer.LastActive);
        peer.LastActive = Math.Max(peer.LastActive, active);
        peer.AvailableGain = Math.Min(MaximumBankedGain, peer.AvailableGain + elapsed * MaximumGainPerActiveSecond);
        bool valid = Finite(peer.AvailableGain);
        foreach (var pair in gains)
        {
            total += pair.Value.Skill;
            if (!seen.Add(pair.Key) || total > peer.AvailableGain || !IsValidGain(record, pair.Key, pair.Value))
            {
                valid = false;
                break;
            }
        }
        peer.Sequence = receivedSequence;
        if (valid)
        {
            peer.AvailableGain -= total;
            ApplyCheckpoint(player, identity, record, gains);
        }
        RebirthSkillAwardService.QueueOrdinaryOwnerPublication(player, record);
        RebirthDebug.Log("survivor", $"[REBIRTH Practice] Checkpoint {receivedSequence}, {gains.Length} skills, accepted={valid}");
    }

    private static bool IsValidGain(RebirthWorldCharacterRecord record, string skillId, Gain gain)
    {
        if (!Finite(gain.Skill) || gain.Skill <= 0 || !Finite(gain.Attribute) || gain.Attribute < 0 ||
            !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out var definition) ||
            !RebirthSkillTrainingProfileRegistry.TryGet(definition.Id, out var profile) || !profile.OwnerLocalPractice ||
            !record.Progression.Skills.TryGetValue(definition.Id, out var state) || state == null ||
            gain.Skill > definition.Max - definition.Min) return false;
        string attributeId = definition.PrimaryAttributeId;
        if (gain.Attribute == 0) return true;
        double attributeLimit = RebirthProgressionRuntimeConfig.SkillProgressToAttribute * MaximumAttributeEquipmentFactor;
        return !string.IsNullOrEmpty(attributeId) && record.Progression.Attributes.TryGetValue(attributeId, out var attribute) &&
            attribute != null && gain.Attribute <= gain.Skill * attributeLimit + 0.0001;
    }

    private static void ApplyCheckpoint(EntityPlayer player, RebirthStablePlayerIdentity identity,
        RebirthWorldCharacterRecord record, KeyValuePair<string, Gain>[] gains)
    {
        var accepted = new List<KeyValuePair<string, float>>(gains.Length);
        foreach (var pair in gains)
        {
            RebirthSurvivorDefinitionRegistry.TryGetSkill(pair.Key, out var definition);
            var skill = record.Progression.Skills[definition.Id];
            if (!RebirthSkillAwardService.TryAddSkillProgress(definition, skill.Value, skill.Progress, pair.Value.Skill,
                out float next, out float progress)) continue;
            float credited = ProgressDifference(skill.Value, skill.Progress, next, progress);
            if (credited <= 0) continue;
            skill.Value = next;
            skill.Progress = progress;
            accepted.Add(new KeyValuePair<string, float>(definition.Id, credited));

            if (pair.Value.Attribute <= 0) continue;
            string attributeId = definition.PrimaryAttributeId;
            var attributeDefinition = RebirthAttributeProgressionService.FindDefinition(attributeId);
            if (attributeDefinition == null) continue;
            var attribute = record.Progression.Attributes[attributeId];
            attribute.Current = (float)Math.Min(attributeDefinition.Max,
                attribute.Current + pair.Value.Attribute * Math.Min(1, credited / pair.Value.Skill));
        }
        if (accepted.Count == 0) return;
        record.Touch("ordinary-practice-checkpoint", requiresDurability: false);
        if (!RebirthWorldCharacterRepository.SaveIfDirty(identity, "ordinary-practice-checkpoint"))
            RebirthWorldCharacterRepository.TryScheduleOrdinaryPracticeCheckpoint(record, retry: true);
        foreach (var gain in accepted)
            RebirthStatisticsService.RecordSkillProgress(player, gain.Key, gain.Value);
    }

    private static bool Finite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Disconnect))]
    internal static class DisconnectPatch
    {
        private static void Prefix(GameManager __instance)
        {
            var world = __instance.World;
            if (world?.IsRemote() == true) Flush(world, world.GetPrimaryPlayer(), disconnect: true);
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthOrdinarySkillPractice : NetPackage
{
    private const byte CheckpointVersion = 2;
    private int playerId;
    private string creation;
    private uint sequence;
    private KeyValuePair<string, RebirthOrdinarySkillPractice.Gain>[] gains;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    internal NetPackageRebirthOrdinarySkillPractice Setup(int id, string character, uint serial,
        Dictionary<string, RebirthOrdinarySkillPractice.Gain> checkpoint)
    {
        playerId = id;
        creation = character;
        sequence = serial;
        gains = new KeyValuePair<string, RebirthOrdinarySkillPractice.Gain>[checkpoint.Count];
        ((ICollection<KeyValuePair<string, RebirthOrdinarySkillPractice.Gain>>)checkpoint).CopyTo(gains, 0);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        gains = null;
        if (reader.ReadByte() != CheckpointVersion) throw new InvalidDataException("Unsupported ordinary practice checkpoint");
        playerId = reader.ReadInt32();
        sequence = reader.ReadUInt32();
        creation = RebirthSurvivorNetworkCodec.ReadBoundedString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        int count = reader.ReadByte();
        if (count < 1 || count > RebirthSurvivorNetworkProtocol.MaxSkills)
            throw new InvalidDataException("Invalid ordinary practice checkpoint size");
        var batch = new KeyValuePair<string, RebirthOrdinarySkillPractice.Gain>[count];
        for (int i = 0; i < count; i++)
        {
            string skillId = RebirthSurvivorNetworkCodec.ReadBoundedString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            var gain = new RebirthOrdinarySkillPractice.Gain(reader.ReadDouble(), reader.ReadDouble());
            batch[i] = new KeyValuePair<string, RebirthOrdinarySkillPractice.Gain>(skillId, gain);
        }
        gains = batch;
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        writer.Write(CheckpointVersion);
        writer.Write(playerId);
        writer.Write(sequence);
        RebirthSurvivorNetworkCodec.WriteString(writer, creation, RebirthSurvivorNetworkProtocol.MaxIdLength);
        writer.Write((byte)gains.Length);
        foreach (var pair in gains)
        {
            RebirthSurvivorNetworkCodec.WriteString(writer, pair.Key, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(pair.Value.Skill);
            writer.Write(pair.Value.Attribute);
        }
    }

    public int GetLength()
    {
        int length = 10 + RebirthSurvivorNetworkCodec.EstimateString(creation, RebirthSurvivorNetworkProtocol.MaxIdLength);
        if (gains != null)
            foreach (var pair in gains)
                length += 16 + RebirthSurvivorNetworkCodec.EstimateString(pair.Key, RebirthSurvivorNetworkProtocol.MaxIdLength);
        return length;
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (gains == null || world == null || world.IsRemote() || !ThreadManager.IsMainThread() || Sender == null ||
            !ReferenceEquals(GameManager.Instance?.World, world) || !ValidEntityIdForSender(playerId)) return;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection?.IsServer != true || !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) ||
            world.GetEntity(playerId) is not EntityPlayer player || !ReferenceEquals(player.world, world)) return;
        RebirthOrdinarySkillPractice.Receive(world, Sender, player, creation, sequence, gains);
    }
}
