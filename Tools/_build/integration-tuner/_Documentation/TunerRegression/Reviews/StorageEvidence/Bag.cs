using System;
using System.Collections.Generic;

public class Bag : InventoryBase
{
	[PublicizedFrom(EAccessModifier.Private)]
	public const byte Version = 2;

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStackGrid itemGrid;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool isReading;

	public ItemStackGrid ItemGrid => itemGrid;

	public XUiC_ItemStack.StackLocationTypes StackLocation => itemGrid.StackLocation;

	public PackedBoolArray LockedSlots => itemGrid.SlotLocks;

	public PreferenceTracker preferences => itemGrid.Preferences;

	public bool Touched => itemGrid.Touched;

	public int SlotCount => itemGrid.Length;

	public event XUiEvent_BackpackItemsChangedInternal OnBackpackItemsChangedInternal;

	public Bag(Vector2i _containerSize, XUiC_ItemStack.StackLocationTypes _stackLocation, IItemStackGridHolder _holder)
	{
		itemGrid = ItemStackGrid.Create(_containerSize, _stackLocation, _hasLocks: true, _hasPreferences: true, _holder);
		itemGrid.SlotChanged += onSlotChanged;
	}

	public void Write(PooledBinaryWriter bw, StreamModeWrite _eStreamMode)
	{
		bw.Write((byte)2);
		itemGrid.Write(bw, _eStreamMode);
	}

	public static Bag Read(PooledBinaryReader br, XUiC_ItemStack.StackLocationTypes stackLocation, StreamModeRead _eStreamMode, IItemStackGridHolder _holder)
	{
		return new Bag(new Vector2i(0, 0), stackLocation, _holder).ReadInto(br, _eStreamMode);
	}

