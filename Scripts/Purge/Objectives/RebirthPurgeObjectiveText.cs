using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal static class RebirthPurgeObjectiveText
{
    internal static string Format(IEnumerable<RebirthPurgeObjectiveFrame.Biome> biomes,int target,Func<string,string> localize,int mode=0)
    {
        if(biomes==null || localize==null || target<1 || target>100)throw new ArgumentException();
        var lines=new StringBuilder();
        lines.AppendFormat(localize(mode==1?"xuiRebirthPurgeObjectiveTargetTier":"xuiRebirthPurgeObjectiveTarget"),target);
        foreach(var biome in biomes.OrderBy(b=>b.Name,StringComparer.Ordinal))
        {
            string key="biome_"+(biome.Name=="forest"?"pine_forest":biome.Name);
            string name=localize(key);if(name==key)name=biome.Name.Replace('_',' ');
            lines.Append('\n');
            if(biome.Eligible==0){lines.AppendFormat(localize("xuiRebirthPurgeObjectiveNotApplicable"),name);continue;}
            int done=RebirthPurgeProgression.Units(biome.Cleared,biome.ClearedByTier,mode);
            int required=RebirthPurgeProgression.Required(RebirthPurgeProgression.Units(biome.Eligible,biome.EligibleByTier,mode),target*75);
            lines.AppendFormat(localize(mode==1?"xuiRebirthPurgeObjectiveBiomeTier":"xuiRebirthPurgeObjectiveBiome"),name,RebirthPurgeProgression.Text(done,mode),RebirthPurgeProgression.Text(required,mode),RebirthPurgeProgression.Text(Math.Max(0,required-done),mode),biome.Discovered,biome.Eligible);
        }
        lines.Append('\n').Append(localize("xuiRebirthPurgeObjectiveRules"));
        return lines.ToString();
    }
}