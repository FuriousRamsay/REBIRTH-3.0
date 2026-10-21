using System;
using System.Linq;
using Newtonsoft.Json.Linq;

// Explicit read-only native endpoint evidence. No admission, checkpoint, save,
// courier, owner grant or terminal producer is entered by these readers.
internal static class RebirthHydrationAudit
{
    internal static object Capture(EntityPlayer actor,Guid operation,string digest)
    {
        var result=new JObject{["schema"]=1,["available"]=false};
        if(!ThreadManager.IsMainThread()||actor?.world==null||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||!actor.IsSpawned())return result;
        var world=actor.world;int deaths=actor.Died;string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        bool remote=world.IsRemote();var local=actor as EntityPlayerLocal;
        RebirthHydrationTransferOriginal original;bool closed=false,financial=false,completed=false;
        if(remote)
        {
            if(local==null||!ReferenceEquals(world.GetPrimaryPlayer(),local)||
                !RebirthHydrationClientOriginals.TryReadAudit(local,operation,digest,out original,out closed))return result;
            var operations=RebirthHydrationClientOriginals.DiscoverAudit(local);if(operations==null)return result;result["operations"]=operations;
        }
        else if(!RebirthHydrationTransferJournal.TryReadAudit(actor,operation,digest,out original,out financial,out completed))return result;
        else{var operations=RebirthHydrationTransferJournal.DiscoverAudit(actor);if(operations==null)return result;result["operations"]=operations;}
        result["remote"]=remote;result["survivorMode"]=RebirthSurvivorMode.IsEnabledForCurrentWorld();
        result["entity"]=actor.entityId;result["deaths"]=deaths;result["creation"]=creation;
        result["ownerHeld"]=local!=null&&RebirthHydrationOwnerHold.IsHeld(local);
        result["sourceHeld"]=!remote&&RebirthHydrationServerSourceHold.HoldsPlayer(actor);
        if(!remote)result["source"]=RebirthMetabolismStateRepository.CaptureHydrationAuditSource(actor);
        if(local!=null&&ReferenceEquals(world.GetPrimaryPlayer(),local))
        {
            var bag=local.bag?.ItemGrid?.items;var belt=local.inventory?.ItemGrid?.items;
            if(bag==null||bag.Length>169||belt==null||belt.Length>20)return result;
            byte[] bagImage=RebirthCamperInventorySnapshot.Capture(bag),beltImage=RebirthCamperInventorySnapshot.Capture(belt);
            if(bagImage.Length>4*1024*1024||beltImage.Length>4*1024*1024)return result;
            result["nativeBag"]=Convert.ToBase64String(bagImage);result["nativeBelt"]=Convert.ToBase64String(beltImage);
        }
        result["outboxClosed"]=closed;result["financialApplied"]=financial;result["completionSaved"]=completed;
        if(original!=null)
        {
            result["operation"]=original.Operation.ToString("N");result["digest"]=original.Digest;
            result["original"]=original.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            result["equip"]=original.Equip;
            if(local!=null&&ReferenceEquals(world.GetPrimaryPlayer(),local)&&original.TryGetPlan(out var plan)&&
                RebirthGearInventorySnapshot.TryCapture(local.bag.ItemGrid.items,local.inventory.ItemGrid.items,original.OwnedBelt,out var snapshot))
            {
                result["liveOwnerBefore"]=plan.MatchesBefore(snapshot.Bag,snapshot.Belt,plan.GearBefore,snapshot.Bag.Length,snapshot.Belt.Length);
                result["liveOwnerAfter"]=plan.MatchesAppliedInventory(snapshot.Bag,snapshot.Belt);
            }
            RebirthStablePlayerIdentity identity;
            bool identified=remote?RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity):RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out identity);
            if(identified&&identity.StorageKey==original.Owner&&RebirthGearNativePlayerFile.TryRead(identity,out var file)&&file.deaths==original.Deaths&&
                file.buffData!=null&&file.buffData.Length>0&&file.buffData.Length<=1024*1024)
            {
                var bytes=file.buffData.ToArray();
                if(RebirthGearReceiptReader.TryReadHydrationStage(bytes,original.Operation,original.Digest,out var stage)&&
                    RebirthGearReceiptReader.TryReadHydrationSeenStage(bytes,original.Operation,original.Digest,out var seen)&&
                    RebirthGearReceiptReader.TryReadHydrationRefusedStage(bytes,original.Operation,original.Digest,out var refused))
                {
                    result["nativeReadable"]=true;result["nativeStage"]=stage;result["nativeSeen"]=seen;result["nativeRefused"]=refused;
                    result["nativeApplied"]=RebirthGearReceiptReader.Contains(bytes,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applied);
                }
            }
        }
        if(!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(actor.world,world)||
            !ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.Died!=deaths||actor.IsDead()||!actor.IsSpawned()||
            RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation)return new JObject{["schema"]=1,["available"]=false};
        result["available"]=true;return result;
    }
}