	public Bag ReadInto(PooledBinaryReader br, StreamModeRead _eStreamMode)
	{
		isReading = true;
		try
		{
			return readInto(br, _eStreamMode);
		}
		finally
		{
			isReading = false;
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public Bag readInto(PooledBinaryReader br, StreamModeRead _eStreamMode)
	{
		byte b = br.ReadByte();
		if (b >= 2)
		{
			itemGrid.ReadInto(br, _eStreamMode);
		}
		else
		{
			int num = br.ReadUInt16();
			ItemStack[] array = ItemStack.CreateArray(num);
			for (int i = 0; i < num; i++)
			{
				array[i].Read(br);
			}
			PackedBoolArray packedBoolArray;
			if (br.ReadBoolean())
			{
				packedBoolArray = new PackedBoolArray();
				packedBoolArray.Read(br);
				packedBoolArray.Length = num;
			}
			else
			{
				packedBoolArray = new PackedBoolArray(num);
			}
			bool flag = false;
			PreferenceTracker preferenceTracker = null;
			if (b >= 1)
			{
				flag = br.ReadBoolean();
				if (br.ReadBoolean())
				{
					preferenceTracker = new PreferenceTracker(-1);
					preferenceTracker.Read(br);
				}
			}
			itemGrid.Resize(new Vector2i(num, 1));
			itemGrid.SetItems(array, _fireSlotChanged: false);
			itemGrid.SetSlotLocks(packedBoolArray);
			itemGrid.SetPreferences(preferenceTracker);
			if (flag)
			{
				itemGrid.Touch();
			}
		}
		return this;
	}

	public void Clear()
	{
		itemGrid.ClearItems();
	}

	public List<ItemStack> Resize(Vector2i _newSize)
	{
		int length = itemGrid.Length;
		List<ItemStack> result = itemGrid.Resize(_newSize);
		if (itemGrid.Length != length)
		{
			onBackpackChanged();
		}
		return result;
	}

	public void onSlotChanged(int _index)
	{
		if (!isReading)
		{
			onBackpackChanged();
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void onBackpackChanged()
	{
		this.OnBackpackItemsChangedInternal?.Invoke();
	}

	public bool CanStackNoEmpty(ItemStack _itemStack)
	{
		for (int i = 0; i < itemGrid.Length; i++)
		{
			if (itemGrid[i].CanStackPartlyWith(_itemStack, out var _))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsEmpty()
	{
		for (int i = 0; i < itemGrid.Length; i++)
		{
			if (!itemGrid[i].IsEmpty())
			{
				return false;
			}
		}
		return true;
	}

	public bool CanStack(ItemStack _itemStack)
	{
		for (int i = 0; i < itemGrid.Length; i++)
		{
			if (itemGrid[i].IsEmpty() || itemGrid[i].CanStackWith(_itemStack))
			{
				return true;
			}
		}
		return false;
	}

	public override (bool anyMoved, bool allMoved) TryStackItem(int startIndex, ItemStack _itemStack)
	{
		if (!_itemStack.CanMoveTo(StackLocation))
		{
			return (false, false);
		}
		int num = 0;
		bool item = false;
		for (int i = startIndex; i < itemGrid.Length; i++)
		{
			num = _itemStack.count;
			if (_itemStack.itemValue.type == itemGrid[i].itemValue.type && itemGrid[i].CanStackPartly(ref num))
			{
				itemGrid[i].count += num;
				_itemStack.count -= num;
				item = true;
				if (_itemStack.count == 0)
				{
					return (true, true);
				}
			}
		}
		return (item, false);
	}

	public bool CanTakeItem(ItemStack _itemStack)
	{
		for (int i = 0; i < itemGrid.Length; i++)
		{
			if (itemGrid[i].CanStackPartlyWith(_itemStack, out var _))
			{
				return true;
			}
			if (itemGrid[i].IsEmpty())
			{
				return true;
			}
		}
		return false;
	}

	public void SetSlots(ItemStack[] _slots, bool _resize = true)
	{
		if (_resize)
		{
			itemGrid.Resize(new Vector2i(_slots.Length, 1));
		}
		itemGrid.SetItems(_slots, _fireSlotChanged: false);
		onBackpackChanged();
	}

	public void SetSlot(int index, ItemStack _stack)
	{
		if (index < SlotCount)
		{
			itemGrid.SetItem(index, _stack);
		}
	}

	public override void PerformActionOnSlots(Action<ItemStack> _action)
	{
		for (int i = 0; i < itemGrid.Length; i++)
		{
			_action(itemGrid[i]);
		}
	}

	public override bool AddItem(ItemStack _itemStack)
	{
		if (!_itemStack.CanMoveTo(StackLocation))
		{
			return false;
		}
		return itemGrid.AddItem(_itemStack) >= 0;
	}

	public int DecItem(ItemValue _itemValue, int _count, bool _ignoreModdedItems = false, IList<ItemStack> _removedItems = null)
	{
		int num = _count;
		int num2 = 0;
		while (_count > 0 && num2 < itemGrid.Length)
		{
			ItemStack itemStack = itemGrid[num2];
			if (itemStack.itemValue.type == _itemValue.type && (!_ignoreModdedItems || !itemStack.itemValue.HasModSlots || !itemStack.itemValue.HasMods()))
			{
				if (ItemClass.GetForId(itemStack.itemValue.type).CanStack())
				{
					int count = itemStack.count;
					int num3 = ((count >= _count) ? _count : count);
					_removedItems?.Add(new ItemStack(itemStack.itemValue.Clone(), num3));
					itemStack.count -= num3;
					_count -= num3;
					if (itemStack.count <= 0)
					{
						itemStack.Clear();
					}
				}
				else
				{
					_removedItems?.Add(itemStack.Clone());
					itemStack.Clear();
					_count--;
				}
			}
			num2++;
		}
		return num - _count;
	}

	public int GetUsedSlotCount()
	{
		int num = 0;
		for (int i = 0; i < itemGrid.Length; i++)
		{
			if (!itemGrid[i].IsEmpty())
			{
				num++;
			}
		}
		return num;
	}

	public int GetItemCount(ItemValue _itemValue, int _seed = -1, int _meta = -1, bool _ignoreModdedItems = true)
	{
		int num = 0;
		for (int i = 0; i < itemGrid.Length; i++)
		{
			ItemStack itemStack = itemGrid[i];
			if ((!_ignoreModdedItems || !itemStack.itemValue.HasModSlots || !itemStack.itemValue.HasMods()) && itemStack.itemValue.type == _itemValue.type && (_seed == -1 || _seed == itemStack.itemValue.Seed) && (_meta == -1 || _meta == itemStack.itemValue.Meta))
			{
				num += itemStack.count;
			}
		}
		return num;
	}

	public int GetItemCount(FastTags<TagGroup.Global> itemTags, int _seed = -1, int _meta = -1, bool _ignoreModdedItems = true)
	{
		int num = 0;
		for (int i = 0; i < itemGrid.Length; i++)
		{
			ItemStack itemStack = itemGrid[i];
			if ((!_ignoreModdedItems || !itemStack.itemValue.HasModSlots || !itemStack.itemValue.HasMods()) && !itemStack.itemValue.IsEmpty() && itemStack.itemValue.ItemClass.ItemTags.Test_AnySet(itemTags) && (_seed == -1 || _seed == itemStack.itemValue.Seed) && (_meta == -1 || _meta == itemStack.itemValue.Meta))
			{
				num += itemStack.count;
			}
		}
		return num;
	}

	public override bool HasItem(ItemValue _item)
	{
		return GetItemCount(_item) > 0;
	}
}
