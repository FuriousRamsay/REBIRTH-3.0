partial class TileEntityWorkstation{public void ActualNativeCandidateWhile(float _timePassed)
	{
		if (RebirthStationOpenPaidExecutionLease.ShouldBlockUserAccess(this, bUserAccessing) || queue.Length == 0 || (isModuleUsed[3] && !isBurning))
		{
			return;
		}
		RecipeQueueItem recipeQueueItem = queue[queue.Length - 1];
		if (recipeQueueItem == null)
		{
			return;
		}
		if (recipeQueueItem.CraftingTimeLeft >= 0f)
		{
			recipeQueueItem.CraftingTimeLeft -= _timePassed;
		}
		while (recipeQueueItem.CraftingTimeLeft < 0f && hasRecipeInQueue())
		{
			if (recipeQueueItem.Multiplier > 0)
			{
				ItemValue itemValue = new ItemValue(recipeQueueItem.Recipe.itemValueType);
				if (ItemClass.list[recipeQueueItem.Recipe.itemValueType] != null && ItemClass.list[recipeQueueItem.Recipe.itemValueType].HasQuality)
				{
					itemValue = new ItemValue(recipeQueueItem.Recipe.itemValueType, recipeQueueItem.Quality, recipeQueueItem.Quality);
				}
				if (CandidateInsert(output, new ItemStack(itemValue, recipeQueueItem.Recipe.count), -1) == -1)
				{
					break;
				}
				RebirthStationPaidCompletionCallsite.Complete(this, recipeQueueItem.StartingEntityId, itemValue, recipeQueueItem.Recipe.GetName(), recipeQueueItem.Recipe.IsScrap ? recipeQueueItem.Recipe.ingredients[0].itemValue.ItemClass.GetItemName() : "", recipeQueueItem.Recipe.craftExpGain, recipeQueueItem.Recipe.count);
				GameSparksCollector.IncrementCounter(GameSparksCollector.GSDataKey.CraftedItems, itemValue.ItemClass.Name, recipeQueueItem.Recipe.count);
				recipeQueueItem.Multiplier--;
				recipeQueueItem.CraftingTimeLeft += recipeQueueItem.OneItemCraftTime;
			}
			if (recipeQueueItem.Multiplier <= 0)
			{
				float craftingTimeLeft = recipeQueueItem.CraftingTimeLeft;
				cycleRecipeQueue();
				recipeQueueItem = queue[queue.Length - 1];
				recipeQueueItem.CraftingTimeLeft += ((craftingTimeLeft < 0f) ? craftingTimeLeft : 0f);
			}
		}
	}public void ActualNativeCandidateUpdateTick(World world)
	{
		BaseTick(world);
		bool flag = (!isModuleUsed[3] && hasRecipeInQueue()) || isBurning;
		float num = ((float)GameTimer.Instance.ticks - (float)lastTickTime) / 20f;
		float num2 = Mathf.Min(num, BurnTotalTimeLeft);
		float timePassed = (isModuleUsed[3] ? num2 : num);
		isBesideWater = IsByWater(world, ToWorldPos());
		isBurning &= !isBesideWater;
		BlockValue blockValue = world.GetBlock(ToWorldPos());
		UpdateLightState(world, blockValue);
		if (isModuleUsed[3])
		{
			HandleFuel(world, timePassed);
		}
		else if (blockValue.Block.HeatMapStrength > 0f && IsCrafting)
		{
			emitHeatMapEvent(world, EnumAIDirectorChunkEvent.Campfire);
		}
		ActualNativeCandidateWhile(timePassed);
		HandleMaterialInput(timePassed);
		if (isModuleUsed[3])
		{
			isBurning &= BurnTotalTimeLeft > 0f;
		}
		lastTickTime = GameTimer.Instance.ticks;
		if ((!isModuleUsed[3] && hasRecipeInQueue()) || isBurning || flag)
		{
			setModified();
		}
		UpdateVisible();
	}public void BaseTick(World world){} }