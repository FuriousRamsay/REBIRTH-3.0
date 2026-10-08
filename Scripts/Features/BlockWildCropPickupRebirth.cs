using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Always-enabled REBIRTH interaction for mature wild/naturally generated crops.
/// Uses the native block activation path, so the player's rebound action key is respected.
/// Player-grown *3HarvestPlayer blocks are intentionally excluded by XML registration.
/// </summary>
public class BlockWildCropPickupRebirth : BlockPlantGrowing
{
    private static readonly BlockActivationCommand[] PickupCommands =
    {
        new BlockActivationCommand("take", "hand", true)
    };

    public override bool HasBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        return CanPickupWildCrop(world, blockValue, blockPos, entityFocusing as EntityPlayer);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        return CanPickupWildCrop(world, blockValue, blockPos, entityFocusing as EntityPlayer)
            ? PickupCommands
            : BlockActivationCommand.Empty;
    }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        EntityPlayer player = entityFocusing as EntityPlayer;
        RebirthWildCropPickupYield preview;
        if (!TryBuildYield(world, blockValue, blockPos, player, false, out preview))
            return null;

        string itemKey = preview.ItemValue.ItemClass != null
            ? preview.ItemValue.ItemClass.GetItemName()
            : string.Empty;
        string itemName = Localization.Get(itemKey);

        return string.Format(Localization.Get("xuiRebirthPickupWildCrop"), preview.Count, itemName);
    }

    public override bool OnBlockActivated(string commandName, WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (!string.Equals(commandName, "take", StringComparison.Ordinal))
            return false;

        return Pickup(world, blockPos, blockValue, player);
    }

    public override bool OnBlockActivated(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        return Pickup(world, blockPos, blockValue, player);
    }

    private bool Pickup(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (!CanPickupWildCrop(world, blockValue, blockPos, player))
            return false;

        RebirthWildCropPickupYield yield;
        if (!TryBuildYield(world, blockValue, blockPos, player, true, out yield))
            return false;

        ItemStack stack = new ItemStack(yield.ItemValue, yield.Count);
        ItemStack uiStack = stack.Clone();

        // Commit the authoritative world removal first and verify it before granting the item.
        // This avoids duplicating crops when SetBlockRPC fails or the target changed underneath us.
        BlockValue live=world.GetBlock(blockPos);
        if(live.type!=blockValue.type||live.damage!=blockValue.damage||live.meta!=blockValue.meta||live.meta2!=blockValue.meta2||live.rotation!=blockValue.rotation)return false;
        world.SetBlockRPC(blockPos, BlockValue.Air);
        if(!world.GetBlock(blockPos).isair)return false;

        if (!RebirthUtilities.TryGiveItemToPlayerOrDrop(world, player, stack, blockPos))
        {
            // Clean grant failure: restore only while our committed Air is still present.
            if(world.GetBlock(blockPos).isair)world.SetBlockRPC(blockPos,blockValue);
            return false;
        }

        // The item+world commit is complete. Presentation/event failures must not make the caller
        // retry the grant, so these are best-effort after the conservation boundary.
        try{player.PlayOneShot("item_plant_pickup", false);}catch{}
        try{QuestEventManager.Current.BlockPickedUp(blockValue.Block.GetBlockName(), blockPos);}catch{}
        try{player.AddUIHarvestingItem(uiStack, false);}catch{}
        return true;
    }

    private bool CanPickupWildCrop(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityPlayer player)
    {
        if (world == null || player == null || blockValue.Block == null)
            return false;

        string name = blockValue.Block.GetBlockName();
        if (string.IsNullOrEmpty(name) || name.IndexOf("3HarvestPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        World concreteWorld = world as World;
        if (concreteWorld != null && concreteWorld.IsWithinTraderArea(blockPos))
            return false;

        RebirthWildCropPickupYield ignored;
        return TryBuildYield(world, blockValue, blockPos, player, false, out ignored);
    }

    private bool TryBuildYield(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityPlayer player, bool rollRandom, out RebirthWildCropPickupYield result)
    {
        result = default(RebirthWildCropPickupYield);
        Block block = blockValue.Block;
        if (block == null || block.itemsToDrop == null)
            return false;

        List<Block.SItemDropProb> drops;
        if (!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out drops) || drops == null)
            return false;

        for (int i = 0; i < drops.Count; i++)
        {
            Block.SItemDropProb drop = drops[i];
            if (string.IsNullOrEmpty(drop.name) || drop.prob <= 0f)
                continue;
            // Pickup resolves the first harvest entry whose probability succeeds. Preview uses
            // the first potentially-successful entry without consuming RNG; grant performs the roll.
            if(rollRandom && drop.prob < 0.9990001f && GameUtils.random.RandomFloat > drop.prob)
                continue;

            ItemValue itemValue = drop.name.Equals("*")
                ? blockValue.ToItemValue()
                : ItemClass.GetItem(drop.name);

            if (itemValue.IsEmpty())
                continue;

            int baseCount = rollRandom
                ? GameUtils.random.RandomRange(drop.minCount, drop.maxCount + 1)
                : drop.minCount;

            ItemValue heldItem = player.inventory != null ? player.inventory.holdingItemItemValue : ItemValue.None;
            float multiplier = EffectManager.GetValue(
                PassiveEffects.HarvestCount,
                heldItem,
                1f,
                player,
                tags: FastTags<TagGroup.Global>.Parse(drop.tag));

            int count = (int)(baseCount * multiplier);


            if (count < 1)
                count = 1;

            result = new RebirthWildCropPickupYield(itemValue, count);
            return true;
        }

        return false;
    }
}

public struct RebirthWildCropPickupYield
{
    public readonly ItemValue ItemValue;
    public readonly int Count;

    public RebirthWildCropPickupYield(ItemValue itemValue, int count)
    {
        ItemValue = itemValue;
        Count = count;
    }
}
