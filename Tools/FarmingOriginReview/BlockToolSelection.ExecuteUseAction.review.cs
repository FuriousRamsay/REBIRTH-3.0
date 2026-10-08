// TOOLS ONLY exact actual block-seed action hook; root owner scope unresolved, no installed producer.
	public bool ExecuteUseAction(ItemInventoryData _data, bool _bReleased, PlayerActionsLocal playerActions)
	{
		if (!_data.IsBlock)
		{
			return false;
		}
		bool flag = GameManager.Instance.IsEditMode() || GameStats.GetInt(EnumGameStats.GameModeId) == 2;
		if (flag && playerActions.Drop.IsPressed)
		{
			return false;
		}
		if (_bReleased)
		{
			return false;
		}
		if (Time.time - lastBuildTime < Constants.cBuildIntervall)
		{
			return true;
		}
		lastBuildTime = Time.time;
		EntityAlive holdingEntity = _data.holdingEntity;
		FastTags<TagGroup.Global> tags = FastTags<TagGroup.Global>.none;
		ItemClassBlock itemClassBlock = _data.item as ItemClassBlock;
		if (itemClassBlock != null)
		{
			tags = itemClassBlock.GetBlock().Tags;
		}
		if (EffectManager.GetValue(PassiveEffects.DisableItem, holdingEntity.inventory.holdingItemItemValue, 0f, holdingEntity, null, tags) > 0f)
		{
			lastBuildTime = Time.time + 1f;
			Manager.PlayInsidePlayerHead("twitch_no_attack");
			return false;
		}
		WorldRayHitInfo worldRayHitInfo = ((EntityPlayerLocal)_data.holdingEntity).HitInfo;
		HitInfoDetails hitInfoDetails = worldRayHitInfo.hit.Clone();
		if (!worldRayHitInfo.bHitValid)
		{
			return false;
		}
		hitInfoDetails.blockPos = ((flag && playerActions.Run.IsPressed) ? worldRayHitInfo.hit.blockPos : worldRayHitInfo.lastBlockPos);
		BlockValue blockValue = _data.itemValue.ToBlockValue();
		Block block = blockValue.Block;
		blockValue.damage = _data.damage;
		blockValue.rotation = _data.rotation;
		World world = GameManager.Instance.World;
		if (!GameManager.Instance.IsEditMode())
		{
			int placementDistanceSq = block.GetPlacementDistanceSq();
			if (hitInfoDetails.distanceSq > (float)placementDistanceSq)
			{
				return true;
			}
			Vector3i freePlacementPosition = block.GetFreePlacementPosition(world, hitInfoDetails.blockPos, blockValue, holdingEntity);
			if (!holdingEntity.IsGodMode.Value && GameUtils.IsColliderWithinBlock(freePlacementPosition, blockValue))
			{
				return true;
			}
			if (hitInfoDetails.blockPos == Vector3i.zero)
			{
				return true;
			}
		}
		_data.holdingEntity.RightArmAnimationUse = true;
		BlockPlacement.Result _bpResult = block.BlockPlacementHelper.OnPlaceBlock(_data.Placement, _data.mode, _data.localRot, GameManager.Instance.World, blockValue, _data.propTransform, hitInfoDetails, _data.holdingEntity.position);
		block.OnBlockPlaceBefore(_data.world, ref _bpResult, _data.holdingEntity, _data.world.GetGameRandom());
		blockValue = _bpResult.blockValue;
		block = blockValue.Block;
		if (blockValue.damage == 0)
		{
			blockValue.damage = block.StartDamage;
			_bpResult.blockValue.damage = block.StartDamage;
		}
		if (!playerActions.Run.IsPressed)
		{
			_bpResult.blockPos = block.GetFreePlacementPosition(_data.holdingEntity.world, _bpResult.blockPos, blockValue, _data.holdingEntity);
		}
		if (!block.CanPlaceBlockAt(_data.world, _bpResult.blockPos, blockValue))
		{
			_data.holdingEntity.PlayOneShot("keystone_build_warning");
			return true;
		}
		if (!BlockLimitTracker.instance.CanAddBlock(blockValue, _bpResult.blockPos, out var _response))
		{
			switch (_response)
			{
			case eSetBlockResponse.PowerBlockLimitExceeded:
				GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), "uicannotaddpowerblock");
				break;
			case eSetBlockResponse.StorageBlockLimitExceeded:
				GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), "uicannotaddstorageblock");
				break;
			}
			return true;
		}
		if (!GameManager.Instance.IsEditMode())
		{
			if (block.IndexName == "lpblock")
			{
				if (!_data.world.CanPlaceLandProtectionBlockAt(worldRayHitInfo.lastBlockPos, _data.world.gameManager.GetPersistentLocalPlayer()))
				{
					_data.holdingEntity.PlayOneShot("keystone_build_warning");
					return true;
				}
				_data.holdingEntity.PlayOneShot("keystone_placed");
			}
			else if (!_data.world.CanPlaceBlockAt(worldRayHitInfo.lastBlockPos, _data.world.gameManager.GetPersistentLocalPlayer()))
			{
				_data.holdingEntity.PlayOneShot("keystone_build_warning");
				return true;
			}
		}
		BiomeDefinition biome = _data.world.GetBiome(_bpResult.blockPos.x, _bpResult.blockPos.z);
		if (biome != null && biome.Replacements.ContainsKey(_bpResult.blockValue.type))
		{
			_bpResult.blockValue.type = biome.Replacements[_bpResult.blockValue.type];
		}
		switch (_bpResult.placement)
		{
		case BlockPlacement.EnumPlacement.Voxel:
			// Root host-local HUMAN seed-use admission; ordinary creative players remain eligible.
			// Begin rejects editor/remote authority; the native edit-or-creative flag is not origin proof.
			using (var originAdmission = AdvancedFarmingLocalPlantingAdmission.Begin(_data, _bpResult, _data.itemValue.TextureFullArray.IsDefault ? _bpResult.blockValue : blockValue))
            using (var remoteCommand = SeedPlacementClientScopeReview.Begin(_data, _bpResult, _data.itemValue.TextureFullArray.IsDefault ? _bpResult.blockValue : blockValue))
			{
				if (remoteCommand != null && !remoteCommand.BeforeNativeInvocation()) return true;
                if (!PlaceBlock(_data, _bpResult, _data, block, blockValue))
				{
					return true;
				}
				originAdmission?.Complete(); // Verify matching callback/current committed incarnation.
			}
			break;
		case BlockPlacement.EnumPlacement.Free:
			if (!PlaceProp(_data, _bpResult, _data, block, blockValue))
			{
				return true;
			}
			break;
		default:
			throw new ArgumentOutOfRangeException();
		}
		_data.holdingEntity.RightArmAnimationUse = true;
		_data.lastBuildTime = Time.time;
		GameManager.Instance.StartCoroutine(decInventoryLater(_data, _data.holdingEntity.inventory.holdingItemIdx));
		return true;
	}