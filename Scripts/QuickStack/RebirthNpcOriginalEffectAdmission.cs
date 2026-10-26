using System;
using System.Collections.Generic;
using System.Linq;

// Original-domain admission only. Ordinary NPC work and inventory writers are
// unaffected. This reserves bounded receipt capacity, never item quantities.
internal static partial class RebirthNpcOriginalEffectAdmission
{
    internal sealed class Claim
    {
        internal readonly RebirthNpcStableId Npc;internal readonly string Binding;internal readonly bool Dog;
        private readonly Guid[] effects;
        internal Claim(RebirthNpcStableId npc,string binding,bool dog,params Guid[] effects)
        {Npc=npc;Binding=binding;Dog=dog;this.effects=effects==null?new Guid[0]:(Guid[])effects.Clone();}
        internal Guid[] CopyEffects()=>(Guid[])effects.Clone();
    }
    internal sealed class Pending
    {
        internal readonly string Domain,World,Owner,Creation;internal readonly int Deaths;internal readonly Guid Operation;
        private readonly Claim[] claims;
        internal Pending(string domain,string world,string owner,string creation,int deaths,Guid operation,IEnumerable<Claim> claims)
        {Domain=domain;World=world;Owner=owner;Creation=creation;Deaths=deaths;Operation=operation;this.claims=claims?.ToArray()??new Claim[0];}
        internal Claim[] CopyClaims()=>(Claim[])claims.Clone();
    }
    internal delegate bool RemotePendingReader(World world,out Pending[] pending);
    internal delegate bool RemotePlanReader(EntityPlayer actor,Guid operation,string sourceId,out RebirthRemoteResourceNpcEffectPlan plan);
    internal delegate bool RemoteTerminalReader(Pending original);
    private static RemotePendingReader remotePending;
    private static RemotePlanReader remotePlan;
    private static RemoteTerminalReader remoteTerminal;
    private static object transitionMutation=new object();
    private static void Changed()=>transitionMutation=new object();
    private static World scopeWorld;private static string directory,worldId;private static bool ready;
    internal sealed class Held{internal Pending Original;internal Claim Claim;}
    private static readonly Dictionary<string,Held> held=new Dictionary<string,Held>(StringComparer.Ordinal);
    internal sealed class Lease:IDisposable
    {
        private readonly World world;private readonly Pending original;private readonly Held[] claims;
        internal Lease(World world,Pending original,Held[] claims){this.world=world;this.original=original;this.claims=claims;}
        internal bool Current()=>Scope(world)&&claims.All(value=>held.TryGetValue(value.Claim.Npc.ToString(),out var live)&&ReferenceEquals(live,value));
        public void Dispose()
        {
            // Unknown originals keep their capacity claim. Dispose is never an
            // implicit cancellation, rollback, item grant, or receipt deletion.
            if(!Current()||!SavedTerminal(original))return;
            Changed();foreach(var claim in claims)if(held.TryGetValue(claim.Claim.Npc.ToString(),out var live)&&ReferenceEquals(live,claim))held.Remove(claim.Claim.Npc.ToString());
        }
    }
    internal static bool RegisterRemoteResourceReaders(RemotePendingReader pending,RemotePlanReader plan,RemoteTerminalReader terminal)
    {
        if(pending==null||plan==null||terminal==null)return false;
        if(remotePending!=null||remotePlan!=null||remoteTerminal!=null)return remotePending==pending&&remotePlan==plan&&remoteTerminal==terminal;
        Changed();remotePending=pending;remotePlan=plan;remoteTerminal=terminal;ready=false;return true;
    }
    private static bool Scope(World world)=>ready&&ThreadManager.IsMainThread()&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&
        ReferenceEquals(scopeWorld,world)&&GameIO.GetSaveGameDir()==directory&&RebirthLogisticsTransferJournal.WorldKey(world)==worldId;
    private static bool Hex(string value,int count)=>value?.Length==count&&value.All(Uri.IsHexDigit)&&value==value.ToLowerInvariant();
    private static bool Valid(World world,Pending value)
    {
        if(value==null||(value.Domain!="logistics"&&value.Domain!="remoteResource"&&value.Domain!="npcWork")||value.Operation==Guid.Empty||!Hex(value.Owner,64)||value.Deaths<0||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation))return false;
        if(value.Domain=="logistics"||value.Domain=="npcWork"){if(value.World!=RebirthLogisticsTransferJournal.WorldKey(world))return false;}
        else if(!Hex(value.World,64)||!RemoteResourceWorldIdentity.TryGet(world,out var identity)||identity!=value.World)return false;
        var claims=value.CopyClaims();if(claims.Length>128)return false;var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var claim in claims)
        {
            if(claim==null||claim.Npc.IsEmpty||!Hex(claim.Npc.ToString(),32)||!Hex(claim.Binding,64)||!ids.Add(claim.Npc.ToString()))return false;
            var effects=claim.CopyEffects();if(effects.Length!=(claim.Dog?0:value.Domain=="logistics"?2:1)||effects.Any(id=>id==Guid.Empty)||effects.Distinct().Count()!=effects.Length)return false;
        }
        return true;
    }
    internal static Pending Logistics(RebirthLogisticsTransferJournal.Record original)
    {
        string binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);var claims=new List<Claim>();
        foreach(var npc in original.Npcs)
        {if(!RebirthNpcStableId.TryParse(npc.StableId,out var id))throw new InvalidOperationException();claims.Add(new Claim(id,binding,false,npc.DebitId,npc.CreditId));}
        foreach(var leg in original.Legs)if(leg.Kind==RemoteResourceSourceKind.NpcStorage)
        {if(!RebirthLogisticsDogTransfer.Original(leg,out var id,out _))throw new InvalidOperationException();claims.Add(new Claim(id,binding,true));}
        return new Pending("logistics",original.World,original.Owner,original.Creation,original.Deaths,original.Operation,claims);
    }
    internal static Pending NpcWork(RebirthNpcWorkNativeTransferOriginal.Record original)
        =>new Pending("npcWork",original.World,original.Owner,original.Creation,original.Deaths,original.Operation,
            new[]{new Claim(original.Npc,original.Binding,false,original.CreditId)});
    private static bool Same(Pending a,Claim ac,Pending b,Claim bc)=>a.Domain==b.Domain&&a.Operation==b.Operation&&a.Owner==b.Owner&&a.World==b.World&&a.Creation==b.Creation&&a.Deaths==b.Deaths&&
        ac.Npc==bc.Npc&&ac.Binding==bc.Binding&&ac.Dog==bc.Dog&&ac.CopyEffects().SequenceEqual(bc.CopyEffects());
    private static bool SavedTerminal(Pending original)
    {
        if(original.Domain=="remoteResource")return remoteTerminal?.Invoke(original)==true;
        if(original.Domain=="npcWork")return RebirthNpcWorkNativeTransferOriginal.TryRead(original.Owner,original.Operation,out var work)&&
            work.Phase==4&&work.Binding==original.CopyClaims().Single().Binding;
        if(original.Domain!="logistics")return false;
        if(!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)||saved.Phase!=4)return false;
        var expected=Logistics(saved);var before=original.CopyClaims();var after=expected.CopyClaims();
        return before.Length==after.Length&&before.All(claim=>after.Any(other=>Same(original,claim,expected,other)));
    }
    private static bool Add(Dictionary<string,Held> target,Pending original)
    {
        foreach(var claim in original.CopyClaims())
        {
            string id=claim.Npc.ToString();
            if(target.TryGetValue(id,out var other)){if(!Same(original,claim,other.Original,other.Claim))return false;}
            else{if(target.Count>=8192)return false;target.Add(id,new Held{Original=original,Claim=claim});}
        }
        return true;
    }
    internal static bool Bootstrap(World world)
    {
        if(Scope(world))return true;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||remotePending==null||remotePlan==null||remoteTerminal==null)return false;
        Changed();ready=false;
        try
        {
            string save=GameIO.GetSaveGameDir(),nativeWorld=RebirthLogisticsTransferJournal.WorldKey(world);
            RebirthNpcAggregatePersistenceStore.EnsureLoaded();
            if(string.IsNullOrEmpty(save)||nativeWorld==null||!RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(world,out var logistics)||
                !remotePending(world,out var remote)||remote==null||remote.Length>4096||
                !RebirthNpcWorkNativeTransferOriginal.TryReadPending(world,out var work))return false;
            var candidates=new Dictionary<string,Held>(StringComparer.Ordinal);
            foreach(var record in logistics){var original=Logistics(record);if(!Valid(world,original)||!Add(candidates,original))return false;}
            foreach(var original in remote)if(original?.Domain!="remoteResource"||!Valid(world,original)||!Add(candidates,original))return false;
            foreach(var row in work){var original=NpcWork(row);if(!Valid(world,original)||!Add(candidates,original))return false;}
            if(!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=save||RebirthLogisticsTransferJournal.WorldKey(world)!=nativeWorld)return false;
            held.Clear();foreach(var item in candidates)held.Add(item.Key,item.Value);scopeWorld=world;directory=save;worldId=nativeWorld;ready=true;return true;
        }
        catch{return false;}
    }
    internal static bool CanRecoverNpcStores(World world,IEnumerable<RebirthNpcStableId> affected,out string error)
    {
        error=string.Empty;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||affected==null||remotePending==null||remotePlan==null||remoteTerminal==null){error="original custody readers are unavailable";return false;}
            string save=GameIO.GetSaveGameDir();var ids=new HashSet<RebirthNpcStableId>(affected);
            if(!RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(world,out var logistics)||logistics==null||
                !remotePending(world,out var remote)||remote==null||
                !RebirthNpcWorkNativeTransferOriginal.TryReadPending(world,out var work)){error="cold original custody is unknown";return false;}
            var originals=new List<Pending>();foreach(var row in logistics)originals.Add(Logistics(row));originals.AddRange(remote);
            foreach(var row in work)originals.Add(NpcWork(row));
            foreach(var original in originals)
            {
                if(!Valid(world,original)){error="malformed original custody must recover before NPC replacement";return false;}
                if(original.CopyClaims().Any(claim=>ids.Contains(claim.Npc))&&!SavedTerminal(original)){error="pending "+original.Domain+" original retains NPC custody";return false;}
            }
            if(ReferenceEquals(scopeWorld,world)&&directory==save)foreach(var value in held.Values)
                if(ids.Contains(value.Claim.Npc)&&(!Valid(world,value.Original)||!SavedTerminal(value.Original))){error="retained original NPC claim remains unsettled";return false;}
            if(!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=save){error="original custody save scope changed";return false;}return true;
        }
        catch(Exception exception){error="original custody preflight failed: "+exception.GetType().Name;return false;}
    }
    private static bool AdmissionActorCurrent(EntityPlayer actor,Pending original)
    {
        if(!actor.IsDead()&&actor.Died==original.Deaths&&RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation))return true;
        return original.Domain=="logistics"&&RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)&&
            retained.Creation==original.Creation&&retained.Deaths==original.Deaths&&RebirthLogisticsDeathSettlement.MatchesOriginalActor(actor,retained);
    }
    private static bool Reserve(EntityPlayer actor,Pending original,out Lease lease)
    {
        lease=null;var world=actor?.world;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||!Bootstrap(world)||!Valid(world,original)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
            !AdmissionActorCurrent(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=original.Owner)return false;
        foreach(var claim in original.CopyClaims())
        {
            if(held.TryGetValue(claim.Npc.ToString(),out var previous)&&!Same(original,claim,previous.Original,previous.Claim))
            {
                if(!SavedTerminal(previous.Original))return false;
                Changed();foreach(var expired in held.Where(pair=>ReferenceEquals(pair.Value.Original,previous.Original)).Select(pair=>pair.Key).ToArray())held.Remove(expired);
            }
            if(!claim.Dog&&!RebirthNpcInventoryTransactionService.CanAdmitOriginalEffectIds(claim.Npc,claim.CopyEffects()))return false;
        }
        Changed();if(!Add(held,original))return false;lease=new Lease(world,original,original.CopyClaims().Select(claim=>held[claim.Npc.ToString()]).ToArray());return lease.Current();
    }
    internal static bool TryReserveLogistics(EntityPlayer actor,RebirthLogisticsTransferJournal.Record expected,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthLogisticsTransferJournal.TryRead(expected.Owner,expected.Operation,out var original)&&original.Phase<4&&
            RebirthLogisticsTransferJournal.Encode(expected)==RebirthLogisticsTransferJournal.Encode(original)&&Reserve(actor,Logistics(original),out lease);}
        catch{return false;}
    }
    // Readonly lease over ALREADY retained typed original claims. No Add,
    // receipt capacity reservation, terminal prune or new original is allowed.
    internal static bool TryRebindPaidSourceOriginal(EntityPlayer actor,RebirthLogisticsTransferJournal.Record expected,out Lease lease)
    {
        lease=null;
        try
        {
            if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||!Scope(actor?.world)||expected==null||!RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,expected)||
                !RebirthLogisticsTransferJournal.TryRead(expected.Owner,expected.Operation,out var original)||original.Phase!=1||
                RebirthLogisticsTransferJournal.Encode(original)!=RebirthLogisticsTransferJournal.Encode(expected))return false;
            var pending=Logistics(original);var claims=pending.CopyClaims();
            if(!Valid(actor.world,pending)||claims.Any(claim=>!held.TryGetValue(claim.Npc.ToString(),out var retained)||!Same(pending,claim,retained.Original,retained.Claim)))return false;
            lease=new Lease(actor.world,pending,claims.Select(claim=>held[claim.Npc.ToString()]).ToArray());return lease.Current();
        }
        catch{lease=null;return false;}
    }
    internal static bool IsReservedLogistics(RebirthLogisticsTransferJournal.Record original,RebirthNpcStableId id)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!Bootstrap(world))return false;
            var expected=Logistics(original);var claims=expected.CopyClaims();
            return claims.Any(value=>value.Npc==id)&&claims.All(claim=>held.TryGetValue(claim.Npc.ToString(),out var retained)&&Same(expected,claim,retained.Original,retained.Claim));
        }
        catch{return false;}
    }
    internal static bool TryReserveRemoteResource(EntityPlayer actor,Guid operation,string sourceId,out Lease lease)
    {
        lease=null;
        try
        {
        if(remotePlan==null||!remotePlan(actor,operation,sourceId,out var plan)||plan==null||!plan.IsCurrent())return false;
        if(!remotePending(actor.world,out var pending)||pending==null)return false;
        var original=pending.SingleOrDefault(value=>value?.Domain=="remoteResource"&&value.Operation==operation&&value.Owner==plan.Owner);
        var claim=new Claim(plan.NpcId,plan.Binding,plan.DogStorage,plan.DogStorage?new Guid[0]:new[]{plan.MutationId});
        var expected=new Pending("remoteResource",plan.World,plan.Owner,plan.Creation,plan.Deaths,plan.Operation,new[]{claim});
        // Reserve the entire original, not only this source. Otherwise one
        // source could consume capacity before another source is admitted, and
        // a subset could never match the full saved terminal for release.
        return original!=null&&original.CopyClaims().Any(value=>Same(expected,claim,original,value))&&Reserve(actor,original,out lease)&&plan.IsCurrent();
        }
        catch{lease=null;return false;}
    }
    internal static bool TryReserveNpcWork(EntityPlayer actor,RebirthNpcWorkNativeTransferOriginal.Record expected,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthNpcWorkNativeTransferOriginal.TryRead(expected.Owner,expected.Operation,expected.Source,out var original)&&
            original.Binding==expected.Binding&&original.Phase<4&&Reserve(actor,NpcWork(original),out lease);}
        catch{return false;}
    }
    internal static bool IsReservedRemoteResource(RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(plan==null||!Bootstrap(GameManager.Instance?.World)||!plan.IsCurrent())return false;
        var claim=new Claim(plan.NpcId,plan.Binding,plan.DogStorage,plan.DogStorage?new Guid[0]:new[]{plan.MutationId});
        var original=new Pending("remoteResource",plan.World,plan.Owner,plan.Creation,plan.Deaths,plan.Operation,new[]{claim});
        return held.TryGetValue(plan.NpcId.ToString(),out var retained)&&Same(original,claim,retained.Original,retained.Claim)&&
            retained.Original.CopyClaims().All(value=>held.TryGetValue(value.Npc.ToString(),out var other)&&Same(retained.Original,value,other.Original,other.Claim));
    }
}
