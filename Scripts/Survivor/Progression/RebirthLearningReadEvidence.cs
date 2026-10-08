using System;
using System.Collections.Generic;

// Exact persisted server reading receipts, never possession, generic knowledge or Theory gain.
public static class RebirthLearningReadEvidence
{
    private static readonly Dictionary<string,string> Lessons=new Dictionary<string,string>(StringComparer.Ordinal)
    {
        {"literature.read.rebirthTheoryCookingPrimer","rebirth.study.cooking_principles"},
        {"literature.recipe_read.recipe.medicalFirstAidKit","rebirth.study.first_aid_recipe"},
        {"literature.recipe_read.recipe.resourceRepairKit","rebirth.study.repair_manual"}
    };
    public static bool IsTutorialMarker(string id) => id!=null&&Lessons.ContainsKey(id);
    public static void CreditVerified(IEnumerable<string> markers,ISet<string> credited,Func<string,bool> credit)
    {
        if(markers==null||credited==null||credit==null)return;
        foreach(string marker in markers)
        {
            string stat;
            if(marker==null||!Lessons.TryGetValue(marker,out stat)||credited.Contains(marker))continue;
            if(credit(stat))credited.Add(marker);
        }
    }
}
