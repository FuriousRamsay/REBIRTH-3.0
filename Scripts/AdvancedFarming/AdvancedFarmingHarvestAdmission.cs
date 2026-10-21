using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable disable

// Bounded farming-only server admission. A transport caller must authenticate
// its native sender before invoking this; owner strings are never an identity.
internal static class AdvancedFarmingHarvestAdmission
{
    private const int MaximumReceipts=100000;
    private static World indexedWorld;private static string indexedDirectory;
    private static int indexedCount;
    private static Dictionary<Guid,Guid> reservedSources;private static bool quarantined;
    private static readonly Dictionary<Guid,string> pendingOriginals=new Dictionary<Guid,string>();
    private static bool SameOriginal(AdvancedFarmingHarvestReceipt.Record a,AdvancedFarmingHarvestReceipt.Record b)=>
        a.Operation==b.Operation&&a.Source==b.Source&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.Deaths==b.Deaths&&
        a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.Raw==b.Raw&&a.Replant==b.Replant&&a.PlanImage==b.PlanImage&&a.Outputs==b.Outputs&&a.Terminal==b.Terminal&&
        a.EventsRequired==b.EventsRequired&&a.DropX==b.DropX&&a.DropY==b.DropY&&a.DropZ==b.DropZ;
    private static bool Reserve(AdvancedFarmingHarvestReceipt.Record receipt)
    {
        var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(receipt.Terminal);
        foreach(var source in terminal==null?new[]{receipt.Source}:new[]{receipt.Source,terminal.Source})
            if(reservedSources.TryGetValue(source,out var existing)&&existing!=receipt.Operation)return false;
        reservedSources[receipt.Source]=receipt.Operation;if(terminal!=null)reservedSources[terminal.Source]=receipt.Operation;return true;
    }
    private static bool PublishReservation(AdvancedFarmingHarvestReceipt.Record receipt)
    {
        bool published=AdvancedFarmingHarvestReceipt.TryReserve(receipt);
        bool exact=AdvancedFarmingHarvestReceipt.TryRead(receipt.World,receipt.Operation,out var actual)&&AdvancedFarmingHarvestReceipt.Encode(actual)==AdvancedFarmingHarvestReceipt.Encode(receipt);
        if(exact)AdvancedFarmingHarvestProtocol.ObservePreparedOriginal(receipt.Operation);
        return published&&exact;
    }
    private static string ClaimPath(AdvancedFarmingHarvestReceipt.Record r)=>Path.Combine(indexedDirectory,r.Operation.ToString("N")+".claim");
    private static bool ExactClaim(string path,AdvancedFarmingHarvestReceipt.Record r)
    {try{return new FileInfo(path).Length<=48L*1024*1024&&AdvancedFarmingHarvestReceipt.Encode(AdvancedFarmingHarvestReceipt.Decode(File.ReadAllText(path,new UTF8Encoding(false,true))))==AdvancedFarmingHarvestReceipt.Encode(r);}catch{return false;}}
    private static bool PublishClaim(AdvancedFarmingHarvestReceipt.Record r)
    {
        string path=ClaimPath(r),temporary=path+".tmp";
        try
        {
            if(File.Exists(path))return ExactClaim(path,r);
            Directory.CreateDirectory(indexedDirectory);
            if(!File.Exists(temporary)){byte[] image=new UTF8Encoding(false,true).GetBytes(AdvancedFarmingHarvestReceipt.Encode(r));using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(image,0,image.Length);output.Flush(true);}}
            else{if(!ExactClaim(temporary,r))return false;using(var output=new FileStream(temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.None))output.Flush(true);}
            return ExactClaim(temporary,r)&&RebirthDurableFileCommit.TryPublish(temporary,path,out _)&&ExactClaim(path,r);
        }
        catch{return false;}
    }
    private static void ArchiveClaim(AdvancedFarmingHarvestReceipt.Record r)
    {try{string path=ClaimPath(r);if(File.Exists(path)&&new FileInfo(path).Length<=48L*1024*1024&&SameOriginal(AdvancedFarmingHarvestReceipt.Decode(File.ReadAllText(path,new UTF8Encoding(false,true))),r)){string destination=Path.Combine(indexedDirectory,"archive",r.Operation.ToString("N")+".claim.done");Directory.CreateDirectory(Path.GetDirectoryName(destination));if(!File.Exists(destination))File.Move(path,destination);}}catch{/* Hold-only history remains on failure; it is not permission. */}}
    private static bool Index(World world)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-harvest","v1");
        if(ReferenceEquals(indexedWorld,world)&&indexedDirectory==directory&&reservedSources!=null)return !quarantined;
        indexedWorld=world;indexedDirectory=directory;reservedSources=new Dictionary<Guid,Guid>();pendingOriginals.Clear();quarantined=false;indexedCount=0;
        try
        {
            if(!Directory.Exists(directory))return true;int count=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.xml"))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var operation)||
                    !AdvancedFarmingHarvestReceipt.TryRead(world.worldState.Guid,operation,out var receipt)){quarantined=true;break;}
                if(receipt.State==AdvancedFarmingHarvestReceipt.Phase.Retired){AdvancedFarmingHarvestReceipt.ArchiveRetired(receipt);continue;}
                if(++count>MaximumReceipts){quarantined=true;break;}
                if(reservedSources.TryGetValue(receipt.Source,out var existing)&&existing!=operation){quarantined=true;break;}
                reservedSources[receipt.Source]=operation;
                var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(receipt.Terminal);if(terminal!=null){if(reservedSources.TryGetValue(terminal.Source,out var other)&&other!=operation){quarantined=true;break;}reservedSources[terminal.Source]=operation;}
            }
            // Uncertain publication is an exclusive hold. A staged or backup
            // record never certifies native source/owner effects, but cannot be
            // discarded when rebuilding the first-winner index after reload.
            int artifacts=0;
            foreach(string path in Directory.EnumerateFiles(directory).Where(path=>path.EndsWith(".claim",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)))
            {
                if(new FileInfo(path).Length>48L*1024*1024){quarantined=true;break;}
                var staged=AdvancedFarmingHarvestReceipt.Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));
                if(staged.World!=world.worldState.Guid){quarantined=true;break;}
                if(AdvancedFarmingHarvestReceipt.TryRead(staged.World,staged.Operation,out var primary))
                {if(!SameOriginal(primary,staged)){quarantined=true;break;}if(primary.State==AdvancedFarmingHarvestReceipt.Phase.Retired){ArchiveClaim(primary);continue;}}
                if(++artifacts>MaximumReceipts){quarantined=true;break;}
                if(!Reserve(staged)){quarantined=true;break;}
                if(staged.State==AdvancedFarmingHarvestReceipt.Phase.Reserved)
                {
                    if(pendingOriginals.TryGetValue(staged.Operation,out var prior)&&!ExactClaim(prior,staged)){quarantined=true;break;}
                    pendingOriginals[staged.Operation]=path;
                }
            }
            indexedCount=reservedSources.Values.Distinct().Count();if(indexedCount>MaximumReceipts)quarantined=true;
        }
        catch(Exception error){quarantined=true;Log.Warning("[REBIRTH Farming] original harvest reservations quarantined: "+error.GetType().Name);}
        return !quarantined;
    }
    private static bool SameItem(ItemValue expected,ItemStack actual)
    {return expected!=null&&actual!=null&&!actual.IsEmpty()&&RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(expected,1),new ItemStack(actual.itemValue,1));}
    internal static bool ValidOriginalYield(World world,EntityPlayer owner,BlockValue source,ItemStack[] outputs,bool replant)
    {
        if(world==null||owner==null||outputs==null||outputs.Length>1||source.Block?.itemsToDrop==null)return false;
        if(!source.Block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest,out var drops)||drops==null)return false;
        bool grown=RebirthUtilities.IsPlayerGrownHarvestCrop(source);
        if(replant&&!grown)return false;
        foreach(var drop in drops)
        {
            if(string.IsNullOrEmpty(drop.name))continue;
            if(grown&&(drop.name.IndexOf("planted",StringComparison.OrdinalIgnoreCase)>=0||(!string.IsNullOrEmpty(drop.tag)&&drop.tag.IndexOf("Seed",StringComparison.OrdinalIgnoreCase)>=0)))continue;
            if(!grown&&drop.prob<=0f)continue;
            var item=drop.name=="*"?source.ToItemValue():ItemClass.GetItem(drop.name);if(item.IsEmpty())continue;
            if(grown)
            {
                // Preserve original architecture: remote owner rolls base1 because
                // RollAdvancedFarmingHarvestCount exits on world.IsRemote(). A host
                // owner's original stochastic result is0..3, never rerolled here.
                if(owner is not EntityPlayerLocal)return outputs.Length==1&&outputs[0].count==1&&SameItem(item,outputs[0]);
                return outputs.Length==0||outputs[0].count>=1&&outputs[0].count<=3&&SameItem(item,outputs[0]);
            }
            // Native owner effects/RNG remain owner-produced. Validate an authored
            // eligible native item and a bounded count; do not substitute stale
            // server-side passive effects or perform a second probability roll.
            if(outputs.Length==1&&outputs[0].count>=1&&outputs[0].count<=ushort.MaxValue&&SameItem(item,outputs[0]))return true;
        }
        return false;
    }
    internal static bool TryAccept(EntityPlayer authenticatedOwner,AdvancedFarmingHarvestReceipt.Record receipt,BlockValue expected)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||authenticatedOwner==null||receipt==null||
            !ReferenceEquals(authenticatedOwner.world,world)||!ReferenceEquals(world.GetEntity(authenticatedOwner.entityId),authenticatedOwner)||
            authenticatedOwner.IsDead()||authenticatedOwner.Died!=receipt.Deaths||world.worldState?.Guid!=receipt.World||
            receipt.State!=AdvancedFarmingHarvestReceipt.Phase.Reserved||!RebirthStablePlayerIdentity.TryResolveServerEntity(authenticatedOwner,out var identity)||identity.CanonicalId!=receipt.Owner||
            RebirthCamperCharacterAccess.CurrentCreation(authenticatedOwner)!=receipt.Creation||!Index(world))return false;
        var position=new Vector3i(receipt.X,receipt.Y,receipt.Z);
        if((authenticatedOwner.GetPosition()-position.ToVector3()).sqrMagnitude>64f||world.IsWithinTraderArea(position)||world.GetBlock(position).rawData!=expected.rawData||
            expected.rawData!=receipt.Raw||expected.type!=receipt.Block||expected.meta!=receipt.Meta||expected.meta2!=receipt.Meta2||expected.rotation!=receipt.Rotation||
            !AdvancedFarmingHarvestSettlement.MatchesSource(world,position,receipt.Source.ToString("N")))return false;
        try
        {
            var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(receipt.Terminal);
            if(terminal!=null){var plot=new Vector3i(receipt.X,receipt.Y-1,receipt.Z);if(world.GetBlock(plot).rawData!=terminal.Raw||world.IsWithinTraderArea(plot)||!AdvancedFarmingHarvestSettlement.MatchesSource(world,plot,terminal.Source.ToString("N"))||reservedSources.TryGetValue(terminal.Source,out var plotOperation)&&plotOperation!=receipt.Operation)return false;}
            if(reservedSources.TryGetValue(receipt.Source,out var operation))
            {
                if(operation!=receipt.Operation)return false;
                if(AdvancedFarmingHarvestReceipt.TryRead(receipt.World,operation,out var existing))return AdvancedFarmingHarvestReceipt.Encode(existing)==AdvancedFarmingHarvestReceipt.Encode(receipt);
                // Pure publication retry of the exact retained prepared original;
                // no stage or missing primary grants a native mutation.
                if(!pendingOriginals.TryGetValue(operation,out var pending)||!(ExactClaim(pending,receipt)||pending==ClaimPath(receipt)&&!File.Exists(pending)&&ExactClaim(pending+".tmp",receipt))||
                    !AdvancedFarmingHarvestOwnerDelta.SavedUnspent(identity,AdvancedFarmingHarvestOwnerDelta.Decode(receipt.PlanImage))||!PublishClaim(receipt)||!PublishReservation(receipt))return false;
                pendingOriginals.Remove(operation);return true;
            }
            if(indexedCount>=MaximumReceipts||!AdvancedFarmingHarvestTerminalPlot.ValidYield(world,authenticatedOwner,receipt,AdvancedFarmingHarvestReceipt.ReadOutputs(receipt.Outputs))||
                !AdvancedFarmingHarvestOwnerDelta.SavedUnspent(identity,AdvancedFarmingHarvestOwnerDelta.Decode(receipt.PlanImage))||!AdvancedFarmingHarvestEvents.PrepareStatistics(authenticatedOwner,receipt))return false;
            if(!Reserve(receipt))return false;pendingOriginals[receipt.Operation]=ClaimPath(receipt);indexedCount++;
            if(!PublishClaim(receipt)||!PublishReservation(receipt))return false;pendingOriginals.Remove(receipt.Operation);return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest admission retained: "+error.GetType().Name);return false;}
    }
    internal static void Retired(AdvancedFarmingHarvestReceipt.Record receipt)
    {if(receipt?.State!=AdvancedFarmingHarvestReceipt.Phase.Retired)return;AdvancedFarmingHarvestReceipt.ArchiveRetired(receipt);ArchiveClaim(receipt);pendingOriginals.Remove(receipt.Operation);if(reservedSources==null)return;foreach(var key in new System.Collections.Generic.List<Guid>(reservedSources.Keys))if(reservedSources[key]==receipt.Operation)reservedSources.Remove(key);indexedCount=System.Linq.Enumerable.Count(System.Linq.Enumerable.Distinct(reservedSources.Values));}
    internal static bool PositivelyClaimedByOther(World world,AdvancedFarmingHarvestReceipt.Record receipt)
    {
        if(world==null||receipt==null||world.IsRemote()||!Index(world))return false;
        var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(receipt.Terminal);
        foreach(var source in terminal==null?new[]{receipt.Source}:new[]{receipt.Source,terminal.Source})
            if(reservedSources.TryGetValue(source,out var operation)&&operation!=receipt.Operation&&
                AdvancedFarmingHarvestReceipt.TryRead(receipt.World,operation,out var winner)&&winner.State!=AdvancedFarmingHarvestReceipt.Phase.Retired&&
                (winner.Source==source||AdvancedFarmingHarvestTerminalPlot.Decode(winner.Terminal)?.Source==source))return true;
        return false;
    }
    internal static Guid[] Pending(World world)
    {if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!Index(world))return Array.Empty<Guid>();var result=new Guid[reservedSources.Count];reservedSources.Values.CopyTo(result,0);return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(result));}
}
