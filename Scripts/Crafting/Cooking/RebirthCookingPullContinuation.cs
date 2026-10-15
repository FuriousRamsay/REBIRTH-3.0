using System;

// Source-free continuation only. Owner receipt was already delivered exactly
// once. This cannot fetch, credit, refund or repeat the original withdrawal.
internal sealed class RebirthCookingPullContinuation
{
    internal enum Outcome { Waiting, Completed, ScopeChanged }
    private readonly Func<bool> current,blocked;
    private readonly Action place;
    private bool completed;
    internal bool IsPending=>!completed;
    internal RebirthCookingPullContinuation(Func<bool> isCurrent,Func<bool> isBlocked,Action placeOnce)
    {current=isCurrent??throw new ArgumentNullException(nameof(isCurrent));blocked=isBlocked??throw new ArgumentNullException(nameof(isBlocked));place=placeOnce??throw new ArgumentNullException(nameof(placeOnce));}
    internal Outcome Advance()
    {
        if(completed)return Outcome.Completed;
        if(!current()){completed=true;return Outcome.ScopeChanged;}
        if(blocked())return Outcome.Waiting;
        if(!current()){completed=true;return Outcome.ScopeChanged;}
        // Claim before callbacks/setters; an exception cannot replay placement.
        completed=true;place();return Outcome.Completed;
    }
}
