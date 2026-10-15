using System;

// Shared native scroll sizing for every added statistics page.
internal static class RebirthStatisticsScrollLayout
{
    internal static void Refresh(XUiController scroll,XUiController content,int width,int height,bool reset=false)
    {
        if(scroll is XUiC_RebirthCharacterOverviewList list)
        {
            var rows=list.GetChildById("listContent")?.Children;
            int stride=rows!=null&&rows.Count>1?Math.Max(1,Math.Abs(rows[1].ViewComponent.Position.y-rows[0].ViewComponent.Position.y)):Math.Max(1,height);
            list.SetItemCount(Math.Max(0,height)/stride,string.Empty);
            if(reset)list.ResetPosition();
            return;
        }
        var view=scroll?.ViewComponent as XUiV_ScrollView;if(view==null)return;
        if(content?.ViewComponent!=null)
        {
            var old=content.ViewComponent.Size;var size=new Vector2i(width,Math.Max(1,height));
            if(old.x!=size.x||old.y!=size.y){content.ViewComponent.Size=size;if(size.y<old.y)reset=true;}
        }
        view.scrollView?.InvalidateBounds();view.updateScrollbarsNextUpdate=true;
        if(reset)view.ResetPosition();
    }
}
