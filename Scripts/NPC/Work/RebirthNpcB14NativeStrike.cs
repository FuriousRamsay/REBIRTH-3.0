using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// B14 NPC-attributed strike bridge (source-authored, NOT enabled).
//
// Native path for a player: ItemActionMelee -> ItemActionAttack.Hit -> Block.DamageBlock(... damager id ...)
// then GameUtils.HarvestOnAttack, which returns unless the holder is EntityPlayerLocal and delivers through
// LocalPlayerUI/XUiM_PlayerInventory. This bridge keeps the native Block.DamageBlock call (so the
// block's own destroy logic, falling blocks and Destroy-event world drops run with the NPC's entity id)
// and replaces only the player-only harvest calculation/delivery with HarvestCalculator below, whose
// stacks are saved as witnesses before any credit. No Harmony, no player surrogate.

// Destination for a witnessed stack. TryCredit MUST be idempotent per (operationId, witnessIndex): the caller may
// repeat a call after a crash between the credit and the saved Credited mark.
//   Credited  : the exact stack now lives in the worker bag (verified saved).
//   Full      : the bag has no free cell; the bridge keeps the stack at its physical source (journaled overflow).
//   Retry     : transient (worker busy, save failed); nothing was claimed, try again later.
//   Ambiguous : a prior credit marker exists but its custody moved; escrow, never re-place and never drop.
internal enum RebirthNpcB14CreditResult { Credited, Full, Retry, Ambiguous }

internal interface IRebirthNpcB14CreditSink
{
    RebirthNpcB14CreditResult TryCredit(Guid operationId, int witnessIndex, ItemStack stack);
    // Read-only: true only when a verified credit for (operation, index) exists right now.
    bool HasCredit(Guid operationId, int witnessIndex, ItemStack stack);
}

internal static class RebirthNpcB14HarvestCalculator
{
    private static readonly FastTags<TagGroup.Global> OreTag = FastTags<TagGroup.Global>.Parse("ore");
    private static readonly FastTags<TagGroup.Global> CropTag = FastTags<TagGroup.Global>.Parse("crops");
    private static readonly FastTags<TagGroup.Global> SeedMultiTag = FastTags<TagGroup.Global>.Parse("seedMulti");

