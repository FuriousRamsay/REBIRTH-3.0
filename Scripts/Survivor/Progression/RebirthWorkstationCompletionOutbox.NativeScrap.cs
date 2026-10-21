using System;

internal static partial class RebirthWorkstationCompletionOutbox
{
    // Configured native scrap was previously excluded from REBIRTH canonical
    // craft awards. Its real native effects are conserved independently; this
    // records an explicit no-awards disposition, never synthetic effect flags.
    private static bool NativeScrapCanonicalUntouched(State state)
        =>!state.Skill&&!state.Statistics&&!state.Decision&&!state.SkillAttempted&&!state.StatisticsAttempted&&!state.SkillMayInvoke&&!state.StatisticsMayInvoke&&
            !state.SkillRefusedBeforeEffect&&!state.ReagentAttempted&&!state.ReagentObserved&&!state.ReagentPublished&&!state.NativeReagentPrepared&&!state.ReagentSetterEntered&&
            state.Reagent==string.Empty&&state.ReagentBefore==string.Empty&&state.ReagentAfter==string.Empty;
    private static bool ValidNativeScrapNoAwards(State state)
        =>state?.Original?.NativeAdditionalScrap==true&&state.Role=="server"&&state.Terminal&&!state.Acknowledged&&NativeScrapCanonicalUntouched(state);
    internal static bool CompleteNativeScrapNoAwards(State state)
    {
        if(state?.Original?.NativeAdditionalScrap!=true||state.Role!="server"||!ThreadManager.IsMainThread()||
            GameManager.Instance?.World?.IsRemote()!=false||state.Original.WorldGuid!=CurrentGuid(GameManager.Instance.World)||
            !NativeScrapCanonicalUntouched(state)||!TryReadSaved(state.Original,out var head)||!ReferenceEquals(head,state))return false;
        state.NoCanonicalAwards=true;state.Terminal=true;return Save(state);
    }
}
