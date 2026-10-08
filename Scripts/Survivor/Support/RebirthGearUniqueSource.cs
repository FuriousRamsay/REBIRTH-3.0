// Detached source selection for callers without a native slot controller.
// Exact bytes must identify ONE usable cell; ambiguity never chooses an older item.
internal static class RebirthGearUniqueSource
{
    internal static bool TryFind(RebirthGearInventorySnapshot snapshot,string itemData,out bool bag,out int index)
    {
        bag=false;index=-1;
        if(snapshot==null||string.IsNullOrEmpty(itemData)||snapshot.Bag==null||snapshot.Belt==null||
            snapshot.OwnedBeltSlots<0||snapshot.OwnedBeltSlots>snapshot.Belt.Length)return false;
        int matches=0;bool selectedBag=false;int selectedIndex=-1;
        for(int side=0;side<2;side++)
        {
            var cells=side==0?snapshot.Bag:snapshot.Belt;
            int limit=side==0?cells.Length:snapshot.OwnedBeltSlots;
            for(int i=0;i<limit;i++)
            {
                var cell=cells[i];
                if(cell==null||cell.Count<=0||cell.ItemData!=itemData)continue;
                if(++matches>1)return false;
                selectedBag=side==0;selectedIndex=i;
            }
        }
        if(matches!=1)return false;
        bag=selectedBag;index=selectedIndex;return true;
    }
}