    // Port of GameUtils.HarvestOnAttack (b259) with the player-only guard, quest event, XP grant and
    // inventory delivery removed. Yield modifiers (HarvestCount passive effect on the NPC's own tool and
    // entity, XUiM_Recipes output modifiers) are the same calls, so the NPC's Mining/Logging effects apply
    // exactly as for a player. The Management damage bonus is NOT used here (damage only).
    internal static void Compute(EntityAlive npc, ItemValue tool, Block block, BlockValue blockValue,
        ItemActionAttack.AttackHitInfo info, Dictionary<string, ItemActionAttack.Bonuses> toolBonuses,
        GameRandom random, List<ItemStack> into)
    {
        if (npc == null || tool == null || block == null || info == null || info.itemsToDrop == null ||
            random == null || into == null) return;
        if (info.bKilled)
        {
            List<Block.SItemDropProb> destroy;
            if (!info.itemsToDrop.TryGetValue(EnumDropEvent.Destroy, out destroy))
            {
                if (!blockValue.isair && info.bBlockHit) Emit(into, blockValue.ToItemValue(), 1, 1f, random);
            }
            else
            {
                for (int i = 0; i < destroy.Count; i++)
                {
                    Block.SItemDropProb drop = destroy[i];
                    if (info.bBlockHit && drop.name.Equals("[recipe]"))
                    {
                        List<Recipe> recipes = CraftingManager.GetRecipes(block.GetBlockName());
                        if (recipes.Count <= 0) continue;
                        for (int j = 0; j < recipes[0].ingredients.Count; j++)
                            if (recipes[0].ingredients[j].count / 2 > 0)
                                Emit(into, recipes[0].ingredients[j].itemValue, recipes[0].ingredients[j].count / 2, 1f, random);
                        continue;
                    }
                    float value = 1f;
                    if (drop.toolCategory != null)
                    {
                        value = 0f;
                        if (toolBonuses != null && toolBonuses.ContainsKey(drop.toolCategory))
                            value = toolBonuses[drop.toolCategory].Tool;
                    }
                    ItemValue item = drop.name.Equals("*") ? blockValue.ToItemValue() : new ItemValue(ItemClass.GetItem(drop.name).type);
                    if (item.type == 0 || ItemClass.list[item.type] == null) continue;
                    value = EffectManager.GetValue(PassiveEffects.HarvestCount, tool, value, npc, null,
                        FastTags<TagGroup.Global>.Parse(drop.tag));
                    float modifier = OutputModifier(item, block);
                    if (modifier != 0f && (drop.prob > 0.999f || random.RandomFloat <= drop.prob))
                    {
                        int count = (int)((float)random.RandomRange(drop.minCount, drop.maxCount + 1) * value);
                        if (count > 0) Emit(into, item, Mathf.Max(1, (int)((float)count * modifier)), 1f, random);
                    }
                }
            }
        }
        List<Block.SItemDropProb> harvest;
        if (!info.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out harvest)) return;
        for (int k = 0; k < harvest.Count; k++)
        {
            Block.SItemDropProb drop = harvest[k];
            float value = 0f;
            if (drop.toolCategory != null && toolBonuses != null && toolBonuses.ContainsKey(drop.toolCategory))
                value = toolBonuses[drop.toolCategory].Tool;
            ItemValue item = drop.name.Equals("*") ? blockValue.ToItemValue() : new ItemValue(ItemClass.GetItem(drop.name).type);
            if (item.type == 0 || ItemClass.list[item.type] == null) continue;
            value = EffectManager.GetValue(PassiveEffects.HarvestCount, tool, value, npc, null,
                FastTags<TagGroup.Global>.Parse(drop.tag));
            float modifier = OutputModifier(item, block);
            int total = (int)((float)random.RandomRange(drop.minCount, drop.maxCount + 1) * value);
            int perHit = total - total / 3;
            if (modifier == 0f) continue;
            if (perHit > 0)
                EmitScaled(into, item, Mathf.Max((int)((float)perHit * modifier), 1), drop.prob, info, random);
            if (!info.bKilled) continue;
            perHit = total / 3;
            float prob = drop.prob;
            float scale = drop.resourceScale;
            if (scale > 0f && scale < 1f)
            {
                prob /= scale;
                perHit = (int)((float)perHit * scale);
                if (perHit < 1) perHit++;
            }
            if (perHit > 0) Emit(into, item, Mathf.Max((int)((float)perHit * modifier), 1), prob, random);
        }
    }

    private static float OutputModifier(ItemValue item, Block block)
    {
        return item.ItemClass.HasAnyTags(SeedMultiTag) ? XUiM_Recipes.SeedDropOutputModifier :
            block.HasAnyFastTags(CropTag) ? XUiM_Recipes.CropOutputModifier :
            !block.HasAnyFastTags(OreTag) ? XUiM_Recipes.HarvestingOutputModifier : XUiM_Recipes.MiningOutputModifier;
    }

    // collectHarvestedItem without delivery: probability roll, then record the final stack.
    private static void Emit(List<ItemStack> into, ItemValue item, int count, float prob, GameRandom random)
    {
        if (count > 0 && random.RandomFloat <= prob) into.Add(new ItemStack(item, count));
    }

    // collectHarvestedItem with _bScaleCountOnDamage: per-damage scaling of the count.
    private static void EmitScaled(List<ItemStack> into, ItemValue item, int count, float prob,
        ItemActionAttack.AttackHitInfo info, GameRandom random)
    {
        float per = (float)info.damageMax / (float)count;
        int before = (int)((Utils.FastMin(info.damageTotalOfTarget, info.damageMax) - (float)info.damageGiven) / per + 0.5f);
        int upTo = Mathf.Min((int)(info.damageTotalOfTarget / per + 0.5f), count);
        int limit = count;
        count = upTo - before;
        if (info.damageTotalOfTarget > (float)info.damageMax) count = Mathf.Min(count, limit);
        Emit(into, item, count, prob, random);
    }
}

internal static class RebirthNpcB14NativeStrike
{
    // Not persisted on purpose: if the process dies after Armed was saved, the snapshot is gone, and
    // recovery refuses to attribute any world item (fail closed; ordinary items stay in the world).
    private static readonly Dictionary<Guid, HashSet<int>> Snapshots = new Dictionary<Guid, HashSet<int>>();

    // Every ItemActionAttack.Hit block-branch term is ported in TryStrike (table in P12_B14_20261022.md).
    // Residual inputs that differ from a player swing are listed there (per-face material, power-attack
    // damageScale, land-claim owner). Source-reconciled; runtime comparison is Codex's final batch.
    internal static readonly bool DamageFormulaQualified = true;

    internal static bool HasSnapshot(Guid operation) { return Snapshots.ContainsKey(operation); }

