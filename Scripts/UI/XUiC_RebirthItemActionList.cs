using UnityEngine.Scripting;
// A single visibility owner for every REBIRTH native action list.
[Preserve]
public class XUiC_RebirthItemActionList : XUiC_ItemActionList
{
    public override void Update(float dt)
    {
        base.Update(dt);
        bool any=itemActionEntries!=null&&itemActionEntries.Count>0;
        if(entryList==null)return;
        foreach(var entry in entryList)
        {
            if(entry?.ViewComponent==null)continue;
            bool show=any&&entry.ItemActionEntry!=null;
            entry.ViewComponent.IsVisible=show;
            if(entry.ViewComponent.UiTransform!=null)entry.ViewComponent.UiTransform.gameObject.SetActive(show);
        }
    }
}