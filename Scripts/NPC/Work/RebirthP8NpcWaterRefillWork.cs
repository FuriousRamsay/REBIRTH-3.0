using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// B07 provider-refill job (separate from the one-crop harvest/replant EAI).
// An NPC takes one real empty bucket from its native bag, physically reaches an eligible
// Rebirth WaterTank/DewCollector source, debits that source by one 200-unit exchange, converts
// the bucket to bucketRiverWater in the same bag slot, carries it to the assigned provider and
// pours it in (provider +200, bucket back to empty). Every effect is bound to a saved
// "farmWaterOriginal" row in the NPC aggregate record and advanced one checkpointed phase at a time:
//   prepared -> src-attempted -> src-published -> fill-attempted -> filled
//   -> dst-attempted -> dst-published -> empty-attempted -> emptied -> completed
// Water is only ever lost (crash between debit and fill), never duplicated: each tank leg carries
// a tile stamp (incarnation, operation, revision, before/after) and each bucket leg is bound to
// a saved native stack id, so neither water counts nor bucket names can authorize a replay.
// Natural water uses the public native seam CollectWaterUtils.CollectWater + NetPackageWaterSet/SetWaterRPC, checkpointed
// on exact per-voxel masses (see NaturalLeg). Only native ExecuteAction's EntityPlayerLocal raycast is player bound.
internal static class RebirthP8NpcWaterRefillWork
{
    internal const string DefinitionId="rebirth.farming.maintain-water";
    internal const int Exchange=200;
    private const string Element="farmWaterOriginal";
    private const string EmptyBucket="bucketEmpty",FullBucket="bucketRiverWater";
    private const int SourceRadius=24,SourceMaxEntries=16;
    // Provisional until the Farming skill owner supplies a water-work curve (see doc risks).
    private const float WaterAwardFactor=0.5f;

