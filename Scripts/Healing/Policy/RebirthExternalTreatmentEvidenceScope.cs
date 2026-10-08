using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

/// <summary>Freezes the two character scopes for authoritative external treatment evidence.</summary>
internal sealed class RebirthExternalTreatmentEvidenceScope
{
    private sealed class Participant
    {
        internal EntityPlayer Player;
        internal int EntityId;
        internal EntityBuffs Buffs;
        internal RebirthWorldCharacterRecord Record;
        internal string Creation, CanonicalOwner, StorageOwner;
        internal object DeathStamp;
    }
    private sealed class DeathState { internal object Stamp = new object(); }
    internal sealed class DeathObservation
    {
        internal EntityPlayer Player;
        internal World World;
        internal GameManager Game;
        internal int EntityId;
    }
    private static readonly ConditionalWeakTable<EntityPlayer, DeathState> DeathStates =
        new ConditionalWeakTable<EntityPlayer, DeathState>();

    // A failed observation can only revoke an existing participant; it cannot admit one.
    // Null is a tombstone, avoiding allocation/world/authority calls on this fault path.
    internal static void RevokeForObservationFault(EntityAlive entity)
    {
        EntityPlayer player = entity as EntityPlayer;
        DeathState state;
        if (!ReferenceEquals(player, null) && DeathStates.TryGetValue(player, out state)) state.Stamp = null;
    }

    private static object GetDeathStampForAdmission(EntityPlayer player)
    {
        DeathState state = DeathStates.GetValue(player, _ => new DeathState());
        if (state.Stamp == null) state.Stamp = new object();
        return state.Stamp;
    }
    internal static DeathObservation ObserveNativeDeath(EntityAlive entity)
    {
        EntityPlayer player = entity as EntityPlayer;
        GameManager game = GameManager.Instance;
        World world = player?.world;
        return !ThreadManager.IsMainThread() || !RebirthWorldCharacterRepository.IsServerAuthority ||
            player == null || game == null || world == null || world.IsRemote() ||
            !ReferenceEquals(game.World, world) || !ReferenceEquals(world.GetEntity(player.entityId), player)
            ? null : new DeathObservation { Player = player, World = world, Game = game, EntityId = player.entityId };
    }

    internal static void CompleteNativeDeath(DeathObservation observation, EntityAlive entity, bool ranOriginal)
    {
        if (!ranOriginal || observation == null || !ReferenceEquals(entity, observation.Player)) return;
        DeathObservation current = ObserveNativeDeath(entity);
        if (current == null || current.EntityId != observation.EntityId || !ReferenceEquals(current.Game, observation.Game) ||
            !ReferenceEquals(current.World, observation.World) || !current.Player.bDead) return;
        DeathState state;
        if (DeathStates.TryGetValue(current.Player, out state)) state.Stamp = new object();
    }
    private GameManager game;
    private World world;
    private Participant healer, patient;

    internal static RebirthExternalTreatmentEvidenceScope Capture(EntityPlayer healer, EntityPlayer patient)
    {
        var game = GameManager.Instance;
        World world = healer?.world;
        if (!ThreadManager.IsMainThread() || game == null || world == null || world.IsRemote() ||
            !ReferenceEquals(game.World, world) || !RebirthWorldCharacterRepository.IsServerAuthority ||
            ReferenceEquals(healer, patient)) return null;
        Participant healerScope = CaptureParticipant(healer, world);
        Participant patientScope = CaptureParticipant(patient, world);
        if (healerScope == null || patientScope == null) return null;
        return new RebirthExternalTreatmentEvidenceScope { game = game, world = world,
            healer = healerScope, patient = patientScope };
    }

    internal bool IsCurrent(EntityPlayer currentHealer, EntityAlive currentPatient)
    {
        return ThreadManager.IsMainThread() && RebirthWorldCharacterRepository.IsServerAuthority &&
            ReferenceEquals(GameManager.Instance, game) && ReferenceEquals(game.World, world) &&
            !world.IsRemote() && ReferenceEquals(currentHealer, healer.Player) &&
            ReferenceEquals(currentPatient, patient.Player) && IsCurrent(healer) && IsCurrent(patient);
    }

    private static Participant CaptureParticipant(EntityPlayer player, World world)
    {
        RebirthWorldCharacterRecord record;
        RebirthStablePlayerIdentity identity;
        if (player == null || player.IsDead() || player.Buffs == null ||
            !ReferenceEquals(player.world, world) || !ReferenceEquals(world.GetEntity(player.entityId), player) ||
            !RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete ||
            !RebirthWorldCharacterService.TryGetIdentity(player, out identity) || identity == null ||
            !RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId, out var creation)) return null;
        return new Participant { Player = player, EntityId = player.entityId, Buffs = player.Buffs,
            Record = record, Creation = creation, CanonicalOwner = identity.CanonicalId, StorageOwner = identity.StorageKey,
            DeathStamp = GetDeathStampForAdmission(player) };
    }

    private bool IsCurrent(Participant scope)
    {
        RebirthWorldCharacterRecord record;
        RebirthStablePlayerIdentity identity;
        DeathState deathState;
        EntityPlayer player = scope.Player;
        return player != null && !player.IsDead() && player.entityId == scope.EntityId &&
            scope.DeathStamp != null && DeathStates.TryGetValue(player, out deathState) && ReferenceEquals(deathState.Stamp, scope.DeathStamp) &&
            ReferenceEquals(player.world, world) && ReferenceEquals(player.Buffs, scope.Buffs) &&
            ReferenceEquals(world.GetEntity(scope.EntityId), player) &&
            RebirthWorldCharacterService.TryGet(player, out record) && ReferenceEquals(record, scope.Record) &&
            record.IsComplete && RebirthSurvivorRequestScope.Matches(scope.Creation, record.Origin?.CreationId) &&
            RebirthWorldCharacterService.TryGetIdentity(player, out identity) && identity != null &&
            string.Equals(identity.CanonicalId, scope.CanonicalOwner, StringComparison.Ordinal) &&
            string.Equals(identity.StorageKey, scope.StorageOwner, StringComparison.Ordinal);
    }
}
// Completed-death stamping requires the successful original; observer faults only revoke prior evidence.
// Shared installer activation remains with the original coordinator.
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetDead))]
internal static class RebirthExternalTreatmentEvidenceDeathPatch
{
    private static void Prefix(EntityAlive __instance, out RebirthExternalTreatmentEvidenceScope.DeathObservation __state)
    {
        __state = null;
        try { __state = RebirthExternalTreatmentEvidenceScope.ObserveNativeDeath(__instance); }
        catch (Exception)
        {
            try { RebirthExternalTreatmentEvidenceScope.RevokeForObservationFault(__instance); }
            catch (Exception) { /* A failed observer cannot interrupt the native method. */ }
        }
    }

    private static void Postfix(EntityAlive __instance, bool __runOriginal,
        RebirthExternalTreatmentEvidenceScope.DeathObservation __state)
    {
        try { RebirthExternalTreatmentEvidenceScope.CompleteNativeDeath(__state, __instance, __runOriginal); }
        catch (Exception)
        {
            try { RebirthExternalTreatmentEvidenceScope.RevokeForObservationFault(__instance); }
            catch (Exception) { /* A failed observer cannot interrupt the native method. */ }
        }
    }
}