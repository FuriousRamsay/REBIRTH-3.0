using UnityEngine;
public abstract partial class EntityRebirthHumanoidNPC
{
    internal bool RebirthStartingLoadoutRead;
    public override void Read(byte version,PooledBinaryReader reader,StreamModeRead streamMode)
    {
        RebirthStartingLoadoutRead=true; // Native saves/network frames already own their equipment.
        base.Read(version,reader,streamMode);
    }
    public override void AddCharacterController()
    {
        base.AddCharacterController();
        // Preserve the resolved 2.6 SDCS feet/controller alignment, on both peers.
        if(m_characterController!=null)m_characterController.SetCenter(new Vector3(0f,0.82f,0f));
        if(PhysicsTransform!=null){PhysicsTransform.tag="Physics";PhysicsTransform.gameObject.layer=15;}
    }
    public override void InitInventory()
    {
        // Allocate before the native read: Inventory.ReadInto truncates to its
        // immutable SlotCount. Dogs do not inherit this class.
        if(inventory==null)inventory=new Inventory(this,8);
    }
    internal void InitializeRebirthEquipmentCargo()
    {
        if(bag==null)
        {
            bag=new Bag(new Vector2i(16,1),XUiC_ItemStack.StackLocationTypes.LootContainer,this);
            bag.ItemGrid.Touch(); // owned cargo, never a random-loot fill surface
        }
    }
}