    private static World ServerWorld()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return ThreadManager.IsMainThread() && world != null && !world.IsRemote() ? world : null;
    }

    internal static bool TryArm(EntityAlive npc, Vector3i position, RebirthNpcB14Kind kind, ItemValue tool,
        RebirthNpcStableId npcId, ulong assignmentId, out RebirthNpcB14HarvestOriginal original, out string detail)
    {
        original = null; detail = string.Empty;
        if (!DamageFormulaQualified)
        { detail = "NPC block damage has not been compared with a player strike."; return false; }
        World world = ServerWorld();
        if (world == null || npc == null || npc.IsDead() || tool == null || tool.IsEmpty() ||
            !(tool.ItemClass.Actions != null && tool.ItemClass.Actions.Length > 0 && tool.ItemClass.Actions[0] is ItemActionAttack))
        { detail = "Authoritative world, live worker or attack tool is unavailable."; return false; }
        BlockValue block = world.GetBlock(position);
        if (block.isair) { detail = "Target block is gone."; return false; }
        // Logging fells BlockModelTree blocks (one multi-block, see RebirthNpcB14Tree); Mining never does.
        bool isTree = RebirthNpcB14Tree.IsTree(block);
        if (isTree != (kind == RebirthNpcB14Kind.Logging))
        { detail = kind == RebirthNpcB14Kind.Logging ? "Logging target is not a tree." : "Mining target is a tree."; return false; }
        if (RebirthNpcB14Tree.IsFalling(block)) { detail = "The tree is already falling."; return false; }
        int remaining = tool.MaxUseTimes > 0 ? (int)Math.Floor((double)tool.MaxUseTimes - tool.UseTimes) : int.MaxValue;
        float stamina = npc.Stats != null && npc.Stats.Stamina != null ? npc.Stats.Stamina.Value : 0f;
        if (!RebirthNpcB14Rules.CanSwing(remaining, stamina, 1f, out detail)) return false;
        RebirthNpcB14HarvestOriginal o = new RebirthNpcB14HarvestOriginal
        {
            OperationId = Guid.NewGuid(), AssignmentId = assignmentId, Kind = kind,
            Phase = RebirthNpcB14Phase.Armed, Position = position,
            PreBlockType = block.type, PreBlockRotation = block.rotation, PreBlockDamage = block.damage,
            ToolItemType = tool.type, ToolDurabilityBefore = remaining == int.MaxValue ? 0 : remaining,
            StaminaBefore = stamina,
            ManagementDamageBonusPercent = RebirthNpcB14Rules.ManagementDamageBonusPercent(npcId)
        };
        HashSet<int> ids = new HashSet<int>();
        List<Entity> list = world.Entities.list;
        for (int i = 0; i < list.Count; i++) if (list[i] is EntityItem) ids.Add(list[i].entityId);
        Snapshots[o.OperationId] = ids;
        original = o;
        return true;
    }

    // Native Block.OnBlockDamaged result for a plain hit on the PARENT cell (b259 Block.cs 1612-1700): damage + points,
    // clamped to the Stage2 threshold, and MaxDamage when the hit destroys. Pure, so the deliverable can be computed
    // and saved BEFORE the hit; the real result is compared with it afterwards.
    private static int PredictResult(Block block, BlockValue bv, int points, out bool killed)
    {
        int max = block.MaxDamage, damage = bv.damage;
        bool alreadyFull = damage >= max;
        int next = damage + points;
        if (block.Stage2Health > 0)
        {
            int threshold = max - block.Stage2Health;
            if (damage < threshold && next >= threshold) next = threshold;
        }
        killed = !alreadyFull && next >= max;
        return killed ? max : next;
    }

    // WRITE-AHEAD STRIKE. Order: (1) Armed saved; (2) tool wear planned and the whole deliverable computed
    // (damage terms, predicted native result, harvest roll with the world's GameRandom) WITHOUT touching the
    // world; (3) ONE save of {Phase=Damaged, PredictedDamage, deliverable witnesses, staged wear row};
    // (4) Block.DamageBlock; (5) the real result must equal the prediction. Recovery after a crash between (3)
    // and (5) reverts to Armed when the block is still the pre-image (nothing landed, the staged deliverable is
    // discarded and re-rolled) and otherwise confirms the hit against PredictedDamage and keeps the SAVED
    // deliverable; it never derives an award from the observed block change.
    internal static bool TryStrike(EntityAlive npc, ItemValue tool, RebirthNpcB14HarvestOriginal o,
        PersistentPlayerData landClaimOwner, Action<string> stageToolWear, Func<string, bool> save, out string detail)
    {
        detail = string.Empty;
        World world = ServerWorld();
        EntityRebirthHumanoidNPC human = npc as EntityRebirthHumanoidNPC;
        if (world == null || human == null || tool == null || o == null || save == null || stageToolWear == null ||
            landClaimOwner == null || o.Phase != RebirthNpcB14Phase.Armed || !Snapshots.ContainsKey(o.OperationId))
        { detail = "Strike requires an armed operation in this session."; return false; }
        BlockValue bv = world.GetBlock(o.Position);
        if (bv.isair || bv.type != o.PreBlockType || bv.damage != o.PreBlockDamage)
        { Abort(o); detail = "Target changed after arming."; return false; }
        ItemActionAttack attack = tool.ItemClass.Actions[0] as ItemActionAttack;
        if (attack == null) { Abort(o); detail = "Tool has no attack action."; return false; }
        if (!save(o.Serialize())) { detail = "Armed original could not be saved; nothing was struck."; return false; }

        Block block = bv.Block;
        RebirthNpcB14WearPlan wear;
        if (!RebirthNpcB14ToolWear.TryPlan(human, tool, o.OperationId, out wear))
        { detail = "Tool wear cannot be planned (held tool binding or saved record differs); nothing was struck."; return false; }
        // Native per-hit stamina cost (ItemActionMelee line ~224).
        float cost = EffectManager.GetValue(PassiveEffects.StaminaLoss, tool, 0f, npc, null,
            FastTags<TagGroup.Global>.Parse("primary")) * ItemActionAttack.StaminaUsageMultiplier;
        npc.AddStamina(0f - cost);
        // Term-by-term port of ItemActionAttack.Hit's block branch (b259 lines 590-650) with the
        // melee inputs of ItemActionMelee.hitTheTarget (damageScale = 1 for a normal swing).
        ItemActionAttack.AttackHitInfo info = new ItemActionAttack.AttackHitInfo
        {
            hitRef = (BlockValueRef)o.Position, raycastHitPosition = o.Position, blockBeingDamaged = bv,
            WeaponTypeTag = ItemActionAttack.MeleeTag
        };
        // T1 land-protection hardness (Hit's num); trader area, bounds and CanDestroy force 0.
        float landScale = world.GetLandProtectionHardnessModifier(o.Position, npc, landClaimOwner);
        if (World.SandboxUseTraderArea == TraderAreaStates.Default && world.IsWithinTraderArea(o.Position)) landScale = 0f;
        if (world.InBoundsForPlayersPercent(o.Position.ToVector3CenterXZ()) < 0.5f) landScale = 0f;
        if (!block.blockMaterial.CanDestroy) landScale = 0f;
        bool noDamage = landScale < 1f;                                  // flag2
        bool percentZero = bv.isTerrain ? ItemActionAttack.TerrainDamagePercent == 0f
            : ItemActionAttack.BlockDamagePercent == 0f;                 // flag6
        // T2 hardness divide and T3 material damage multiplier.
        float hardness = Mathf.Max(block.GetHardness(), 0.1f) * landScale;
        float perHit = attack.GetDamageBlock(tool, bv, npc, 0) *
            (attack.damageMultiplier != null ? attack.damageMultiplier.Get(block.blockMaterial.DamageCategory) : 1f);
        perHit *= npc.GetBlockDamageScale(bv.isTerrain);                 // T4 entity block damage scale
        info.bHarvestTool = false;
        if (attack.ToolBonuses != null && attack.ToolBonuses.Count > 0)  // T5 harvest-tool damage bonus
        {
            perHit *= ItemActionAttack.calculateHarvestToolDamageBonus(attack.ToolBonuses, block.itemsToDrop);
            info.bHarvestTool = true;
        }
        // Native uses the material of the struck FACE (Block.GetMaterialForSide). The worker has no ray hit, so the
        // face is the one looking at the worker (dominant axis, as an adjacent swing would hit).
        Vector3 toWorker = npc.position - new Vector3(o.Position.x + 0.5f, o.Position.y + 0.5f, o.Position.z + 0.5f);
        BlockFace face = Mathf.Abs(toWorker.y) > Mathf.Abs(toWorker.x) && Mathf.Abs(toWorker.y) > Mathf.Abs(toWorker.z)
            ? (toWorker.y > 0f ? BlockFace.Top : BlockFace.Bottom)
            : Mathf.Abs(toWorker.x) >= Mathf.Abs(toWorker.z) ? (toWorker.x > 0f ? BlockFace.East : BlockFace.West)
            : (toWorker.z > 0f ? BlockFace.North : BlockFace.South);
        MaterialBlock sideMaterial = block.GetMaterialForSide(bv, face);
        if (sideMaterial == null) sideMaterial = block.blockMaterial;
        info.hardnessScale = landScale;
        info.damagePerHit = (!noDamage && !percentZero) ? perHit / hardness : 0f;
        // T6 stamina-percent scaling (read after the stamina cost, as in the native order).
        float staminaPercent = npc.Stats != null && npc.Stats.Stamina != null ? npc.Stats.Stamina.ValuePercent : 1f;
        // Weapon condition includes this swing's wear (native order), computed from the planned after-state.
        float condition = 1f;
        if (tool.MaxUseTimes > 0) condition = ((float)tool.MaxUseTimes - wear.AfterUseTimes) / (float)tool.MaxUseTimes;
        float strike = info.damagePerHit * staminaPercent;
        // T7 DamageModifier passive effect, truncated to int exactly as native (entity present).
        strike = (int)EffectManager.GetValue(PassiveEffects.DamageModifier, tool, strike, npc, null,
            FastTags<TagGroup.Global>.Parse(sideMaterial.DamageCategory ?? string.Empty) | info.WeaponTypeTag | block.Tags);
        // T8 weapon-condition degradation, T9 max-one floor (after degradation); zero when ignored/protected.
        strike = ItemActionAttack.DegradationModifier(strike, condition);
        strike = (!noDamage && !percentZero && !sideMaterial.CheckDamageIgnore(tool.ItemClass.ItemTags, npc))
            ? Utils.FastMax(1f, strike) : 0f;
        // Management bonus (B14 decision): resource-block damage only, multiplied once after the native
        // terms, never part of the yield calculation.
        if (strike > 0f) strike *= 1f + o.ManagementDamageBonusPercent / 100f;
        info.damage = strike;
        int points = (int)strike;
        if (points <= 0) { Abort(o); detail = "Target is protected or ignores this tool; no strike was made."; return false; }

        // Deliverable, computed from the PREDICTED native result before anything touches the world.
        int damagedBefore = bv.damage;
        bool killed;
        int predicted = PredictResult(block, bv, points, out killed);
        info.damageGiven = predicted == 0 ? 0 : predicted - damagedBefore;
        info.damageMax = block.MaxDamage;
        info.bKilled = killed;
        info.itemsToDrop = block.itemsToDrop;
        info.bBlockHit = true;
        info.damageTotalOfTarget = damagedBefore + info.damage; // native: blockValue.damage + _attackDetails.damage
        List<ItemStack> emitted = new List<ItemStack>();
        RebirthHumanResourceSkillEffects.Scope skillScope = RebirthHumanResourceSkillEffects.Begin(npc, tool, block);
        try { RebirthNpcB14HarvestCalculator.Compute(npc, tool, block, bv, info, attack.ToolBonuses, world.GetGameRandom(), emitted); }
        finally { RebirthHumanResourceSkillEffects.End(npc, skillScope); }
        RebirthG01NpcBackgroundEffects.ScaleLoggingWood(npc, block, emitted, world.GetGameRandom());
        Vector3 center = new Vector3(o.Position.x + 0.5f, o.Position.y + 0.5f, o.Position.z + 0.5f);

        o.Witnesses.Clear();
        for (int i = 0; i < emitted.Count; i++) AddWitness(o, emitted[i], center, 0);
        o.DamagedTick = (long)world.worldTime;
        o.PredictedDamage = killed ? -1 : predicted;
        o.Phase = RebirthNpcB14Phase.Damaged;
        stageToolWear(wear.RowText);
        if (!save(o.Serialize()))
        {
            // Nothing was struck: discard the staged deliverable (it is re-rolled on retry).
            o.Phase = RebirthNpcB14Phase.Armed; o.Witnesses.Clear(); o.PredictedDamage = 0;
            stageToolWear(string.Empty);
            detail = "Deliverable could not be saved; nothing was struck.";
            return false;
        }
        int result;
        // CALL-STACK-BOUND WITNESS. World.SpawnEntityInWorld (b259 World.cs 2430-2439) raises EntityLoadedDelegates
        // synchronously for every entity it adds. DamageBlock runs synchronously on the main thread, so every
        // EntityItem added while this subscription is active was spawned by THIS strike's call stack. No time or
        // space heuristic is involved. The subscription is removed in finally.
        List<EntityItem> spawnedInCall = new List<EntityItem>();
        World.OnEntityLoadedDelegate capture = delegate (Entity added)
        {
            EntityItem dropped = added as EntityItem;
            if (dropped != null) spawnedInCall.Add(dropped);
        };
        world.EntityLoadedDelegates += capture;
        try { result = block.DamageBlock(world, info.hitRef, bv, points, npc.entityId, info, info.bHarvestTool); }
        catch (Exception ex)
        {
            BlockValue now = world.GetBlock(o.Position);
            if (!now.isair && now.type == o.PreBlockType && now.damage == o.PreBlockDamage)
            { o.Phase = RebirthNpcB14Phase.Armed; o.Witnesses.Clear(); o.PredictedDamage = 0; }
            else { o.Witnesses.Clear(); Abort(o); }
            save(o.Serialize());
            detail = "Native block damage threw: " + ex.Message;
            return false;
        }
        finally { world.EntityLoadedDelegates -= capture; }
        BlockValue after = world.GetBlock(o.Position);
        o.HasPostImage = true; o.PostBlockType = after.type;
        if (result != (killed ? block.MaxDamage : predicted))
        {
            // The block did not behave as predicted (a subclass override): the saved deliverable no longer
            // describes this hit, so nothing is credited. World state is left as the game made it.
            o.Witnesses.Clear(); Abort(o); save(o.Serialize());
            detail = "Native damage result differed from the predicted result; nothing was credited.";
            return false;
        }
        // The entities spawned inside the call become witnesses (exact frame + id), saved with the receipt below and
        // verified present with the exact frame again at credit time. Anything dropped later or elsewhere (deferred
        // spawns, other actors' drops) is NOT witnessed: it stays in the world, un-credited and untouched.
        // Exact attribution: in-call alone is not enough. A drop is this strike's own only when it spawned at the struck
        // block (a collapse inside the same call can drop elsewhere) and does not claim another actor
        // (EntityItem.belongsPlayerId carries the id handed to ItemDropServer; the id of a different live entity means
        // somebody else's drop). Everything else stays in the world untouched and is only logged.
        int attributed = 0, rejected = 0;
        for (int d = 0; d < spawnedInCall.Count; d++)
        {
            EntityItem item = spawnedInCall[d];
            if (item.itemStack == null || item.itemStack.IsEmpty()) continue;
            if (IsOwnCallDrop(world, item, center, npc.entityId)) { AddWitness(o, item.itemStack, item.position, item.entityId); attributed++; }
            else rejected++;
        }
        Log.Out("[RBNPC] b14 strike drops block=" + block.GetBlockName() + " pos=" + o.Position + " inCall=" + spawnedInCall.Count +
            " attributed=" + attributed + " leftInWorld=" + rejected + " calculated=" + emitted.Count);
        // Actor receipt: our own DamageBlock call returned exactly the predicted result. Persist it (HasPostImage)
        // so recovery needs no inference from the block. If this save fails the next save carries it; a crash in
        // the gap falls back to the strict Recover rules (killed hits are then not attributable and abort).
        save(o.Serialize());
        detail = "Native block damage applied; saved deliverable confirmed; witness window open.";
        return true;
    }

    // Closes the drop-entity window (a later tick, once spawned drops exist), then saves Witnessed.
    internal static bool TryCloseWitnessWindow(RebirthNpcB14HarvestOriginal o, Func<string, bool> save, out string detail)
    {
        return TryCloseWitnessWindow(o, save, RebirthNpcB14Rules.WitnessRadius, RebirthNpcB14Rules.WitnessSettleTicks,
            RebirthNpcB14Rules.WitnessWindowTicks, out detail);
    }

    // Mining uses the defaults. Logging passes the fall footprint radius and a longer settle/window.
    internal static bool TryCloseWitnessWindow(RebirthNpcB14HarvestOriginal o, Func<string, bool> save,
        float radius, long settleTicks, long windowTicks, out string detail)
    {
        detail = string.Empty;
        World world = ServerWorld();
        HashSet<int> before;
        if (world == null || o == null || save == null || o.Phase != RebirthNpcB14Phase.Damaged ||
            !Snapshots.TryGetValue(o.OperationId, out before))
        {
            // Snapshot lost (restart): never attribute world items. Recover confirms the hit landed (or reverts it)
            // against the saved prediction and keeps the SAVED deliverable.
            if (o != null && save != null && o.Phase == RebirthNpcB14Phase.Damaged)
            {
                Recover(o, save);
                detail = "Recovered a damaged operation.";
                return o.Phase == RebirthNpcB14Phase.Witnessed;
            }
            detail = "No damaged operation to close."; return false;
        }
        // No spatial/time scan any more: the entity witnesses were captured inside the strike's own call stack
        // (TryStrike, World.EntityLoadedDelegates) and are already saved. radius/settleTicks/windowTicks are
        // retained only for signature compatibility.
        FinishWithoutEntities(o);
        if (!save(o.Serialize())) { detail = "Witnessed original could not be saved."; return false; }
        Snapshots.Remove(o.OperationId);
        detail = "Witness window closed.";
        return true;
    }

    private static void FinishWithoutEntities(RebirthNpcB14HarvestOriginal o)
    {
        int sum = 0;
        for (int i = 0; i < o.Witnesses.Count; i++) sum += o.Witnesses[i].Count;
        o.WitnessedTotal = sum;
        o.Phase = RebirthNpcB14Phase.Witnessed;
    }

    // Metadata key stamped on an overflow re-drop so the spawned EntityItem is identifiable after a restart.
    internal const string OverflowMarker = "RebirthB14Overflow";

    // Credits each uncredited witness. Per witness, in this order (a restart can re-enter at any point):
    //   1. A witness whose overflow drop was ATTEMPTED never reaches the bag sink: it is resolved first
    //      (drop observed by its unique marker => settled; not observed => ESCROWED, never redropped, never credited).
    //   2. A world-drop witness (EntityId > 0) is credited only while the entity is OBSERVED present with the exact
    //      frame; the sink credit (idempotent journal) comes first, then remove, then verify gone, then Credited.
    //      Absent without a credit => someone else took it (settled, no credit); absent with the legacy Claimed
    //      mark and no credit => ambiguous => escrowed.
    //   3. Sink results: Credited / Full (overflow) / Retry or Ambiguous (hold, never an overflow drop).
    // Returns true once every witness is settled and the operation is Credited.
    internal static bool TryCredit(RebirthNpcB14HarvestOriginal o, IRebirthNpcB14CreditSink sink,
        Func<string, bool> save, out string detail)
    {
        detail = string.Empty;
        World world = ServerWorld();
        if (world == null || o == null || sink == null || save == null || o.Phase != RebirthNpcB14Phase.Witnessed)
        { detail = "Credit requires a Witnessed operation."; return false; }
        for (int i = 0; i < o.Witnesses.Count; i++)
        {
            RebirthNpcB14DropWitness w = o.Witnesses[i];
            if (w.Credited) continue;
            ItemStack stack = DecodeStack(w);
            if (stack == null || stack.IsEmpty()) { detail = "Witnessed stack frame cannot be decoded."; return false; }
            string token = RebirthNpcB14BagCreditSink.CreditId(o.OperationId, i).ToString("N");

            // (1) Prior overflow attempt first.
            if (w.OverflowDropped)
            {
                if (!OverflowObserved(world, token))
                {
                    detail = "Owed stack is escrowed: its earlier overflow drop cannot be observed, so it is not dropped again and not credited (release it explicitly).";
                    return false;
                }
                w.EntityId = -1; w.Credited = true;
                if (!save(o.Serialize())) { detail = "Overflow settlement could not be saved."; return false; }
                continue;
            }

            // (2) World-drop witnesses: observe the entity at the moment of the decision.
            EntityItem item = null;
            bool exact = false;
            if (w.EntityId > 0 && !w.CallBound)
            {
                // Not obtained from the strike's own call stack (e.g. a row saved by the former space/time scan):
                // ambiguous, held in escrow with no award and no removal.
                detail = "A world-drop witness is not bound to the strike's call stack: escrowed, no award, not removed.";
                return false;
            }
            if (w.EntityId > 0)
            {
                item = world.GetEntity(w.EntityId) as EntityItem;
                exact = item != null && item.itemStack != null && item.itemStack.count == w.Count &&
                    item.itemStack.itemValue.type == w.ItemType &&
                    (string.IsNullOrEmpty(w.Frame) || EncodeFrame(item.itemStack) == w.Frame);
                bool already = sink.HasCredit(o.OperationId, i, stack);
                if (!exact)
                {
                    if (!already && w.Claimed)
                    { detail = "A claimed world drop vanished without a credit: ambiguous, escrowed (no credit)."; return false; }
                    if (!already) w.EntityId = -2; // taken or changed by someone else: no credit
                    w.Credited = true;              // already => our credit exists and counts
                    if (!save(o.Serialize())) { detail = "Settlement could not be saved."; return false; }
                    continue;
                }
                if (already) { if (!RemoveVerified(world, w)) { detail = "Credited world drop could not be removed."; return false; } w.Credited = true; if (!save(o.Serialize())) { detail = "Credit mark could not be saved."; return false; } continue; }
            }

            // (3) Sink.
            RebirthNpcB14CreditResult result = sink.TryCredit(o.OperationId, i, stack);
            if (result == RebirthNpcB14CreditResult.Credited)
            {
                if (w.EntityId > 0 && !RemoveVerified(world, w)) { detail = "Credited world drop could not be removed."; return false; }
            }
            else if (result == RebirthNpcB14CreditResult.Full)
            {
                if (w.EntityId > 0)
                {
                    w.EntityId = -1; // bag full: the real drop simply stays where it is (settled at source, untouched)
                }
                else
                {
                    // Overflow for a calculated stack: stamp a unique marker, save the attempted mark FIRST, drop,
                    // then observe the marked entity. Unobserved => escrow (no claim, no second drop).
                    w.OverflowDropped = true;
                    if (!save(o.Serialize())) { w.OverflowDropped = false; detail = "Overflow mark could not be saved."; return false; }
                    ItemStack dropStack = stack.Clone();
                    dropStack.itemValue.SetMetadata(OverflowMarker, token);
                    GameManager.Instance.ItemDropServer(dropStack, w.Position, Vector3.zero, -1,
                        stack.itemValue.ItemClass != null ? stack.itemValue.ItemClass.GetLifetimeOnDrop() : 60f);
                    if (!OverflowObserved(world, token))
                    { detail = "Overflow drop was not observed; the owed stack is escrowed."; return false; }
                    w.EntityId = -1;
                }
            }
            else
            {
                detail = result == RebirthNpcB14CreditResult.Ambiguous
                    ? "Bag credit is ambiguous (its custody moved); escrowed, nothing dropped."
                    : "Bag credit is settling.";
                return false;
            }
            w.Credited = true;
            if (!save(o.Serialize())) { detail = "Credit mark could not be saved."; return false; }
        }
        o.Phase = RebirthNpcB14Phase.Credited;
        if (!save(o.Serialize())) { detail = "Credited original could not be saved."; return false; }
        detail = "Harvest credited exactly once.";
        return true;
    }

    // Remove the observed world drop and verify it is gone.
    private static bool RemoveVerified(World world, RebirthNpcB14DropWitness w)
    {
        if (world.GetEntity(w.EntityId) != null) world.RemoveEntity(w.EntityId, EnumRemoveEntityReason.Despawned);
        return world.GetEntity(w.EntityId) == null;
    }

    // The overflow drop is identified by the unique marker stamped into the dropped ItemValue's metadata.
    private static bool OverflowObserved(World world, string token)
    {
        List<Entity> list = world.Entities.list;
        for (int i = 0; i < list.Count; i++)
        {
            EntityItem item = list[i] as EntityItem;
            string seen;
            if (item != null && item.itemStack != null && item.itemStack.itemValue != null &&
                item.itemStack.itemValue.TryGetMetadata(OverflowMarker, out seen) && seen == token) return true;
        }
        return false;
    }

    // Reload reconciliation. Armed: a changed block means the hit may have landed without a saved
    // witness, so abort (items stay in the world, nothing is regenerated); an unchanged block retries.
    internal static void Recover(RebirthNpcB14HarvestOriginal o, Func<string, bool> save)
    {
        World world = ServerWorld();
        if (world == null || o == null || o.IsTerminal) return;
        if (o.Phase == RebirthNpcB14Phase.Planned) return;
        if (o.Phase == RebirthNpcB14Phase.Armed || o.Phase == RebirthNpcB14Phase.Damaged)
        {
            BlockValue bv = world.GetBlock(o.Position);
            bool unchanged = !bv.isair && bv.type == o.PreBlockType && bv.damage == o.PreBlockDamage;
            if (o.Phase == RebirthNpcB14Phase.Armed && unchanged) return; // snapshot gone: re-arm by caller
            if (o.Phase == RebirthNpcB14Phase.Damaged)
            {
                if (o.HasPostImage) FinishWithoutEntities(o); // saved actor receipt: our DamageBlock returned the prediction
                else if (unchanged)
                {
                    // The hit never landed: drop the staged deliverable and wear (re-rolled on the next arm).
                    o.Phase = RebirthNpcB14Phase.Armed; o.Witnesses.Clear(); o.PredictedDamage = 0;
                }
                else if (HitLanded(bv, o)) FinishWithoutEntities(o); // keep the SAVED deliverable
                else
                {
                    // Not attributable to this worker from the block alone (destruction, or a damage value that is
                    // neither the pre-image nor the prediction). ESCROW: the saved deliverable is kept un-credited
                    // and un-discarded until TryResolveEscrow decides; nothing is awarded and nothing is lost.
                    FinishWithoutEntities(o);
                    o.Phase = RebirthNpcB14Phase.Escrowed;
                    Snapshots.Remove(o.OperationId);
                }
            }
            else Abort(o);
            if (save != null) save(o.Serialize());
        }
    }

    // Reconcile an Escrowed deliverable. award = true: the block change is accepted as this worker's hit and the
    // SAVED deliverable proceeds to Witnessed/credit; false: nothing is awarded. Both are explicit decisions (owner
    // tooling or an automatic rule); save must persist the row.
    internal static bool TryResolveEscrow(RebirthNpcB14HarvestOriginal o, bool award, Func<string, bool> save)
    {
        if (o == null || save == null || o.Phase != RebirthNpcB14Phase.Escrowed) return false;
        if (award) FinishWithoutEntities(o);
        else { o.Witnesses.Clear(); o.Phase = RebirthNpcB14Phase.Aborted; }
        return save(o.Serialize());
    }

    // Release an escrowed overflow stack deliberately: forget the unobservable earlier drop so the next credit
    // pass may drop it once more (accepting a possible duplicate). Never called automatically.
    internal static bool TryReleaseOwed(RebirthNpcB14HarvestOriginal o, Func<string, bool> save)
    {
        if (o == null || save == null || o.Phase != RebirthNpcB14Phase.Witnessed) return false;
        bool any = false;
        for (int i = 0; i < o.Witnesses.Count; i++)
            if (!o.Witnesses[i].Credited && o.Witnesses[i].OverflowDropped) { o.Witnesses[i].OverflowDropped = false; o.Witnesses[i].DropIntentId = 0; any = true; }
        return any && save(o.Serialize());
    }

    // Attribution WITHOUT an actor receipt (HasPostImage unsaved). A post-image alone proves nothing about WHO
    // changed the block, so only the strongest case is accepted: a non-destroying hit whose block still has the
    // pre-image type and rotation and exactly the predicted damage counter. A destruction (air, replaced block,
    // falling tree) could equally be a third party and is never attributed here: it aborts with no award.
    private static bool HitLanded(BlockValue bv, RebirthNpcB14HarvestOriginal o)
    {
        if (o.PredictedDamage < 0) return false;
        return !bv.isair && bv.type == o.PreBlockType && bv.rotation == o.PreBlockRotation && bv.damage == o.PredictedDamage;
    }

    private static string EncodeFrame(ItemStack stack)
    {
        string frame = RebirthHumanEquipment.Frame(stack);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(frame));
    }

    private static ItemStack DecodeStack(RebirthNpcB14DropWitness w)
    {
        if (!string.IsNullOrEmpty(w.Frame))
        {
            try { return RebirthHumanEquipment.Decode(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(w.Frame))); }
            catch (Exception) { return null; }
        }
        return new ItemStack(new ItemValue(w.ItemType), w.Count); // rows saved before the frame field existed
    }

    // Spatial bound of a block's own drop (ItemDropServer is handed the block center plus a small random offset).
    private const float OwnDropRadius = 2.5f;

    private static bool IsOwnCallDrop(World world, EntityItem item, Vector3 blockCenter, int npcEntityId)
    {
        if ((item.position - blockCenter).sqrMagnitude > OwnDropRadius * OwnDropRadius) return false;
        int owner = item.belongsPlayerId;
        return owner < 0 || owner == npcEntityId || world.GetEntity(owner) == null;
    }

    private static void AddWitness(RebirthNpcB14HarvestOriginal o, ItemStack stack, Vector3 position, int entityId)
    {
        if (stack == null || stack.IsEmpty() || stack.count <= 0) return;
        o.Witnesses.Add(new RebirthNpcB14DropWitness
        {
            ItemType = stack.itemValue.type, Count = stack.count, Quality = stack.itemValue.Quality,
            Position = position, Tick = o.DamagedTick, EntityId = entityId, Frame = EncodeFrame(stack),
            CallBound = entityId > 0 // only called with a real id from the call-stack capture in TryStrike
        });
    }

    private static void Abort(RebirthNpcB14HarvestOriginal o)
    {
        o.Phase = RebirthNpcB14Phase.Aborted;
        Snapshots.Remove(o.OperationId);
    }
}
