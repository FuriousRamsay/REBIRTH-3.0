using System;using System.IO;static class ActualComparator{public static bool IsSameStackSnapshot(ItemStack current,ItemStack admitted)
    {
        try{return SameStack(current,admitted);}catch{return false;}
    }
    private static bool SameStack(ItemStack a, ItemStack b)
    {
        if(a==null||b==null)return a==null&&b==null;
        if(a.count!=b.count)return false;
        if(a.itemValue==null||b.itemValue==null)return a.itemValue==null&&b.itemValue==null;
        using(var left=new MemoryStream()) using(var right=new MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(left);a.itemValue.Write(writer);writer.Flush();}
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(right);b.itemValue.Write(writer);writer.Flush();}
            if(left.Length!=right.Length)return false;
            byte[] x=left.GetBuffer(),y=right.GetBuffer();
            for(int i=0;i<left.Length;i++)if(x[i]!=y[i])return false;
            return true;
        }
    }

}