public static partial class RebirthMetabolismStateRepository
{
    internal static JObject CaptureHydrationAuditSource(EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread())return null;
        lock(Sync)
        {
            if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,player.world))return null;
            string id=GetStablePlayerId(player);
            if(string.IsNullOrEmpty(id)||!States.TryGetValue(id,out var state)||state==null)return null;
            var item=state.HydrationSlotItem;
            return new JObject{["revision"]=state.Revision,["count"]=item==null||item.IsEmpty()?0:item.count,
                ["item"]=item==null||item.IsEmpty()?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue),
                ["receipt"]=state.HydrationTransferReceipt?.Digest};
        }
    }
}

internal static partial class RebirthHydrationClientOriginals
{
    internal static JArray DiscoverAudit(EntityPlayerLocal player)
    {
        try
        {
            if(!Scope(player,out var world,out var owner,out var creation,out var path))return null;
            var session=new Session(player,world,owner,creation,path);var result=new JArray();
            if(System.IO.Directory.Exists(path))foreach(var record in new RebirthCamperTransferJournal(path).ReadForIndex())
            {
                if(result.Count>=64)return null;var original=Decode(record.Before);
                if(!Matches(record,original)||original.World!=world||original.Owner!=owner)return null;
                result.Add(new JObject{["operation"]=original.Operation.ToString("N"),["digest"]=original.Digest});
            }
            return session.Current()?result:null;
        }
        catch{return null;}
    }
    internal static bool TryReadAudit(EntityPlayerLocal player,Guid operation,string digest,out RebirthHydrationTransferOriginal original,out bool closed)
    {
        original=null;closed=false;
        try
        {
            if(operation==Guid.Empty)return TryRead(player,out original);
            if(!Scope(player,out var world,out var owner,out var creation,out var path)||!System.IO.Directory.Exists(path))return false;
            var session=new Session(player,world,owner,creation,path);
            var record=new RebirthCamperTransferJournal(path).Read(operation);if(record==null)return session.Current();
            var found=Decode(record.Before);
            if(!Matches(record,found)||found.World!=world||found.Owner!=owner||found.Digest!=digest)return false;
            original=found;closed=record.State==RebirthCamperTransferJournal.Phase.Applied;return session.Current();
        }
        catch{original=null;closed=false;return false;}
    }
}

internal static partial class RebirthHydrationTransferJournal
{
    internal static JArray DiscoverAudit(EntityPlayer player)
    {
        try
        {
            if(!Owner(player,out var world,out var owner,out var creation))return null;
            string path=DirectoryPath(world,owner,creation);RequireSafe(path);var result=new JArray();
            if(System.IO.Directory.Exists(path))foreach(var record in new RebirthCamperTransferJournal(path).ReadForIndex())
            {
                if(result.Count>=64)return null;var original=Decode(record.Before);
                if(!Matches(record,original)||original.World!=world||original.Owner!=owner||original.Creation!=creation)return null;
                result.Add(new JObject{["operation"]=original.Operation.ToString("N"),["digest"]=original.Digest});
            }
            return result;
        }
        catch{return null;}
    }
    internal static bool TryReadAudit(EntityPlayer player,Guid operation,string digest,out RebirthHydrationTransferOriginal original,out bool applied,out bool completed)
    {
        original=null;applied=completed=false;
        try
        {
            if(operation==Guid.Empty)
            {
                if(!TryReadPending(player,out original,out _))return false;
                if(original==null)return true;
            }
            else
            {
                if(!Owner(player,out var world,out var owner,out var creation))return false;
                string path=DirectoryPath(world,owner,creation);RequireSafe(path);
                if(!System.IO.Directory.Exists(path))return true;
                var record=new RebirthCamperTransferJournal(path).Read(operation);if(record==null)return true;
                var found=Decode(record.Before);
                if(!Matches(record,found)||found.World!=world||found.Owner!=owner||found.Creation!=creation||found.Digest!=digest)return false;
                original=found;
            }
            applied=HasApplied(player,original);completed=HasSavedCompletion(original);return true;
        }
        catch{original=null;applied=completed=false;return false;}
    }
}
