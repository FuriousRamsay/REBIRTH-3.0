// TOOLS ONLY exact local action hook placement; unresolved owner scope, not universal seed producer.
	public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
	{
		if (!_bReleased || Time.time - _actionData.lastUseTime < Delay || Time.time - _actionData.lastUseTime < Constants.cBuildIntervall)
		{
			return;
		}
		EntityAlive holdingEntity = _actionData.invData.holdingEntity;
		if (EffectManager.GetValue(PassiveEffects.DisableItem, holdingEntity.inventory.holdingItemItemValue, 0f, holdingEntity, null, _actionData.invData.item.ItemTags) > 0f)
		{
			_actionData.lastUseTime = Time.time + 1f;
			Manager.PlayInsidePlayerHead("twitch_no_attack");
			return;
		}
		ItemInventoryData invData = _actionData.invData;
		WorldRayHitInfo hitInfo = ((EntityPlayerLocal)invData.holdingEntity).HitInfo;
		Vector3i lastBlockPos = hitInfo.lastBlockPos;
		if (!hitInfo.bHitValid || lastBlockPos == Vector3i.zero || hitInfo.tag.StartsWith("E_"))
		{
			return;
		}
		BlockValue block = invData.world.GetBlock(lastBlockPos);
		if (!block.isair || (!invData.world.IsEditor() && GameUtils.IsColliderWithinBlock(lastBlockPos, block)))
		{
			return;
		}
		BlockValue blockValue = invData.item.OnConvertToBlockValue(invData.itemValue, blockToPlace);
		hitInfo.hit.blockPos = lastBlockPos;
		int placementDistanceSq = blockValue.Block.GetPlacementDistanceSq();
		if (hitInfo.hit.distanceSq > (float)placementDistanceSq)
		{
			return;
		}
		if (!blockValue.Block.CanPlaceBlockAt(invData.world, lastBlockPos, blockValue))
		{
			GameManager.ShowTooltip(invData.holdingEntity as EntityPlayerLocal, "blockCantPlaced");
			return;
		}
		BlockPlacement.EnumPlacement placement;
		BlockPlacement.EnumRotationMode mode;
		int localRot;
		PropTransform propTransform;
		if (invData.IsBlock)
		{
			placement = invData.Placement;
			mode = invData.mode;
			localRot = invData.localRot;
			propTransform = invData.propTransform;
		}
		else
		{
			placement = BlockPlacement.EnumPlacement.Voxel;
			mode = BlockPlacement.EnumRotationMode.Auto;
			localRot = 0;
			propTransform = PropTransform.identity;
		}
		BlockPlacement.Result _bpResult = blockValue.Block.BlockPlacementHelper.OnPlaceBlock(placement, mode, localRot, invData.world, blockValue, propTransform, hitInfo.hit, invData.holdingEntity.position);
		blockValue.Block.OnBlockPlaceBefore(invData.world, ref _bpResult, invData.holdingEntity, invData.world.GetGameRandom());
		blockValue = _bpResult.blockValue;
		if (blockValue.Block.IndexName == "lpblock")
		{
			if (!invData.world.CanPlaceLandProtectionBlockAt(_bpResult.blockPos, invData.world.gameManager.GetPersistentLocalPlayer()))
			{
				invData.holdingEntity.PlayOneShot("keystone_build_warning");
				return;
			}
			invData.holdingEntity.PlayOneShot("keystone_placed");
		}
		else if (!invData.world.CanPlaceBlockAt(_bpResult.blockPos, invData.world.gameManager.GetPersistentLocalPlayer()))
		{
			invData.holdingEntity.PlayOneShot("keystone_build_warning");
			return;
		}
		_actionData.lastUseTime = Time.time;
		// UNIMPLEMENTED original trusted LOCAL item-use placement scope only.
		// No origin inferred from actor-id; remote seed authority remains unresolved.
		using (var originAdmission = OriginalOwnerBeginQualifiedLocalPlantPlacement(invData, _bpResult, _actionData))
		{
			blockValue.Block.PlaceBlock(invData.world, _bpResult, invData.holdingEntity);
			originAdmission?.Complete(); // Must verify actual matching committed incarnation.
		}
		_actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
		_actionData.invData.holdingEntity.MinEventContext.BlockValue = blockValue;
		_actionData.invData.holdingEntity.MinEventContext.Position = _bpResult.pos;
		_actionData.invData.holdingEntity.FireEvent(MinEventTypes.onSelfPlaceBlock);
		QuestEventManager.Current.BlockPlaced(blockValue.Block.GetBlockName(), _bpResult.blockPos);
		invData.holdingEntity.RightArmAnimationUse = true;
		if (changeItemTo != null)
		{
			ItemValue itemValue = ItemClass.GetItem(changeItemTo);
			if (!itemValue.IsEmpty())
			{
				invData.holdingEntity.inventory.SetItem(invData.holdingEntity.inventory.holdingItemIdx, new ItemStack(itemValue, 1));
			}
		}
		else
		{
			GameManager.Instance.StartCoroutine(decInventoryLater(invData, invData.holdingEntity.inventory.holdingItemIdx));
		}
		invData.holdingEntity.PlayOneShot((soundStart != null) ? soundStart : "placeblock");
		(invData.holdingEntity as EntityPlayerLocal).DropTimeDelay = 0.5f;
	}