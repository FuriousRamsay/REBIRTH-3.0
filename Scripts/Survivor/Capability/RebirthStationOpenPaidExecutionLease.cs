using System;
using System.Collections.Generic;

// INACTIVE candidate. Requires future readonly-paid UI/writeback protocol before any guard installation.
// Actual local server window only; a client DTO cannot mint a remote execution lease.
internal sealed class RebirthStationOpenPaidExecutionLease : IDisposable
{
    [ThreadStatic] private static RebirthStationOpenPaidExecutionLease active;
    private readonly XUiC_WorkstationWindowGroup group;
    private readonly object ui,window;
    private readonly EntityPlayer viewer;
    private readonly RebirthWorldCharacterRecord viewerOwner;
    private readonly string viewerCreation;
    private readonly TileEntityWorkstation station;
    private readonly Dictionary<RecipeQueueItem,RebirthStationCompletionOriginalScope> originals;
    private readonly Dictionary<RecipeQueueItem,Recipe> recipes;
    private readonly int thread;
    private bool closed;
    internal bool YieldedToUiOwnedJob {get;private set;}
    private RebirthStationOpenPaidExecutionLease(XUiC_WorkstationWindowGroup g,EntityPlayer v,RebirthWorldCharacterRecord owner,
        TileEntityWorkstation s,Dictionary<RecipeQueueItem,RebirthStationCompletionOriginalScope> scopes)
    {group=g;ui=g.xui;window=g.windowGroup;viewer=v;viewerOwner=owner;viewerCreation=owner.Origin.CreationId;station=s;originals=scopes;recipes=new Dictionary<RecipeQueueItem,Recipe>(ReferenceItems.Instance);foreach(var pair in scopes)recipes.Add(pair.Key,pair.Key.Recipe);thread=Environment.CurrentManagedThreadId;}
    internal static bool TryEnter(XUiC_WorkstationWindowGroup group,out RebirthStationOpenPaidExecutionLease lease)
    {
        lease=null;
        try
        {
            if(active!=null||group?.windowGroup?.isShowing!=true||group.xui?.playerUI?.entityPlayer==null)return false;
            var viewer=group.xui.playerUI.entityPlayer;var station=group.WorkstationData?.TileEntity;
            if(station==null||!station.IsUserAccessing()||!RebirthWorldCharacterService.TryGet(viewer,out var owner)||owner?.Origin==null||
                !RebirthStationLiveAccess.TryResolve(viewer,station.ToWorldPos(),owner.Origin.CreationId,out var held,out var heldOwner)||
                !ReferenceEquals(held,station)||!ReferenceEquals(heldOwner,owner)||station.Queue==null||station.Queue.Length==0)return false;
            var scopes=new Dictionary<RecipeQueueItem,RebirthStationCompletionOriginalScope>(ReferenceItems.Instance);
            foreach(var queued in station.Queue)
            {
                if(queued?.Recipe==null||!RebirthStationGridQueue.IsMarked(queued.Recipe)||RebirthCookingBatch.IsBatch(queued.Recipe))continue;
                if(scopes.Count>=64||scopes.ContainsKey(queued)||!RebirthStationCompletionOriginalScope.TryCapture(station,queued,out var original))return false;
                // Existing normal/discovery gate remains authoritative; this candidate does not bypass policy.
                bool handled=RebirthStationQueuedDiscoveryAuthorization.TryEvaluate(station,queued,original.Player,out var allowed,out _);
                if(handled?!allowed:RebirthCapabilityService.EvaluateRecipe(original.Player,queued.Recipe.GetName())?.IsAllowed!=true)return false;
                scopes.Add(queued,original);
            }
            if(scopes.Count==0)return false;
            var candidate=new RebirthStationOpenPaidExecutionLease(group,viewer,owner,station,scopes);
            var current=station.Queue[station.Queue.Length-1];
            if(!candidate.Owns(current))return false;
            active=candidate;lease=candidate;return true;
        }
        catch{return false;}
    }
    private bool WindowCurrent()
    {
        try
        {
            if(closed||thread!=Environment.CurrentManagedThreadId||!ReferenceEquals(group.xui,ui)||!ReferenceEquals(group.windowGroup,window)||
                group.windowGroup?.isShowing!=true||!ReferenceEquals(group.xui?.playerUI?.entityPlayer,viewer)||
                !ReferenceEquals(group.WorkstationData?.TileEntity,station)||!station.IsUserAccessing()||
                !RebirthStationLiveAccess.TryResolve(viewer,station.ToWorldPos(),viewerCreation,out var held,out var owner)||
                !ReferenceEquals(held,station)||!ReferenceEquals(owner,viewerOwner))return false;
            foreach(var original in originals.Values)if(!original.IsCurrent())return false;
            return !closed&&thread==Environment.CurrentManagedThreadId&&ReferenceEquals(group.xui,ui)&&ReferenceEquals(group.windowGroup,window)&&
                group.windowGroup?.isShowing==true&&ReferenceEquals(group.xui?.playerUI?.entityPlayer,viewer)&&
                ReferenceEquals(group.WorkstationData?.TileEntity,station)&&station.IsUserAccessing();
        }
        catch{return false;}
    }
    private bool Owns(RecipeQueueItem queued)
    {
        if(!WindowCurrent()||queued?.Recipe==null||!RebirthStationGridQueue.IsMarked(queued.Recipe)||RebirthCookingBatch.IsBatch(queued.Recipe)||
            !originals.TryGetValue(queued,out var original)||!original.IsCurrent()||
            !recipes.TryGetValue(queued,out var recipe)||!ReferenceEquals(recipe,queued.Recipe)||queued.StartingEntityId!=original.Actor||
            !RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,queued,out var admission)||admission==null||
            !System.Xml.Linq.XNode.DeepEquals(admission.Write(),original.Admission.Write()))return false;
        bool handled=RebirthStationQueuedDiscoveryAuthorization.TryEvaluate(station,queued,original.Player,out var allowed,out _);
        return (handled?allowed:RebirthCapabilityService.EvaluateRecipe(original.Player,queued.Recipe.GetName())?.IsAllowed==true)&&
            original.IsCurrent()&&WindowCurrent();
    }
    // Candidate replacement of ONLY original bUserAccessing guard read; native false remains false.
    internal static bool ShouldBlockUserAccess(TileEntityWorkstation station,bool nativeUserAccess)
    {
        if(!nativeUserAccess)return false;
        var lease=active;
        var queue=station?.Queue;var queued=queue!=null&&queue.Length>0?queue[queue.Length-1]:null;
        return lease==null||!ReferenceEquals(lease.station,station)||!lease.Owns(queued);
    }
    // Candidate insertion seam MUST run before physical effect; false means native while yields without decrement/output/reward.
    internal static bool AllowsInsertion(TileEntityWorkstation station,RecipeQueueItem queued)
    {
        var lease=active;
        if(lease==null)return true;
        if(!ReferenceEquals(lease.station,station)||!lease.Owns(queued)){lease.YieldedToUiOwnedJob=true;return false;}
        return true;
    }
    private sealed class ReferenceItems : IEqualityComparer<RecipeQueueItem>
    {
        internal static readonly ReferenceItems Instance=new ReferenceItems();
        public bool Equals(RecipeQueueItem x,RecipeQueueItem y)=>ReferenceEquals(x,y);
        public int GetHashCode(RecipeQueueItem value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
    public void Dispose()
    {if(closed||thread!=Environment.CurrentManagedThreadId)return;closed=true;if(ReferenceEquals(active,this))active=null;}
}