    internal static bool IsDefinition(string definition)=>definition==DefinitionId;
    // Range and travel use the block centre on x/z (the stored position is the block corner).
    internal static Vector3 Centre(Vector3i p)=>new Vector3(p.x+.5f,p.y,p.z+.5f);
    internal static void Register()=>RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(DefinitionId,
        RebirthNpcWorkKind.Farming,RebirthNpcWorkCapability.Farming,300,1,TimeSpan.FromSeconds(90),false),true);

    // ---- shape / identity ------------------------------------------------------------
    internal static bool Shape(string definition,string key,bool hasPosition,Vector3 position)
    {
        if(!IsDefinition(definition)||!hasPosition||key==null||key.Length>256||float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z))return false;
        var parts=key.Split(':');
        return parts.Length==3&&parts[0]=="desk"&&parts[1].Length>0&&parts[2]=="Farm"&&
            position==new Vector3i(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),Mathf.FloorToInt(position.z)).ToVector3();
    }
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)=>intent!=null&&intent.Path=="concrete"&&intent.PrimaryId!=0&&intent.SecondaryId!=0&&
        intent.ExecutorId=="rebirth.work.crafting-workstation"&&intent.Actor is EntityRebirthHumanoidNPC&&intent.MatchesOriginal()&&
        RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)&&Allows(assignment)&&assignment.NpcId==intent.StableId&&
        RebirthNpcWorkReservationService.TryGetForAssignment(intent.PrimaryId,out var reservation)&&reservation.ReservationId==intent.SecondaryId;
    internal static Vector3i Position(RebirthNpcWorkAssignment a)=>new Vector3i(Mathf.FloorToInt(a.TargetPosition.x),Mathf.FloorToInt(a.TargetPosition.y),Mathf.FloorToInt(a.TargetPosition.z));
    internal static string TargetId(RebirthNpcWorkAssignment a)
    {var p=Position(a);return "provider:"+p.x.ToString(CultureInfo.InvariantCulture)+":"+p.y.ToString(CultureInfo.InvariantCulture)+":"+p.z.ToString(CultureInfo.InvariantCulture);}

    private static Guid Derive(Guid operation,string label)
    {
        using(var sha=SHA256.Create())
        {
            var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("p8-water:"+operation.ToString("N")+":"+label));
            var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);
        }
    }

    // ---- authorization ---------------------------------------------------------------
    private static bool ClaimAllows(World world,EntityPlayer owner,Vector3i position)
    {
        var persistent=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        if(persistent==null||world.IsWithinTraderArea(position))return false;
        var claim=world.GetLandClaimOwner(position,persistent);
        return (claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self)&&world.CanPickupBlockAt(position,persistent);
    }
    private static EntityPlayer Owner(World world,string actorId)
    {
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(actorId);
        return owner==null||owner.IsDead()||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)?null:owner;
    }
    internal static bool Authorized(RebirthNpcWorkAssignment a,out long revision)
    {
        revision=0;
        var world=GameManager.Instance?.World;
        if(!Allows(a)||world?.IsRemote()!=false||!AdvancedFarmingRuntimePolicy.Enabled||
           !RebirthNpcDeskVolumeWorkTargetAdapter.ContainsFarmPoint(a,a.TargetPosition+new Vector3(.5f,.5f,.5f),out revision))return false;
        var owner=Owner(world,a.ActorId);
        return owner!=null&&ClaimAllows(world,owner,Position(a));
    }

    // ---- cross-NPC claims --------------------------------------------------------------
    // A tank leg is stamped with the previous operation/revision, so a second NPC touching the same tank while a
    // first refill is in flight would strand the first one at src-attempted/dst-attempted (the stamp can no longer
    // be proven). Claims are derived from the SAVED rows; this table only remembers which (npc, operation) pairs to
    // look at, and is re-filled by Prepare/Adopt/Advance (Advance also runs from restart Recover). Bounded; entries
    // with no saved row or a completed row are dropped lazily.
    private const int MaxLive=256;
    private static readonly Dictionary<Guid,RebirthNpcStableId> Live=new Dictionary<Guid,RebirthNpcStableId>();
    private static void Register(Guid operation,RebirthNpcStableId npc)
    {
        if(operation==Guid.Empty||npc.IsEmpty)return;
        if(!Live.ContainsKey(operation)&&Live.Count>=MaxLive)ClaimedPositions(Guid.Empty); // prunes dead entries
        if(Live.Count<MaxLive||Live.ContainsKey(operation))Live[operation]=npc;
    }
    // Tank positions another live refill is mutating or about to mutate: its provider while unfinished (a held
    // retained bucket or an abandoned owner does not count) and its source until the source leg is published.
    internal static HashSet<Vector3i> ClaimedPositions(Guid exceptOperation)
    {
        var claimed=new HashSet<Vector3i>();List<Guid> dead=null;
        foreach(var pair in Live)
        {
            if(pair.Key==exceptOperation)continue;
            if(!TryReadSaved(pair.Value,pair.Key,out var row)){(dead??(dead=new List<Guid>())).Add(pair.Key);continue;}
            string phase=(string)row.Attribute("phase");
            if(phase=="completed"){(dead??(dead=new List<Guid>())).Add(pair.Key);continue;}
            if(phase=="filled"&&((string)row.Attribute("held")=="1"||OwnerGone(row)))continue;
            claimed.Add(Pos(row,"d"));
            if(phase=="prepared"||phase=="src-attempted")claimed.Add(Pos(row,"s"));
        }
        if(dead!=null)foreach(var key in dead)Live.Remove(key);
        return claimed;
    }
    internal static bool ClaimedByOther(Vector3i position,Guid exceptOperation)=>ClaimedPositions(exceptOperation).Contains(position);

    // ---- provider and source evaluation ----------------------------------------------
    internal enum Need:byte{Needed,NotAProvider,AboveThreshold,NoRoomForBucket}
    internal static Need EvaluateProvider(World world,Vector3i position,int percent,out int count,out int max,out int threshold)
    {
        count=max=threshold=0;
        var block=world.GetBlock(position);
        var tank=world.GetTileEntity(position) as TileEntityWaterTankRebirth;
        if(tank==null||!AdvancedFarmingItemWaterExchange.IsAdvancedWaterStorage(block))return Need.NotAProvider;
        max=AdvancedFarmingItemWaterExchange.ResolveMaxWater(block,tank);
        count=tank.waterCount;threshold=RebirthP8NpcWaterPolicy.ThresholdUnits(max,percent);
        if(count>=threshold)return Need.AboveThreshold;
        return max-count>=Exchange?Need.Needed:Need.NoRoomForBucket;
    }
    internal static bool NeedsRefill(RebirthNpcWorkAssignment a)
    {
        var world=GameManager.Instance?.World;
        return world!=null&&Allows(a)&&EvaluateProvider(world,Position(a),RebirthP8NpcWaterPolicy.GetPercent(a.NpcId),out _,out _,out _)==Need.Needed;
    }

    // Eligible: another Rebirth WaterTank/DewCollector, claim-accessible to the owner, that stays at
    // or above the same threshold after giving one exchange. Fullest first, so a surplus store
    // feeds a depleted one and no source is drained below its own minimum.
    private static bool TryFindSource(World world,EntityPlayer owner,EntityRebirthHumanoidNPC npc,Vector3i provider,int percent,out Vector3i position)
    {
        position=default(Vector3i);
        // Scope is the assigned provider, never the worker's current position, so readiness (before travel),
        // preparation (at the provider) and any later look all plan the same source.
        var center=provider;
        var entries=AdvancedFarmingWaterProviderRegistry.CollectProvidersInArea(world,center,SourceRadius,center.y-4,center.y+4,true,false,SourceMaxEntries);
        int best=-1;
        var claimed=ClaimedPositions(Guid.Empty);
        foreach(var e in entries)
        {
            if(e==null||claimed.Contains(e.Pos)||e.Pos==provider||e.Tank==null||!ReferenceEquals(world.GetTileEntity(e.Pos),e.Tank)||
               !AdvancedFarmingItemWaterExchange.IsAdvancedWaterStorage(world.GetBlock(e.Pos))||
               e.Tank.WaterExchangeRevision==ulong.MaxValue||!ClaimAllows(world,owner,e.Pos))continue;
            int max=AdvancedFarmingItemWaterExchange.ResolveMaxWater(world.GetBlock(e.Pos),e.Tank);
            int keep=RebirthP8NpcWaterPolicy.ThresholdUnits(max,percent);
            if(e.Tank.waterCount-Exchange<keep||e.Tank.waterCount<=best)continue;
            best=e.Tank.waterCount;position=e.Pos;
        }
        return best>=0;
    }

    // Any source: a placed Rebirth store with surplus, else natural water.
    private static bool TryFindAnySource(World world,EntityPlayer owner,EntityRebirthHumanoidNPC npc,Vector3i provider,int percent)
        =>TryFindSource(world,owner,npc,provider,percent,out _)||TryFindNatural(world,owner,provider,out _);

    // ---- natural water source ---------------------------------------------------------
    // Native seam (b259 ItemActionCollectWater.OnHoldingUpdate): CollectWaterUtils.CollectWater plans the removal from
    // the live water grid, NetPackageWaterSet + GameManager.SetWaterRPC apply it (ChunkCluster.SetWater, synchronous),
    // and the bucket becomes bucketRiverWater with Meta = old Meta + collected mass. Only ExecuteAction's raycast
    // (castclass EntityPlayerLocal -> HitInfo) is player bound; the NPC supplies the shore water voxel itself.
    private const string NaturalKind="water";
    private const int NaturalRadius=16,NaturalDown=6,NaturalUp=4,NaturalCollectRadius=2,NaturalMaxPoints=128,NaturalMinMass=195;
    private const float NaturalCacheSeconds=10f;
    private sealed class NaturalSearch{internal float Until;internal bool Found;internal Vector3i Position;}
    private static readonly Dictionary<Vector3i,NaturalSearch> NaturalCache=new Dictionary<Vector3i,NaturalSearch>();
    private struct NaturalPoint{internal Vector3i Position;internal int Before,After;}

    private static bool IsNatural(XElement row)=>(string)row.Attribute("sKind")==NaturalKind;
    private static int MassAt(ChunkCluster cc,Vector3i p){var w=cc.GetWater(p);return w.HasMass()?w.GetMass():0;}
    // Same claim rule as ClaimAllows without the block-pickup test (a water voxel is not a pickable block).
    private static bool NaturalClaimAllows(World world,EntityPlayer owner,Vector3i position)
    {
        var persistent=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        if(persistent==null||world.IsWithinTraderArea(position))return false;
        var claim=world.GetLandClaimOwner(position,persistent);
        return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
    // Nearest shore voxel (water voxel with mass, water-free above, and a water-free horizontal neighbour) around the
    // provider. Bounded: one (2R+1)^2 x 11 scan, cached 10 s per provider (positive and negative).
    private static bool TryFindNatural(World world,EntityPlayer owner,Vector3i provider,out Vector3i position)
    {
        position=default(Vector3i);
        var cc=world?.ChunkCache;if(cc==null||owner==null)return false;
        float now=Time.realtimeSinceStartup;
        if(!NaturalCache.TryGetValue(provider,out var hit)||now>=hit.Until)
        {
            if(NaturalCache.Count>=32)NaturalCache.Clear();
            hit=new NaturalSearch{Until=now+NaturalCacheSeconds};
            int best=int.MaxValue;
            for(int dx=-NaturalRadius;dx<=NaturalRadius;dx++)
                for(int dz=-NaturalRadius;dz<=NaturalRadius;dz++)
                {
                    int flat=dx*dx+dz*dz;if(flat>NaturalRadius*NaturalRadius||flat*16>best)continue;
                    for(int y=provider.y+NaturalUp;y>=provider.y-NaturalDown;y--)
                    {
                        var p=new Vector3i(provider.x+dx,y,provider.z+dz);
                        if(MassAt(cc,p)<=0)continue;
                        // Top of this column only; deeper voxels are not shore.
                        if(MassAt(cc,new Vector3i(p.x,y+1,p.z))>0)break;
                        if(MassAt(cc,new Vector3i(p.x+1,y,p.z))>0&&MassAt(cc,new Vector3i(p.x-1,y,p.z))>0&&
                           MassAt(cc,new Vector3i(p.x,y,p.z+1))>0&&MassAt(cc,new Vector3i(p.x,y,p.z-1))>0)break;
                        int score=flat*16+Math.Abs(y-provider.y);
                        if(score<best){best=score;hit.Found=true;hit.Position=p;}
                        break;
                    }
                }
            NaturalCache[provider]=hit;
        }
        if(!hit.Found||MassAt(cc,hit.Position)<=0||!NaturalClaimAllows(world,owner,hit.Position))return false;
        position=hit.Position;return true;
    }
    // Native plan: target mass = MaxMass - Meta (>195 required), radius-2 shells around the voxel.
    private static bool TryPlanNatural(World world,Vector3i target,string emptyFrame,out int collected,out string encoded,out string fullFrame)
    {
        collected=0;encoded="";fullFrame="";
        try
        {
            var cc=world?.ChunkCache;var empty=RebirthHumanEquipment.Decode(emptyFrame);
            var container=empty.itemValue?.ItemClass as ItemClassWaterContainer;
            if(cc==null||container==null||empty.count!=1)return false;
            int targetMass=Math.Max(0,container.MaxMass-empty.itemValue.Meta);
            if(targetMass<NaturalMinMass||MassAt(cc,target)<=0)return false;
            var points=new List<CollectWaterUtils.WaterPoint>();
            int taken=CollectWaterUtils.CollectWater(cc,targetMass,target,NaturalCollectRadius,points);
            if(taken<=NaturalMinMass)return false;
            var parts=new List<string>();int sum=0;
            foreach(var point in points)
            {
                if(point.massToTake<=0)continue;
                int before=point.mass,after=point.finalMass;
                if(before<=0||after<0||after>=before||MassAt(cc,point.worldPos)!=before)return false;
                parts.Add(S(point.worldPos.x)+","+S(point.worldPos.y)+","+S(point.worldPos.z)+","+S(before)+","+S(after));
                sum+=point.massToTake;
            }
            if(sum!=taken||parts.Count==0||parts.Count>NaturalMaxPoints)return false;
            // Mirror native: a fresh bucketRiverWater carrying Meta = old Meta + collected mass.
            var full=new ItemStack(ItemClass.GetItem(FullBucket),1);
            if(full.IsEmpty())return false;
            full.itemValue.Meta=empty.itemValue.Meta+sum;
            string frame=RebirthHumanEquipment.Frame(full);
            if(!RebirthHumanEquipment.Accept(10,full))return false;
            collected=sum;encoded=string.Join(";",parts.ToArray());fullFrame=frame;return true;
        }
        catch{collected=0;encoded="";fullFrame="";return false;}
    }
    private static bool TryPlanNaturalSource(World world,EntityPlayer owner,Vector3i provider,string emptyFrame,
        out Vector3i source,out int collected,out string encoded,out string fullFrame)
    {
        collected=0;encoded="";fullFrame="";
        return TryFindNatural(world,owner,provider,out source)&&TryPlanNatural(world,source,emptyFrame,out collected,out encoded,out fullFrame);
    }
    private static bool ParseNatural(string text,out List<NaturalPoint> points)
    {
        points=new List<NaturalPoint>();
        if(string.IsNullOrEmpty(text)||text.Length>16384)return false;
        foreach(var part in text.Split(';'))
        {
            var f=part.Split(',');
            if(f.Length!=5||!int.TryParse(f[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out int x)||
               !int.TryParse(f[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int y)||
               !int.TryParse(f[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out int z)||
               !int.TryParse(f[3],NumberStyles.Integer,CultureInfo.InvariantCulture,out int before)||
               !int.TryParse(f[4],NumberStyles.Integer,CultureInfo.InvariantCulture,out int after)||before<=0||after<0||after>=before)
            {points=null;return false;}
            points.Add(new NaturalPoint{Position=new Vector3i(x,y,z),Before=before,After=after});
            if(points.Count>NaturalMaxPoints){points=null;return false;}
        }
        return points.Count>0;
    }
    private static string PointsDigest(string text)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("p8-natural:"+text))).Replace("-","").ToLowerInvariant();
    }

    // One checkpointed step of the natural source leg (phase src-attempted). Three-state recovery on the exact per-voxel
    // masses: all-before (apply allowed), all-after (the removal happened: publish), anything else (the grid moved: the
    // bucket is still unfilled, so re-plan from the live grid; water may be lost, a bucket is never filled without removal).
    private static bool NaturalLeg(World world,RebirthNpcStableId npc,Guid operation,bool allowEffect)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!TryReadSaved(npc,operation,out var row)||
               (string)row.Attribute("world")!=world.worldState?.Guid||!IsNatural(row))return false;
            string phase=(string)row.Attribute("phase");
            if(phase=="src-published")return true;
            if(phase!="src-attempted")return false;
            var cc=world.ChunkCache;string text=(string)row.Attribute("sPts");
            if(cc==null||!ParseNatural(text,out var points))return false;
            int atBefore=0,atAfter=0;
            foreach(var point in points){int mass=MassAt(cc,point.Position);if(mass==point.Before)atBefore++;if(mass==point.After)atAfter++;}
            if(atAfter!=points.Count)
            {
                if(!allowEffect)return false;
                var target=Pos(row,"s");
                if(!ulong.TryParse((string)row.Attribute("assignment"),out var assignmentId)||
                   !RebirthNpcWorkAssignmentService.TryGet(assignmentId,out var assignment)||
                   Owner(world,assignment.ActorId) is not EntityPlayer owner||!NaturalClaimAllows(world,owner,target))return false;
                if(atBefore!=points.Count)
                {
                    if(!TryPlanNatural(world,target,(string)row.Attribute("emptyFrame"),out int collected,out string encoded,out string frame))return false;
                    // Same-phase checkpoint: the new plan is saved before any effect is attempted against it.
                    Checkpoint(npc,operation,"src-attempted","src-attempted","sPts",encoded,"sMass",S(collected),"fullFrame",frame);
                    return false;
                }
                var package=NetPackageManager.GetPackage<NetPackageWaterSet>();
                foreach(var point in points)package.AddChange(point.Position,new WaterValue(point.After));
                GameManager.Instance.SetWaterRPC(package);
                foreach(var point in points)if(MassAt(cc,point.Position)!=point.After)return false;
            }
            return Checkpoint(npc,operation,"src-attempted","src-published","sProof","1:"+PointsDigest(text));
        }
        catch{return false;}
    }

    // ---- saved original ---------------------------------------------------------------
    private static XElement Read(RebirthNpcPersistentRecord person)
    {
        XElement result=null;
        foreach(var raw in person.UnknownOptionalElements??Array.Empty<string>())
        {
            var row=RebirthCompanionDeskStore.Parse(raw);
            if(row.Name!=Element)continue;
            if(result!=null)throw new InvalidDataException("Duplicate water original.");
            result=row;
        }
        return result;
    }
    internal static bool TryReadSaved(RebirthNpcStableId npc,Guid operation,out XElement original)
    {
        original=null;
        try
        {
            if(operation==Guid.Empty||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var row=Read(person);
            if(row==null||(string)row.Attribute("operation")!=operation.ToString("N")||(string)row.Attribute("npc")!=npc.ToString()||
               row.Attribute("version")?.Value!="1")return false;
            original=row;return true;
        }
        catch{return false;}
    }
    private static bool TryReadAny(RebirthNpcStableId npc,out XElement original)
    {
        original=null;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            original=Read(person);return original!=null&&original.Attribute("version")?.Value=="1";
        }
        catch{return false;}
    }
    private static bool Replace(RebirthNpcStableId npc,XElement previous,XElement updated)
    {
        try
        {
            string oldImage=previous.ToString(SaveOptions.DisableFormatting),newImage=updated.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,record=>
            {
                var rows=record.UnknownOptionalElements??Array.Empty<string>();
                if(!rows.Contains(oldImage)||rows.Count(x=>XElement.Parse(x).Name==Element)!=1)
                    throw new InvalidDataException("Water original changed before phase publication.");
                record.UnknownOptionalElements=rows.Select(x=>x==oldImage?newImage:x).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var saved)&&XNode.DeepEquals(Read(saved),updated);
        }
        catch{return false;}
    }
    private static bool Checkpoint(RebirthNpcStableId npc,Guid operation,string expected,string next,params string[] attributes)
    {
        if(!TryReadSaved(npc,operation,out var previous)||(string)previous.Attribute("phase")!=expected)return false;
        var updated=new XElement(previous);
        updated.SetAttributeValue("phase",next);
        for(int i=0;i+1<attributes.Length;i+=2)updated.SetAttributeValue(attributes[i],attributes[i+1]);
        return Replace(npc,previous,updated);
    }
    private static string S(int v)=>v.ToString(CultureInfo.InvariantCulture);

    // ---- prepare ----------------------------------------------------------------------
    // Truthful blocker categories: no row is written unless every input exists.
    internal static bool TryPrepare(RebirthNpcConcreteWorkContext context,EntityRebirthHumanoidNPC npc,
        out string detail,out RebirthNpcWorkFailureCategory failure)
    {
        detail="Saved Farm target or provider is unavailable.";failure=RebirthNpcWorkFailureCategory.TargetUnavailable;
        var world=GameManager.Instance?.World;var a=context?.Assignment;
        if(world==null||world.IsRemote()||string.IsNullOrEmpty(world.worldState?.Guid)||!ThreadManager.IsMainThread()||a==null||
           context.OperationId==Guid.Empty||npc==null||npc.IsDead()||!Authorized(a,out long revision))return false;
        if(TryReadSaved(a.NpcId,context.OperationId,out _)){detail="Saved water original remains in progress.";failure=RebirthNpcWorkFailureCategory.None;return true;}
        // A leftover "filled" row (provider became unusable after the bucket was filled) is resumed by the
        // next assignment instead of blocking forever; any other unfinished phase stays held.
        bool orphanFilled=false;
        if(TryReadAny(a.NpcId,out var other)&&(string)other.Attribute("phase")!="completed")
        {
            if((string)other.Attribute("phase")!="filled")
            {detail="Another water original remains held for reconciliation.";failure=RebirthNpcWorkFailureCategory.PersistenceRecovery;return false;}
            // Only a bucket released after a terminal failure, or one whose owning assignment is gone/finished,
            // may be taken over; a live assignment keeps its own filled row.
            if((string)other.Attribute("held")!="1"&&!OwnerGone(other))
            {detail="Another refill assignment still owns the filled bucket.";failure=RebirthNpcWorkFailureCategory.PersistenceRecovery;return false;}
            orphanFilled=true;
        }
        else if(other!=null&&(string)other.Attribute("settled")!="1")
        {
            // A completed but unsettled row must finish its outcome and award before it can be replaced.
            if(!Guid.TryParseExact((string)other.Attribute("operation"),"N",out var finishing)||!TryComplete(a.NpcId,finishing))
            {detail="Previous refill is still settling its outcome.";failure=RebirthNpcWorkFailureCategory.PersistenceRecovery;return false;}
        }
        if(npc.bag?.ItemGrid==null||npc.IsPreparedRestorationPending||npc.Hand==null||npc.Hand.IsBusy||
           RebirthNpcWorkNativeTransferOriginal.HasPending(a.NpcId)||!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a))
        {detail="Worker inventory or outcome identity is not ready.";failure=RebirthNpcWorkFailureCategory.InventoryUnavailable;return false;}
        int percent=RebirthP8NpcWaterPolicy.GetPercent(a.NpcId);
        var provider=Position(a);
        int count,max,threshold;
        switch(EvaluateProvider(world,provider,percent,out count,out max,out threshold))
        {
            case Need.NotAProvider:detail="Target is not a Rebirth water tank or dew collector.";failure=RebirthNpcWorkFailureCategory.InvalidTarget;return false;
            case Need.AboveThreshold:detail="Provider is at or above the minimum level ("+S(count)+"/"+S(threshold)+"); nothing to refill.";failure=RebirthNpcWorkFailureCategory.MissingInput;return false;
            case Need.NoRoomForBucket:detail="Provider cannot accept a full "+S(Exchange)+"-unit bucket ("+S(count)+"/"+S(max)+").";failure=RebirthNpcWorkFailureCategory.OutputBlocked;return false;
        }
        var providerTank=(TileEntityWaterTankRebirth)world.GetTileEntity(provider);
        if(providerTank.WaterExchangeRevision==ulong.MaxValue)return false;
        if(orphanFilled)return Adopt(context,npc,a,other,revision,provider,threshold,max,percent,out detail,out failure);
        var owner=Owner(world,a.ActorId);
        if(owner==null)return false;
        var emptyStack=new ItemStack(ItemClass.GetItem(EmptyBucket),1);
        var fullStack=new ItemStack(ItemClass.GetItem(FullBucket),1);
        string emptyFrame=RebirthHumanEquipment.Frame(emptyStack),fullFrame=RebirthHumanEquipment.Frame(fullStack);
        if(emptyStack.IsEmpty()||fullStack.IsEmpty())return false;
        // Placed Rebirth storage is preferred; natural water is the fallback when no store has surplus.
        bool natural=false;int naturalMass=0;string naturalPoints="";
        if(!TryFindSource(world,owner,npc,provider,percent,out var source))
        {
            if(!TryPlanNaturalSource(world,owner,provider,emptyFrame,out source,out naturalMass,out naturalPoints,out fullFrame))
            {detail="No accessible Rebirth water source with surplus and no reachable natural water.";failure=RebirthNpcWorkFailureCategory.MissingInput;return false;}
            natural=true;fullStack=RebirthHumanEquipment.Decode(fullFrame);
        }
        int slot=-1;
        // Only a pristine one-count bucket is converted; a bucket carrying other metadata is left alone.
        for(int i=0;i<npc.bag.SlotCount;i++)
            if(RebirthHumanEquipment.Frame(npc.bag.ItemGrid[i])==emptyFrame&&!BucketLocked(npc,i)&&RebirthHumanEquipment.Accept(10+i,fullStack)){slot=i;break;}
        if(slot<0){detail="Worker has no unlocked empty bucket in its native bag.";failure=RebirthNpcWorkFailureCategory.MissingInput;return false;}
        // One native binding id is kept across fill and empty; the custody code re-captures its stack record in place.
        if(!TryBoundId(a.NpcId,slot,out var bucketId)||!SavedStack(a.NpcId,bucketId,slot,emptyFrame))
        {detail="Empty bucket is not yet saved as a bound native stack.";failure=RebirthNpcWorkFailureCategory.InventoryUnavailable;return false;}
        var owned=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(owned==null||!Allows(owned)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(owned)||!ReferenceEquals(owned.Actor,npc))return false;
        Guid birth=Guid.Empty,incarnation=Guid.Empty;uint raw=0;TileEntityWaterTankRebirth sourceTank=null;
        if(!natural)
        {
            var sourceBlock=world.GetBlock(source);sourceTank=(TileEntityWaterTankRebirth)world.GetTileEntity(source);
            if(sourceTank.WaterIncarnation==Guid.Empty){sourceTank.WaterIncarnation=Guid.NewGuid();sourceTank.setModified();}
            // The source identity must be natively published before any effect is prepared against it.
            birth=Derive(context.OperationId,"src-birth");incarnation=sourceTank.WaterIncarnation;raw=sourceBlock.rawData;
            Track(birth);
            if(!AdvancedFarmingHarvestPublication.Observe(birth,incarnation,world,source,
                ()=>ReferenceEquals(world.GetTileEntity(source),sourceTank)&&sourceTank.WaterIncarnation==incarnation&&world.GetBlock(source).rawData==raw,out _))
            {detail="Water source identity is settling.";failure=RebirthNpcWorkFailureCategory.None;return false;}
        }
        var row=new XElement(Element,new XAttribute("version",1),new XAttribute("npc",a.NpcId.ToString()),
            new XAttribute("definition",DefinitionId),new XAttribute("startedTicks",a.CreatedUtcTicks),new XAttribute("embodiment",owned.Generation),
            new XAttribute("assignment",a.AssignmentId),new XAttribute("operation",context.OperationId.ToString("N")),
            new XAttribute("target",a.TargetKey),new XAttribute("revision",revision),new XAttribute("world",world.worldState.Guid),
            new XAttribute("percent",percent),new XAttribute("threshold",threshold),new XAttribute("max",max),
            new XAttribute("slot",slot),new XAttribute("emptyFrame",emptyFrame),new XAttribute("fullFrame",fullFrame),
            new XAttribute("bucketId",bucketId.ToString("N")),
            new XAttribute("dX",provider.x),new XAttribute("dY",provider.y),new XAttribute("dZ",provider.z),
            new XAttribute("sX",source.x),new XAttribute("sY",source.y),new XAttribute("sZ",source.z),
            new XAttribute("sOp",Derive(context.OperationId,"src").ToString("N")));
        if(natural)
            // Natural water has no tile stamp: the saved per-voxel before/after masses are the effect witness.
            row.Add(new XAttribute("sKind",NaturalKind),new XAttribute("sMass",naturalMass),new XAttribute("sPts",naturalPoints));
        else
            row.Add(new XAttribute("sInc",incarnation.ToString("N")),
                new XAttribute("sPrev",sourceTank.WaterExchangeOperation.ToString("N")),new XAttribute("sRev",sourceTank.WaterExchangeRevision+1),
                new XAttribute("sRaw",raw),new XAttribute("sBefore",sourceTank.waterCount),new XAttribute("sAfter",sourceTank.waterCount-Exchange));
        row.Add(new XAttribute("phase","prepared"));
        if(!RebirthNpcAggregatePersistenceStore.TryGet(a.NpcId,out _))return false;
        string image=row.ToString(SaveOptions.DisableFormatting);
        if(!RebirthNpcAggregatePersistenceStore.Mutate(a.NpcId,record=>
        {
            var rows=record.UnknownOptionalElements??Array.Empty<string>();
            var prior=rows.Where(x=>XElement.Parse(x).Name==Element).ToArray();
            if(prior.Length>1||prior.Length==1&&(string)XElement.Parse(prior[0]).Attribute("phase")!="completed")
                throw new InvalidDataException("Water original changed before publication.");
            record.UnknownOptionalElements=rows.Where(x=>XElement.Parse(x).Name!=Element).Concat(new[]{image}).ToArray();
        }))return false;
        RebirthNpcAggregatePersistenceStore.Save();
        Register(context.OperationId,a.NpcId);
        if(!natural)AdvancedFarmingHarvestPublication.Complete(birth);
        failure=RebirthNpcWorkFailureCategory.None;
        detail=natural?"Natural water, empty bucket and provider selected with saved original.":"Source, empty bucket and provider selected with saved original.";
        return TryReadSaved(a.NpcId,context.OperationId,out var saved)&&XNode.DeepEquals(saved,row);
    }

    // ---- tank legs ---------------------------------------------------------------------
    private static Vector3i Pos(XElement row,string p)=>new Vector3i((int)row.Attribute(p+"X"),(int)row.Attribute(p+"Y"),(int)row.Attribute(p+"Z"));
    private static bool Stamped(XElement row,string p,TileEntityWaterTankRebirth tank)
        =>tank!=null&&tank.WaterIncarnation.ToString("N")==(string)row.Attribute(p+"Inc")&&tank.WaterExchangeOperation.ToString("N")==(string)row.Attribute(p+"Op")&&
          tank.WaterExchangeRevision==(ulong)row.Attribute(p+"Rev")&&tank.WaterExchangeBefore==(int)row.Attribute(p+"Before")&&
          tank.WaterExchangeAfter==(int)row.Attribute(p+"After");
    private static bool CanApply(XElement row,string p,TileEntityWaterTankRebirth tank,uint raw)
        =>tank!=null&&tank.WaterIncarnation.ToString("N")==(string)row.Attribute(p+"Inc")&&raw==(uint)row.Attribute(p+"Raw")&&
          tank.WaterExchangeOperation.ToString("N")==(string)row.Attribute(p+"Prev")&&tank.WaterExchangeRevision+1==(ulong)row.Attribute(p+"Rev")&&
          tank.waterCount==(int)row.Attribute(p+"Before");

    // One checkpointed step of a tank leg. allowEffect=false (restart recovery) observes only.
    private static bool TankLeg(World world,RebirthNpcStableId npc,Guid operation,string p,string attempted,string published,bool allowEffect)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!TryReadSaved(npc,operation,out var row)||
               (string)row.Attribute("world")!=world.worldState?.Guid)return false;
            string phase=(string)row.Attribute("phase");
            if(phase==published)return true;
            if(phase!=attempted)return false;
            // Assumption: neither ApplyWaterDelta (count/time/modified/sync) nor the tile stamp touches the block
            // rawData, so the saved raw stays a valid identity witness across the water change.
            var position=Pos(row,p);var tank=world.GetTileEntity(position) as TileEntityWaterTankRebirth;
            if(tank==null||!Guid.TryParseExact((string)row.Attribute(p+"Inc"),"N",out var inc)||!Guid.TryParseExact((string)row.Attribute(p+"Op"),"N",out var op))return false;
            uint raw=(uint)row.Attribute(p+"Raw");
            // Stamp present but the count is still the before-count: a stamp without its delta (torn persistence).
            // Never publish it and never apply again; stay held for review.
            // Three-state recovery on the exact count: before (apply/re-plan allowed), after (proof only with our stamp),
            // other (never proof). A stamp with any count except the saved after-count holds without publishing.
            int savedBefore=(int)row.Attribute(p+"Before"),savedAfter=(int)row.Attribute(p+"After");
            bool stamped=Stamped(row,p,tank);
            if(stamped&&tank.waterCount!=savedAfter)return false;
            // Unstamped after-count on the exact previous operation/revision: a crash after ApplyWaterDelta but before the
            // stamp, or an independent change to the same number. No saved effect witness separates them, so hold: no second
            // source debit and no destination credit claim.
            if(!stamped&&savedBefore!=savedAfter&&tank.waterCount==savedAfter&&
               tank.WaterIncarnation.ToString("N")==(string)row.Attribute(p+"Inc")&&
               tank.WaterExchangeOperation.ToString("N")==(string)row.Attribute(p+"Prev")&&tank.WaterExchangeRevision+1==(ulong)row.Attribute(p+"Rev"))return false;
            bool Current()=>Stamped(row,p,tank)&&tank.waterCount==savedAfter&&ReferenceEquals(world.GetTileEntity(position),tank)&&world.GetBlock(position).rawData==raw;
            if(!Current())
            {
                if(allowEffect&&!CanApply(row,p,tank,world.GetBlock(position).rawData)&&
                   tank.WaterIncarnation.ToString("N")==(string)row.Attribute(p+"Inc")&&
                   tank.WaterExchangeOperation.ToString("N")==(string)row.Attribute(p+"Prev")&&tank.WaterExchangeRevision+1==(ulong)row.Attribute(p+"Rev")&&
                   AdvancedFarmingItemWaterExchange.IsAdvancedWaterStorage(world.GetBlock(position)))
                {
                    // Our stamp can only appear together with the operation/revision change, so an unchanged
                    // operation and revision prove nothing was applied: re-plan from the live tile, never clamp.
                    var live=world.GetBlock(position);
                    if(p=="d")Checkpoint(npc,operation,attempted,"filled");
                    else if(tank.waterCount-Exchange>=RebirthP8NpcWaterPolicy.ThresholdUnits(
                        AdvancedFarmingItemWaterExchange.ResolveMaxWater(live,tank),(int)row.Attribute("percent")))
                        Checkpoint(npc,operation,attempted,"prepared","sRaw",live.rawData.ToString(CultureInfo.InvariantCulture),
                            "sBefore",S(tank.waterCount),"sAfter",S(tank.waterCount-Exchange));
                    return false;
                }
                if(!allowEffect||!CanApply(row,p,tank,world.GetBlock(position).rawData))return false;
                int before=(int)row.Attribute(p+"Before"),after=(int)row.Attribute(p+"After");
                // Same order as the player original: mutate, verify the exact after-count, THEN stamp. The stamp
                // is therefore never written without the delta; a failure restores the count so the pre-image
                // (before-count, no stamp) is exactly what the re-plan branch above expects.
                int priorCount=tank.waterCount;
                bool applied=false;
                try
                {
                    AdvancedFarmingItemWaterExchange.ApplyWaterDelta(world,position,world.GetBlock(position),tank,after-before);
                    applied=tank.waterCount==after;
                }
                catch{applied=false;}
                if(!applied)
                {
                    tank.waterCount=priorCount;tank.setModified();
                    if(AdvancedFarmingItemWaterExchange.IsServerAuthority())AdvancedFarmingSyncService.SendWaterTank(position,priorCount);
                    return false;
                }
                tank.WaterExchangeBefore=before;tank.WaterExchangeAfter=after;tank.WaterExchangeOperation=op;
                tank.WaterExchangeRevision=(ulong)row.Attribute(p+"Rev");
                tank.setModified();
                if(AdvancedFarmingItemWaterExchange.IsServerAuthority())AdvancedFarmingSyncService.SendWaterTank(position,tank.waterCount);
            }
            Track(op);
            if(!Current())return false;
            if(!AdvancedFarmingHarvestPublication.Observe(op,inc,world,position,Current,out var proof)||proof==null||proof.Operation!=op||proof.Source!=inc||
               proof.World!=world.worldState.Guid||proof.X!=position.x||proof.Y!=position.y||proof.Z!=position.z||proof.Digest?.Length!=64||proof.Generation<=0)return false;
            string witness=proof.Generation.ToString(CultureInfo.InvariantCulture)+":"+proof.Digest;
            if(!Checkpoint(npc,operation,attempted,published,p+"Proof",witness))return false;
            AdvancedFarmingHarvestPublication.Complete(op);
            return true;
        }
        catch{return false;}
    }

    // ---- bucket legs -------------------------------------------------------------------
    private static bool SavedStack(RebirthNpcStableId npc,Guid id,int slot,string frame)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person)||
               !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc,out var inventory)||inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
            if((string)binding?.Attribute("id")!=id.ToString("N"))return false;
            var stack=RebirthHumanEquipment.Decode(frame);
            var saved=inventory.NativeStacks.Write().Elements().Select(x=>RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null).SingleOrDefault(x=>x?.StackId==id);
            return saved!=null&&saved.Count==stack.count&&saved.Payload==RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch{return false;}
    }

    // Converts the bound slot between the two exact saved frames. A live slot still holding the
    // exact old frame with no saved new stack is an unentered setter; anything else is held.
    private static bool BucketLeg(EntityRebirthHumanoidNPC npc,Guid operation,string fromPhase,string attempted,string to,
        string oldFrameAttr,string newFrameAttr,string idAttr,bool allowEffect)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||npc==null||npc.IsDead()||npc.IsPreparedRestorationPending||
               !ReferenceEquals(world.GetEntity(npc.entityId),npc)||npc.RebirthRuntimeState==null)return false;
            var id=npc.RebirthRuntimeState.StableId;
            if(!TryReadSaved(id,operation,out var row)||(string)row.Attribute("world")!=world.worldState?.Guid)return false;
            string phase=(string)row.Attribute("phase");
            if(phase==to)return true;
            if(phase!=fromPhase&&phase!=attempted)return false;
            if(npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null||RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||
               RebirthNpcWorkNativeTransferOriginal.HasPending(id))return false;
            if(!int.TryParse((string)row.Attribute("slot"),out int slot)||slot<0||slot>=npc.bag.SlotCount||
               !Guid.TryParseExact((string)row.Attribute(idAttr),"N",out var newId))return false;
            string oldFrame=(string)row.Attribute(oldFrameAttr),newFrame=(string)row.Attribute(newFrameAttr);
            var intended=RebirthHumanEquipment.Decode(newFrame);
            if(intended.IsEmpty()||intended.count!=1||!RebirthHumanEquipment.Accept(10+slot,intended))return false;
            bool alreadySaved=SavedStack(id,newId,slot,newFrame);
            string live=RebirthHumanEquipment.Frame(npc.bag.ItemGrid[slot]);
            if(!(alreadySaved&&live==newFrame))
            {
                if(allowEffect&&live==newFrame&&!alreadySaved&&phase==attempted)
                {
                    // The setter ran but its save never landed: re-save once, only under the matching binding id.
                    var pending=RebirthHumanEquipment.Read(npc);
                    var bound=pending.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
                    if((string)bound?.Attribute("id")!=newId.ToString("N"))return false;
                    RebirthHumanEquipment.Save(npc,pending);
                    return SavedStack(id,newId,slot,newFrame)&&Checkpoint(id,operation,attempted,to);
                }
                if(!allowEffect||live!=oldFrame||alreadySaved)return false;
                if(phase==fromPhase&&!Checkpoint(id,operation,fromPhase,attempted))return false;
                if(!SavedStack(id,newId,slot,oldFrame))return false;
                var equipment=RebirthHumanEquipment.Read(npc);
                var binding=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
                if((string)binding?.Attribute("id")!=newId.ToString("N"))return false;
                npc.bag.SetSlot(slot,intended);
                RebirthHumanEquipment.Save(npc,equipment);
                if(!SavedStack(id,newId,slot,newFrame))return false;
            }
            else if(phase==fromPhase&&!Checkpoint(id,operation,fromPhase,attempted))return false;
            return Checkpoint(id,operation,attempted,to);
        }
        catch{return false;}
    }

    // Slot lock (P7 convention: a missing or short lock array counts as locked) and the companion item-name lock.
    private static bool BucketLocked(EntityRebirthHumanoidNPC npc,int slot)
    {
        var locks=npc.bag.ItemGrid.SlotLocks;
        // Repo rule for unknown lock state: refuse (a null or short array is treated as locked).
        if(locks==null||slot>=locks.Length||locks[slot])return true;
        var id=npc.RebirthRuntimeState.StableId;
        return RebirthCompanionInventoryLockService.IsNpcItemLocked(id,EmptyBucket)||RebirthCompanionInventoryLockService.IsNpcItemLocked(id,FullBucket);
    }

    // Own observation attempts, with start time, so a pinned or suspended job cannot hold the shared table.
    private static readonly Dictionary<Guid,float> Tracked=new Dictionary<Guid,float>();
    private static void Track(Guid operation){Tracked[operation]=Time.realtimeSinceStartup;}
    private static float nextReap;
    internal static void ReapStale()
    {
        if(Time.realtimeSinceStartup<nextReap||Tracked.Count==0)return;
        nextReap=Time.realtimeSinceStartup+15f;
        List<Guid> old=null;
        foreach(var pair in Tracked)if(Time.realtimeSinceStartup-pair.Value>90f)(old??(old=new List<Guid>())).Add(pair.Key);
        if(old==null)return;
        foreach(var operation in old){try{AdvancedFarmingHarvestPublication.Complete(operation);}catch{}Tracked.Remove(operation);}
    }
    // For the coordinator settle/cancel path (End may never reach EndCore for a pinned job).
    public static void ReleaseFor(RebirthNpcStableId npc,ulong assignmentId)
        =>Release(npc,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId));

    // True only when releasing the assignment cannot orphan an in-flight effect: the retained filled-bucket
    // state after a terminal failure (marked held) or a fully settled completion.
    private static bool OwnerGone(XElement row)
    {
        if(!ulong.TryParse((string)row.Attribute("assignment"),out var id))return true;
        if(!RebirthNpcWorkAssignmentService.TryGet(id,out var owner))return true;
        return owner.Status==RebirthNpcWorkAssignmentStatus.Completed||owner.Status==RebirthNpcWorkAssignmentStatus.Cancelled||
            owner.Status==RebirthNpcWorkAssignmentStatus.Failed;
    }
    public static bool IsReleasedTerminal(RebirthNpcStableId npc,ulong assignmentId)
    {
        try
        {
            if(!TryReadSaved(npc,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId),out var row))return false;
            string phase=(string)row.Attribute("phase");
            if(phase=="filled")return (string)row.Attribute("held")=="1";
            // A settled row whose assignment already reached Completed needs no release.
            return phase=="completed"&&(string)row.Attribute("settled")=="1"&&
                !(RebirthNpcWorkAssignmentService.TryGet(assignmentId,out var owner)&&owner.Status==RebirthNpcWorkAssignmentStatus.Completed);
        }
        catch{return false;}
    }
    // For the coordinator when a session is gone (restart or suspend): no-op unless the NPC's single water
    // row is `filled` and belongs to this assignment's operation. Idempotent; main thread; no executor state.
    public static void MarkHeldIfFilled(RebirthNpcStableId npc,ulong assignmentId)
    {
        try
        {
            if(!ThreadManager.IsMainThread())return;
            var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId);
            if(TryReadSaved(npc,operation,out var row)&&(string)row.Attribute("phase")=="filled")MarkHeld(npc,operation);
        }
        catch{}
    }
    internal static bool MarkHeld(RebirthNpcStableId npc,Guid operation)
    {
        if(!TryReadSaved(npc,operation,out var row)||(string)row.Attribute("phase")!="filled")return false;
        if((string)row.Attribute("held")=="1")return true;
        var updated=new XElement(row);updated.SetAttributeValue("held","1");
        return Replace(npc,row,updated);
    }

    private static bool TryBoundId(RebirthNpcStableId npc,int slot,out Guid id)
    {
        id=Guid.Empty;
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==slot);
            return Guid.TryParseExact((string)binding?.Attribute("id"),"N",out id)&&id!=Guid.Empty;
        }
        catch{return false;}
    }

    // Frees every native publication observation this operation may have started (the table is shared with
    // crop harvest and capped), so a cancel, hold or loss mid-settle cannot starve harvest publication.
    internal static void Release(RebirthNpcStableId npc,Guid operation)
    {
        try
        {
            if(operation==Guid.Empty)return;
            foreach(var label in new[]{"src-birth","dst-birth","src","dst"})AdvancedFarmingHarvestPublication.Complete(Derive(operation,label));
            if(TryReadSaved(npc,operation,out var row))
                foreach(var name in new[]{"sOp","dOp"})
                    if(Guid.TryParseExact((string)row.Attribute(name),"N",out var op))AdvancedFarmingHarvestPublication.Complete(op);
        }
        catch{}
    }

    // Resume a retained filled bucket for a new assignment: provider and identity are rebound, the
    // already-spent source leg and bucket custody are kept, so no second source debit or bucket is used.
    private static bool Adopt(RebirthNpcConcreteWorkContext context,EntityRebirthHumanoidNPC npc,RebirthNpcWorkAssignment a,XElement other,
        long revision,Vector3i provider,int threshold,int max,int percent,out string detail,out RebirthNpcWorkFailureCategory failure)
    {
        detail="Retained filled bucket cannot be resumed.";failure=RebirthNpcWorkFailureCategory.InventoryUnavailable;
        var owned=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(owned==null||!Allows(owned)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(owned)||!ReferenceEquals(owned.Actor,npc)||
           (string)other.Attribute("world")!=GameManager.Instance.World.worldState.Guid||
           !int.TryParse((string)other.Attribute("slot"),out int slot)||slot<0||slot>=npc.bag.SlotCount||
           !Guid.TryParseExact((string)other.Attribute("bucketId"),"N",out var bucketId)||
           RebirthHumanEquipment.Frame(npc.bag.ItemGrid[slot])!=(string)other.Attribute("fullFrame")||
           !SavedStack(a.NpcId,bucketId,slot,(string)other.Attribute("fullFrame")))return false;
        var updated=new XElement(other);
        updated.SetAttributeValue("held",null);
        updated.SetAttributeValue("operation",context.OperationId.ToString("N"));updated.SetAttributeValue("assignment",a.AssignmentId);
        updated.SetAttributeValue("startedTicks",a.CreatedUtcTicks);updated.SetAttributeValue("embodiment",owned.Generation);
        updated.SetAttributeValue("target",a.TargetKey);updated.SetAttributeValue("revision",revision);
        updated.SetAttributeValue("percent",percent);updated.SetAttributeValue("threshold",threshold);updated.SetAttributeValue("max",max);
        updated.SetAttributeValue("dX",provider.x);updated.SetAttributeValue("dY",provider.y);updated.SetAttributeValue("dZ",provider.z);
        if(!Replace(a.NpcId,other,updated))return false;
        Register(context.OperationId,a.NpcId);
        failure=RebirthNpcWorkFailureCategory.None;detail="Retained filled bucket resumed for the new provider assignment.";return true;
    }

    internal static bool HasUnfinishedOriginal(RebirthNpcStableId npc)
        =>TryReadAny(npc,out var row)&&(string)row.Attribute("phase")!="completed"&&(string)row.Attribute("phase")!="filled";

    // Truthful pre-check for the standing maintainer; mirrors TryPrepare without writing anything.
    internal static bool CanStart(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc,out string blocker)
    {
        blocker="Water refill is unavailable.";
        var world=GameManager.Instance?.World;
        if(world==null||npc?.bag?.ItemGrid==null||!Authorized(a,out _))return false;
        var owner=Owner(world,a.ActorId);int percent=RebirthP8NpcWaterPolicy.GetPercent(a.NpcId);
        if(owner==null)return false;
        if(ClaimedByOther(Position(a),RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId))){blocker="Another refill is already serving this provider.";return false;}
        if(EvaluateProvider(world,Position(a),percent,out _,out _,out _)!=Need.Needed){blocker="Provider is not below the minimum or has no room for a bucket.";return false;}
        if(TryReadAny(a.NpcId,out var row)&&(string)row.Attribute("phase")=="filled")return true;
        if(!TryFindAnySource(world,owner,npc,Position(a),percent)){blocker="No accessible water source with surplus and no reachable natural water.";return false;}
        string empty=RebirthHumanEquipment.Frame(new ItemStack(ItemClass.GetItem(EmptyBucket),1));
        for(int i=0;i<npc.bag.SlotCount;i++)if(RebirthHumanEquipment.Frame(npc.bag.ItemGrid[i])==empty&&!BucketLocked(npc,i))return true;
        blocker="Worker has no unlocked empty bucket.";return false;
    }

    // ---- state machine -----------------------------------------------------------------
    internal static bool InProgress(string phase)=>phase!=null&&phase!="completed";
    // Which saved position the worker must be physically at for the next effect.
    internal static bool TryLegPosition(XElement row,out Vector3i position)
    {
        position=default(Vector3i);if(row==null)return false;
        string phase=(string)row.Attribute("phase");
        bool atSource=phase=="prepared"||phase=="src-attempted"||phase=="src-published"||phase=="fill-attempted";
        position=Pos(row,atSource?"s":"d");return true;
    }

    // Dest leg intent is captured only at the provider, against its live tile.
    private static bool PrepareDestination(World world,EntityRebirthHumanoidNPC npc,XElement row,Guid operation,out string detail,out RebirthNpcWorkFailureCategory failure)
    {
        detail="Provider identity is settling.";failure=RebirthNpcWorkFailureCategory.None;
        var position=Pos(row,"d");var block=world.GetBlock(position);var tank=world.GetTileEntity(position) as TileEntityWaterTankRebirth;
        if(tank==null||!AdvancedFarmingItemWaterExchange.IsAdvancedWaterStorage(block)||tank.WaterExchangeRevision==ulong.MaxValue)
        {detail="Provider is no longer a Rebirth water tank or dew collector; the filled bucket is retained.";failure=RebirthNpcWorkFailureCategory.TargetChanged;return false;}
        int max=AdvancedFarmingItemWaterExchange.ResolveMaxWater(block,tank);
        if(max-tank.waterCount<Exchange)
        {detail="Provider is full ("+S(tank.waterCount)+"/"+S(max)+"); the filled bucket is retained.";failure=RebirthNpcWorkFailureCategory.OutputBlocked;return false;}
        // Another live refill is stamping this tank (as its provider or its source): wait instead of racing its stamp.
        if(ClaimedByOther(position,operation)){detail="Provider is in use by another refill; waiting.";return false;}
        if(tank.WaterIncarnation==Guid.Empty){tank.WaterIncarnation=Guid.NewGuid();tank.setModified();}
        Guid birth=Derive(operation,"dst-birth"),incarnation=tank.WaterIncarnation;uint raw=block.rawData;
        Track(birth);
        if(!AdvancedFarmingHarvestPublication.Observe(birth,incarnation,world,position,
            ()=>ReferenceEquals(world.GetTileEntity(position),tank)&&tank.WaterIncarnation==incarnation&&world.GetBlock(position).rawData==raw,out _))return false;
        if(!Checkpoint(npc.RebirthRuntimeState.StableId,operation,"filled","dst-attempted",
            "dOp",Derive(operation,"dst").ToString("N"),"dInc",incarnation.ToString("N"),"dPrev",tank.WaterExchangeOperation.ToString("N"),
            "dRev",(tank.WaterExchangeRevision+1).ToString(CultureInfo.InvariantCulture),"dRaw",raw.ToString(CultureInfo.InvariantCulture),
            "dBefore",S(tank.waterCount),"dAfter",S(tank.waterCount+Exchange)))return false;
        AdvancedFarmingHarvestPublication.Complete(birth);
        return true;
    }

    // Advances at most one phase. Called by the work adapter (allowEffect, at the proper leg
    // position) and by restart recovery (observe-only).
    internal static bool Advance(EntityRebirthHumanoidNPC npc,Guid operation,bool allowEffect,out string detail,out RebirthNpcWorkFailureCategory failure)
    {
        detail="Saved water original is settling.";failure=RebirthNpcWorkFailureCategory.None;
        var world=GameManager.Instance?.World;
        if(world==null||npc?.RebirthRuntimeState==null||!TryReadSaved(npc.RebirthRuntimeState.StableId,operation,out var row))return false;
        var id=npc.RebirthRuntimeState.StableId;
        Register(operation,id);
        switch((string)row.Attribute("phase"))
        {
            case "prepared":
                if(!allowEffect||!Checkpoint(id,operation,"prepared","src-attempted"))return false;
                return IsNatural(row)?NaturalLeg(world,id,operation,true):TankLeg(world,id,operation,"s","src-attempted","src-published",true);
            case "src-attempted":return IsNatural(row)?NaturalLeg(world,id,operation,allowEffect):TankLeg(world,id,operation,"s","src-attempted","src-published",allowEffect);
            case "src-published":
            case "fill-attempted":
                return BucketLeg(npc,operation,"src-published","fill-attempted","filled","emptyFrame","fullFrame","bucketId",allowEffect);
            case "filled":
                return allowEffect&&PrepareDestination(world,npc,row,operation,out detail,out failure);
            case "dst-attempted":return TankLeg(world,id,operation,"d","dst-attempted","dst-published",allowEffect);
            case "dst-published":
            case "empty-attempted":
                return BucketLeg(npc,operation,"dst-published","empty-attempted","emptied","fullFrame","emptyFrame","bucketId",allowEffect);
            case "emptied":
            case "completed":return TryComplete(id,operation);
        }
        return false;
    }

    // Restart recovery: observe saved effects only; never starts a new world or item effect.
    internal static void Recover(EntityRebirthHumanoidNPC npc,RebirthNpcStableId id)
    {
        try
        {
            ReapStale();
            if(!TryReadAny(id,out var row)||!Guid.TryParseExact((string)row.Attribute("operation"),"N",out var operation))return;
            Advance(npc,operation,false,out _,out _);
        }
        catch{}
    }

    // ---- completion -------------------------------------------------------------------
    private static string Witness(XElement row)
        =>"saved-water-refill:src="+((string)row.Attribute("sProof")??"")+"|dst="+((string)row.Attribute("dProof")??"")+
          "|bucket="+((string)row.Attribute("bucketId")??"")+"|operation="+((string)row.Attribute("operation")??"");

    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        try
        {
            if(outcome==null||!IsDefinition(outcome.DefinitionId)||outcome.State!=RebirthNpcWorkOutcomeState.Completed||!outcome.DurableCompletion||
               outcome.OutcomeId==Guid.Empty||!TryReadSaved(outcome.NpcId,outcome.OutcomeId,out var row)||(string)row.Attribute("phase")!="completed"||
               outcome.AssignmentId.ToString(CultureInfo.InvariantCulture)!=(string)row.Attribute("assignment")||
               outcome.TargetId!="provider:"+S((int)row.Attribute("dX"))+":"+S((int)row.Attribute("dY"))+":"+S((int)row.Attribute("dZ"))||outcome.StartedUtcTicks!=(long?)row.Attribute("startedTicks")||
               outcome.DurableWitness!=Witness(row)||!long.TryParse((string)row.Attribute("completedTicks"),out var completed)||
               outcome.CompletedUtcTicks!=completed)return false;
            // Settles from the saved row; the live bag is only consulted when the outcome is first written.
            return !string.IsNullOrEmpty((string)row.Attribute("sProof"))&&!string.IsNullOrEmpty((string)row.Attribute("dProof"));
        }
        catch{return false;}
    }

    internal static bool TryComplete(RebirthNpcStableId npc,Guid operation)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!TryReadSaved(npc,operation,out var row))return false;
            string phase=(string)row.Attribute("phase");
            if(phase!="emptied"&&phase!="completed")return false;
            if((string)row.Attribute("settled")=="1")return true;
            if(!Guid.TryParseExact((string)row.Attribute("bucketId"),"N",out var emptyId)||!int.TryParse((string)row.Attribute("slot"),out int slot)||
               phase=="emptied"&&!SavedStack(npc,emptyId,slot,(string)row.Attribute("emptyFrame"))||string.IsNullOrEmpty((string)row.Attribute("sProof"))||
               string.IsNullOrEmpty((string)row.Attribute("dProof")))return false;
            if(phase=="emptied")
            {
                var updated=new XElement(row);updated.SetAttributeValue("phase","completed");
                updated.SetAttributeValue("completedTicks",DateTime.UtcNow.Ticks);
                if(!Replace(npc,row,updated))return false;
                row=updated;
            }
            if(!ulong.TryParse((string)row.Attribute("assignment"),out var assignment)||!long.TryParse((string)row.Attribute("completedTicks"),out var ticks)||ticks<=0)return false;
            var outcome=new RebirthNpcWorkOutcomeRecord{OutcomeId=operation,AssignmentId=assignment,NpcId=npc,DefinitionId=DefinitionId,
                TargetId="provider:"+S((int)row.Attribute("dX"))+":"+S((int)row.Attribute("dY"))+":"+S((int)row.Attribute("dZ")),
                DurableCompletion=true,DurableWitness=Witness(row),StartedUtcTicks=(long?)row.Attribute("startedTicks")??0L,CompletedUtcTicks=ticks,
                State=RebirthNpcWorkOutcomeState.Completed,FailureCategory=RebirthNpcWorkFailureCategory.None,
                Produced=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"waterUnitsMoved",Exchange}},Consumed=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)};
            if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome))return false;
            // One completed refill is a one-shot discrete completion with no truthful duration; retry only on Unavailable.
            var metric=RebirthP1SharedWorkAward.SupplyOwnerDiscrete(outcome);
            if(metric!=RebirthP1AwardQueueResult.Acknowledged&&metric!=RebirthP1AwardQueueResult.Journaled)return false;
            float raw=RebirthPhase8WorldOutputTrainingService.CalculateFarmingHarvestRaw(1)*WaterAwardFactor;
            bool awarded=raw<=0f||RebirthHumanProgression.Award(npc,new RebirthSkillTrainingEvidence{
                SkillId="skill.farming",SourceKey="p8:farming:npc:provider-refill",
                DurableReceiptId="npc:farming-water-original:"+operation.ToString("N"),
                ReferenceDescription="saved source debit, saved provider refill and native NPC bucket conversions",
                AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw});
            if(!awarded)return false;
            // Recovery stops revisiting this row once outcome and award are both committed.
            var settled=new XElement(row);settled.SetAttributeValue("settled","1");
            return Replace(npc,row,settled);
        }
        catch{return false;}
    }

    // ---- assignment --------------------------------------------------------------------
    internal static RebirthNpcWorkAssignmentResult Assign(string session,string savedFarmTarget,Vector3i provider,
        out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="An accessible Rebirth water tank or dew collector inside the saved Farm target is required.";
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||context==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var request=new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,TargetKey=savedFarmTarget,
            HasTargetPosition=true,TargetPosition=provider.ToVector3(),ExpectedRuntimeRevision=context.RuntimeRevision};
        var probe=new RebirthNpcWorkAssignment{DefinitionId=DefinitionId,TargetKey=savedFarmTarget,HasTargetPosition=true,
            TargetPosition=provider.ToVector3(),ActorId=context.ActorId,NpcId=context.NpcId};
        var world=GameManager.Instance?.World;
        if(world==null||!Authorized(probe,out _))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var need=EvaluateProvider(world,provider,RebirthP8NpcWaterPolicy.GetPercent(context.NpcId),out int count,out int max,out int threshold);
        if(need!=Need.Needed)
        {
            detail=need==Need.AboveThreshold?"Provider is at or above the minimum level ("+S(count)+"/"+S(threshold)+").":
                need==Need.NoRoomForBucket?"Provider cannot accept a full bucket ("+S(count)+"/"+S(max)+").":"Target is not a Rebirth water tank or dew collector.";
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        }
        return RebirthNpcWorkAssignmentService.Create(request,out assignment,out detail);
    }

    // Read-only readiness used by the coordinator gate (request: see doc).
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        if(!Authorized(a,out _)||npc.bag?.ItemGrid==null||npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy)return false;
        var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        if(TryReadSaved(a.NpcId,operation,out var retained))return (string)retained.Attribute("startedTicks")==a.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture);
        // A kept filled bucket needs neither a spare bucket nor a source: the assignment only resumes it.
        if(TryReadAny(a.NpcId,out var kept)&&(string)kept.Attribute("phase")=="filled")
            return ((string)kept.Attribute("held")=="1"||OwnerGone(kept))&&NeedsRefill(a)&&RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a);
        var world=GameManager.Instance.World;var owner=Owner(world,a.ActorId);int percent=RebirthP8NpcWaterPolicy.GetPercent(a.NpcId);
        if(owner==null||!NeedsRefill(a)||!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a)||!TryFindAnySource(world,owner,npc,Position(a),percent))return false;
        string empty=RebirthHumanEquipment.Frame(new ItemStack(ItemClass.GetItem(EmptyBucket),1));
        for(int i=0;i<npc.bag.SlotCount;i++)if(RebirthHumanEquipment.Frame(npc.bag.ItemGrid[i])==empty&&!BucketLocked(npc,i))return true;
        return false;
    }
}

