// TOOLS exact native method review. SeedPlacementClientScopeReview.TryConsumeMatchingSend is a Tools review implementation; root session/policy/recovery interfaces remain unimplemented.
	public void SetBlocksRPC(List<BlockChangeInfo> _changes, PlatformUserIdentifierAbs _persistentPlayerId = null)
	{
		ChangeBlocks(_persistentPlayerId, _changes);
		NetPackageSetBlock package = NetPackageManager.GetPackage<NetPackageSetBlock>().Setup(persistentLocalPlayer, _changes, IsDedicatedServer ? (-1) : myPlayerId);
		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			SetBlocksOnClients(-1, package);
		}
		else
		{
			/* PROPOSED original-owner seam: one exact matching client voxel scope, pre-prediction receipt. */
			if (SeedPlacementClientScopeReview.TryConsumeMatchingSend(package, out NetPackageRebirthSeedPlacementReview envelope))
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(envelope);
                SeedPlacementClientScopeReview.ObserveNativeSendReturned(envelope);
            }
			else
				SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(package);
		}
	}



