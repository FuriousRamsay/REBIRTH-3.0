partial class PlayerDataFile { public void Save(string _dir, string _playerId)
	{
		try
		{
			if (!SdDirectory.Exists(_dir))
			{
				SdDirectory.CreateDirectory(_dir);
			}
			string text = _dir + "/" + _playerId + "." + EXT;
			if (SdFile.Exists(text))
			{
				SdFile.Copy(text, text + ".bak", overwrite: true);
			}
			if (SdFile.Exists(text + ".tmp"))
			{
				SdFile.Delete(text + ".tmp");
			}
			using (Stream baseStream = SdFile.Open(text + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
			{
				using PooledBinaryWriter pooledBinaryWriter = MemoryPools.poolBinaryWriter.AllocSync(_bReset: false);
				pooledBinaryWriter.SetBaseStream(baseStream);
				pooledBinaryWriter.Write('t');
				pooledBinaryWriter.Write('t');
				pooledBinaryWriter.Write('p');
				pooledBinaryWriter.Write((byte)0);
				pooledBinaryWriter.Write((byte)62);
				Write(pooledBinaryWriter, StreamModeWrite.Persistency);
				bModifiedSinceLastSave = false;
			}
			if (SdFile.Exists(text + ".tmp"))
			{
				SdFile.Copy(text + ".tmp", text, overwrite: true);
				SdFile.Delete(text + ".tmp");
			}
			metadata.Write(text + ".meta");
		}
		catch (Exception ex)
		{
			Log.Error("Save PlayerData file: " + ex.Message + "\n" + ex.StackTrace);
		}
	} }
partial class GameManager { public void SavePlayerData(ClientInfo _cInfo, PlayerDataFile _playerDataFile)
	{
		_cInfo.latestPlayerData = _playerDataFile;
		int entityId = _cInfo.entityId;
		if (entityId != -1)
		{
			EntityPlayer entityPlayer = (EntityPlayer)m_World.GetEntity(entityId);
			if (entityPlayer != null)
			{
				_playerDataFile.Save(GameIO.GetPlayerDataDir(), _cInfo.InternalId.CombinedString);
				if (entityPlayer.ChunkObserver.mapDatabase != null)
				{
					ThreadManager.AddSingleTask(entityPlayer.ChunkObserver.mapDatabase.SaveAsync, new IMapChunkDatabase.DirectoryPlayerId(GameIO.GetPlayerDataDir(), _cInfo.InternalId.CombinedString));
				}
				entityPlayer.QuestJournal = _playerDataFile.questJournal;
				if (persistentPlayers != null)
				{
					foreach (KeyValuePair<PlatformUserIdentifierAbs, PersistentPlayerData> player in persistentPlayers.Players)
					{
						if (player.Value.EntityId == _playerDataFile.id)
						{
							player.Value.Position = new Vector3i(_playerDataFile.ecd.pos);
							break;
						}
					}
				}
			}
		}
		ModEvents.SSavePlayerDataData _data = new ModEvents.SSavePlayerDataData(_cInfo, _playerDataFile);
		ModEvents.SavePlayerData.Invoke(ref _data);
	} }
class NetPackagePlayerData:NetPackage {public PlayerDataFile playerDataFile; public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		if (ValidEntityIdForSender(playerDataFile.id))
		{
			_callbacks.SavePlayerData(base.Sender, playerDataFile);
		}
	} }
