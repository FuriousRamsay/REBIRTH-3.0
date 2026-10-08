using System;
using System.Collections.Generic;
using System.IO;

// Exact two-domain conservation. No inventory, gear state or journal mutation.
public static class RebirthBackpackLibraryConservation
{
    private static string Payload(ItemValue value,bool outerBackpack)
    {
        if(value==null||value.IsEmpty())return null;
        var snapshot=value.Clone();
        if(outerBackpack&&snapshot.Metadata!=null)snapshot.Metadata.Remove(RebirthBackpackLibraryContents.MetadataKey);
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);
            ItemValue.Write(snapshot,writer);writer.Flush();
            return Convert.ToBase64String(stream.ToArray());
        }
    }
    private static bool Add(Dictionary<string,long> totals,ItemStack stack,int sign)
    {
        if(stack==null||stack.itemValue==null||stack.count<0||stack.count>ushort.MaxValue)return false;
        if(stack.count==0)return true;
        if(stack.IsEmpty()||stack.itemValue.ItemClass==null||stack.count>stack.itemValue.ItemClass.MaxCount)return false;
        string key=Payload(stack.itemValue,false);if(key==null)return false;
        totals.TryGetValue(key,out long prior);totals[key]=prior+(long)sign*stack.count;return true;
    }
    public static bool IsConserved(ItemValue beforeBackpack,ItemValue afterBackpack,
        ItemStack beforeInventory,ItemStack afterInventory)
    {
        try
        {
            if(!RebirthBackpackLibraryContents.TryRead(beforeBackpack,out var before)||
                !RebirthBackpackLibraryContents.TryRead(afterBackpack,out var after)||before.Length!=after.Length)return false;
            string outer=Payload(beforeBackpack,true);
            if(outer==null||!string.Equals(outer,Payload(afterBackpack,true),StringComparison.Ordinal))return false;
            var totals=new Dictionary<string,long>(StringComparer.Ordinal);
            if(!Add(totals,beforeInventory,1)||!Add(totals,afterInventory,-1))return false;
            foreach(var stack in before)if(!Add(totals,stack,1))return false;
            foreach(var stack in after)if(!Add(totals,stack,-1))return false;
            foreach(long count in totals.Values)if(count!=0)return false;
            return true;
        }
        catch{return false;}
    }
}
