using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// Explicit maintenance only. Shares earned-tier policy with the automatic worker;
// it never awards receipts, activates rooms or emits discovery feedback.
internal static class RebirthPurgeDiscoveryMaintenance
{
    private sealed class Job
    {
        internal World World;
        internal EntityPlayer Player;
        internal ClientInfo ReplyClient;
        internal RebirthPoiWorldBinding Binding;
        internal object Census,Receipts;
        internal IEnumerator<RebirthPurgePoiCensus.Entry> Scan;
        internal Dictionary<string,HashSet<int>> Tiers;
        internal readonly List<RebirthPoiIdentity> Pending=new List<RebirthPoiIdentity>();
        internal readonly List<RebirthPoiIdentity> AwaitingPublication=new List<RebirthPoiIdentity>();
        internal Vector3 Origin;
        internal double RangeSquared;
        internal float Deadline;
        internal bool Preview,Ended;
        internal int Examined,Eligible,Existing,Published;
    }
    private static Job current;
    internal static bool Running=>current!=null;
    internal static string Status{get;private set;}="idle";
    internal static void Reset(){Finish("cancelled: world lifecycle changed");}
    internal static void Cancel(){Finish("cancelled");}
    internal static bool Cancel(EntityPlayer requester)
    {
        if(current==null)return true;
        if(requester==null||!ReferenceEquals(requester,current.Player))return false;
        Finish("cancelled");return true;
    }
    private static bool CurrentClient(ClientInfo client,EntityPlayer player)
    {
        if(client==null)return true;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return player!=null&&client.loginDone&&client.entityId==player.entityId&&connection!=null&&
            ReferenceEquals(connection.Clients.ForEntityId(player.entityId),client);
    }
    private static void Finish(string result)
    {
        var job=current;if(job==null)return;
        job.Scan?.Dispose();current=null;
        Status=result+"; examined="+job.Examined+" eligible="+job.Eligible+" existing="+job.Existing+" published="+job.Published;
        Log.Out("[REBIRTH Purge discovery] "+Status);
        if(job.ReplyClient!=null && CurrentClient(job.ReplyClient,job.Player))
            job.ReplyClient.SendPackage(NetPackageManager.GetPackage<NetPackageConsoleCmdClient>().Setup(Status,false));
    }
    internal static bool Start(EntityPlayer player,bool preview,int range,out string reason,ClientInfo replyClient=null)
    {
        reason="Purge discovery state is not ready.";
        var world=GameManager.Instance?.World;
        RebirthPoiWorldStore store;
        var source=RebirthPurgePoiCensus.Instance;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null||world.IsRemote()||
            player==null||!CurrentClient(replyClient,player)||!player.IsSpawned()||player.IsDead()||player.AttachedToEntity!=null||player.IsFlyMode.Value||
            RebirthPurgeKillContributor.Identify(player,world)==null||!source.Ready||source.Published==null||
            !RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)||store.Published==null||!store.Published.Binding.IsCurrent)return false;
        if(current!=null){reason="Another discovery maintenance job is running.";return false;}
        var tiers=RebirthPurgeDiscoveryService.EarnedTiers();if(tiers==null)return false;
        range=Math.Max(1,Math.Min(100000,range));
        current=new Job{World=world,Player=player,ReplyClient=replyClient,Binding=store.Published.Binding,Census=source.Published,
            Receipts=RebirthPurgeMilestoneService.PublishedAll,Scan=source.Published.Values.GetEnumerator(),Tiers=tiers,
            Origin=player.position,RangeSquared=(double)range*range,Deadline=Time.realtimeSinceStartup+60,Preview=preview};
        Status=preview?"preview running":"replay running";reason=Status;return true;
    }
    internal static void Pulse()
    {
        var job=current;if(job==null)return;
        RebirthPoiWorldStore store;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||
            !ReferenceEquals(GameManager.Instance?.World,job.World)||!job.Binding.IsCurrent||
            !ReferenceEquals(job.Census,RebirthPurgePoiCensus.Instance.Published)||
            !ReferenceEquals(job.Receipts,RebirthPurgeMilestoneService.PublishedAll)||
            !CurrentClient(job.ReplyClient,job.Player)||
            !ReferenceEquals(job.World.GetEntity(job.Player.entityId),job.Player)||!job.Player.IsSpawned()||job.Player.IsDead()||
            job.Player.AttachedToEntity!=null||job.Player.IsFlyMode.Value||
            RebirthPurgeKillContributor.Identify(job.Player,job.World)==null)
        {Finish("cancelled: source or player changed");return;}
        // Timeout stalled work, not a healthy all-world scan. The supported
        // 200,000-prefab census can take longer than a minute at this slice size.
        if(Time.realtimeSinceStartup>=job.Deadline){Finish("failed: no progress for 60 seconds");return;}
        // Inspect the retained store while its lifecycle owns an in-flight retry.
        // Never start another mutation until the ordinary ready accessor succeeds.
        if(!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store))
        {Finish("cancelled: discovery store unavailable");return;}
        if(store.HasPending || !RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))return;
        var snapshot=store.Published;
        if(snapshot==null||!ReferenceEquals(snapshot.Binding,job.Binding)){Finish("cancelled: snapshot unavailable");return;}
        // The lifecycle retries this job's retained transaction before admitting
        // another writer. Count its successful markers as published, not existing.
        foreach(var identity in job.AwaitingPublication)
        {
            RebirthPoiClearanceRecord saved;
            if(snapshot.TryGet(identity,out saved)&&!saved.ResetOnly)
            {job.Published++;job.Pending.Remove(identity);job.Deadline=Time.realtimeSinceStartup+60;}
        }
        job.AwaitingPublication.Clear();
        if(job.Pending.Count==0&&!job.Ended)
        {
            var budget=Stopwatch.StartNew();int count=0;
            while(count++<256&&job.Pending.Count<32&&budget.ElapsedMilliseconds<4)
            {
                if(!job.Scan.MoveNext()){job.Scan.Dispose();job.Scan=null;job.Ended=true;break;}
                var entry=job.Scan.Current;job.Examined++;job.Deadline=Time.realtimeSinceStartup+60;
                HashSet<int> tiers;
                if(!job.Tiers.TryGetValue(entry.Identity.Biome,out tiers)||!tiers.Contains(entry.Tier))continue;
                double dx=job.Origin.x-(entry.Identity.X+entry.Identity.SizeX*.5),dz=job.Origin.z-(entry.Identity.Z+entry.Identity.SizeZ*.5);
                if(dx*dx+dz*dz>job.RangeSquared)continue;
                job.Eligible++;
                RebirthPoiClearanceRecord prior;
                if(snapshot.TryGet(entry.Identity,out prior)&&!prior.ResetOnly){job.Existing++;continue;}
                if(!job.Preview)job.Pending.Add(entry.Identity);
            }
        }
        if(job.Pending.Count>0)
        {
            // Automatic discovery can publish between maintenance updates.
            // Recheck before counting and publishing, and batch new markers so
            // a large replay does not flush the manifest once per POI.
            snapshot=store.Published;
            int promotion=-1;
            for(int i=job.Pending.Count-1;i>=0;i--)
            {
                RebirthPoiClearanceRecord prior;
                if(!snapshot.TryGet(job.Pending[i],out prior))continue;
                if(!prior.ResetOnly){job.Existing++;job.Pending.RemoveAt(i);}
            }
            if(job.Pending.Count==0){if(job.Ended)Finish("replay complete");return;}
            for(int i=0;i<job.Pending.Count;i++)
            {
                RebirthPoiClearanceRecord prior;
                if(snapshot.TryGet(job.Pending[i],out prior)&&prior.ResetOnly){promotion=i;break;}
            }
            int publishedCount=promotion>=0?1:job.Pending.Count;
            var result=promotion>=0?store.TryDiscover(snapshot,job.Pending[promotion]):store.TryDiscoverBatch(snapshot,job.Pending);
            if(result==RebirthPoiStoreResult.Published)
            {job.Published+=publishedCount;job.Deadline=Time.realtimeSinceStartup+60;if(promotion>=0)job.Pending.RemoveAt(promotion);else job.Pending.Clear();}
            else if(result==RebirthPoiStoreResult.Duplicate)
            {job.Existing+=publishedCount;job.Deadline=Time.realtimeSinceStartup+60;if(promotion>=0)job.Pending.RemoveAt(promotion);else job.Pending.Clear();}
            else if(result==RebirthPoiStoreResult.Uncertain&&store.HasPending)
            {
                if(promotion>=0)job.AwaitingPublication.Add(job.Pending[promotion]);
                else job.AwaitingPublication.AddRange(job.Pending);
            }
            else if(result!=RebirthPoiStoreResult.Deferred&&result!=RebirthPoiStoreResult.Conflict&&
                !(result==RebirthPoiStoreResult.Uncertain&&store.HasPending))
            {Finish("failed: publication "+result);return;}
        }
        if(job.Ended&&job.Pending.Count==0)Finish(job.Preview?"preview complete":"replay complete");
    }
}

