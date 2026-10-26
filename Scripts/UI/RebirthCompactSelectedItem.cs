using System;

// Uses the Creative/Character inventory action models and shared stat rows in
// compact vehicle and turret inspectors. The source remains the real native slot.
internal sealed class RebirthCompactSelectedItem
{
    private readonly XUiController owner;
    private readonly XUiC_ItemActionList actions;
    private readonly string prefix;
    private readonly string[] names=new string[6],values=new string[6];
    private XUiC_ItemStack renderedSource;
    private string renderedSignature;
    internal RebirthCompactSelectedItem(XUiController owner,string prefix)
    {
        this.owner=owner;this.prefix=prefix;
        actions=owner.GetChildById(prefix+"Actions") as XUiC_ItemActionList;
    }
    internal void Render(XUiC_ItemStack slot,ItemStack stack)
    {
        bool valid=stack!=null&&!stack.IsEmpty()&&stack.itemValue?.ItemClass!=null;
        if(valid)RebirthSelectedItemRows.Build(owner.xui,stack,null,null,6,names,values);
        for(int i=0;i<6;i++)
        {
            var label=owner.GetChildById(prefix+"Stat"+i)?.ViewComponent as XUiV_Label;
            if(label!=null)label.SetTextImmediately(valid&&!string.IsNullOrEmpty(names[i])?names[i]+": "+values[i]:string.Empty);
        }
        if(actions==null)return;
        string signature=valid?stack.itemValue.type+":"+stack.count+":"+stack.itemValue.Quality+":"+
            stack.itemValue.UseTimes+":"+stack.itemValue.ModificationCount+":"+stack.itemValue.CosmeticModCount:string.Empty;
        if(ReferenceEquals(renderedSource,slot)&&renderedSignature==signature)return;
        renderedSource=slot;renderedSignature=signature;
        actions.SetCraftingActionList(valid&&slot!=null
            ?XUiC_ItemActionList.ItemActionListTypes.Item:XUiC_ItemActionList.ItemActionListTypes.None,valid?slot:null);
        if(valid&&slot!=null)
        {
            RebirthCharacterGearEquipEntry.Adapt(actions,slot);
            RebirthEditorActions.Adapt(actions,slot);
        }
        actions.Update(0f);
        foreach(var entry in actions.entryList)
            if(entry?.ViewComponent!=null)entry.ViewComponent.IsVisible=valid&&entry.ItemActionEntry!=null;
    }
}
