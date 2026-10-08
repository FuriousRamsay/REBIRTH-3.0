partial class TraceSlot { public void Pickup(){ int count=ItemStack.count; if (xui.PlayerInventory.AddItem(ItemStack))
			{
				PlayPlaceSound();
				ItemStack = ItemStack.Empty;
				HandleSlotChangeEvent();
			}
			else if (count != ItemStack.count)
			{
				PlayPlaceSound();
				if (ItemStack.count == 0)
				{
					ItemStack = ItemStack.Empty;
				}
				HandleSlotChangeEvent();
				return;
			}
			 }}
