using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 fuel slots, driven by the block's FuelType. A station that names a fuel accepts only that
/// item. Stations without one keep native fuel rules. The station workspace sets the greyed icons.
/// </summary>
[Preserve]
public class XUiC_RebirthWorkstationFuelGrid : XUiC_WorkstationFuelGrid
{
    private ItemClass requiredFuel;

    public override void OnOpen()
    {
        base.OnOpen();
        string fuelName = workstationData?.TileEntity?.blockValue.Block?.Properties?.GetString("FuelType");
        var fuel = string.IsNullOrEmpty(fuelName) ? null : ItemClass.GetItem(fuelName);
        requiredFuel = fuel != null && fuel.type != 0 ? fuel.ItemClass : null;

        for (int i = 0; i < itemControllers.Length; i++)
        {
            var slot = itemControllers[i] as XUiC_RequiredItemStack;
            if (slot == null) continue;
            slot.RequiredType = XUiC_RequiredItemStack.RequiredTypes.ItemClass;
            slot.RequiredItemOnly = requiredFuel != null;
            if (requiredFuel != null) slot.SetAllowedItemClassSingle(requiredFuel);
            else slot.ClearAllowedItemClasses();
        }

    }

    // Shift-click and quick moves skip the slot's own check.
    public override bool AddItem(ItemClass _itemClass, ItemStack _itemStack)
    {
        if (requiredFuel != null && _itemClass != requiredFuel) return false;
        return base.AddItem(_itemClass, _itemStack);
    }
}