public sealed class RebirthP8NpcWaterRefillTargetAdapter:IRebirthNpcWorkTargetAdapter
{
    public string AdapterId=>"rebirth.target.farm-provider-refill";
    public int Priority=>400;
    public bool CanResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d)=>RebirthP8NpcWaterRefillWork.Allows(a);
    // The travel target follows the saved leg: source until the bucket is filled, then provider.
    private static Vector3 LegPosition(RebirthNpcWorkAssignment a)
    {
        var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        if(RebirthP8NpcWaterRefillWork.TryReadSaved(a.NpcId,operation,out var row)&&RebirthP8NpcWaterRefillWork.TryLegPosition(row,out var p))return RebirthP8NpcWaterRefillWork.Centre(p);
        return a.TargetPosition;
    }
    public bool TryResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d,out RebirthNpcWorkTargetSnapshot target,out string detail)
    {
        target=null;detail="Saved Farm and provider position are unavailable.";
        if(!RebirthP8NpcWaterRefillWork.Authorized(a,out long revision))return false;
        target=new RebirthNpcWorkTargetSnapshot{AdapterId=AdapterId,StableTargetId=RebirthP8NpcWaterRefillWork.TargetId(a),
            TargetRevision=revision.ToString(CultureInfo.InvariantCulture),Position=LegPosition(a),IsLoaded=true,IsAvailable=true,
            InteractionRange=RebirthNpcWorkRuntime.WorkRadius,Detail="Water source or provider for the current refill leg."};
        detail=target.Detail;return true;
    }
    public bool Validate(RebirthNpcConcreteWorkContext c,out RebirthNpcWorkFailureCategory failure,out string detail)
    {
        failure=RebirthNpcWorkFailureCategory.AuthorizationDenied;detail="Farm target permission or saved revision changed.";
        if(c?.Assignment==null||c.Target==null||!RebirthP8NpcWaterRefillWork.Authorized(c.Assignment,out long revision)||
           c.Target.StableTargetId!=RebirthP8NpcWaterRefillWork.TargetId(c.Assignment)||
           c.Target.TargetRevision!=revision.ToString(CultureInfo.InvariantCulture))
        {
            // The executor validates before TickCore, so a filled bucket is marked held here; the coordinator
            // can then release this assignment and the next one resumes the bucket.
            if(c?.Assignment!=null)RebirthP8NpcWaterRefillWork.MarkHeld(c.Assignment.NpcId,c.OperationId);
            return false;
        }
        failure=RebirthNpcWorkFailureCategory.None;detail="Saved Farm and provider remain authorized.";return true;
    }
    public bool IsNpcInRange(RebirthNpcConcreteWorkContext c,out string detail)
    {
        detail="Worker must reach the current water leg.";if(!RebirthNpcWorkNavigationService.TryResolveNpc(c.Assignment.NpcId,out var npc))return false;
        bool current=(npc.position-LegPosition(c.Assignment)).sqrMagnitude<=RebirthNpcWorkRuntime.WorkRadiusSquared;
        if(current)detail="Worker reached the current water leg.";return current;
    }
}

