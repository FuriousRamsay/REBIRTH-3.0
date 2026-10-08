using UnityEngine.Scripting;
// Presentation-only native item_stack; uses the same square icon, quality rail and count as Backpack.
[Preserve]
public sealed class XUiC_RebirthEditorPreviewSlot : XUiC_ItemStack
{
    private XUiC_RebirthAssembleItem equipment;
    private XUiC_RebirthEditorBackpack backpack;
    private bool equipmentPreview;
    private readonly XUiV_Sprite[] backgrounds=new XUiV_Sprite[3];
    public override void Init()
    {
        base.Init();
        var root=GetParentByType<XUiC_RebirthItemEditorHeader>();
        equipment=root?.GetChildByType<XUiC_RebirthAssembleItem>();backpack=root?.GetChildByType<XUiC_RebirthEditorBackpack>();
        equipmentPreview=ReferenceEquals(root?.GetChildById("editorEquipmentPreview"),this);
        backgrounds[0]=GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite;
        backgrounds[1]=GetChildById("background")?.ViewComponent as XUiV_Sprite;
        backgrounds[2]=GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
        DisableInput(this);
    }
    private static void DisableInput(XUiController node)
    {
        if(node.ViewComponent!=null){node.ViewComponent.EventOnPress=false;node.ViewComponent.EventOnHover=false;node.ViewComponent.EventOnDrag=false;node.ViewComponent.EventOnScroll=false;}
        if(node.Children!=null)foreach(var child in node.Children)DisableInput(child);
    }
    public override void Update(float dt)
    {
        var source=equipmentPreview?equipment?.ItemStack:backpack?.SelectedItem?.ItemStack;
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(ItemStack,source??global::ItemStack.Empty))ItemStack=source?.Clone()??global::ItemStack.Empty.Clone();
        base.Update(dt);
        RebirthSelectedDurability.CopyNativePresentation(this,equipmentPreview?((XUiController)xui.AssembleItem?.CurrentItemStackController??xui.AssembleItem?.CurrentEquipmentStackController):backpack?.SelectedItem);
        foreach(var view in backgrounds)if(view!=null)view.IsVisible=false;
    }
    public override void HandleClickComplete(){}
    public override void updateItemInfoWindow(XUiC_ItemStack stack){}
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="tooltip"){value="";return true;}
        return base.GetBindingValueInternal(ref value,name);
    }
}