using System;
using System.Collections.Generic;

/// <summary>
/// Candidate snapshot adapter for a frozen external treatment whose ORIGINAL native buff
/// has not started yet. It never supplies an owner-derived first healing baseline.
/// Installer integration remains withheld pending native-source qualification.
/// </summary>
internal static class RebirthExternalPendingBuffSnapshot
{
    internal sealed class State
    {
        internal EntityPlayer Patient;
        internal World World;
        internal EntityBuffs BuffStorage;
        internal GameManager Game;
        internal ConnectionManager Connection;
        internal ClientInfo Sender;
        internal int Recipient;
        internal RebirthWorldCharacterRecord Record;
        internal string Creation, CanonicalOwner, StorageOwner;
        internal readonly List<Original> Buffs = new List<Original>(4);
    }
    internal sealed class Original
    {
        internal string Name;
        internal BuffValue Value;
        internal int Instigator;
        internal float Factor;
    }
    private const string Marker = "$rebirthExternalMending_";
    private static readonly string[] Names = { "buffLegSplinted", "buffLegCast", "buffArmSplinted", "buffArmCast" };

    internal static State Capture(World world, ClientInfo sender, int recipient)
    {
        var game = GameManager.Instance;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        EntityPlayer patient = world?.GetEntity(recipient) as EntityPlayer;
        RebirthWorldCharacterRecord record;
        RebirthStablePlayerIdentity owner, authenticated;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || game == null ||
            !ReferenceEquals(game.World, world) || patient == null || patient.entityId != recipient || patient.IsDead() ||
            !ReferenceEquals(patient.world, world) || sender?.InternalId == null || sender.entityId != recipient ||
            connection == null || !connection.IsServer || connection.Clients == null ||
            !ReferenceEquals(connection.Clients.ForEntityId(recipient), sender) ||
            !RebirthWorldCharacterRepository.IsServerAuthority ||
            !RebirthWorldCharacterService.TryGet(patient, out record) || record == null || !record.IsComplete ||
            !RebirthWorldCharacterService.TryGetIdentity(patient, out owner) || owner == null ||
            !RebirthStablePlayerIdentity.TryFromClientInfo(sender, out authenticated) || authenticated == null ||
            !string.Equals(owner.CanonicalId, authenticated.CanonicalId, StringComparison.Ordinal) ||
            !string.Equals(owner.StorageKey, authenticated.StorageKey, StringComparison.Ordinal) ||
            !RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId, out var creation)) return null;
        var state = new State { Patient = patient, World = world, BuffStorage = patient.Buffs, Game = game, Connection = connection,
            Sender = sender, Recipient = recipient, Record = record, Creation = creation,
            CanonicalOwner = owner.CanonicalId, StorageOwner = owner.StorageKey };
        foreach (string name in Names)
        {
            BuffValue buff = patient.Buffs?.GetBuff(name);
            if (buff == null || buff.Started || buff.Remove || buff.Invalid || buff.Finished) continue;
            string key = Marker + name;
            float factor = patient.Buffs.GetCustomVar(key);
            if (!(factor > 0f) || float.IsNaN(factor) || float.IsInfinity(factor) ||
                patient.Buffs.GetCustomVar(key + "_lo") != (buff.InstigatorId & 65535) ||
                patient.Buffs.GetCustomVar(key + "_hi") != (int)((uint)buff.InstigatorId >> 16)) continue;
            state.Buffs.Add(new Original { Name = name, Value = buff, Instigator = buff.InstigatorId, Factor = factor });
        }
        return state.Buffs.Count > 0 ? state : null;
    }

    internal static int Restore(State state)
    {
        if (!IsCurrent(state)) return 0;
        int restored = 0;
        foreach (Original original in state.Buffs)
        {
            // Death/removal/start of the ACTUAL captured original while the native reader runs
            // revokes this admission. Never reset a timer or undo an independently removed buff.
            if (original.Value.Started || original.Value.Remove || original.Value.Invalid || original.Value.Finished ||
                original.Value.InstigatorId != original.Instigator) continue;
            int found = -1;
            bool duplicate = false;
            var live = state.Patient.Buffs.ActiveBuffs;
            for (int i = 0; i < live.Count; i++)
            {
                BuffValue candidate = live[i];
                if (candidate?.BuffClass == null ||
                    !string.Equals(candidate.BuffClass.Name, original.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (found >= 0) { duplicate = true; break; }
                found = i;
            }
            if (duplicate || found < 0) continue;
            BuffValue incoming = live[found];
            if (incoming.Remove || incoming.Invalid || incoming.Finished || incoming.InstigatorId != original.Instigator) continue;
            // Same named/instigated live snapshot cannot force the server's already accepted
            // pending original to skip its own native initialization. Retain its exact object,
            // flags/timers/stack data; unrelated incoming buffs and native CVars stay untouched.
            live[found] = original.Value;
            string key = Marker + original.Name;
            state.Patient.Buffs.SetCustomVar(key + "_lo", original.Instigator & 65535, false);
            state.Patient.Buffs.SetCustomVar(key + "_hi", (int)((uint)original.Instigator >> 16), false);
            state.Patient.Buffs.SetCustomVar(key, original.Factor, false);
            // Native Read notified visible incoming instances before this narrow replacement.
            // Balance only the discarded pointer; the original gets its own notification when
            // native Tick starts it. Iconless visible buffs still have native delegates.
            if (!ReferenceEquals(incoming, original.Value) && !incoming.BuffClass.Hidden)
                state.Patient.Stats.EntityBuffRemoved(incoming);
            restored++;
        }
        return restored;
    }

    private static bool IsCurrent(State state)
    {
        if (state == null || !ThreadManager.IsMainThread() || state.Patient == null || state.Patient.IsDead() ||
            state.Patient.Buffs?.ActiveBuffs == null || !ReferenceEquals(state.Patient.Buffs, state.BuffStorage) ||
            state.World == null || state.World.IsRemote() ||
            !ReferenceEquals(GameManager.Instance, state.Game) || !ReferenceEquals(state.Game.World, state.World) ||
            !ReferenceEquals(state.Patient.world, state.World) ||
            state.Patient.entityId != state.Recipient || !ReferenceEquals(state.World.GetEntity(state.Recipient), state.Patient) ||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance, state.Connection) ||
            !state.Connection.IsServer || state.Connection.Clients == null ||
            !ReferenceEquals(state.Connection.Clients.ForEntityId(state.Recipient), state.Sender) ||
            state.Sender.entityId != state.Recipient || !RebirthWorldCharacterRepository.IsServerAuthority) return false;
        RebirthWorldCharacterRecord record;
        RebirthStablePlayerIdentity owner, authenticated;
        return RebirthWorldCharacterService.TryGet(state.Patient, out record) && ReferenceEquals(record, state.Record) &&
            record.IsComplete && RebirthSurvivorRequestScope.Matches(state.Creation, record.Origin?.CreationId) &&
            RebirthWorldCharacterService.TryGetIdentity(state.Patient, out owner) && owner != null &&
            RebirthStablePlayerIdentity.TryFromClientInfo(state.Sender, out authenticated) && authenticated != null &&
            string.Equals(owner.CanonicalId, state.CanonicalOwner, StringComparison.Ordinal) &&
            string.Equals(owner.StorageKey, state.StorageOwner, StringComparison.Ordinal) &&
            string.Equals(authenticated.CanonicalId, state.CanonicalOwner, StringComparison.Ordinal) &&
            string.Equals(authenticated.StorageKey, state.StorageOwner, StringComparison.Ordinal);
    }
}