public sealed class RebirthP8NpcWaterRefillWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.farming.physical-provider-refill";
    public int Priority=>815;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthP8NpcWaterRefillWork.Allows(c?.Assignment);
    private static RebirthNpcWorkMutationResult Hold(string detail,RebirthNpcWorkFailureCategory category=RebirthNpcWorkFailureCategory.MissingInput)=>
        new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=category,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>CanHandle(c)?
        RebirthNpcWorkMutationResult.Continue(0f,"Provider refill accepted; awaiting physical arrival at the water source."):Hold("Saved provider-refill assignment is unavailable.");
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)
    {
        var result=TickCore(c);
        // Anything other than "keep going" must not leave native publication observations pinned.
        // Continue and Suspend keep their attempts (a pending publication must survive to the next tick).
        if((result.Disposition==RebirthNpcWorkExecutionDisposition.Completed||result.Disposition==RebirthNpcWorkExecutionDisposition.Failed)&&c?.Assignment!=null)
            RebirthP8NpcWaterRefillWork.Release(c.Assignment.NpcId,c.OperationId);
        return result;
    }
    private RebirthNpcWorkMutationResult TickCore(RebirthNpcConcreteWorkContext c)
    {
        if(CanHandle(c)&&!RebirthP8NpcWaterRefillWork.Authorized(c.Assignment,out _)&&
           RebirthP8NpcWaterRefillWork.TryReadSaved(c.Assignment.NpcId,c.OperationId,out var retained)&&(string)retained.Attribute("phase")=="filled")
            return !RebirthP8NpcWaterRefillWork.MarkHeld(c.Assignment.NpcId,c.OperationId)?Hold("Retained filled bucket is settling.",RebirthNpcWorkFailureCategory.PersistenceRecovery):
                new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,
                FailureCategory=RebirthNpcWorkFailureCategory.AuthorizationDenied,
                Detail="Provider is no longer authorized; the filled bucket is kept for the next refill assignment."};
        if(!CanHandle(c)||RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
           !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthP8NpcWaterRefillWork.Allows(intent)||
           intent.Actor is not EntityRebirthHumanoidNPC npc||npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy||
           !RebirthP8NpcWaterRefillWork.Authorized(c.Assignment,out _))return Hold("Embodied dispatch, Farm permission or worker state is unavailable.",RebirthNpcWorkFailureCategory.AuthorizationDenied);
        if(!RebirthP8NpcWaterRefillWork.TryReadSaved(c.Assignment.NpcId,c.OperationId,out var row))
            return RebirthP8NpcWaterRefillWork.TryPrepare(c,npc,out string prepared,out var category)?
                RebirthNpcWorkMutationResult.Continue(0f,prepared):
                category==RebirthNpcWorkFailureCategory.None?RebirthNpcWorkMutationResult.Continue(0f,prepared):Hold(prepared,category);
        if((string)row.Attribute("definition")!=RebirthP8NpcWaterRefillWork.DefinitionId||
           (string)row.Attribute("assignment")!=c.Assignment.AssignmentId.ToString(CultureInfo.InvariantCulture)||
           (long?)row.Attribute("startedTicks")!=c.Assignment.CreatedUtcTicks||(string)row.Attribute("target")!=c.Assignment.TargetKey||
           (uint?)row.Attribute("embodiment")!=intent.Generation)
            return Hold("Saved water original belongs to another assignment or embodiment.",RebirthNpcWorkFailureCategory.PersistenceRecovery);
        string phase=(string)row.Attribute("phase");
        if(phase=="completed"&&RebirthP8NpcWaterRefillWork.TryComplete(c.Assignment.NpcId,c.OperationId))
            return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                Detail="Provider refilled with one real bucket; source debit, provider credit and bucket conversions are saved."};
        if(!RebirthP8NpcWaterRefillWork.TryLegPosition(row,out var leg))return Hold("Saved water leg is unavailable.",RebirthNpcWorkFailureCategory.PersistenceRecovery);
        if((npc.position-RebirthP8NpcWaterRefillWork.Centre(leg)).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)
            return RebirthNpcWorkMutationResult.Continue(0f,"Travelling to the "+(leg==new Vector3i((int)row.Attribute("dX"),(int)row.Attribute("dY"),(int)row.Attribute("dZ"))?"provider.":"water source."));
        if(!RebirthP8NpcWaterRefillWork.Advance(npc,c.OperationId,true,out string detail,out var failure))
            // A provider that became full, unusable or foreign after the bucket was filled ends this assignment
            // terminally with the filled bucket kept in the bag; the next refill assignment resumes it.
            return failure==RebirthNpcWorkFailureCategory.None?RebirthNpcWorkMutationResult.Continue(0f,detail):
                failure==RebirthNpcWorkFailureCategory.TargetChanged||failure==RebirthNpcWorkFailureCategory.OutputBlocked?
                    !RebirthP8NpcWaterRefillWork.MarkHeld(c.Assignment.NpcId,c.OperationId)?Hold("Retained filled bucket is settling.",RebirthNpcWorkFailureCategory.PersistenceRecovery):
                    new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=failure,Detail=detail}:
                    Hold(detail,failure);
        if(!RebirthP8NpcWaterRefillWork.TryReadSaved(c.Assignment.NpcId,c.OperationId,out row))return Hold("Saved water original is settling.",RebirthNpcWorkFailureCategory.PersistenceRecovery);
        if((string)row.Attribute("phase")=="completed")
            return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                Detail="Provider refilled with one real bucket; source debit, provider credit and bucket conversions are saved."};
        return RebirthNpcWorkMutationResult.Continue(0f,"Water refill phase "+(string)row.Attribute("phase")+" saved.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason)
    {
        if(c?.Assignment!=null)RebirthP8NpcWaterRefillWork.Release(c.Assignment.NpcId,c.OperationId);
    }
}
