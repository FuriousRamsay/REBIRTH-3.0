using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

/// <summary>Detached recovery calculation. Caller must supply an authoritative committed
/// failure receipt, its fixed roll seed and its recipe category; this is not debit or settlement.</summary>
public static class RebirthImprovisationRecovery
{
    public sealed class Plan
    {
        public string Category { get; private set; }
        public float Proficiency { get; private set; }
        public double RecoveryFraction { get; private set; }
        private readonly List<ItemStack> returned;
        private readonly List<ItemStack> lost;
        internal Plan(string category, float proficiency, double fraction,
            List<ItemStack> returned, List<ItemStack> lost)
        { Category=category; Proficiency=proficiency; RecoveryFraction=fraction;
          this.returned=returned; this.lost=lost; }
        public ReadOnlyCollection<ItemStack> ReturnedSnapshot() => Snapshot(returned);
        public ReadOnlyCollection<ItemStack> LostSnapshot() => Snapshot(lost);
        private static ReadOnlyCollection<ItemStack> Snapshot(List<ItemStack> source)
        {
            var result=new List<ItemStack>(source.Count);
            foreach(ItemStack item in source) result.Add(item.Clone());
            return result.AsReadOnly();
        }
    }

    public static bool TryPlan(string category, IDictionary<string,float> categoryProgress,
        IList<ItemStack> paid, uint receiptSeed, double minimumRecovery, double maximumRecovery,
        out Plan plan)
    {
        plan=null;
        if(string.IsNullOrEmpty(category)||categoryProgress==null||paid==null||paid.Count<1||paid.Count>9||
            double.IsNaN(minimumRecovery)||double.IsNaN(maximumRecovery)||
            minimumRecovery<0||maximumRecovery>=1||minimumRecovery>maximumRecovery) return false;
        // Exact category IDs only. No generic/parent skill or neighbouring-category fallback.
        foreach(char c in category)
            if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='.'||c=='-')) return false;
        if(category.Length>96) return false;
        float proficiency=0;
        // Do not inherit a dictionary's case-insensitive comparison policy.
        foreach(var entry in categoryProgress)
            if(string.Equals(entry.Key,category,StringComparison.Ordinal))
            { proficiency=entry.Value; break; }
        if(float.IsNaN(proficiency)||float.IsInfinity(proficiency)) return false;
        proficiency=Math.Max(0,Math.Min(100,proficiency));
        double fraction=minimumRecovery+(maximumRecovery-minimumRecovery)*(proficiency/100d);
        var returned=new List<ItemStack>(); var lost=new List<ItemStack>();
        uint roll=receiptSeed;
        for(int i=0;i<paid.Count;i++)
        {
            ItemStack item=paid[i];
            if(item==null||item.itemValue==null||item.count<1||item.count>ushort.MaxValue||item.IsEmpty()) return false;
            // Stable receipt/slot roll, randomized rounding allows recovery of single units.
            // Holding the receipt seed fixed makes recovery monotonic with proficiency.
            roll=unchecked(roll*1664525u+1013904223u);
            double expected=item.count*fraction;
            int count=(int)Math.Floor(expected+roll/4294967296d);
            count=Math.Max(0,Math.Min(item.count,count));
            if(count>0){ItemStack copy=item.Clone();copy.count=count;returned.Add(copy);}
            if(count<item.count){ItemStack copy=item.Clone();copy.count=item.count-count;lost.Add(copy);}
        }
        plan=new Plan(category,proficiency,fraction,returned,lost);
        return true;
    }
}
