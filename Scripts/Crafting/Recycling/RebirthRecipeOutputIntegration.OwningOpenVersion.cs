using System;
using System.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // The actual OnOpen may establish a zero observation baseline before the
    // first native Update. Ordinary getters still cannot initialize counters.
    internal static bool TryCaptureOwningOpenVersion(XUiC_RecipeStack card,XUiC_WorkstationWindowGroup group,long session,out long version)
    {
        version=-1;
        if(!ThreadManager.IsMainThread()||card==null||group==null||
            !RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(group,session)||
            !ReferenceEquals(card.GetParentByType<XUiC_WorkstationWindowGroup>(),group))return false;
        var cards=group.craftingQueue?.GetRecipesToCraft();if(cards==null||cards.Count(value=>ReferenceEquals(value,card))!=1)return false;
        version=nativeVersions.GetValue(card,_=>new NativeVersion()).Value;return version>=0;
    }
}
