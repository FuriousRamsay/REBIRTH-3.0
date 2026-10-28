using System;
using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthCamperTurretBackpack:XUiC_RebirthBackpackSectionBackpack
{
    internal int Filter;
    private XUiController[] presentationCells;
    private XUiController PresentationCell(int index)
    {
        if(presentationCells==null||presentationCells.Length!=itemControllers.Length)
            presentationCells=new XUiController[itemControllers.Length];
        return presentationCells[index]??(presentationCells[index]=GetChildById("sellAvailable"+index+"Cell"));
    }
    internal void ToggleFilter(int kind)
    {
        Filter=Filter==kind?0:kind;SetStacks(GetSlots());
        foreach(var pair in new[]{Tuple.Create("filterTurrets",1),Tuple.Create("filterAmmo",2)})
            if(windowGroup.Controller.GetChildById(pair.Item1)?.ViewComponent is XUiV_Button button)button.Selected=Filter==pair.Item2;
    }
    private bool Matches(ItemStack stack)=>Filter==0||stack!=null&&!stack.IsEmpty()&&
        (Filter==1?stack.itemValue.ItemClass.GetItemName()==RebirthCamperTurretProfiles.ItemName:RebirthCamperTurretProfiles.IsAmmo(stack.itemValue));
    public override void SetStacks(ItemStack[] stacks)
    {
        if(stacks==null)return;
        if(itemControllers==null||itemControllers.Length==0)itemControllers=GetChildrenByType<XUiC_ItemStack>();
        base.SetStacks(stacks);maxSlotCount=xui.playerUI.entityPlayer.CarryCapacity;
        int physical=Math.Min(stacks.Length,itemControllers.Length);
        int visible=0;
        for(int i=0;i<itemControllers.Length;i++)
        {
            var slot=itemControllers[i];if(slot?.ViewComponent==null)continue;
            bool show=i<physical&&Matches(stacks[i]);slot.ViewComponent.IsVisible=show;slot.ViewComponent.Enabled=show;
            var cell=PresentationCell(i)?.ViewComponent;
            if(cell!=null)
            {
                if(cell.IsVisible!=show)cell.IsVisible=show;if(cell.UiTransform!=null&&cell.UiTransform.gameObject.activeSelf!=show)cell.UiTransform.gameObject.SetActive(show);
                if(show){var position=new Vector2i(visible%13*67,(i/13-visible/13)*67);if(cell.Position!=position){cell.Position=position;cell.TryUpdatePosition();}}
            }
            if(show)visible++;
            slot.SlotNumber=i;slot.InfoWindow=null;
        }
        GetParentByType<XUiC_RebirthCharacterOverviewList>()?.SetItemCount((visible+12)/13,string.Empty);
        RebirthCraftingInventoryBridge.ApplyLockedSlots(xui,itemControllers,physical);
    }
    public override void HandleSlotChangedEvent(int slotNumber,ItemStack stack)
    {
        RebirthCraftingInventoryBridge.WriteNativeBackpackSlot(xui,slotNumber,stack);
    }
    public override ItemStack[] getUISlots()
    {
        var actual=GetSlots();var result=ItemStack.CreateArray(actual?.Length??0);
        for(int i=0;i<result.Length;i++)result[i]=i<itemControllers.Length?itemControllers[i].ItemStack.Clone():actual[i].Clone();
        return result;
    }
}

