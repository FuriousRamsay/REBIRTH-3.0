using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Detached exact native personal inventory debit. Demands must come from original native preflight.
internal sealed class RebirthTheorySoloNativePaymentPlan
{
    internal string BeforeHash { get;private set; }
    internal string AfterHash { get;private set; }
    private readonly List<ItemStack> paid;
    private RebirthTheorySoloNativePaymentPlan(string before,string after,List<ItemStack> removed){BeforeHash=before;AfterHash=after;paid=removed;}
    internal List<ItemStack> PaidSnapshot(){var copy=new List<ItemStack>();foreach(var item in paid)copy.Add(item.Clone());return copy;}
    internal static bool TryCreate(IList<ItemStack> demand,int batches,IList<ItemStack> bag,IList<ItemStack> belt,out RebirthTheorySoloNativePaymentPlan plan)
    {
        plan=null;
        try
        {
            if(demand==null||demand.Count==0||demand.Count>64||batches<1||batches>10000||!TryHash(bag,belt,out var before))return false;
            var b=Clone(bag);var t=Clone(belt);var removed=new List<ItemStack>();
            foreach(var item in demand)
            {
                if(item==null||item.itemValue==null||item.itemValue.IsEmpty()||item.count<1)return false;
                long quantity=(long)item.count*batches;if(quantity>Int32.MaxValue)return false;
                int remaining=(int)quantity;
                Debit(b,item.itemValue.type,ref remaining,removed);
                Debit(t,item.itemValue.type,ref remaining,removed);
                if(remaining!=0)return false;
            }
            if(!TryHash(b,t,out var after))return false;
            plan=new RebirthTheorySoloNativePaymentPlan(before,after,removed);return true;
        }
        catch{return false;}
    }
    private static ItemStack[] Clone(IList<ItemStack> source)
    {
        var copy=new ItemStack[source.Count];
        for(int i=0;i<copy.Length;i++)copy[i]=source[i]==null||source[i].count==0?new ItemStack(new ItemValue(),0):source[i].Clone();
        return copy;
    }
    private static void Debit(ItemStack[] slots,int type,ref int needed,List<ItemStack> removed)
    {
        for(int i=0;i<slots.Length&&needed>0;i++)
        {
            var stack=slots[i];if(stack.count==0||stack.itemValue.type!=type||stack.itemValue.HasModSlots&&stack.itemValue.HasMods())continue;
            int take=stack.itemValue.ItemClass.CanStack()?Math.Min(stack.count,needed):1;
            if(!stack.itemValue.ItemClass.CanStack()&&stack.count!=1)throw new FormatException();
            var paid=stack.Clone();paid.count=take;removed.Add(paid);
            stack.count-=take;needed-=take;
            if(stack.count==0)slots[i]=new ItemStack(new ItemValue(),0);
        }
    }
    internal static bool TryHash(IList<ItemStack> bag,IList<ItemStack> belt,out string hash)
    {
        hash=null;
        try
        {
            if(bag==null||bag.Count>169||belt==null||belt.Count>20)return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                int budget=1048576;
                foreach(var array in new[]{bag,belt})
                {
                    writer.Write(array.Count);
                    foreach(var item in array)
                    {
                        if(item==null||item.count==0){writer.Write(0);continue;}
                        if(item.count<0||item.count>UInt16.MaxValue||item.itemValue==null||item.itemValue.IsEmpty()||item.itemValue.ItemClass==null)return false;
                        string encoded=RebirthNativeItemCodec.Encode(item.itemValue);budget-=encoded.Length;if(budget<0)return false;
                        writer.Write(item.count);writer.Write(encoded);
                    }
                }
                writer.Flush();using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
            return true;
        }
        catch{return false;}
    }
}
