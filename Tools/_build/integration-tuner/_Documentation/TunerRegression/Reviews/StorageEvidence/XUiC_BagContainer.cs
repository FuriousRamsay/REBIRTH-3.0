using System;
using System.Collections;
using Audio;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_BagContainer : XUiC_ItemStackGrid
{
	[PublicizedFrom(EAccessModifier.Private)]
	public bool isClosing;

	[PublicizedFrom(EAccessModifier.Private)]
	public XUiV_Window window;

	[PublicizedFrom(EAccessModifier.Private)]
	public XUiV_Grid grid;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool userLockMode;

	[PublicizedFrom(EAccessModifier.Private)]
	public XUiC_ContainerStandardControls standardControls;

	[PublicizedFrom(EAccessModifier.Private)]
	public string containerName;

	[PublicizedFrom(EAccessModifier.Private)]
	public Action onBagModified;

	[PublicizedFrom(EAccessModifier.Private)]
	public int windowWidth;

	[PublicizedFrom(EAccessModifier.Private)]
	public int windowGridWidthDifference;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool activeKeyDown;

	[PublicizedFrom(EAccessModifier.Private)]
	public bool wasReleased;

	[field: PublicizedFrom(EAccessModifier.Private)]
	public Bag Bag
	{
		get; [PublicizedFrom(EAccessModifier.Private)]
		set;
	}

	[XuiXmlBinding("gridDimensions")]
	public Vector2i GridDimensions
	{
		get
		{
			if (grid == null)
			{
				return Vector2i.one;
			}
			return new Vector2i(grid.Columns, grid.Rows);
		}
	}

	[field: PublicizedFrom(EAccessModifier.Private)]
	public Vector2i GridCellSize
	{
		get; [PublicizedFrom(EAccessModifier.Private)]
		set;
	}

	public override XUiC_ItemStack.StackLocationTypes StackLocation
	{
		[PublicizedFrom(EAccessModifier.Protected)]
		get
		{
			return XUiC_ItemStack.StackLocationTypes.Vehicle;
		}
	}

	[PublicizedFrom(EAccessModifier.Protected)]
	public override void UpdateBackend(ItemStack[] _stackList)
	{
	}

	[PublicizedFrom(EAccessModifier.Protected)]
	public override void SetStacks(ItemStack[] _stackList)
	{
	}

	public override void Init()
	{
		base.Init();
		window = (XUiV_Window)viewComponent;
		grid = (XUiV_Grid)GetChildById("queue").ViewComponent;
		GridCellSize = new Vector2i(grid.CellWidth, grid.CellHeight);
		grid.OnSizeChanged += delegate
		{
			RefreshBindings();
		};
		standardControls = GetChildByType<XUiC_ContainerStandardControls>();
		if (standardControls == null)
		{
			return;
		}
		standardControls.GetLockedSlotsFromStorage = () => Bag.LockedSlots;
		standardControls.SetLockedSlotsToStorage = delegate(PackedBoolArray _slots)
		{
			Bag.LockedSlots?.CopyFrom(_slots);
		};
		standardControls.ApplyLockedSlotStates = ApplyLockedSlotStates;
		standardControls.LockModeToggled = delegate
		{
			IsDirty = true;
		};
		standardControls.SearchFiltersChanged = delegate(XUiC_ContainerStandardControls.MatchesFiltersDelegate filter)
		{
			base.MatchesFiltersFunc = filter;
		};
		standardControls.SortPressed = btnSort_OnPress;
		standardControls.MoveAllowed = delegate(out XUiController _parentWindow, out XUiC_ItemStackGrid _grid, out IInventory _inventory)
		{
			_parentWindow = this;
			_grid = this;
			_inventory = xui.PlayerInventory;
			return true;
		};
		standardControls.MoveAllDone = delegate(bool _allMoved, bool _anyMoved)
		{
			if (_anyMoved)
			{
				Manager.BroadcastPlayByLocalPlayer(XUiM_Player.GetPlayer().position, "UseActions/takeall1");
			}
			if (_allMoved)
			{
				ThreadManager.StartCoroutine(closeInventoryLater());
			}
		};
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void btnSort_OnPress(PackedBoolArray _ignoredSlots)
	{
		if (Bag != null)
		{
			Bag.ItemGrid.Sort();
		}
	}

	public void SetBag(Bag _bag, LootContainer _lootContainer, string _containerName, Action _onModified = null)
	{
		if (_bag == null || _lootContainer == null)
		{
			return;
		}
		Bag = _bag;
		containerName = _containerName;
		onBagModified = _onModified;
		grid.Columns = _lootContainer.size.x;
		grid.Rows = _lootContainer.size.y;
		ItemStackGrid itemGrid = _bag.ItemGrid;
		int num = Mathf.CeilToInt((float)itemGrid.Length / (float)grid.Columns);
		windowWidth = new Vector2i(grid.Columns * GridCellSize.x, num * GridCellSize.y).x + windowGridWidthDifference;
		XUiC_ItemInfoWindow childByType = xui.GetChildByType<XUiC_ItemInfoWindow>();
		int length = itemGrid.Length;
		for (int i = 0; i < itemControllers.Length; i++)
		{
			XUiC_ItemStack xUiC_ItemStack = itemControllers[i];
			xUiC_ItemStack.SlotNumber = i;
			xUiC_ItemStack.SlotChangedEvent -= HandleBagSlotChangedEvent;
			xUiC_ItemStack.InfoWindow = childByType;
			xUiC_ItemStack.StackLocation = XUiC_ItemStack.StackLocationTypes.LootContainer;
			xUiC_ItemStack.UnlockStack();
			if (i < length)
			{
				xUiC_ItemStack.ForceSetItemStack(itemGrid[i]);
				xUiC_ItemStack.SlotChangedEvent += HandleBagSlotChangedEvent;
			}
			else
			{
				xUiC_ItemStack.ItemStack = ItemStack.Empty;
			}
		}
		RefreshBindings();
	}

	public override void Update(float _dt)
	{
		if (GameManager.Instance == null || GameManager.Instance.World == null)
		{
			return;
		}
		if (IsDirty)
		{
			base.ViewComponent.IsVisible = Bag != null;
			RefreshBindings();
			IsDirty = false;
		}
		base.Update(_dt);
		if (!windowGroup.isShowing)
		{
			return;
		}
		if (!xui.playerUI.playerInput.PermanentActions.Activate.IsPressed)
		{
			wasReleased = true;
		}
		if (wasReleased && !xui.playerUI.windowManager.IsInputActive())
		{
			if (xui.playerUI.playerInput.PermanentActions.Activate.IsPressed)
			{
				activeKeyDown = true;
			}
			if (xui.playerUI.playerInput.PermanentActions.Activate.WasReleased && activeKeyDown)
			{
				activeKeyDown = false;
				OnClose();
				xui.playerUI.windowManager.CloseAllOpenModalWindows();
			}
		}
		if (!isClosing && base.ViewComponent != null && base.ViewComponent.IsVisible && Bag != null && (PlatformManager.NativePlatform.Input.CurrentInputStyle != PlayerInputManager.InputStyle.Keyboard || !xui.playerUI.windowManager.IsInputActive()) && (xui.playerUI.playerInput.GUIActions.LeftStick.WasPressed || xui.playerUI.playerInput.PermanentActions.Reload.WasPressed))
		{
			standardControls.MoveAll();
		}
	}

	public void HandleBagSlotChangedEvent(int _slotNumber, ItemStack _stack)
	{
		if (Bag != null)
		{
			Bag.SetSlot(_slotNumber, _stack);
			updateSearch();
		}
	}

	public void OnBagItemChangedInternal()
	{
		if (Bag != null)
		{
			for (int i = 0; i < Bag.SlotCount; i++)
			{
				SetItemInSlot(i, Bag.ItemGrid[i]);
			}
			onBagModified?.Invoke();
		}
	}

	public void SetItemInSlot(int _i, ItemStack _stack)
	{
		if (_i < itemControllers.Length)
		{
			XUiC_ItemStack obj = itemControllers[_i];
			obj.SlotChangedEvent -= HandleBagSlotChangedEvent;
			obj.ItemStack = _stack;
			obj.SlotChangedEvent += HandleBagSlotChangedEvent;
		}
	}

	public override void OnOpen()
	{
		base.OnOpen();
		wasReleased = false;
		activeKeyDown = false;
		IsDirty = true;
		if (Bag != null)
		{
			Bag.OnBackpackItemsChangedInternal += OnBagItemChangedInternal;
			Bag.ItemGrid.SlotChanged += OnItemGridSlotChanged;
		}
	}

	public override void OnClose()
	{
		base.OnClose();
		if (Bag != null)
		{
			Bag.OnBackpackItemsChangedInternal -= OnBagItemChangedInternal;
			Bag.ItemGrid.SlotChanged -= OnItemGridSlotChanged;
		}
		Bag = null;
		onBagModified = null;
	}

	[PublicizedFrom(EAccessModifier.Protected)]
	public IEnumerator closeInventoryLater()
	{
		isClosing = true;
		yield return null;
		xui.playerUI.windowManager.Close(windowGroup);
		isClosing = false;
	}

	public bool AddItem(ItemStack _itemStack)
	{
		if (Bag == null)
		{
			return false;
		}
		Bag.TryStackItem(0, _itemStack);
		if (_itemStack.count > 0)
		{
			return Bag.AddItem(_itemStack);
		}
		return false;
	}

	public override bool ParseAttribute(string _name, string _value)
	{
		if (_name == "window_grid_width_difference")
		{
			windowGridWidthDifference = StringParsers.ParseSInt32(_value);
			return true;
		}
		return base.ParseAttribute(_name, _value);
	}

	[PublicizedFrom(EAccessModifier.Protected)]
	public override bool GetBindingValueInternal(ref string _value, string _bindingName)
	{
		switch (_bindingName)
		{
		case "windowWidth":
			_value = windowWidth.ToString();
			return true;
		case "take_all_tooltip":
			_value = string.Format(Localization.Get("xuiLootTakeAllTooltip"), "[action:permanent:Reload:emptystring:KeyboardWithAngleBrackets]");
			return true;
		case "buttons_visible":
			_value = (windowWidth >= 450).ToString();
			return true;
		case "container_slots":
			_value = (Bag?.SlotCount ?? 0).ToString();
			return true;
		case "userlockmode":
			_value = (standardControls?.UserLockMode ?? false).ToString();
			return true;
		case "hasslotlocks":
			_value = (standardControls?.LockedSlots != null).ToString();
			return true;
		case "lootcontainer_name":
			_value = ((!string.IsNullOrEmpty(containerName)) ? containerName : Localization.Get("xuiStorage"));
			return true;
		default:
			return base.GetBindingValueInternal(ref _value, _bindingName);
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void ApplyLockedSlotStates(PackedBoolArray _lockedSlots)
	{
		XUiC_ItemStack[] itemStackControllers = GetItemStackControllers();
		for (int i = 0; i < itemStackControllers.Length; i++)
		{
			itemStackControllers[i].UserLockedSlot = _lockedSlots != null && i < _lockedSlots.Length && _lockedSlots[i];
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void UpdateLockedSlots(XUiC_ContainerStandardControls _csc)
	{
		if (_csc != null && Bag != null)
		{
			int slotCount = Bag.SlotCount;
			PackedBoolArray packedBoolArray = _csc.LockedSlots ?? new PackedBoolArray(slotCount);
			if (packedBoolArray.Length != slotCount)
			{
				packedBoolArray.Length = slotCount;
			}
			XUiC_ItemStack[] itemStackControllers = GetItemStackControllers();
			for (int i = 0; i < itemStackControllers.Length && i < packedBoolArray.Length; i++)
			{
				packedBoolArray[i] = itemStackControllers[i].UserLockedSlot;
			}
			_csc.LockedSlots = packedBoolArray;
		}
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void OnItemGridSlotChanged(int _slotNumber)
	{
		GetItemStackControllers()[_slotNumber].UserLockedSlot = Bag.ItemGrid.IsSlotLocked(_slotNumber);
		updateSearch();
	}

	[PublicizedFrom(EAccessModifier.Protected)]
	public override void dropItemToGrid(ItemStack _dropStack)
	{
		base.dropItemToGrid(_dropStack);
		if (Bag != null && Bag.CanStack(_dropStack) && AddItem(_dropStack))
		{
			_dropStack.count = 0;
		}
	}
}
