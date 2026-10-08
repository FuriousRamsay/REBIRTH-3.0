using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

#nullable disable

/// <summary>
/// Vanilla-equivalent replacement for GameUtils.HarvestOnAttack.
/// 
/// This is intentionally copied from 2.6 b10 source semantics and calls the vanilla
/// collectHarvestedItem helper so inventory, drops, quest events, and XP remain vanilla.
/// </summary>
public static class RebirthAttackHarvestReplacementService
{
    public static bool TryRunVanillaEquivalent(
        ItemActionData actionData,
        Dictionary<string, ItemActionAttack.Bonuses> toolBonuses,
        out RebirthAttackHarvestReplacementResultKind result)
    {
        result = RebirthAttackHarvestReplacementResultKind.Unknown;
        bool replacementEffectsStarted = false;

        try
        {
            if (actionData == null)
            {
                result = RebirthAttackHarvestReplacementResultKind.NullActionData;
                return false;
            }

            if (actionData.invData == null || actionData.invData.world == null)
            {
                result = RebirthAttackHarvestReplacementResultKind.NullActionData;
                return false;
            }

            if (actionData.invData.world.IsEditor())
            {
                result = RebirthAttackHarvestReplacementResultKind.EditorWorld;
                return false;
            }

            if (!(actionData.invData.holdingEntity is EntityPlayerLocal))
            {
                result = RebirthAttackHarvestReplacementResultKind.NotLocalPlayer;
                return false;
            }

            if (actionData.attackDetails == null)
            {
                result = RebirthAttackHarvestReplacementResultKind.MissingAttackDetails;
                return false;
            }

            if (actionData.attackDetails.itemsToDrop == null)
            {
                result = RebirthAttackHarvestReplacementResultKind.MissingDrops;
                return false;
            }

            EnsureRandom();

            int destroyDropEvents = 0;
            int harvestDropEvents = 0;

            Block block = actionData.attackDetails.blockBeingDamaged.Block;
            // From this point forward replacement-owned state may be changed. Any exception after
            // this boundary is handled by suppressing native fallback, because native replay could
            // duplicate awards/events or observe partially rewritten attack details.
            replacementEffectsStarted = true;
            if (block.RepairItemsMeshDamage != null)
            {
                BlockValue blockBeingDamaged = actionData.attackDetails.blockBeingDamaged;
                blockBeingDamaged.damage += actionData.attackDetails.damageGiven;
                actionData.attackDetails.bKilled = blockBeingDamaged.damage < block.MaxDamage
                    && block.shape.UseRepairDamageState(blockBeingDamaged);
            }

            if (actionData.attackDetails.bKilled)
            {
                if (!actionData.attackDetails.itemsToDrop.ContainsKey(EnumDropEvent.Destroy))
                {
                    if (!actionData.attackDetails.blockBeingDamaged.isair && actionData.attackDetails.bBlockHit)
                    {
                        ItemValue itemValue = actionData.attackDetails.blockBeingDamaged.ToItemValue();
                        GameUtils.collectHarvestedItem(actionData, itemValue, 1, 1f, false);
                        destroyDropEvents++;
                    }
                }
                else
                {
                    List<Block.SItemDropProb> destroyDrops = actionData.attackDetails.itemsToDrop[EnumDropEvent.Destroy];
                    for (int i = 0; i < destroyDrops.Count; i++)
                    {
                        Block.SItemDropProb drop = destroyDrops[i];
                        if (actionData.attackDetails.bBlockHit && drop.name.Equals("[recipe]"))
                        {
                            List<Recipe> recipes = CraftingManager.GetRecipes(actionData.attackDetails.blockBeingDamaged.Block.GetBlockName());
                            if (recipes.Count > 0)
                            {
                                for (int r = 0; r < recipes[0].ingredients.Count; r++)
                                {
                                    if (recipes[0].ingredients[r].count / 2 > 0)
                                    {
                                        int vanillaRecipeCount = recipes[0].ingredients[r].count / 2;
                                        int adjustedRecipeCount = RebirthAttackHarvestBonusPolicy.ApplyDestroyBonus(vanillaRecipeCount);
                                        RebirthAttackHarvestCounters.RecordBonusAdjustment(vanillaRecipeCount, adjustedRecipeCount);
                                        GameUtils.collectHarvestedItem(
                                            actionData,
                                            recipes[0].ingredients[r].itemValue,
                                            adjustedRecipeCount,
                                            1f,
                                            false);
                                        destroyDropEvents++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            float originalValue = 1f;
                            if (drop.toolCategory != null)
                            {
                                originalValue = 0.0f;
                                if (toolBonuses != null && toolBonuses.ContainsKey(drop.toolCategory))
                                    originalValue = toolBonuses[drop.toolCategory].Tool;
                            }

                            float countMultiplier = EffectManager.GetValue(
                                PassiveEffects.HarvestCount,
                                actionData.invData.itemValue,
                                originalValue,
                                actionData.invData.holdingEntity,
                                tags: FastTags<TagGroup.Global>.Parse(drop.tag));

                            ItemValue itemValue = drop.name.Equals("*")
                                ? actionData.attackDetails.blockBeingDamaged.ToItemValue()
                                : new ItemValue(ItemClass.GetItem(drop.name).type);

                            if (itemValue.type != 0
                                && ItemClass.list[itemValue.type] != null
                                && ((double)drop.prob > 0.99900001287460327 || (double)GameUtils.random.RandomFloat <= (double)drop.prob))
                            {
                                int count = (int)((double)GameUtils.random.RandomRange(drop.minCount, drop.maxCount + 1) * (double)countMultiplier);
                                if (count > 0)
                                {
                                    int adjustedCount = RebirthAttackHarvestBonusPolicy.ApplyDestroyBonus(count);
                                    RebirthAttackHarvestCounters.RecordBonusAdjustment(count, adjustedCount);
                                    GameUtils.collectHarvestedItem(actionData, itemValue, adjustedCount, 1f, false);
                                    destroyDropEvents++;
                                }
                            }
                        }
                    }
                }
            }

            if (actionData.attackDetails.bBlockHit)
            {
                actionData.invData.holdingEntity.MinEventContext.BlockValue = actionData.attackDetails.blockBeingDamaged;
                actionData.invData.holdingEntity.FireEvent(MinEventTypes.onSelfHarvestBlock);
            }
            else
            {
                actionData.invData.holdingEntity.MinEventContext.Other = actionData.attackDetails.entityHit as EntityAlive;
                actionData.invData.holdingEntity.FireEvent(MinEventTypes.onSelfHarvestOther);
            }

            if (actionData.attackDetails.itemsToDrop.ContainsKey(EnumDropEvent.Harvest))
            {
                List<Block.SItemDropProb> harvestDrops = actionData.attackDetails.itemsToDrop[EnumDropEvent.Harvest];
                for (int i = 0; i < harvestDrops.Count; i++)
                {
                    Block.SItemDropProb drop = harvestDrops[i];

                    float originalValue = 0.0f;
                    if (drop.toolCategory != null)
                    {
                        originalValue = 0.0f;
                        if (toolBonuses != null && toolBonuses.ContainsKey(drop.toolCategory))
                            originalValue = toolBonuses[drop.toolCategory].Tool;
                    }

                    ItemValue itemValue = drop.name.Equals("*")
                        ? actionData.attackDetails.blockBeingDamaged.ToItemValue()
                        : new ItemValue(ItemClass.GetItem(drop.name).type);

                    if (itemValue.type != 0 && ItemClass.list[itemValue.type] != null)
                    {
                        float countMultiplier = EffectManager.GetValue(
                            PassiveEffects.HarvestCount,
                            actionData.invData.itemValue,
                            originalValue,
                            actionData.invData.holdingEntity,
                            tags: FastTags<TagGroup.Global>.Parse(drop.tag));

                        int totalCount = (int)((double)GameUtils.random.RandomRange(drop.minCount, drop.maxCount + 1) * (double)countMultiplier);
                        int countBeforeKillSplit = totalCount - totalCount / 3;

                        if (countBeforeKillSplit > 0)
                        {
                            int adjustedCountBeforeKillSplit = RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(countBeforeKillSplit);
                            RebirthAttackHarvestCounters.RecordBonusAdjustment(countBeforeKillSplit, adjustedCountBeforeKillSplit);
                            GameUtils.collectHarvestedItem(actionData, itemValue, adjustedCountBeforeKillSplit, drop.prob);
                            harvestDropEvents++;
                        }

                        if (actionData.attackDetails.bKilled)
                        {
                            int killCount = totalCount / 3;
                            float probability = drop.prob;
                            float resourceScale = drop.resourceScale;

                            if ((double)resourceScale > 0.0 && (double)resourceScale < 1.0)
                            {
                                probability /= resourceScale;
                                killCount = (int)((double)killCount * (double)resourceScale);
                                if (killCount < 1)
                                    ++killCount;
                            }

                            if (killCount > 0)
                            {
                                int adjustedKillCount = RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(killCount);
                                RebirthAttackHarvestCounters.RecordBonusAdjustment(killCount, adjustedKillCount);
                                GameUtils.collectHarvestedItem(actionData, itemValue, adjustedKillCount, probability, false);
                                harvestDropEvents++;
                            }
                        }
                    }
                }
            }

            bool blockHit = actionData.attackDetails.bBlockHit;
            actionData.attackDetails.blockBeingDamaged = BlockValue.Air;

            RebirthAttackHarvestCounters.RecordReplacementSuccess(destroyDropEvents, harvestDropEvents, blockHit);
            result = RebirthAttackHarvestReplacementResultKind.ReplacedVanillaEquivalent;
            return true;
        }
        catch
        {
            if (replacementEffectsStarted)
            {
                result = RebirthAttackHarvestReplacementResultKind.ExceptionAfterEffectsSuppressed;
                return true;
            }
            RebirthAttackHarvestCounters.RecordReplacementFallback();
            result = RebirthAttackHarvestReplacementResultKind.ExceptionFallbackToVanilla;
            return false;
        }
    }

    private static void EnsureRandom()
    {
        if (GameUtils.random != null)
            return;

        GameUtils.random = GameRandomManager.Instance.CreateGameRandom();
        GameUtils.random.SetSeed((int)Stopwatch.GetTimestamp());
    }

    public static string GetSafetyReport()
    {
        return "[RebirthAttackHarvestReplacementService] vanilla-equivalent replacement; pre-effect exceptions may fall back to native; post-effect exceptions suppress native replay to prevent duplicate rewards/events.";
    }
}
