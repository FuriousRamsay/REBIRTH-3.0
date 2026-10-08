partial class ItemStack{public bool IsEmpty()
	{
		if (count >= 1)
		{
			return itemValue.type == 0;
		}
		return true;
	}
public static int AddToItemStackArray(ItemStack[] _itemStackArr, ItemStack _itemStack, int maxItemCount = -1)
	{
		int num = -1;
		int num2 = 0;
		while (num == -1 && num2 < _itemStackArr.Length)
		{
			if (_itemStackArr[num2].CanStackWith(_itemStack))
			{
				_itemStackArr[num2].count += _itemStack.count;
				_itemStack.count = 0;
				num = num2;
			}
			num2++;
		}
		int num3 = 0;
		while (num == -1 && num3 < _itemStackArr.Length && (maxItemCount == -1 || num3 != maxItemCount))
		{
			if (_itemStackArr[num3].IsEmpty())
			{
				_itemStackArr[num3].Set(_itemStack.itemValue.Clone(), _itemStack.count);
				num = num3;
			}
			num3++;
		}
		return num;
	}
public bool CanStackWith(ItemStack _other, bool allowPartialStack = false)
	{
		int _count = _other.count;
		if (_other.itemValue != null && itemValue != null && _other.itemValue.type == itemValue.type && (itemValue.type >= Block.ItemsStartHere || _other.itemValue.TextureFullArray == itemValue.TextureFullArray || itemValue.IsShapeHelperBlock))
		{
			if (!allowPartialStack)
			{
				return CanStack(_other.count);
			}
			return CanStackPartly(ref _count);
		}
		return false;
	}
public bool CanStack(int _count)
	{
		if (itemValue.type == 0)
		{
			return true;
		}
		return ItemClass.GetForId(itemValue.type).MaxCount >= _count + count;
	}}