[UnityEngine.Scripting.Preserve]
public sealed class ConsoleCmdRebirthPurge : ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbpurge","rebirthpurge"};
    public override string getDescription()=>"Preview or replay earned Purge discoveries.";
    public override string getHelp()=>"rbpurge discover|autodiscover [dryrun|preview|replay] [all|distance <meters>] | status | cancel";
    public override void Execute(List<string> args,CommandSenderInfo senderInfo)
    {
        var world=GameManager.Instance?.World;
        var remote=senderInfo.RemoteClientInfo;
        if(remote!=null)
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||world.IsRemote()||!remote.loginDone||connection==null||
               !ReferenceEquals(connection.Clients.ForEntityId(remote.entityId),remote))
            {SdtdConsole.Instance.Output("Purge discovery requires an authenticated current player.");return;}
        }
        EntityPlayer player=senderInfo.RemoteClientInfo!=null
            ?(senderInfo.RemoteClientInfo.loginDone?world?.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer:null)
            :world?.GetPrimaryPlayer();
        if(args.Count==1&&args[0].Equals("status",StringComparison.OrdinalIgnoreCase))
        {
            SdtdConsole.Instance.Output(RebirthPurgeDiscoveryMaintenance.Status);
            SdtdConsole.Instance.Output(RebirthPurgePoiCensus.Instance.Status);
            SdtdConsole.Instance.Output(RebirthPoiNativeEvidence.Instance.ScannerStatus(world,player));
            return;
        }
        if(args.Count==1&&args[0].Equals("cancel",StringComparison.OrdinalIgnoreCase))
        {if(senderInfo.RemoteClientInfo==null)RebirthPurgeDiscoveryMaintenance.Cancel();else if(!RebirthPurgeDiscoveryMaintenance.Cancel(player))SdtdConsole.Instance.Output("Only the requesting player can cancel this maintenance job.");return;}
        if(args.Count==0 || !(args[0].Equals("discover",StringComparison.OrdinalIgnoreCase)||
            args[0].Equals("autodiscover",StringComparison.OrdinalIgnoreCase)||
            args[0].Equals("replaydiscover",StringComparison.OrdinalIgnoreCase)||
            args[0].Equals("repairdiscover",StringComparison.OrdinalIgnoreCase)))
        {SdtdConsole.Instance.Output(getHelp());return;}
        int range=15000;bool preview=false;
        for(int i=1;i<args.Count;i++)
        {
            string arg=(args[i]??string.Empty).Trim().ToLowerInvariant();
            if(arg=="dryrun"||arg=="dry"||arg=="preview"||arg=="test"){preview=true;continue;}
            if(arg=="replay")continue;
            if(arg=="all"||arg=="world"||arg=="global"){range=100000;continue;}
            if((arg=="distance"||arg=="dist"||arg=="range")&&i+1<args.Count)arg=args[++i];
            float meters;
            if(!float.TryParse(arg,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out meters)||
                float.IsNaN(meters)||float.IsInfinity(meters))
            {SdtdConsole.Instance.Output(getHelp());return;}
            range=(int)Math.Ceiling(Math.Max(1,Math.Min(100000,meters)));
        }
        string reason;RebirthPurgeDiscoveryMaintenance.Start(player,preview,range,out reason,senderInfo.RemoteClientInfo);
        SdtdConsole.Instance.Output("[REBIRTH Purge discovery] "+reason);
    }
}
