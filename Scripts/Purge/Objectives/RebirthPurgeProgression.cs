using System;
using System.Collections.Generic;
using System.Globalization;

// Integer quarter-points keep milestone boundaries exact. Count mode retains
// its original one-POI unit; callers keep raw POI statistics separately.
internal static class RebirthPurgeProgression
{
    internal const int Count=0, Tier=1;
    internal static int Weight(int tier)
    {
        if(tier<0||tier>255)throw new ArgumentOutOfRangeException(nameof(tier));
        switch(tier){case 0:return 4;case 1:return 8;case 2:return 14;case 3:return 22;case 4:return 32;case 5:return 44;default:return 60+16*(tier-6);}
    }
    internal static int Units(int count,IReadOnlyDictionary<int,int> tiers,int mode)
    {
        if(mode==Count)return count;
        if(mode!=Tier||tiers==null)throw new ArgumentException("Invalid progression basis.");
        long total=0;foreach(var pair in tiers)total+=(long)Weight(pair.Key)*pair.Value;
        return checked((int)total);
    }
    internal static int Required(int units,int numerator)
    {
        if(units<0||numerator<1||numerator>7500)throw new ArgumentOutOfRangeException();
        return (int)(((long)units*numerator+7499)/7500);
    }
    internal static string Text(int units,int mode)=>(units/(mode==Tier?4m:1m)).ToString("0.##",CultureInfo.InvariantCulture);
}
