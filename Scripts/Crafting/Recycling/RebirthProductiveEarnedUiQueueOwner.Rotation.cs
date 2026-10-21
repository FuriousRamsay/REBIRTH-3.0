using System;
using System.Linq;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Follow only the actual native CopyTo invocation from an already captured
    // insertion. Neither equal recipes nor a later queue scan can transfer it.
    [HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.CopyTo))]
    private static class PendingCopy
    {
        private sealed class Move
        {internal LocalInsertion Source;internal XUiC_RecipeStack From,To;internal int Index;internal object Handoff,Remote;}
        private static void Prefix(XUiC_RecipeStack __instance,XUiC_RecipeStack _recipeStack,bool __runOriginal,out Move __state)
        {
            __state=new Move{Handoff=RebirthProductiveEarnedUiCoordinator.BeforeQueueHandoffCopy(__instance,_recipeStack,__runOriginal),To=_recipeStack};
            BeforeRemoteCopy(__instance,_recipeStack,__runOriginal,out __state.Remote);
            try
            {
                if(!__runOriginal||!installed||ReferenceEquals(__instance,_recipeStack))return;
                var matches=local.Where(value=>ReferenceEquals(value.Card,__instance)).ToArray();
                if(matches.Length!=1)return;var source=matches[0];
                if(!IsPendingOwningInsertion(__instance,source.Station)||local.Any(value=>ReferenceEquals(value.Card,_recipeStack)))return;
                var cards=source.Queue.GetRecipesToCraft();int index=Array.FindIndex(cards,value=>ReferenceEquals(value,_recipeStack));
                if(index<0||cards.Count(value=>ReferenceEquals(value,_recipeStack))!=1)return;
                __state.Source=source;__state.From=__instance;__state.Index=index;
            }
            catch{/* Observation never changes native rotation. */}
        }
        private static void Postfix(bool __runOriginal,Move __state)
        {
            RebirthProductiveEarnedUiCoordinator.AfterQueueHandoffCopy(__state?.To,__runOriginal,__state?.Handoff);
            AfterRemoteCopy(__state?.To,__runOriginal,__state?.Remote);
            try
            {
                if(!__runOriginal||__state?.Source==null||!local.Contains(__state.Source)||
                    !ReferenceEquals(__state.Source.Card,__state.From)||!IsPendingOwningInsertion(__state.From,__state.Source.Station)||
                    Binding(InsertionRow(__state.To))!=__state.Source.FrozenBinding||
                    __state.To.GetRecipeCount()!=__state.Source.Count)return;
                var cards=__state.Source.Queue.GetRecipesToCraft();
                if(__state.Index>=cards.Length||!ReferenceEquals(cards[__state.Index],__state.To))return;
                __state.Source.Card=__state.To;__state.Source.Index=__state.Index;
            }
            catch{/* An uncertain copy retains the old original; no inferred transfer. */}
        }
    }
}
