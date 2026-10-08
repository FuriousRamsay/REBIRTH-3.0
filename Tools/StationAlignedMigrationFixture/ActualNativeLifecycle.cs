namespace NativeLifecycle {
public partial class TileEntityWorkstation {
public TileEntityWorkstation(Chunk _chunk)
		: base(_chunk)
	{
		fuel = ItemStack.CreateArray(3);
		tools = ItemStack.CreateArray(3);
		output = ItemStack.CreateArray(6);
		input = ItemStack.CreateArray(3);
		lastInput = ItemStack.CreateArray(3);
		queue = new RecipeQueueItem[4];
		materialNames = new string[0];
		isModuleUsed = new bool[5];
		currentMeltTimesLeft = new float[input.Length];
	}
public override void OnSetLocalChunkPosition()
	{
		if (base.localChunkPos == Vector3i.zero)
		{
			return;
		}
		Block block = chunk.GetBlock(World.toBlockXZ(base.localChunkPos.x), base.localChunkPos.y, World.toBlockXZ(base.localChunkPos.z)).Block;
		if (block.Properties.Contains("Workstation", "InputMaterials"))
		{
			string @string = block.Properties.GetString("Workstation", "InputMaterials");
			if (@string.Contains(","))
			{
				materialNames = @string.Replace(" ", "").Split(',');
			}
			else
			{
				materialNames = new string[1] { @string };
			}
			if (input.Length != 3 + materialNames.Length)
			{
				ItemStack[] array = new ItemStack[3 + materialNames.Length];
				for (int i = 0; i < input.Length; i++)
				{
					array[i] = input[i].Clone();
				}
				input = array;
				for (int j = 0; j < materialNames.Length; j++)
				{
					ItemClass itemClass = ItemClass.GetItemClass("unit_" + materialNames[j]);
					if (itemClass != null)
					{
						int num = j + 3;
						input[num] = new ItemStack(new ItemValue(itemClass.Id), 0);
					}
				}
			}
		}
		if (block.Properties.Contains("Workstation", "Modules"))
		{
			string string2 = block.Properties.GetString("Workstation", "Modules");
			string[] array2 = ((!string2.Contains(",")) ? new string[1] { string2 } : string2.Replace(" ", "").Split(','));
			for (int k = 0; k < array2.Length; k++)
			{
				Module module = EnumUtils.Parse<Module>(array2[k], _ignoreCase: true);
				isModuleUsed[(int)module] = true;
			}
			if (isModuleUsed[4])
			{
				isModuleUsed[1] = true;
			}
		}
	}
public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		base.read(_br, _eStreamMode);
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
				int num6 = _br.ReadByte();
				for (int l = 0; l < num6; l++)
				{
					currentMeltTimesLeft[l] = _br.ReadSingle();
				}
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
			int num4 = _br.ReadByte();
			for (int k = 0; k < num4; k++)
			{
				currentMeltTimesLeft[k] = _br.ReadSingle();
			}
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
				int num = _br.ReadByte();
				for (int i = 0; i < num; i++)
				{
					currentMeltTimesLeft[i] = _br.ReadSingle();
				}
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
public override void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)
	{
		base.write(_bw, _eStreamMode);
		_bw.Write((byte)50);
		switch (_eStreamMode)
		{
		case StreamModeWrite.Persistency:
		{
			_bw.Write(lastTickTime);
			writeItemStackArray(_bw, fuel);
			writeItemStackArray(_bw, input);
			writeItemStackArray(_bw, tools);
			writeItemStackArray(_bw, output);
			writeRecipeStackArray(_bw, 50);
			writeCraftCompleteData(_bw, 50);
			_bw.Write(isBurning);
			_bw.Write(currentBurnTimeLeft);
			int num3 = currentMeltTimesLeft.Length;
			_bw.Write((byte)num3);
			for (int k = 0; k < num3; k++)
			{
				_bw.Write(currentMeltTimesLeft[k]);
			}
			_bw.Write(isPlayerPlaced);
			writeItemStackArray(_bw, lastInput);
			break;
		}
		case StreamModeWrite.ToServer:
		{
			writeItemStackArray(_bw, fuel);
			writeItemStackArray(_bw, input);
			writeItemStackArray(_bw, tools);
			writeItemStackArray(_bw, output);
			writeRecipeStackArray(_bw, 50);
			writeCraftCompleteData(_bw, 50);
			_bw.Write(isBurning);
			_bw.Write(currentBurnTimeLeft);
			int num2 = currentMeltTimesLeft.Length;
			_bw.Write((byte)num2);
			for (int j = 0; j < num2; j++)
			{
				_bw.Write(currentMeltTimesLeft[j]);
			}
			_bw.Write(isPlayerPlaced);
			_bw.Write(GameTimer.Instance.ticks - lastTickTime);
			writeItemStackArray(_bw, lastInput);
			break;
		}
		case StreamModeWrite.ToClient:
		{
			writeItemStackArray(_bw, fuel);
			writeItemStackArray(_bw, input);
			writeItemStackArray(_bw, tools);
			writeItemStackArray(_bw, output);
			writeRecipeStackArray(_bw, 50);
			writeCraftCompleteData(_bw, 50);
			_bw.Write(isBurning);
			_bw.Write(currentBurnTimeLeft);
			int num = currentMeltTimesLeft.Length;
			_bw.Write((byte)num);
			for (int i = 0; i < num; i++)
			{
				_bw.Write(currentMeltTimesLeft[i]);
			}
			_bw.Write(isPlayerPlaced);
			_bw.Write(GameTimer.Instance.ticks - lastTickTime);
			writeItemStackArray(_bw, lastInput);
			break;
		}
		}
	}
public void readItemStackArray(PooledBinaryReader _br, ref ItemStack[] stack)
	{
		int num = _br.ReadByte();
		if (stack == null || stack.Length != num)
		{
			stack = ItemStack.CreateArray(num);
		}
		if (!bUserAccessing)
		{
			for (int i = 0; i < num; i++)
			{
				stack[i].Read(_br);
			}
			return;
		}
		ItemStack empty = ItemStack.Empty;
		for (int j = 0; j < num; j++)
		{
			empty.Read(_br);
		}
	}
public void writeItemStackArray(PooledBinaryWriter bw, ItemStack[] stack)
	{
		byte value = (byte)((stack != null) ? ((byte)stack.Length) : 0);
		bw.Write(value);
		if (stack != null)
		{
			for (int i = 0; i < stack.Length; i++)
			{
				stack[i].Write(bw);
			}
		}
	}
}
public partial class XUiC_ItemStackGrid {
public virtual void SetStacks(ItemStack[] stackList)
	{
		if (stackList != null)
		{
			XUiC_ItemInfoWindow childByType = xui.GetChildByType<XUiC_ItemInfoWindow>();
			for (int i = 0; i < stackList.Length && itemControllers.Length > i && stackList.Length > i; i++)
			{
				XUiC_ItemStack obj = itemControllers[i];
				obj.SlotChangedEvent -= handleSlotChangedDelegate;
				obj.ItemStack = stackList[i].Clone();
				obj.SlotChangedEvent += handleSlotChangedDelegate;
				obj.SlotNumber = i;
				obj.InfoWindow = childByType;
				obj.StackLocation = StackLocation;
			}
		}
	}
public virtual ItemStack[] GetSlots()
	{
		return getUISlots();
	}
public virtual ItemStack[] getUISlots()
	{
		ItemStack[] array = new ItemStack[itemControllers.Length];
		for (int i = 0; i < itemControllers.Length; i++)
		{
			array[i] = itemControllers[i].ItemStack.Clone();
		}
		return array;
	}
}
public partial class XUiC_WorkstationGrid {
public virtual void SetSlots(ItemStack[] stacks)
	{
		base.SetStacks(stacks);
	}
}
public partial class XUiM_Workstation {
public ItemStack[] GetInputStacks()
	{
		return tileEntity.Input;
	}
public void SetInputStacks(ItemStack[] _itemStacks)
	{
		tileEntity.Input = _itemStacks;
	}
}
}
