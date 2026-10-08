using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class NetPackageTileEntity {public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		if (_world == null)
		{
			return;
		}
		TileEntity tileEntity = _world.GetTileEntity(teWorldPos);
		if (tileEntity == null)
		{
			return;
		}
		if (_world.GetBlock(teWorldPos).type != teBlockId)
		{
			Log.Warning("[NetPackageTileEntity] Block type changed. Dropping package.");
			return;
		}
		tileEntity.SetHandle(handle);
		using (PooledBinaryReader pooledBinaryReader = MemoryPools.poolBinaryReader.AllocSync(_bReset: false))
		{
			lock (ms)
			{
				pooledBinaryReader.SetBaseStream(ms);
				ms.Position = 0L;
				tileEntity.read(pooledBinaryReader, _world.IsRemote() ? StreamModeRead.FromServer : StreamModeRead.FromClient);
			}
		}
		tileEntity.NotifyListeners();
		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			tileEntity.SetChunkModified();
			Vector3? entitiesInRangeOfWorldPos = tileEntity.ToWorldCenterPos();
			if (entitiesInRangeOfWorldPos.Value == Vector3.zero)
			{
				entitiesInRangeOfWorldPos = null;
			}
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(tileEntity, StreamModeWrite.ToClient, handle), _onlyClientsAttachedToAnEntity: true, -1, -1, -1, entitiesInRangeOfWorldPos);
		}
	}}}