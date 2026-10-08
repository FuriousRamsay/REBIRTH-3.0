using UnityEngine.Scripting;
// Presentation-only native item_stack; uses the same square icon, quality rail and count as Backpack.
[Preserve]
public sealed class XUiC_RebirthEditorPreviewSlot : XUiC_ItemStack
{
    private XUiC_RebirthAssembleItem equipment;
    private XUiC_RebirthEditorBackpack backpack;
    private bool equipmentPreview;
    private readonly XUiV_Sprite[] backgrounds=new XUiV_Sprite[3];
    private XUiV_Label countLabel;
    public override void Init()
    {
        base.Init();
        var root=GetParentByType<XUiC_RebirthItemEditorHeader>();
        equipment=root?.GetChildByType<XUiC_RebirthAssembleItem>();backpack=root?.GetChildByType<XUiC_RebirthEditorBackpack>();
        equipmentPreview=ReferenceEquals(root?.GetChildById("editorEquipmentPreview"),this);
        backgrounds[0]=GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite;
        backgrounds[1]=GetChildById("background")?.ViewComponent as XUiV_Sprite;
        backgrounds[2]=GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
        countLabel=GetChildById("stackValue")?.ViewComponent as XUiV_Label;
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
        foreach(var view in backgrounds)if(view!=null)view.IsVisible=false;
        var count=countLabel;
        if(count!=null&&!ItemStack.IsEmpty())
        {
            bool quality=ItemStack.itemValue.ItemClass.HasQuality;
            bool drink=RebirthConsumableResolver.TryResolve(ItemStack.itemValue,out var definition)&&definition.IsDrink;
                        count.Alignment=quality||drink&&ItemStack.count==1?NGUIText.Alignment.Center:NGUIText.Alignment.Right;
            if(drink)count.SetTextImmediately(ItemStack.count>1?ItemStack.count.ToString():RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(ItemStack.itemValue,definition)));
        }
    }
    public override void HandleClickComplete(){}
    public override void updateItemInfoWindow(XUiC_ItemStack stack){}
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="tooltip"){value="";return true;}
        return base.GetBindingValueInternal(ref value,name);
    }
}