using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthSelectedItemSalePrice : XUiController
{
    private System.Func<ItemStack> selection;
    private float nextRefresh;
    public override void Init()
    {
        base.Init();
        var context=GetParentByType<XUiC_RebirthContainerWorkspace>();
        var crafting=GetParentByType<XUiC_RebirthCraftingItemContext>();
        var library=GetParentByType<XUiC_RebirthBackpackLibrary>();
        var sale=GetParentByType<XUiC_RebirthBackpackSellStash>();
        var character=GetParentByType<XUiC_RebirthCharacterBackpack>();
        var editor=GetParentByType<XUiC_RebirthEditorBackpack>();
        var assemble=GetParentByType<XUiC_RebirthAssembleItem>();
        var info=GetParentByType<XUiC_ItemInfoWindow>();
        if(context!=null)selection=()=> (context.SelectedItem as XUiC_ItemStack)?.ItemStack;
        else if(crafting!=null)selection=()=>crafting.SelectedSlot?.ItemStack;
        else if(library!=null)selection=()=>library.SelectedItemStack;
        else if(sale!=null)selection=()=>sale.SelectedItemStack;
        else if(character!=null)selection=()=> (character.SelectedItem as XUiC_ItemStack)?.ItemStack;
        else if(editor!=null)selection=()=>editor.SelectedItem?.ItemStack;
        else if(assemble!=null)selection=()=>assemble.ItemStack;
        else if(info!=null)selection=()=>info.itemStack;
        else for(var owner=Parent;owner!=null;owner=owner.Parent)
        {
            info=owner.GetChildByType<XUiC_ItemInfoWindow>();
            if(info!=null){var selectedInfo=info;selection=()=>selectedInfo.itemStack;break;}
        }
    }
    public override void OnOpen(){base.OnOpen();nextRefresh=0;Refresh();}
    public override void Update(float dt)
    {
        base.Update(dt);
        if(UnityEngine.Time.realtimeSinceStartup<nextRefresh)return;
        nextRefresh=UnityEngine.Time.realtimeSinceStartup+.25f;
        Refresh();
    }
    private void Refresh()
    {
        var label=ViewComponent as XUiV_Label;
        if(label==null)return;
        if(Parent?.ViewComponent!=null){int width=Parent.ViewComponent.Size.x;var position=label.Position;int right=System.Math.Max(0,width-label.Size.x-14);if(position.x!=right){label.Position=new Vector2i(right,position.y);label.TryUpdatePosition();}}
        var stack=selection?.Invoke();
        string text=stack==null||stack.IsEmpty()?string.Empty:
            RebirthItemSaleEstimate.TryGetSaleBundle(xui,stack,out var value,out var quantity)?"SELL PRICE: "+value.ToString("N0")+" $"+(quantity>1?" for "+quantity.ToString("N0"):""):"SELL PRICE: —";
        if(label.Text!=text)label.SetTextImmediately(text);
    }
}

[Preserve]
public sealed class XUiC_RebirthBackpackCashCaption : XUiController
{
    private float nextRefresh;
    public override void Update(float dt)
    {
        base.Update(dt);
        if(UnityEngine.Time.realtimeSinceStartup<nextRefresh)return;
        nextRefresh=UnityEngine.Time.realtimeSinceStartup+.25f;
        var label=ViewComponent as XUiV_Label;
        if(label==null||xui?.PlayerInventory==null)return;
        string text=xui.PlayerInventory.CurrencyAmount.ToString("N0")+" $";
        if(label.Text!=text)label.SetTextImmediately(text);
    }
}