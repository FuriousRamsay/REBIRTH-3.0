using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Pages real slots without rebinding them or changing native storage/queue capacity.</summary>
[Preserve]
public sealed class XUiC_RebirthCookingScroll : XUiController
{
    private XUiController grid;
    private XUiController[] entries;
    private XUiView[] scrollControls;
    private XUiV_Label[] dishNames;
    private XUiView thumbView;
    private int row, columns, visibleRows, pitch;
    private float drag;
    public override void Init()
    {
        base.Init(); grid=GetChildById("scrollGrid");
        bool bag=ViewComponent.ID=="backpackScroll", queue=ViewComponent.ID=="queueScroll";
        columns=bag?13:queue?1:3;visibleRows=bag||queue?2:1;pitch=bag?96:queue?82:75;
        entries=queue?(XUiController[])grid.GetChildrenByType<XUiC_RecipeStack>():grid.GetChildrenByType<XUiC_ItemStack>();
        // Native queues execute from the last slot. Reverse presentation, never storage.
        if(queue)entries=entries.Reverse().ToArray();
        dishNames=new XUiV_Label[entries.Length];
        for(int i=0;i<entries.Length;i++)
            if(entries[i] is XUiC_RecipeStack) dishNames[i]=entries[i].GetChildById("dishName")?.ViewComponent as XUiV_Label;
        scrollControls=new XUiView[4];
        string[] controlIds={"scrollUp","scrollDown","scrollTrack","scrollThumb"};
        for(int i=0;i<controlIds.Length;i++)scrollControls[i]=GetChildById(controlIds[i]).ViewComponent;
        thumbView=scrollControls[3];
        GetChildById("scrollUp").OnPress+=(s,b)=>Scroll(-1);
        GetChildById("scrollDown").OnPress+=(s,b)=>Scroll(1);
        foreach(var e in entries)e.OnScroll+=(s,d)=>Scroll(d>0?-1:1);
        OnScroll+=(s,d)=>Scroll(d>0?-1:1);
        var thumb=GetChildById("scrollThumb");
        thumb.OnScroll+=(s,d)=>Scroll(d>0?-1:1);
        thumb.OnDrag+=(s,type,delta)=>
        {
            if(type==EDragType.DragStart){drag=0;return;}
            if(type!=EDragType.Dragging)return;
            int max=Math.Max(0,(Count+columns-1)/columns-visibleRows);
            if(max==0)return;
            float step=Math.Max(1,(visibleRows*pitch-thumb.ViewComponent.Size.y)/(float)max);
            drag-=delta.y;
            int change=(int)(drag/step);
            if(change!=0){drag-=change*step;Scroll(change);}
        };
    }
    public override void OnOpen(){base.OnOpen();row=0;Apply();}
    public override void Update(float dt){base.Update(dt);if(windowGroup.isShowing)Apply();}
    private int Count => grid is XUiC_RebirthCraftingInventory bag?bag.PhysicalSlotCount:entries.Length;
    private void Scroll(int delta){row=Math.Max(0,Math.Min(Math.Max(0,(Count+columns-1)/columns-visibleRows),row+delta));Apply();}
    private void Apply()
    {
        int count=Count;
        int rows=(count+columns-1)/columns,max=Math.Max(0,rows-visibleRows);row=Math.Min(row,max);
        for(int i=0;i<entries.Length;i++)
        {
            var v=entries[i].ViewComponent;if(v==null)continue;
            // Queue time processing continues in hidden slots through always_update.
            v.Position=new Vector2i((i%columns)*pitch,-(i/columns-row)*pitch);
            v.IsVisible=i<count&&i/columns>=row&&i/columns<row+visibleRows;
            if(entries[i] is XUiC_RecipeStack recipeSlot && dishNames[i] is XUiV_Label name)
            {
                var recipe=recipeSlot.GetRecipe();
                name.Text=recipe==null?"":Localization.Get(recipe.GetName());
            }
        }
        foreach(var control in scrollControls)control.IsVisible=max>0;
        var thumb=thumbView;
        int track=visibleRows*pitch;
        thumb.Size=new Vector2i(7,Math.Max(20,track*visibleRows/Math.Max(1,rows)));
        thumb.Position=new Vector2i(thumb.Position.x,max==0?0:-row*(track-thumb.Size.y)/max);
    }
}
