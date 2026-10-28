using System;
using System.Collections.Generic;

public class ItemStackGrid
{
	public readonly struct Address
	{
		public readonly WorldAddress HolderAddress;

		public readonly byte Key;

		public Address(WorldAddress _holderAddress, byte _key)
		{
			HolderAddress = _holderAddress;
			Key = _key;
		}

		public ItemStackGrid Resolve()
		{
			return HolderAddress.Resolve<IItemStackGridHolder>()?.GetItemStackGrid(Key);
		}

		public void Write(PooledBinaryWriter _bw)
		{
			HolderAddress.Write(_bw);
			_bw.Write(Key);
		}

		public static Address Read(PooledBinaryReader _br)
		{
			return new Address(WorldAddress.Read(_br), _br.ReadByte());
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public readonly IItemStackGridHolder holder;

	[PublicizedFrom(EAccessModifier.Private)]
	public const ushort Version = 2;

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStack[] items;

	[PublicizedFrom(EAccessModifier.Private)]
	public PackedBoolArray slotLocks;

	[PublicizedFrom(EAccessModifier.Private)]
	public PreferenceTracker preferences;

	[PublicizedFrom(EAccessModifier.Private)]
	public ulong worldTimeTouched;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool suppressChanged;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool playerOwned;

	[field: PublicizedFrom(EAccessModifier.Private)]
	public Vector2i ContainerSize
	{
		get; [PublicizedFrom(EAccessModifier.Private)]
		set;
	}

	[field: PublicizedFrom(EAccessModifier.Private)]
	public XUiC_ItemStack.StackLocationTypes StackLocation
	{
		get; [PublicizedFrom(EAccessModifier.Private)]
		set;
	}

	public int Length => items.Length;

	public ItemStack this[int _index]
	{
		get
		{
			return items[_index];
		}
		set
		{
			SetItem(_index, value);
		}
	}

	public PackedBoolArray SlotLocks => slotLocks;

	public PreferenceTracker Preferences => preferences;

	public bool Touched => worldTimeTouched != 0;

	public ulong WorldTimeTouched
	{
		get
		{
			return worldTimeTouched;
		}
		set
		{
			worldTimeTouched = value;
		}
	}

	public bool PlayerOwned
	{
		get
		{
			return playerOwned;
		}
		set
		{
			playerOwned = value;
		}
	}

	public event Action<int> SlotChanged;

	public Address GetAddress()
	{
		if (holder == null)
		{
			return default(Address);
		}
		return new Address(holder.GetAddress(), holder.GetItemStackGridKey(this));
	}

	public static ItemStackGrid Create(Vector2i _containerSize, XUiC_ItemStack.StackLocationTypes _stackLocation, bool _hasLocks, bool _hasPreferences, IItemStackGridHolder _holder)
	{
		return new ItemStackGrid(_containerSize, _stackLocation, _hasLocks, _hasPreferences, _holder);
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStackGrid(Vector2i _containerSize, XUiC_ItemStack.StackLocationTypes _stackLocation, bool _hasLocks, bool _hasPreferences, IItemStackGridHolder _holder)
	{
		holder = _holder;
		ContainerSize = _containerSize;
		StackLocation = _stackLocation;
		int num = _containerSize.x * _containerSize.y;
		items = new ItemStack[num];
		for (int i = 0; i < num; i++)
		{
			items[i] = CreateSlot(i);
		}
		slotLocks = (_hasLocks ? new PackedBoolArray(num) : null);
		preferences = (_hasPreferences ? new PreferenceTracker(-1) : null);
		worldTimeTouched = 0uL;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStackGrid()
	{
		items = Array.Empty<ItemStack>();
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStack CreateSlot(int _slotIndex)
	{
		ItemStack itemStack = new ItemStack();
		itemStack.BindGridSlot(this, _slotIndex);
		return itemStack;
	}

	public void OnSlotChanged(int _slotIndex)
	{
		if (!suppressChanged)
		{
			this.SlotChanged?.Invoke(_slotIndex);
		}
	}

	public void Touch()
	{
		worldTimeTouched = GameManager.Instance.World.GetWorldTime();
	}

	public void CopyFrom(ItemStackGrid _other, bool _resize = true)
	{
		if (_resize)
		{
			Resize(_other.ContainerSize);
		}
		suppressChanged = true;
		int num = Utils.FastMin(items.Length, _other.items.Length);
		for (int i = 0; i < num; i++)
		{
			ItemStack itemStack = _other.items[i];
			items[i].Set(itemStack.itemValue.Clone(), itemStack.count);
		}
		suppressChanged = false;
		if (slotLocks != null && _other.slotLocks != null && slotLocks.Length == _other.slotLocks.Length)
		{
			slotLocks.CopyFrom(_other.slotLocks);
		}
		preferences = _other.preferences?.Clone();
		worldTimeTouched = _other.worldTimeTouched;
		playerOwned = _other.playerOwned;
		for (int j = 0; j < num; j++)
		{
			this.SlotChanged?.Invoke(j);
		}
	}

	public List<ItemStack> Resize(Vector2i _containerSize)
	{
		int num = _containerSize.x * _containerSize.y;
		bool num2 = num == Length;
		ContainerSize = _containerSize;
		if (num2)
		{
			return null;
		}
		ItemStack[] array = new ItemStack[num];
		List<ItemStack> list = null;
		for (int i = 0; i < items.Length; i++)
		{
			if (i < num)
			{
				array[i] = items[i];
				continue;
			}
			items[i].BindGridSlot(null);
			if (!items[i].IsEmpty())
			{
				if (list == null)
				{
					list = new List<ItemStack>();
				}
				list.Add(items[i]);
			}
		}
		for (int j = items.Length; j < num; j++)
		{
			array[j] = CreateSlot(j);
		}
		items = array;
		if (slotLocks != null)
		{
			slotLocks.Length = num;
		}
		return list;
	}

	public bool Reset()
	{
		if (PlayerOwned)
		{
			return false;
		}
		Clear();
		worldTimeTouched = 0uL;
		return true;
	}

	public void Clear()
	{
		ClearItems();
		slotLocks?.Clear();
		preferences?.Clear();
	}

	public void SetSlotLocks(PackedBoolArray _slotLocks)
	{
		if (_slotLocks == null)
		{
			slotLocks = null;
		}
		else if (_slotLocks.Length != Length)
		{
			Log.Error(string.Format("[{0}] Cannot set slot locks. Expected length {1}, received {2}. {3} should be called first.", "ItemStackGrid", items.Length, _slotLocks.Length, "Resize"));
		}
		else
		{
			slotLocks = _slotLocks;
		}
	}

	public void SetPreferences(PreferenceTracker _preferences)
	{
		preferences = _preferences;
	}

	public bool IsLocked(int _index)
	{
		if (slotLocks != null)
		{
			return slotLocks[_index];
		}
		return false;
	}

	public bool IsSlotLocked(int _index)
	{
		if (_index >= slotLocks?.Length)
		{
			return false;
		}
		if (slotLocks != null)
		{
			return slotLocks[_index];
		}
		return false;
	}

	public void SetLocked(int _index, bool _value)
	{
		if (slotLocks != null && slotLocks[_index] != _value)
		{
			slotLocks[_index] = _value;
			this.SlotChanged?.Invoke(_index);
		}
	}

	public static ItemStackGrid Read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		ItemStackGrid itemStackGrid = new ItemStackGrid();
		itemStackGrid.ReadInto(_br, _eStreamMode);
		return itemStackGrid;
	}

	public void ReadInto(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		ushort num = _br.ReadUInt16();
		Vector2i vector2i = StreamUtils.ReadVector2i(_br);
		int num2 = _br.ReadInt16();
		if (vector2i.x < 0 || vector2i.y < 0 || num2 != (long)vector2i.x * (long)vector2i.y)
		{
			throw new InvalidOperationException(string.Format("[{0}] Stream slot count {1} does not match container size {2}", "ItemStackGrid", num2, vector2i));
		}
		Resize(vector2i);
		suppressChanged = true;
		for (int i = 0; i < num2; i++)
		{
			items[i].Read(_br);
		}
		suppressChanged = false;
		if (_br.ReadBoolean())
		{
			if (slotLocks == null)
			{
				slotLocks = new PackedBoolArray(items.Length);
			}
			slotLocks.Read(_br);
		}
		else
		{
			slotLocks = null;
		}
		if (_br.ReadBoolean())
		{
			if (preferences == null)
			{
				preferences = new PreferenceTracker(-1);
			}
			preferences.Read(_br);
		}
		else
		{
			preferences = null;
		}
		if (num >= 2)
		{
			worldTimeTouched = _br.ReadUInt64();
			playerOwned = _br.ReadBoolean();
		}
	}

	public void Write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)
	{
		_bw.Write((ushort)2);
		StreamUtils.Write(_bw, ContainerSize);
		_bw.Write((short)items.Length);
		for (int i = 0; i < items.Length; i++)
		{
			items[i].Write(_bw);
		}
		bool flag = slotLocks != null;
		_bw.Write(flag);
		if (flag)
		{
			slotLocks.Write(_bw);
		}
		bool flag2 = preferences != null;
		_bw.Write(flag2);
		if (flag2)
		{
			preferences.Write(_bw);
		}
		_bw.Write(worldTimeTouched);
		_bw.Write(playerOwned);
	}

	public ItemStack GetItem(int _index)
	{
		return items[_index];
	}

	public ItemStack GetItemClone(int _index)
	{
		return items[_index].Clone();
	}

	public void SetItem(int _index, ItemStack _stack)
	{
		if (_stack == null || _stack.count <= 0)
		{
			items[_index].Clear();
		}
		else
		{
			items[_index].Set(_stack.itemValue.Clone(), _stack.count);
		}
	}

	public void SetItems(ItemStack[] _items, bool _fireSlotChanged = true)
	{
		if (_items == null || _items.Length != Length)
		{
			Log.Error(string.Format("[{0}] Cannot set slots. Expected length {1}, received {2}. {3} should be called first.", "ItemStackGrid", items.Length, (_items != null) ? _items.Length : 0, "Resize"));
			return;
		}
		suppressChanged = !_fireSlotChanged;
		for (int i = 0; i < _items.Length; i++)
		{
			ItemStack itemStack = _items[i];
			if (itemStack == null)
			{
				items[i].Set(ItemValue.None, 0);
			}
			else
			{
				items[i].Set(itemStack.itemValue.Clone(), itemStack.count);
			}
		}
		suppressChanged = false;
	}

	public int AddItem(ItemStack _itemStack, int _maxIndex = -1)
	{
		for (int i = 0; i < items.Length; i++)
		{
			if (items[i].CanStackWith(_itemStack))
			{
				items[i].count += _itemStack.count;
				_itemStack.count = 0;
				return i;
			}
		}
		int num = ((_maxIndex < 0) ? items.Length : Utils.FastMin(_maxIndex, items.Length));
		for (int j = 0; j < num; j++)
		{
			if (items[j].IsEmpty())
			{
				items[j].Set(_itemStack.itemValue.Clone(), _itemStack.count);
				return j;
			}
		}
		return -1;
	}

	public int ChangeCount(int _index, int _delta)
	{
		if (_delta == 0)
		{
			return 0;
		}
		ItemStack itemStack = items[_index];
		if (itemStack.IsEmpty())
		{
			return 0;
		}
		int _count;
		if (_delta > 0)
		{
			_count = _delta;
			if (!itemStack.CanStackPartly(ref _count))
			{
				return 0;
			}
			itemStack.count += _count;
		}
		else
		{
			_count = -Utils.FastMin(-_delta, itemStack.count);
			if (itemStack.count + _count <= 0)
			{
				itemStack.Clear();
			}
			else
			{
				itemStack.count += _count;
			}
		}
		return _count;
	}

	public IList<ItemStack> CopyItemsFrom(ItemStackGrid _source)
	{
		int num = Utils.FastMin(items.Length, _source.items.Length);
		for (int i = 0; i < num; i++)
		{
			SetItem(i, _source.items[i]);
		}
		List<ItemStack> list = null;
		for (int j = num; j < _source.items.Length; j++)
		{
			ItemStack itemStack = _source.items[j];
			if (!itemStack.IsEmpty())
			{
				if (list == null)
				{
					list = new List<ItemStack>();
				}
				list.Add(itemStack.Clone());
			}
		}
		return list;
	}

	public ItemStack[] CloneItems()
	{
		ItemStack[] array = new ItemStack[items.Length];
		for (int i = 0; i < items.Length; i++)
		{
			array[i] = items[i].Clone();
		}
		return array;
	}

	public void ClearItems()
	{
		for (int i = 0; i < items.Length; i++)
		{
			items[i].Clear();
		}
	}

	public void Compact()
	{
		suppressChanged = true;
		CompactSlots();
		suppressChanged = false;
		NotifyUnlockedSlotsChanged();
	}

	public void Sort()
	{
		suppressChanged = true;
		CompactSlots();
		for (int i = 0; i < items.Length; i++)
		{
			if (IsLocked(i))
			{
				continue;
			}
			int num = i;
			for (int j = i + 1; j < items.Length; j++)
			{
				if (!IsLocked(j) && CompareForSort(items[j], items[num]) < 0)
				{
					num = j;
				}
			}
			if (num != i)
			{
				items[i].SwapWith(items[num]);
			}
		}
		suppressChanged = false;
		NotifyUnlockedSlotsChanged();
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void NotifyUnlockedSlotsChanged()
	{
		for (int i = 0; i < items.Length; i++)
		{
			if (!IsLocked(i))
			{
				this.SlotChanged?.Invoke(i);
			}
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void CompactSlots()
	{
		for (int i = 0; i < items.Length; i++)
		{
			ItemStack itemStack = items[i];
			if (IsLocked(i))
			{
				continue;
			}
			if (itemStack.IsEmpty())
			{
				for (int j = i + 1; j < items.Length; j++)
				{
					if (!IsLocked(j) && !items[j].IsEmpty())
					{
						itemStack.SwapWith(items[j]);
						break;
					}
				}
				if (itemStack.IsEmpty())
				{
					break;
				}
			}
			ItemClass itemClass = itemStack.itemValue.ItemClass;
			if (itemClass == null || itemClass.HasQuality)
			{
				continue;
			}
			int num = itemClass.MaxCount - itemStack.count;
			for (int k = i + 1; k < items.Length; k++)
			{
				if (num <= 0)
				{
					break;
				}
				ItemStack itemStack2 = items[k];
				if (!IsLocked(k) && !itemStack2.IsEmpty() && itemStack2.itemValue.type == itemStack.itemValue.type)
				{
					int num2 = Utils.FastMin(itemStack2.count, num);
					itemStack.count += num2;
					itemStack2.count -= num2;
					num -= num2;
					if (itemStack2.count == 0)
					{
						itemStack2.Clear();
					}
				}
			}
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static int CompareForSort(ItemStack _a, ItemStack _b)
	{
		bool flag = _a.IsEmpty();
		bool flag2 = _b.IsEmpty();
		if (flag || flag2)
		{
			return flag.CompareTo(flag2);
		}
		ItemClass itemClass = _a.itemValue.ItemClass;
		ItemClass itemClass2 = _b.itemValue.ItemClass;
		int num;
		if (itemClass == null || itemClass2 == null)
		{
			num = (itemClass == null).CompareTo(itemClass2 == null);
		}
		else
		{
			bool flag3 = itemClass.IsBlock();
			num = itemClass2.IsBlock().CompareTo(flag3);
			if (num == 0 && !flag3)
			{
				num = string.Compare(itemClass.Groups[0], itemClass2.Groups[0], StringComparison.CurrentCulture);
			}
			if (num == 0)
			{
				num = string.Compare(itemClass.GetLocalizedItemName(), itemClass2.GetLocalizedItemName(), StringComparison.CurrentCulture);
			}
		}
		if (num == 0)
		{
			num = _a.itemValue.Quality.CompareTo(_b.itemValue.Quality);
		}
		if (num == 0)
		{
			num = _b.itemValue.UseTimes.CompareTo(_a.itemValue.UseTimes);
		}
		return num;
	}
}
