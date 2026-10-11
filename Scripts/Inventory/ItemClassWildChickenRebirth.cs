using UnityEngine.Scripting;

// Preserve the dynamically created native item type while inheriting all chicken behavior.
[Preserve]
public sealed class ItemClassWildChickenRebirth : ItemClassWildChicken
{
    [Preserve]
    public ItemClassWildChickenRebirth() { }
}