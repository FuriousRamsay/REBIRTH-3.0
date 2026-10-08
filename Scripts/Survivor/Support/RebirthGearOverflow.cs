using System;
using System.Collections.Generic;
using UnityEngine;

// Called only from authoritative gear transactions, before reducing capacity.
public static class RebirthGearOverflow
{
    public const float ToolbeltLifetimeSeconds = 1800f;
    public const string BackpackEntity = "rebirthGearRecoveryBackpack";

    public static bool TryRelease(EntityPlayer player, string slot, int bagCapacity, int beltCapacity, IList<ItemStack> sectionOverflow = null)
    {
        if (player == null || player.world == null || player.world.IsRemote()) return false;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        // Native ItemDropServer silently returns when its world is absent. Never clear
        // inventory based on that no-op, or send recovery into a replacement world.
        if (connection == null || !connection.IsServer || player.world.gameManager == null ||
            GameManager.Instance == null || !ReferenceEquals(player.world.gameManager, GameManager.Instance) ||
            !ReferenceEquals(GameManager.Instance.World, player.world)) return false;
        try
        {
            if (slot == RebirthSurvivorGearService.BackpackSlotId)
            {
                ItemStack[] items = player.bag.ItemGrid.items;
                var contents = new List<ItemStack>();
                if(sectionOverflow!=null)foreach(var item in sectionOverflow)
                    if(item!=null&&!item.IsEmpty())contents.Add(item.Clone());
                for (int i = bagCapacity; i < items.Length; i++)
                    if (items[i] != null && !items[i].IsEmpty()) contents.Add(items[i].Clone());
                if (contents.Count == 0) return true;
                // Create and populate one native lootable bag before clearing any source.
                var bag = EntityFactory.CreateEntity(BackpackEntity.GetHashCode(), player.GetPosition() + Vector3.up,
                    Vector3.zero) as EntityLootContainer;
                if (bag == null) return false;
                bag.SetContent(contents.ToArray());
                bag.spawnById = player.entityId;
                player.world.SpawnEntityInWorld(bag);
                for (int i = bagCapacity; i < items.Length; i++) player.bag.SetSlot(i, ItemStack.Empty.Clone());
            }
            else if (slot == RebirthSurvivorGearService.BeltSlotId)
            {
                ItemStack[] items = player.inventory.ItemGrid.items;
                // Every installed ItemGrid slot contains owned storage; Hand transient state is separate.
                int limit = Math.Min(RebirthToolbeltCapacity.BackingPublicSlots, items.Length);
                for (int i = beltCapacity; i < limit; i++)
                {
                    if (items[i] == null || items[i].IsEmpty()) continue;
                    player.world.gameManager.ItemDropServer(items[i].Clone(), player.GetPosition() + Vector3.up,
                        Vector3.zero, player.entityId, ToolbeltLifetimeSeconds, false);
                    player.inventory.SetItem(i, ItemStack.Empty.Clone());
                }
                // A selected slot must remain inside the new public range.
                if (player.inventory.holdingItemIdx >= beltCapacity) player.inventory.SetHoldingItemIdx(0);
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Gear] Capacity release failed; gear retained: " + ex.Message);
            return false;
        }
    }
}
