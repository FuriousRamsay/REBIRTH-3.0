using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine.Scripting;

#nullable disable

// These classes are restricted to former POI broken aliases, not player station definitions.
[Preserve]
public sealed class BlockRebirthWorldStation : BlockWorkstation
{
    public override void OnBlockLoaded(WorldBase world, Vector3i pos, BlockValue value)
    { RebirthWorldStationMigration.Ensure(world, pos, value); base.OnBlockLoaded(world, pos, value); }
}
[Preserve]
public sealed class BlockRebirthWorldForge : BlockForge
{
    public override void OnBlockLoaded(WorldBase world, Vector3i pos, BlockValue value)
    { RebirthWorldStationMigration.Ensure(world, pos, value); base.OnBlockLoaded(world, pos, value); }
}
[Preserve]
public sealed class BlockRebirthWorldChemistry : BlockCampfire
{
    public override void OnBlockLoaded(WorldBase world, Vector3i pos, BlockValue value)
    { RebirthWorldStationMigration.Ensure(world, pos, value); base.OnBlockLoaded(world, pos, value); }
}
internal static class RebirthWorldStationMigration
{
    // Old saves may retain composite loot. Keep its native search/security path available
    // until a lossless conversion fits; never cast that container to a workstation.
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(TileEntityComposite), nameof(TileEntityComposite.read),
            new[] { typeof(PooledBinaryReader), typeof(StreamModeRead), typeof(int[]) }),
            transpiler: new HarmonyMethod(typeof(RebirthWorldStationMigration), nameof(LegacyReader)));
        harmony.Patch(AccessTools.Method(typeof(BlockWorkstation), nameof(BlockWorkstation.OnBlockActivated),
            new[] { typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) }),
            prefix: new HarmonyMethod(typeof(RebirthWorldStationMigration), nameof(BeforeActivate)));
        harmony.Patch(AccessTools.Method(typeof(BlockWorkstation), nameof(BlockWorkstation.GetBlockActivationCommands)),
            prefix: new HarmonyMethod(typeof(RebirthWorldStationMigration), nameof(BeforeCommands)));
    }
    private static IEnumerable<CodeInstruction> LegacyReader(IEnumerable<CodeInstruction> source)
    {
        var code = source.ToList(); int matches = 0;
        foreach (var instruction in code)
        {
            if (instruction.opcode != OpCodes.Isinst || !Equals(instruction.operand, typeof(BlockCompositeTileEntity))) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(RebirthWorldStationMigration), nameof(ResolveLegacySchema));
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException("Legacy station storage reader is not qualified.");
        return code;
    }
    internal static BlockCompositeTileEntity ResolveLegacySchema(Block block)
    {
        if (block is BlockCompositeTileEntity composite) return composite;
        if (!(block is BlockRebirthWorldStation) && !(block is BlockRebirthWorldForge) && !(block is BlockRebirthWorldChemistry)) return null;
        return Block.GetBlockByName("RebirthLegacyStorage_" + block.GetBlockName()) as BlockCompositeTileEntity;
    }
    private static bool BeforeActivate(WorldBase __0, Vector3i __1, BlockValue __2,
        EntityPlayerLocal __3, ref bool __result)
    {
        var legacy = __0.GetTileEntity(__1) as TileEntityComposite;
        if (legacy == null) return true;
        var storage = legacy.GetFeature<TEFeatureStorage>();
        __result = storage != null && storage.OnBlockActivated("Search".AsSpan(), __0, __1, __2, __3);
        return false;
    }
    private static bool BeforeCommands(WorldBase __0, BlockValue __1, Vector3i __2,
        ref BlockActivationCommand[] __result)
    {
        var entity = __0.GetTileEntity(__2);
        if (entity is TileEntityWorkstation) return true;
        __result = entity is TileEntityComposite
            ? new[] { new BlockActivationCommand("open", "search", true) }
            : Array.Empty<BlockActivationCommand>();
        return false;
    }
    internal static void Ensure(WorldBase world, Vector3i pos, BlockValue value)
    {
        if (world == null || world.IsRemote() || value.ischild || world.GetTileEntity(pos) is TileEntityWorkstation) return;
        // Do not guess provenance from the current attacker or migrate player-owned containers.
        var existing = world.GetTileEntity(pos) as TileEntityComposite;
        PlatformUserIdentifierAbs owner;
        if (existing == null || existing.PlayerPlaced || RebirthWorkstationSecurityService.TryGetOwner(pos, out owner)) return;
        var storage = existing.GetFeature<TEFeatureStorage>();
        if (storage?.ItemGrid == null || storage.ItemGrid.PlayerOwned) return;
        var contents = new List<ItemStack>();
        foreach (var item in storage.ItemGrid.items)
            if (item != null && !item.IsEmpty()) contents.Add(item.Clone());
        var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        var concrete = world as World;
        if (chunk == null || concrete == null) return;
        var replacement = new TileEntityWorkstation(chunk);
        if (contents.Count > replacement.Output.Length) return; // Leave oversized legacy storage intact.
        replacement.localChunkPos = World.toBlock(pos);
        for (int index = 0; index < contents.Count; index++) replacement.Output[index] = contents[index];
        chunk.RemoveTileEntity(concrete, existing);
        chunk.AddTileEntity(replacement);
        replacement.setModified();
        chunk.isModified = true;
    }
}
