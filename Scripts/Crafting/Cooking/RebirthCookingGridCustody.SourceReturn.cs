using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthCookingGridCustody
{
    private sealed class SourceOrigin
    {internal string Key,Npc,SourceId,Incarnation;internal RemoteResourceSourceKind Kind;internal int Slot;internal ItemStack Item;}
    private static readonly Dictionary<Guid,RebirthCookingNpcReturn.Attempt> SourceAttempts=new Dictionary<Guid,RebirthCookingNpcReturn.Attempt>();
    private static readonly HashSet<Guid> DetachedKeys=new HashSet<Guid>();
    private static World detachedWorld;private static string detachedDirectory,detachedCreation;private static float nextDetached;private static bool detachedIndexed;
    private static ItemStack[] SourceReturnItems(XElement body)
    {
        var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("items").Value));
        if(items.Length!=1||items[0].IsEmpty()||items[0].count<1)throw new InvalidDataException();return items;
    }
    // Reconstruct the existing receipt-order reconciliation from immutable grid
    // transitions. Consumption permanently reduces an association; a later
    // same-rich local input cannot revive an already consumed source receipt.
    private static List<SourceOrigin> SourceOrigins(EntityPlayerLocal player,Guid key)
    {
        // Retain only operation/revision descriptors while ordering the history.
        // Reread each immutable original separately, so cumulative station age
        // neither revokes provenance nor retains every native frame at once.
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        var rows=Records().Where(row=>row.Vehicle==key&&row.OwnerCreation==creation&&row.State==RebirthCamperTransferJournal.Phase.Applied)
            .Select(row=>new{row.Operation,row.Revision}).OrderBy(row=>row.Revision);
        var origins=new List<SourceOrigin>();ItemStack[] previous=null;
        foreach(var descriptor in rows)
        {
            var row=SourceHistoryRecord(descriptor.Operation);
            if(row==null||row.Operation!=descriptor.Operation||row.Vehicle!=key||row.OwnerCreation!=creation||row.Revision!=descriptor.Revision||
                row.State!=RebirthCamperTransferJournal.Phase.Applied)throw new InvalidDataException("Original grid history changed during return observation");
            var body=XElement.Parse(Encoding.UTF8.GetString(row.Before));
            if((string)body.Attribute("owner")==null||(string)body.Attribute("world")!=player.world.worldState.Guid||
                (string)body.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player))throw new InvalidDataException();
            var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("beforeGrid").Value));
            var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("afterGrid").Value));
            if(previous!=null&&!Same(previous,before))throw new InvalidDataException("Original grid source lineage changed");
            var returned=body.Element("sourceReturn");
            if(returned!=null)
            {
                var item=SourceReturnItems(returned)[0];var origin=origins.SingleOrDefault(value=>value.Key==(string)returned.Attribute("origin"));
                if(origin==null||origin.Item.count<item.count||!Same(new ItemStack(origin.Item.itemValue,1),new ItemStack(item.itemValue,1)))throw new InvalidDataException();
                origin.Item.count-=item.count;
            }
            if((string)body.Attribute("action")=="return")
            {
                foreach(var unit in before.Where(item=>!item.IsEmpty()).GroupBy(item=>RebirthNativeItemCodec.Encode(item.itemValue)))
                {
                    long moved=unit.Sum(item=>(long)item.count)-after.Where(item=>!item.IsEmpty()&&RebirthNativeItemCodec.Encode(item.itemValue)==unit.Key).Sum(item=>(long)item.count);
                    foreach(var origin in origins.Where(value=>RebirthNativeItemCodec.Encode(value.Item.itemValue)==unit.Key))
                    {int take=(int)Math.Min(Math.Max(0,moved),origin.Item.count);origin.Item.count-=take;moved-=take;}
                }
            }
            if((string)body.Attribute("action")=="fetched"&&(long?)body.Attribute("fetchId") is long id&&id>0)
                AppendSourceOrigins(player,row,body,id,origins);
            var held=ItemStack.Clone(after);
            foreach(var origin in origins)
            {
                int left=origin.Item.count,matched=0;
                foreach(var item in held)
                {
                    if(left<=0)break;if(item.IsEmpty()||!Same(new ItemStack(item.itemValue,1),new ItemStack(origin.Item.itemValue,1)))continue;
                    int take=Math.Min(left,item.count);left-=take;matched+=take;item.count-=take;
                }
                origin.Item.count=matched;
            }
            origins.RemoveAll(value=>value.Item.count<=0);previous=after;
        }
        return origins;
    }
    private static void AppendSourceOrigins(EntityPlayerLocal player,RebirthCamperTransferJournal.Record grid,XElement body,long request,List<SourceOrigin> origins)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||!RemoteResourceWorldIdentity.TryGet(player.world,out string world))throw new InvalidDataException();
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,grid.OwnerCreation,1,(ulong)request,RemoteResourceClientOperation.CookingPull);
        if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out var saved,out bool unknown)||unknown||saved==null||saved.Phase!=4||saved.World!=world||saved.Owner!=owner.StorageKey||saved.Creation!=grid.OwnerCreation)throw new InvalidDataException("Fetched source terminal is not yet available");
        var root=saved.Copy();var action=root.Element("ownerAction");
        if((string)root.Attribute("disposition")!="grid-owned"||(string)action?.Attribute("domain")!="cooking-grid"||(string)action.Attribute("child")!=grid.Operation.ToString("N")||
            action.Element("original")?.Value!=Encoding.UTF8.GetString(grid.Before))throw new InvalidDataException("Fetched source/grid association differs");
        foreach(var source in root.Element("sources").Elements("source"))
        {
            var npc=source.Element("npc");if((int)source.Attribute("published")!=1)continue;
            var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("before").Value));
            var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("after").Value));
            if(before.Length!=after.Length)throw new InvalidDataException();
            for(int slot=0;slot<before.Length;slot++)
            {
                if(before[slot].IsEmpty())continue;
                int count=before[slot].count-(after[slot].IsEmpty()?0:after[slot].count);if(count<=0)continue;
                origins.Add(new SourceOrigin{Key=operation.ToString("N")+":"+(string)source.Attribute("id")+":"+slot,Npc=(string)npc?.Attribute("stable"),
                    SourceId=(string)source.Attribute("id"),Incarnation=(string)source.Attribute("incarnation"),Kind=(RemoteResourceSourceKind)(int)source.Attribute("kind"),Slot=slot,Item=new ItemStack(before[slot].itemValue.Clone(),count)});
            }
        }
    }
    private static bool TryBeginSourceReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,ItemStack[] grid,string detachedLocation=null)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return true;
        Guid key=Key(player,owner.CanonicalId,detachedLocation??RebirthCookingStationLocator.Encode(workspace.CustodyStation));
        var origins=SourceOrigins(player,key);if(origins.Count==0)return false;
        var nearby=RemoteResourceSourceDiscovery.ResolveNearby(player.world,player,RemoteResourcesRuntimePolicy.Radius).OfType<RemoteNpcResourceSource>().ToArray();
        foreach(var origin in origins)
        {
            int count;uint revision=0;XElement native=null;
            if(origin.Npc!=null)
            {
                var source=nearby.SingleOrDefault(value=>value.NativeIdentity.ToString()==origin.Npc);if(source==null)continue;
                count=RebirthCookingNpcReturn.PlanBound(source,player,origin.Item,origin.Slot,origin.Item.count,out revision);
            }
            else count=RebirthCookingNativeReturn.Plan(player,origin.SourceId,origin.Kind,origin.Incarnation,origin.Slot,origin.Item,out native);
            if(count<1)continue;
            bool refused=Records().Any(row=>row.Vehicle==key&&row.State==RebirthCamperTransferJournal.Phase.Rejected&&
                XElement.Parse(Encoding.UTF8.GetString(row.Before)).Element("sourceReturn") is XElement rejected&&
                (string)rejected.Attribute("origin")==origin.Key&&
                (origin.Npc!=null?(uint?)rejected.Attribute("revision")==revision:
                    rejected.Element("native")?.Element("before")?.Value==native.Element("before")?.Value));
            if(refused)continue;
            var next=ItemStack.Clone(grid);int left=count;
            foreach(var item in next)
            {
                if(left<=0)break;if(item.IsEmpty()||!Same(new ItemStack(item.itemValue,1),new ItemStack(origin.Item.itemValue,1)))continue;
                int take=Math.Min(left,item.count);item.count-=take;left-=take;
            }
            if(left!=0)throw new InvalidDataException();for(int i=0;i<next.Length;i++)if(next[i].count<=0)next[i]=ItemStack.Empty.Clone();
            var selected=new XElement("sourceReturn",new XAttribute("origin",origin.Key),new XAttribute("slot",origin.Slot),
                origin.Npc!=null?new XAttribute("npc",origin.Npc):null,origin.Npc!=null?new XAttribute("revision",revision):null,native,
                new XElement("items",Grid(new[]{new ItemStack(origin.Item.itemValue.Clone(),count)})));
            var actor=RebirthCamperEnergyActorImage.Capture(player);
            Begin(workspace,player,"source-return",actor,actor.Copy(),grid,next,sourceReturn:selected,detachedLocation:detachedLocation);return true;
        }
        // No original destination currently accepts a fresh delta. Existing
        // preferred owner return remains the fallback, without old snapshots.
        return false;
    }
    // -1 retained, 0 positively refused without source effect, 1 saved deposit.
    private static int AdvanceSourceReturn(Intent intent)
    {
        var body=XElement.Parse(Encoding.UTF8.GetString(intent.Record.Before)).Element("sourceReturn");var item=SourceReturnItems(body)[0];
        if(body.Element("native")!=null)return RebirthCookingNativeReturn.Advance(intent.Operation);
        string npc=(string)body.Attribute("npc");int slot=(int)body.Attribute("slot");uint revision=(uint)body.Attribute("revision");
        SourceAttempts.TryGetValue(intent.Operation,out var attempt);
        if(attempt==null&&RebirthCookingNpcReturn.TryReadBoundOutcome(intent.Player,intent.Operation,npc,item,slot,item.count,revision,intent.Deaths,out var saved))return saved.Pending?-1:saved.Count==item.count?1:0;
        var origins=SourceOrigins(intent.Player,intent.Key);var origin=origins.SingleOrDefault(value=>value.Key==(string)body.Attribute("origin"));
        if(origin==null||origin.Npc!=npc||origin.Slot!=slot||origin.Item.count<item.count||!Same(new ItemStack(origin.Item.itemValue,1),new ItemStack(item.itemValue,1)))return -1;
        var source=attempt?.Source??RemoteResourceSourceDiscovery.ResolveNearby(intent.Player.world,intent.Player,RemoteResourcesRuntimePolicy.Radius).OfType<RemoteNpcResourceSource>().SingleOrDefault(value=>value.NativeIdentity.ToString()==npc);
        if(source==null)return 0;
        var result=RebirthCookingNpcReturn.RestoreBound(intent.Player,source,item,slot,item.count,revision,intent.Operation,ref attempt);
        if(attempt!=null)SourceAttempts[intent.Operation]=attempt;
        if(result.Pending)return -1;SourceAttempts.Remove(intent.Operation);return result.Count==item.count?1:0;
    }
    // Positive named source publication owns only the off-bag grid delta. This
    // can retire it after death without loading the former owner's actor image.
    private static bool TryRecoverSavedSourceReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,RebirthCamperTransferJournal.Record record,out bool recovered)
    {
        recovered=false;
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));var body=root.Element("sourceReturn");
        if(body==null)return false;
        if((string)root.Attribute("action")!="source-return"||(string)root.Attribute("world")!=player.world.worldState.Guid||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||(string)root.Attribute("owner")!=owner.CanonicalId||
            (string)root.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||(int?)root.Attribute("deaths") is not int deaths||deaths>player.Died||
            root.Element("beforeActor")?.Value!=root.Element("afterActor")?.Value)return true;
        var item=SourceReturnItems(body)[0];
        RebirthCookingNpcReturn.Result saved;
        bool outcome=body.Element("native")!=null?RebirthCookingNativeReturn.TryReadOutcome(record.Operation,body,out saved):
            RebirthCookingNpcReturn.TryReadBoundOutcome(player,record.Operation,(string)body.Attribute("npc"),item,(int)body.Attribute("slot"),item.count,(uint)body.Attribute("revision"),deaths,out saved);
        if(!outcome)
            return deaths<player.Died;
        if(saved.Pending)return deaths<player.Died;
        var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("beforeGrid").Value));
        var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("afterGrid").Value));
        bool detached=workspace==null&&ValidDetachedLocation((string)root.Attribute("location"))&&before.Length>=12&&before.Length<=4096&&after.Length==before.Length;
        if(!detached&&(before.Length!=12||after.Length!=12)||!Conserved(Actor(root.Element("beforeActor").Value),after,Actor(root.Element("beforeActor").Value),before,new[]{item}))return true;
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
            record=Journal().Advance(record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,saved.Count==item.count?RebirthCamperTransferJournal.Phase.Applied:RebirthCamperTransferJournal.Phase.Rejected);
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied&&record.State!=RebirthCamperTransferJournal.Phase.Rejected)return true;
        if(record.State==RebirthCamperTransferJournal.Phase.Applied&&saved.Count!=item.count||record.State==RebirthCamperTransferJournal.Phase.Rejected&&saved.Count!=0)return true;
        var desired=record.State==RebirthCamperTransferJournal.Phase.Applied?after:before;
        var live=workspace?.ReadCustodyGrid();if(live!=null&&!live.All(value=>value.IsEmpty())&&!Compatible(live,before,after))return true;
        var world=player.world;int currentDeaths=player.Died;string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        bool Scope()=>CurrentOwner(player)&&ReferenceEquals(GameManager.Instance?.World,world)&&player.Died==currentDeaths&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation;
        if(workspace!=null)for(int index=0;index<desired.Length;index++)if(!Same(live[index],desired[index]))
        {if(!Scope())return true;workspace.WriteCustodyGrid(index,desired[index].Clone());}
        if(!Scope()||workspace!=null&&!Same(workspace.ReadCustodyGrid(),desired))return true;
        Active.RemoveAll(value=>value.Operation==record.Operation);SourceAttempts.Remove(record.Operation);recovered=true;
        workspace?.CustodyStatus("");workspace?.CustodyTransitionFinished("source-return");return true;
    }
    internal static IEnumerable<RebirthCamperTransferJournal.Record> NativeSourceReturnOriginals()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)yield break;
        foreach(var record in Records())
        {
            if(record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain)continue;
            var body=XElement.Parse(Encoding.UTF8.GetString(record.Before));
            if(body.Element("sourceReturn")?.Element("native")!=null&&(string)body.Attribute("action")=="source-return"&&
                (string)body.Attribute("world")==world.worldState.Guid&&record.Before.SequenceEqual(record.After))yield return record;
        }
    }
    internal static bool NativeSourceReturnPending(Guid operation,out RebirthCamperTransferJournal.Record record)
    {
        record=null;var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return false;
        var active=Active.FirstOrDefault(value=>value.Operation==operation&&ReferenceEquals(value.WorldObject,world));
        if(active!=null){record=active.Record;return active.Action=="source-return"&&(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain);}
        var found=Journal().Read(operation);if(found==null||found.State!=RebirthCamperTransferJournal.Phase.Prepared&&found.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
        var body=XElement.Parse(Encoding.UTF8.GetString(found.Before));if(body.Element("sourceReturn")?.Element("native")==null||(string)body.Attribute("world")!=world.worldState.Guid||
            (string)body.Attribute("action")!="source-return"||!found.Before.SequenceEqual(found.After))return false;record=found;return true;
    }
    internal static bool TryNativeSourceReturn(Guid operation,out EntityPlayerLocal player,out RebirthCamperTransferJournal.Record record)
    {
        player=GameManager.Instance?.World?.GetPrimaryPlayer();record=null;
        if(!CurrentOwner(player)||player.world.IsRemote()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return false;
        var originalPlayer=player;
        var active=Active.FirstOrDefault(value=>value.Operation==operation&&ReferenceEquals(value.Player,originalPlayer)&&ReferenceEquals(value.WorldObject,originalPlayer.world));
        if(active!=null)
        {
            if(active.Action!="source-return"||active.Owner!=owner.CanonicalId||active.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
                active.Record.State!=RebirthCamperTransferJournal.Phase.Prepared&&active.Record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
            record=active.Record;return true;
        }
        var found=active?.Record??Journal().Read(operation);
        if(found==null||found.State!=RebirthCamperTransferJournal.Phase.Prepared&&found.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
        var body=XElement.Parse(Encoding.UTF8.GetString(found.Before));
        if(!found.Before.SequenceEqual(found.After)||body.Element("sourceReturn")?.Element("native")==null||(string)body.Attribute("world")!=player.world.worldState.Guid||
            (string)body.Attribute("owner")!=owner.CanonicalId||(string)body.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            (int?)body.Attribute("deaths")>player.Died)return false;
        record=found;return true;
    }
    private static bool ValidDetachedLocation(string location)=>location!=null&&location.StartsWith("fetch-return:",StringComparison.Ordinal)&&
        long.TryParse(location.Substring(13),out long request)&&request>0&&location=="fetch-return:"+request;
    private static bool BeginDetachedFetchedReturn(EntityPlayerLocal player,long request,IList<ItemStack> fetched)
    {
        if(fetched.Count>4096||fetched.Any(item=>item==null||item.IsEmpty()))return false;
        var before=ItemStack.CreateArray(Math.Max(12,fetched.Count));var after=ItemStack.CreateArray(before.Length);
        for(int index=0;index<fetched.Count;index++)after[index]=fetched[index].Clone();
        var actor=RebirthCamperEnergyActorImage.Capture(player);
        return Begin(null,player,"fetched",actor,actor.Copy(),before,after,request,fetched,detachedLocation:"fetch-return:"+request);
    }
    private static void AdvanceDetachedSourceReturns()
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();if(!CurrentOwner(player)||player.world.IsRemote()||IsHeld(player)||!CanAdmit(player)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player),directory=GameIO.GetSaveGameDir();
        if(!ReferenceEquals(detachedWorld,player.world)||detachedDirectory!=directory||detachedCreation!=creation)
        {detachedWorld=player.world;detachedDirectory=directory;detachedCreation=creation;DetachedKeys.Clear();detachedIndexed=false;nextDetached=0;}
        if(UnityEngine.Time.realtimeSinceStartup<nextDetached)return;nextDetached=UnityEngine.Time.realtimeSinceStartup+1f;
        try
        {
            if(!detachedIndexed)
            {
                Latest(Guid.Empty); // Loads the existing immutable journal index once per save scope.
                foreach(var operation in LatestOperations.Values)
                {
                    var row=Journal().Read(operation);var body=XElement.Parse(Encoding.UTF8.GetString(row.Before));
                    if(ValidDetachedLocation((string)body.Attribute("location"))&&row.OwnerCreation==creation&&(string)body.Attribute("world")==player.world.worldState.Guid&&
                        (string)body.Attribute("owner")==owner.CanonicalId)DetachedKeys.Add(row.Vehicle);
                }
                detachedIndexed=true;
            }
            var latest=DetachedKeys.Select(key=>Latest(key)).Where(row=>row!=null).Select(row=>new{Record=row,Body=XElement.Parse(Encoding.UTF8.GetString(row.Before))})
                .Where(value=>ValidDetachedLocation((string)value.Body.Attribute("location"))&&(string)value.Body.Attribute("world")==player.world.worldState.Guid&&
                    (string)value.Body.Attribute("owner")==owner.CanonicalId).Take(16).ToArray();
            foreach(var entry in latest)
            {
                var record=entry.Record;var body=entry.Body;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                {
                    if(TryRecoverSavedOwnerReturn(null,player,record,out bool ownerRecovered)&&ownerRecovered)continue;
                    if(body.Element("sourceReturn")!=null&&TryRecoverSavedSourceReturn(null,player,record,out bool recovered)&&recovered)continue;
                    continue;
                }
                var grid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element(record.State==RebirthCamperTransferJournal.Phase.Rejected?"beforeGrid":"afterGrid").Value));
                if(grid.All(item=>item.IsEmpty())){DetachedKeys.Remove(record.Vehicle);continue;}
                string location=(string)body.Attribute("location");
                if(TryBeginSourceReturn(null,player,grid,location))return;
                var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();var next=ItemStack.Clone(grid);var overflow=new List<ItemStack>();
                for(int index=0;index<next.Length;index++)if(!next[index].IsEmpty())
                {
                    int left=PlanPreferred(player,after,next[index]);
                    if(left>0&&overflow.Count<255){overflow.Add(new ItemStack(next[index].itemValue.Clone(),left));left=0;}
                    next[index]=left==0?ItemStack.Empty.Clone():new ItemStack(next[index].itemValue.Clone(),left);
                }
                if(!Same(next,grid)){Begin(null,player,"return",before,after,grid,next,detachedLocation:location,overflow:overflow.Count==0?null:overflow);return;}
            }
        }
        catch(Exception error){Log.Warning("[Cooking] Detached ingredient return retained: "+error.GetType().Name);}
    }
}
