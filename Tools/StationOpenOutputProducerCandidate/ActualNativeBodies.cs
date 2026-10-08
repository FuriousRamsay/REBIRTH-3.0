partial class XUiC_RecipeStack{public bool outputStack()
	{
		if (recipe == null)
		{
			return false;
		}
		EntityPlayerLocal entityPlayer = xui.playerUI.entityPlayer;
		if (entityPlayer == null)
		{
			return false;
		}
		if (originalItem == null || originalItem.IsEmpty())
		{
			outputItemValue = new ItemValue(recipe.itemValueType, outputQuality, outputQuality);
			ItemClass itemClass = outputItemValue.ItemClass;
			if (outputItemValue == null)
			{
				return false;
			}
			if (itemClass == null)
			{
				return false;
			}
			if (entityPlayer.entityId == startingEntityId)
			{
				giveExp(outputItemValue, itemClass);
			}
			else
			{
				(windowGroup.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData.TileEntity.AddCraftComplete(startingEntityId, outputItemValue, recipe.GetName(), recipe.IsScrap ? recipe.ingredients[0].itemValue.ItemClass.GetItemName() : "", recipe.craftExpGain, recipe.count);
			}
			if (recipe.GetName().Equals("meleeToolRepairT0StoneAxe"))
			{
				PlatformManager.NativePlatform.AchievementManager?.SetAchievementStat(EnumAchievementDataStat.StoneAxeCrafted, 1);
			}
			else if (recipe.GetName().Equals("frameShapes:VariantHelper"))
			{
				PlatformManager.NativePlatform.AchievementManager?.SetAchievementStat(EnumAchievementDataStat.WoodFrameCrafted, 1);
			}
		}
		else if (amountToRepair > 0)
		{
			ItemValue itemValue = originalItem.Clone();
			ItemAction.HandleDegradation(itemValue, xui.playerUI.entityPlayer);
			itemValue.UseTimes -= amountToRepair;
			_ = itemValue.ItemClass;
			if (itemValue.UseTimes < 0f)
			{
				itemValue.UseTimes = 0f;
			}
			outputItemValue = itemValue.Clone();
			QuestEventManager.Current.RepairedItem(outputItemValue);
			amountToRepair = 0;
		}
		if (outputItemValue != null)
		{
			GameSparksCollector.IncrementCounter(GameSparksCollector.GSDataKey.CraftedItems, outputItemValue.ItemClass.Name, recipe.count);
		}
		else
		{
			outputItemValue = originalItem;
		}
		XUiC_WorkstationOutputGrid childByType = windowGroup.Controller.GetChildByType<XUiC_WorkstationOutputGrid>();
		if (childByType != null && (originalItem == null || originalItem.IsEmpty()))
		{
			ItemStack itemStack = new ItemStack(outputItemValue, recipe.count);
			ItemStack[] slots = RebirthStationOpenOutputObserverCandidate.ReadSlots(childByType,this);
			bool flag = false;
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i].CanStackWith(itemStack))
				{
					slots[i].count += recipe.count;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				for (int j = 0; j < slots.Length; j++)
				{
					if (slots[j].IsEmpty())
					{
						slots[j] = itemStack;
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				childByType.SetSlots(slots);
				RebirthStationOpenOutputObserverCandidate.WriteOutput(childByType,slots,this);
				childByType.IsDirty = true;
				if (recipe.IsScrap)
				{
					QuestEventManager.Current.ScrappedItem(recipe.ingredients[0]);
					entityPlayer.equipment.UnlockCosmeticItem(recipe.ingredients[0].itemValue.ItemClass);
				}
				else
				{
					QuestEventManager.Current.CraftedItem(itemStack);
				}
				if (playSound)
				{
					if (recipe.craftingArea != null)
					{
						WorkstationData workstationData = CraftingManager.GetWorkstationData(recipe.craftingArea);
						if (workstationData != null)
						{
							Manager.PlayInsidePlayerHead(workstationData.CraftCompleteSound);
						}
					}
					else
					{
						Manager.PlayInsidePlayerHead("craft_complete_item");
					}
				}
			}
			else if (!AddItemToInventory())
			{
				isInventoryFull = true;
				string text = "No room in workstation output, crafting has been halted until space is cleared.";
				if (Localization.Exists("wrnWorkstationOutputFull"))
				{
					text = Localization.Get("wrnWorkstationOutputFull");
				}
				GameManager.ShowTooltip(entityPlayer, text);
				Manager.PlayInsidePlayerHead("ui_denied");
				return false;
			}
		}
		else
		{
			if (!xui.DragAndDropWindow.CurrentStack.IsEmpty() && xui.DragAndDropWindow.CurrentStack.itemValue.ItemClass is ItemClassQuest)
			{
				return false;
			}
			ItemStack itemStack2 = new ItemStack(outputItemValue, recipe.count);
			if (!xui.PlayerInventory.AddItemNoPartial(itemStack2, _playCollectSound: false))
			{
				if (itemStack2.count != recipe.count)
				{
					xui.PlayerInventory.DropItem(itemStack2);
					QuestEventManager.Current.CraftedItem(itemStack2);
					return true;
				}
				isInventoryFull = true;
				string text2 = "No room in inventory, crafting has been halted until space is cleared.";
				if (Localization.Exists("wrnInventoryFull"))
				{
					text2 = Localization.Get("wrnInventoryFull");
				}
				GameManager.ShowTooltip(entityPlayer, text2);
				Manager.PlayInsidePlayerHead("ui_denied");
				return false;
			}
			if (originalItem != null && !originalItem.IsEmpty())
			{
				if (recipe.ingredients.Count > 0 && recipe.IsScrap)
				{
					QuestEventManager.Current.ScrappedItem(recipe.ingredients[0]);
					entityPlayer.equipment.UnlockCosmeticItem(recipe.ingredients[0].itemValue.ItemClass);
				}
			}
			else
			{
				itemStack2.count = recipe.count - itemStack2.count;
				if (recipe.IsScrap)
				{
					QuestEventManager.Current.ScrappedItem(recipe.ingredients[0]);
					entityPlayer.equipment.UnlockCosmeticItem(recipe.ingredients[0].itemValue.ItemClass);
				}
				else
				{
					QuestEventManager.Current.CraftedItem(itemStack2);
				}
			}
			if (playSound)
			{
				Manager.PlayInsidePlayerHead("craft_complete_item");
			}
		}
		if (!isInventoryFull)
		{
			originalItem = ItemValue.None;
		}
		return true;
	}public override void Update(float _dt)
	{
		if (isInventoryFull)
		{
			if (recipe != null && outputItemValue != null)
			{
				XUiC_WorkstationOutputGrid childByType = windowGroup.Controller.GetChildByType<XUiC_WorkstationOutputGrid>();
				bool flag = false;
				ItemStack[] array = new ItemStack[0];
				if (childByType != null)
				{
					array = RebirthStationOpenOutputObserverCandidate.ReadSlots(childByType,this);
					for (int i = 0; i < array.Length; i++)
					{
						if (array[i].CanStackWith(new ItemStack(outputItemValue, recipe.count)))
						{
							array[i].count += recipe.count;
							flag = true;
							break;
						}
						if (array[i].IsEmpty())
						{
							array[i] = new ItemStack(outputItemValue, recipe.count);
							flag = true;
							break;
						}
					}
				}
				if (flag)
				{
					childByType.SetSlots(array);
					RebirthStationOpenOutputObserverCandidate.WriteOutput(childByType,array,this);
					childByType.IsDirty = true;
					isInventoryFull = false;
					recipeCount--;
					if (recipeCount <= 0)
					{
						isCrafting = false;
						if (recipe != null || craftingTimeLeft != 0f)
						{
							ClearRecipe();
						}
					}
					else
					{
						craftingTimeLeft += oneItemCraftTime;
					}
					base.Update(_dt);
					return;
				}
				if (!xui.DragAndDropWindow.CurrentStack.IsEmpty() && xui.DragAndDropWindow.CurrentStack.itemValue.ItemClass is ItemClassQuest)
				{
					base.Update(_dt);
					return;
				}
				ItemStack itemStack = new ItemStack(outputItemValue, recipe.count);
				if (!xui.PlayerInventory.AddItemNoPartial(itemStack, _playCollectSound: false))
				{
					updateRecipeData();
					if (itemStack.count != recipe.count)
					{
						xui.PlayerInventory.DropItem(itemStack);
						QuestEventManager.Current.CraftedItem(itemStack);
						isInventoryFull = false;
						recipeCount--;
						if (recipeCount <= 0)
						{
							isCrafting = false;
							if (recipe != null || craftingTimeLeft != 0f)
							{
								ClearRecipe();
							}
						}
						else
						{
							craftingTimeLeft += oneItemCraftTime;
						}
					}
					base.Update(_dt);
					return;
				}
				QuestEventManager.Current.CraftedItem(new ItemStack(outputItemValue, recipe.count));
				isInventoryFull = false;
				recipeCount--;
				if (recipeCount <= 0)
				{
					isCrafting = false;
					if (recipe != null || craftingTimeLeft != 0f)
					{
						ClearRecipe();
					}
				}
				else
				{
					craftingTimeLeft += oneItemCraftTime;
				}
				base.Update(_dt);
				return;
			}
			isInventoryFull = false;
			isCrafting = false;
		}
		if (recipe == null)
		{
			isCrafting = false;
		}
		if (recipeCount > 0)
		{
			if (isCrafting && craftingTimeLeft <= 0f && recipe != null && outputStack())
			{
				recipeCount--;
				if (recipeCount <= 0)
				{
					isCrafting = false;
					if (recipe != null || craftingTimeLeft != 0f)
					{
						ClearRecipe();
					}
				}
				else
				{
					craftingTimeLeft += oneItemCraftTime;
				}
			}
		}
		else
		{
			isCrafting = false;
			if (recipe != null && (recipe != null || craftingTimeLeft != 0f))
			{
				ClearRecipe();
			}
		}
		if (base.ViewComponent.IsVisible)
		{
			updateRecipeData();
		}
		if (recipeCount > 0 && isCrafting)
		{
			craftingTimeLeft -= _dt;
			totalCraftTimeLeft = oneItemCraftTime * ((float)recipeCount - 1f) + craftingTimeLeft;
		}
		else
		{
			if (craftingTimeLeft < 0f)
			{
				craftingTimeLeft = 0f;
			}
			if (totalCraftTimeLeft < 0f)
			{
				totalCraftTimeLeft = 0f;
			}
		}
		base.Update(_dt);
	}public bool AddItemToInventory()
	{
		ItemStack itemStack = new ItemStack(outputItemValue, recipe.count);
		if (!xui.PlayerInventory.AddItemNoPartial(itemStack, _playCollectSound: false))
		{
			updateRecipeData();
			return false;
		}
		QuestEventManager.Current.CraftedItem(new ItemStack(outputItemValue, recipe.count));
		isInventoryFull = false;
		if (playSound)
		{
			if (recipe.craftingArea != null)
			{
				WorkstationData workstationData = CraftingManager.GetWorkstationData(recipe.craftingArea);
				if (workstationData != null)
				{
					Manager.PlayInsidePlayerHead(workstationData.CraftCompleteSound);
				}
			}
			else
			{
				Manager.PlayInsidePlayerHead("craft_complete_item");
			}
		}
		if (recipeCount <= 0)
		{
			isCrafting = false;
			if (recipe != null || craftingTimeLeft != 0f)
			{
				ClearRecipe();
			}
		}
		return true;
	}}partial class XUiC_WorkstationOutputGrid{public void UpdateData(ItemStack[] stackList)
	{
		UpdateBackend(stackList);
	}public override void UpdateBackend(ItemStack[] stackList)
	{
		base.UpdateBackend(stackList);
		workstationData.SetOutputStacks(stackList);
		windowGroup.Controller.SetAllChildrenDirty();
	}}partial class XUiM_Workstation{public void SetOutputStacks(ItemStack[] _itemStacks)
	{
		tileEntity.Output = _itemStacks;
	}}partial class XUiC_ItemStackGrid{public virtual ItemStack[] GetSlots()
	{
		return getUISlots();
	}public virtual ItemStack[] getUISlots()
	{
		ItemStack[] array = new ItemStack[itemControllers.Length];
		for (int i = 0; i < itemControllers.Length; i++)
		{
			array[i] = itemControllers[i].ItemStack.Clone();
		}
		return array;
	}}