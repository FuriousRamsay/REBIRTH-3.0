using System;

// Display-side destination choice only. The saved custody receipt revalidates it.
public static class RebirthBackpackSectionDestination
{
    public static bool TryFind(ItemStack source,int length,Func<int,ItemStack> read,Func<int,bool> locked,
        out int slot,out int quantity)
    {
        slot=-1;quantity=0;
        if(source==null||source.IsEmpty()||source.itemValue?.ItemClass==null||source.count<1
            ||source.count>source.itemValue.ItemClass.MaxCount||length<1||length>169||read==null)return false;
        try
        {
            for(int pass=0;pass<2;pass++)for(int i=0;i<length;i++)
            {
                if(locked!=null&&locked(i))continue;
                var target=read(i);if(target==null)continue;
                if(pass==0)
                {
                    if(target.IsEmpty()||target.count<1||target.itemValue?.ItemClass==null)continue;
                    int count=Math.Min(source.count,Math.Max(0,target.itemValue.ItemClass.MaxCount-target.count));
                    if(count==0)continue;
                    var moved=source.Clone();moved.count=count;
                    if(!RebirthBackpackLibraryTransfer.CanMerge(target,moved))continue;
                    slot=i;quantity=count;return true;
                }
                if(target.IsEmpty()){slot=i;quantity=source.count;return true;}
            }
        }
        catch{slot=-1;quantity=0;return false;}
        return false;
    }
}