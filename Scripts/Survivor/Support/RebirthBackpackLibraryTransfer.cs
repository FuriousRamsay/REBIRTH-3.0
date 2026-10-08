using System;
using System.IO;

// Detached plans only. The authoritative custody transaction must save and publish both sides together.
public static class RebirthBackpackLibraryTransfer
{
    internal static bool CanMerge(ItemStack target,ItemStack moved)
    {
        if(!target.CanStackWith(moved))return false;
        try
        {
            using(var a=new MemoryStream())using(var b=new MemoryStream())
            using(var wa=MemoryPools.poolBinaryWriter.AllocSync(true))using(var wb=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                wa.SetBaseStream(a);wb.SetBaseStream(b);
                ItemValue.Write(target.itemValue,wa);ItemValue.Write(moved.itemValue,wb);
                wa.Flush();wb.Flush();
                if(a.Length!=b.Length)return false;
                byte[] x=a.ToArray(),y=b.ToArray();
                for(int i=0;i<x.Length;i++)if(x[i]!=y[i])return false;
                return true;
            }
        }
        catch{return false;}
    }
    public static bool TryDeposit(ItemValue backpack,ItemStack source,int librarySlot,int count,
        out ItemValue nextBackpack,out ItemStack nextSource)
    {
        nextBackpack=null;nextSource=null;
        ItemStack[] contents;
        if(!RebirthBackpackLibraryContents.TryRead(backpack,out contents)||
            librarySlot<0||librarySlot>=contents.Length||source==null||source.IsEmpty()||
            count<=0||count>source.count||!RebirthBackpackLibraryPolicy.IsLearningMaterial(source.itemValue))return false;
        var moved=source.Clone();moved.count=count;
        if(moved.itemValue?.ItemClass==null||count>moved.itemValue.ItemClass.MaxCount)return false;
        var target=contents[librarySlot];
        if(!target.IsEmpty()&&!CanMerge(target,moved))return false;
        var replacement=target.IsEmpty()?moved:target.Clone();
        if(!target.IsEmpty())replacement.count+=count;
        contents[librarySlot]=replacement;
        ItemValue candidate;
        if(!RebirthBackpackLibraryContents.TryWrite(backpack,contents,out candidate))return false;
        var remaining=source.Clone();remaining.count-=count;
        var sourceAfter=remaining.count==0?ItemStack.Empty.Clone():remaining;
        if(!RebirthBackpackLibraryConservation.IsConserved(backpack,candidate,source,sourceAfter))return false;
        nextSource=sourceAfter;
        nextBackpack=candidate;return true;
    }

    public static bool TryWithdraw(ItemValue backpack,int librarySlot,int count,ItemStack destination,
        out ItemValue nextBackpack,out ItemStack nextDestination)
    {
        nextBackpack=null;nextDestination=null;
        ItemStack[] contents;
        if(!RebirthBackpackLibraryContents.TryRead(backpack,out contents)||
            librarySlot<0||librarySlot>=contents.Length||destination==null)return false;
        var source=contents[librarySlot];
        if(source.IsEmpty()||count<=0||count>source.count)return false;
        var moved=source.Clone();moved.count=count;
        if(moved.itemValue?.ItemClass==null||count>moved.itemValue.ItemClass.MaxCount)return false;
        if(!destination.IsEmpty()&&!CanMerge(destination,moved))return false;
        var received=destination.IsEmpty()?moved:destination.Clone();
        if(!destination.IsEmpty())received.count+=count;
        source.count-=count;
        if(source.count==0)contents[librarySlot]=ItemStack.Empty.Clone();
        ItemValue candidate;
        if(!RebirthBackpackLibraryContents.TryWrite(backpack,contents,out candidate))return false;
        if(!RebirthBackpackLibraryConservation.IsConserved(backpack,candidate,destination,received))return false;
        nextBackpack=candidate;nextDestination=received;return true;
    }
}