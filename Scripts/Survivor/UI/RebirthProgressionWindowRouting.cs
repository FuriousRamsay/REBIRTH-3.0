using System;

#nullable disable

/// <summary>Input-time admission for selector and direct Character/Skills routes.
/// A primary-owner projection cannot be consumed by a foreign or retired XUi.</summary>
internal static class RebirthProgressionWindowRouting
{
    internal enum Admission { Native, Blocked, Rebirth }

    internal sealed class Witness
    {
        internal object Game, World, State, Ui, OwnerUi, Player, Manager;
        internal string WorldGuid, Creation;
        internal RebirthSurvivorOwnerHeader Header;
    }

    internal static bool StillCurrent(XUiController source, Witness original)
    {
        Witness current;
        return original != null && Resolve(source, out current) == Admission.Rebirth &&
            ReferenceEquals(original.Game, current.Game) && ReferenceEquals(original.World, current.World) &&
            ReferenceEquals(original.State, current.State) && ReferenceEquals(original.Ui, current.Ui) &&
            ReferenceEquals(original.OwnerUi, current.OwnerUi) && ReferenceEquals(original.Player, current.Player) &&
            ReferenceEquals(original.Manager, current.Manager) && original.Header.Equals(current.Header) &&
            string.Equals(original.WorldGuid, current.WorldGuid, StringComparison.Ordinal) &&
            string.Equals(original.Creation, current.Creation, StringComparison.Ordinal);
    }

    internal static Admission Resolve(XUiController source)
    {
        Witness ignored;
        return Resolve(source, out ignored);
    }

    internal static Admission Resolve(XUiController source, out Witness witness)
    {
        witness = null;
        // Disabled mode retains native behavior without probing custom owner/UI state.
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
            return Admission.Native;
        var game = GameManager.Instance;
        var world = game?.World;
        var worldState = world?.worldState;
        string worldGuid = worldState?.Guid;
        RebirthSurvivorOwnerHeader header;
        var snapshot = RebirthSurvivorClientState.GetOwnerStateSnapshot(out header);
        if (!header.Available || !header.RebirthModeEnabled || !header.HasCharacter ||
            header.CreationState != RebirthSurvivorOwnerCreationState.Ready)
            return Admission.Native;

        string creation = snapshot?.CreationId;
        var ui = source?.xui;
        var ownerUi = ui?.playerUI;
        var player = ownerUi?.entityPlayer;
        var windowManager = ownerUi?.windowManager;
        if (game == null || world == null || worldState == null ||
            string.IsNullOrEmpty(worldGuid) || string.IsNullOrEmpty(creation) ||
            snapshot == null || !snapshot.DefinitionsCompatible || player == null ||
            windowManager == null ||
            !ReferenceEquals(world.GetPrimaryPlayer(), player) ||
            !ReferenceEquals(player.world, world) ||
            !ReferenceEquals(world.GetEntity(player.entityId), player) ||
            !ReferenceEquals(player.PlayerUI?.xui, ui) ||
            !player.IsSpawned() || player.IsDead() || RebirthCharacterCreationHoldService.IsHeld(player))
            return Admission.Blocked;

        // Native getters/owner projection retrieval can retire a scope. Check both
        // the projection key and exact creation; header equality alone lacks creation.
        RebirthSurvivorOwnerHeader currentHeader;
        var current = RebirthSurvivorClientState.GetOwnerStateSnapshot(out currentHeader);
        if (!ReferenceEquals(GameManager.Instance, game) || !ReferenceEquals(game.World, world) ||
            !ReferenceEquals(world.worldState, worldState) ||
            !string.Equals(worldState.Guid, worldGuid, StringComparison.Ordinal) ||
            !header.Equals(currentHeader) || current == null || !current.DefinitionsCompatible ||
            !string.Equals(current.CreationId, creation, StringComparison.Ordinal) ||
            RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth ||
            !ReferenceEquals(source?.xui, ui) || !ReferenceEquals(ui.playerUI, ownerUi) ||
            !ReferenceEquals(ownerUi?.entityPlayer, player) || !ReferenceEquals(ownerUi?.windowManager, windowManager) ||
            !ReferenceEquals(world.GetPrimaryPlayer(), player) || !ReferenceEquals(player.world, world) ||
            !ReferenceEquals(world.GetEntity(player.entityId), player) ||
            !ReferenceEquals(player.PlayerUI?.xui, ui) || !player.IsSpawned() || player.IsDead() ||
            RebirthCharacterCreationHoldService.IsHeld(player))
            return Admission.Blocked;
        witness = new Witness { Game = game, World = world, State = worldState, WorldGuid = worldGuid,
            Ui = ui, OwnerUi = ownerUi, Player = player, Manager = windowManager, Header = header, Creation = creation };
        return Admission.Rebirth;
    }
}
