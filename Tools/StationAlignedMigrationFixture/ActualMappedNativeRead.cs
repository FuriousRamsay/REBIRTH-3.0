namespace NativeLifecycle {public partial class MappedReviewTile {
public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		ReadNativeBase(_br, _eStreamMode); // Exact base member alias; avoid invoking baseline TE.read twice.
		int version = _br.ReadByte();
		switch (_eStreamMode)
		{
		case StreamModeRead.Persistency:
			lastTickTime = _br.ReadUInt64();
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			if (!bUserAccessing)
			{
				isBurning = _br.ReadBoolean();
				currentBurnTimeLeft = _br.ReadSingle();
				currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			}
			else
			{
				_br.ReadBoolean();
				_br.ReadSingle();
				int num7 = _br.ReadByte();
				for (int m = 0; m < num7; m++)
				{
					_br.ReadSingle();
				}
			}
			isPlayerPlaced = _br.ReadBoolean();
			readItemStackArray(_br, ref lastInput);
			break;
		case StreamModeRead.FromClient:
		{
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			isBurning = _br.ReadBoolean();
			currentBurnTimeLeft = _br.ReadSingle();
			currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			isPlayerPlaced = _br.ReadBoolean();
			ulong num5 = _br.ReadUInt64();
			lastTickTime = GameTimer.Instance.ticks - num5;
			readItemStackArray(_br, ref lastInput);
			break;
		}
		case StreamModeRead.FromServer:
		{
			readItemStackArray(_br, ref fuel);
			readItemStackArray(_br, ref input);
			readItemStackArray(_br, ref toolsNet);
			readItemStackArray(_br, ref output);
			readRecipeStackArray(_br, version, ref queue);
			readCraftCompleteData(_br, version);
			if (!bUserAccessing)
			{
				isBurning = _br.ReadBoolean();
				currentBurnTimeLeft = _br.ReadSingle();
				currentMeltTimesLeft = RebirthPlainStationNativeMigrationCandidate.ReadStoredMeltTimers(_br,currentMeltTimesLeft);
			}
			else
			{
				_br.ReadBoolean();
				_br.ReadSingle();
				int num2 = _br.ReadByte();
				for (int j = 0; j < num2; j++)
				{
					_br.ReadSingle();
				}
			}
			isPlayerPlaced = _br.ReadBoolean();
			ulong num3 = _br.ReadUInt64();
			if (!bUserAccessing)
			{
				lastTickTime = GameTimer.Instance.ticks - num3;
			}
			readItemStackArray(_br, ref lastInput);
			break;
		}
		}
		OnSetLocalChunkPosition();
		SetDataFromNet();
	}
}}