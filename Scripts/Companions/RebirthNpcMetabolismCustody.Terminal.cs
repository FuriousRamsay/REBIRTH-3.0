using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private static bool CompleteWarmTerminalMetadata(Original original)
    {
        // Already-positive financial history permits metadata only. Later NPC
        // movement, ownership or source inventory changes are unrelated to it.
        var actor=original.Actor;var world=GameManager.Instance?.World;
        if(original.Terminal||original.ObservationRetired||actor==null||world==null||!ReferenceEquals(original.World,world)||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
            original.Save!=Path.GetFullPath(GameIO.GetSaveGameDir())||original.Recovery!=RebirthNpcRecoveryGeneration.RequireCurrent()||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var account)||account.CanonicalId!=original.Account||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return false;
        using(memory)
        {
            var image=ParseOriginal(original.Text);if(!IsPositiveFinancialOutcome(original.Directory,original.Text,image))return false;
            ReadOriginalBody(image.Element("nativeBody"),original.Id);
            string saved=(string)ReadRecord(Path.Combine(original.Directory,"source-saved.xml")).Element("saved");
            if(!RebirthNpcInventoryPersistenceStore.ValidateHistoricalConsumptionSource(saved,original.Id,original.Before.Revision+1,original.Operation,original.AfterQuantities,original.Before.Reservations,original.AfterNative))return false;
            string canonical=(string)ReadRecord(Path.Combine(original.Directory,"secondary-state-saved.xml")).Element("saved");
            if(RebirthMetabolismStateRepository.ReadHistoricalNpcServing(original.Metabolism.Id,canonical)==null)return false;
        }
        ObserveScenario(original,"before-terminal-metadata");
        Record(original,"committed");ArchiveCompletedOriginal(original);
        if(actor is EntityPlayerLocal local)
        {local.Buffs.RemoveCustomVar(OwnerReceipt(original.Operation));local.Buffs.RemoveCustomVar(SecondaryReceipt(original.Operation));local.Buffs.RemoveCustomVar(BeforeReceipt(original.Operation));GameManager.Instance.SaveLocalPlayerData();}
        else
        {
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
            if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.CanonicalId!=original.Account)return false;
            peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwner>().Setup(actor.entityId,2,Purpose(original)));
        }
        original.Terminal=true;pending.Remove(original.Id);ReleaseOwner(original);return true;
    }
    private static bool IsPositiveTerminal(string directory,string text,XElement image)
    {
        return File.Exists(Path.Combine(directory,"committed.xml"))&&Stage(directory,"committed",text)&&IsPositiveFinancialOutcome(directory,text,image);
    }
    private static bool IsPositiveFinancialOutcome(string directory,string text,XElement image)
    {
        return (int?)image.Attribute("version")==2&&File.Exists(Path.Combine(directory,"owner-after-saved.xml"))&&!File.Exists(Path.Combine(directory,"no-effect.xml"))&&!File.Exists(Path.Combine(directory,"source-not-entered.xml"))&&Stage(directory,"source-saved",text)&&Stage(directory,"ingestion-saved",text)&&
            Stage(directory,"secondary-returned",text)&&Stage(directory,"secondary-state-saved",text)&&Stage(directory,"owner-secondary-returned",text)&&Stage(directory,"owner-effect-returned",text)&&
            HistoricalOwner(directory,"before",text)&&HistoricalOwner(directory,"secondary",text)&&HistoricalOwner(directory,"after",text);
    }
    private static bool SendTerminalMetadata(NpcOwnerLoad loaded,Guid operation,string purpose,byte phase=2)
    {
        if(loaded.Actor is EntityPlayerLocal local)
        {
            if(phase==2){local.Buffs.RemoveCustomVar(OwnerReceipt(operation));local.Buffs.RemoveCustomVar(SecondaryReceipt(operation));}local.Buffs.RemoveCustomVar(BeforeReceipt(operation));GameManager.Instance.SaveLocalPlayerData();return true;
        }
        if(!loaded.Ready.Contains(operation))return false;
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(loaded.Actor.entityId);
        if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account)||account.CanonicalId!=loaded.Account)return false;
        peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwner>().Setup(loaded.Actor.entityId,phase,purpose));return true;
    }
    private static void RemoveNativeOwnerOriginal(NpcOwnerLoad loaded,string directory)
    {
        loaded.Originals.Remove(directory);
        if(loaded.LiveByDirectory.TryGetValue(directory,out string life)){loaded.LiveByDirectory.Remove(directory);if(!loaded.LiveByDirectory.Values.Contains(life))loaded.Lives.Remove(life);}
        if(loaded.Originals.Count==0)ReleaseNativeOwner(loaded);
    }
    private static void ArchiveCompletedOriginal(Original original)
    {
        // Four bounded 16-MiB XML parse/encoding buffers plus two complete
        // 16-MiB native images, independent of the retained original graph.
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))throw new InvalidDataException("Completed original archive scratch unavailable");
        using var retainedScratch=scratch;
        string save=Path.GetFullPath(original.Save),pendingRoot=Path.GetFullPath(Path.Combine(save,"RebirthData","NpcConsumption")),archiveRoot=Path.GetFullPath(Path.Combine(save,"RebirthData","NpcConsumptionTerminal"));
        string source=Path.GetFullPath(original.Directory),destination=Path.Combine(archiveRoot,original.Operation.ToString("N"));RequirePath(source);RequirePath(archiveRoot);
        if(source==destination)return;
        if(Path.GetDirectoryName(source)!=pendingRoot||Path.GetFileName(source)!=original.Operation.ToString("N"))throw new InvalidDataException("Completed original outside its pending namespace");
        var image=ParseOriginal(original.Text);if(!IsPositiveTerminal(source,original.Text,image)&&!PositiveNoEffect(source,original.Text))throw new InvalidDataException("Unsettled consumption cannot be archived");
        Directory.CreateDirectory(archiveRoot);if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("Completed original archive destination already occupied");
        Directory.Move(source,destination);original.Directory=destination;InvalidateColdIndex();
        foreach(var loaded in nativeOwners.Values.ToArray())
            if(loaded.Originals.TryGetValue(source,out string text)&&text==original.Text)
            {
                loaded.Originals.Remove(source);loaded.Originals.Add(destination,text);
                if(loaded.LiveByDirectory.TryGetValue(source,out string life)){loaded.LiveByDirectory.Remove(source);if(!loaded.LiveByDirectory.Values.Contains(life))loaded.Lives.Remove(life);}
            }
        if(!original.Id.IsEmpty&&!HasColdHold(original.Id))
            foreach(var body in nativeBodies.Values.Where(value=>value.Stable==original.Id).ToArray())
            {nativeBodies.Remove(body.Body);body.Memory?.Dispose();body.Memory=null;body.Payload=null;body.Body=null;body.World=null;}
    }
    private static bool TryRetireNoEffectMetadata(NpcOwnerLoad loaded,string directory,string text)
    {
        if(!PositiveNoEffect(directory,text))return false;
        var actor=loaded.Actor;var image=ParseOriginal(text);
        if((int?)image.Attribute("version")!=2||actor==null||actor.IsDead()||!loaded.Hydrated||!loaded.ReadReturned||!ReferenceEquals(loaded.World,GameManager.Instance?.World)||!ReferenceEquals(actor.world,loaded.World)||!ReferenceEquals(loaded.World.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var account)||account.CanonicalId!=loaded.Account||(string)image.Attribute("owner")!=loaded.Account||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            Path.GetFullPath((string)image.Attribute("save"))!=loaded.Save||(ulong?)image.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent()||!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=operation.ToString("N")||
            !RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var id))return true;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,BeforeReceipt(operation),out float marker,out bool present)||!present||marker!=1f||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,OwnerReceipt(operation),out float owner,out bool ownerPresent)||ownerPresent&&owner!=0f||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,SecondaryReceipt(operation),out float secondary,out bool secondaryPresent)||secondaryPresent&&secondary!=0f)return true;
        string full=(string)image.Element("metabolismAfter"),historicalId=(string)ParseOriginal(full).Attribute("id");var state=RebirthMetabolismStateRepository.ReadHistoricalNpcServing(historicalId,full);if(state==null)return true;
        ReadOriginalBody(image.Element("nativeBody"),id);
        var plan=RebirthMetabolismService.HydrateNpcPlan(actor,state,ReadOriginalStack(image.Element("source")),ReadOriginalStack(image.Element("serving")),ReadOriginalStack(image.Element("replacement")),ReadOriginalStack(image.Element("siblings")),image.Element("plan"));if(plan==null)return true;
        var original=new Original{Operation=operation,Id=id,Account=loaded.Account,Creation=(string)image.Attribute("creation"),Deaths=(int)image.Attribute("deaths"),Source=plan.Expected,Plan=plan,Text=text,Directory=directory,Save=loaded.Save};
        if(!SendTerminalMetadata(loaded,operation,Purpose(original),3))return true;
        ArchiveCompletedOriginal(original);RemoveNativeOwnerOriginal(loaded,original.Directory);return true;
    }
    private static bool TryRetireAlreadyCommitted(NpcOwnerLoad loaded,string directory,string text)
    {
        if(TryRetireNoEffectMetadata(loaded,directory,text))return true;
        var image=ParseOriginal(text);if(!IsPositiveFinancialOutcome(directory,text,image))return false;
        var actor=loaded.Actor;if(actor==null||actor.IsDead()||!loaded.Hydrated||!loaded.ReadReturned||!ReferenceEquals(loaded.World,GameManager.Instance?.World)||!ReferenceEquals(actor.world,loaded.World)||!ReferenceEquals(loaded.World.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var account)||account.CanonicalId!=loaded.Account||(string)image.Attribute("owner")!=loaded.Account||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            Path.GetFullPath((string)image.Attribute("save"))!=loaded.Save||(ulong?)image.Attribute("recovery")!=RebirthNpcRecoveryGeneration.RequireCurrent()||!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=operation.ToString("N")||
            !RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var id))return true;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,OwnerReceipt(operation),out float marker,out bool present)||!present||marker!=1f||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(loaded.Buffs,SecondaryReceipt(operation),out float secondary,out bool secondaryPresent)||!secondaryPresent||secondary!=1f)return true;
        var before=ReadOriginalSource(image.Element("sourceBefore")?.Element("inventory"),id,(uint)image.Attribute("revision"));if(before.Revision==uint.MaxValue)return true;
        ReadOriginalBody(image.Element("nativeBody"),id);
        var after=ReadOriginalSource(image.Element("sourceAfter")?.Element("inventory"),id,before.Revision+1);
        string sourceSaved=(string)ReadRecord(Path.Combine(directory,"source-saved.xml")).Element("saved");
        if(!RebirthNpcInventoryPersistenceStore.ValidateHistoricalConsumptionSource(sourceSaved,id,after.Revision,operation,after.Quantities,after.Reservations,after.NativeStacks))return true;
        string canonical=(string)ReadRecord(Path.Combine(directory,"secondary-state-saved.xml")).Element("saved"),historicalId=(string)ParseOriginal((string)image.Element("metabolismAfter")).Attribute("id");
        var state=RebirthMetabolismStateRepository.ReadHistoricalNpcServing(historicalId,canonical);if(state==null)return true;
        var plan=RebirthMetabolismService.HydrateNpcPlan(actor,state,ReadOriginalStack(image.Element("source")),ReadOriginalStack(image.Element("serving")),ReadOriginalStack(image.Element("replacement")),ReadOriginalStack(image.Element("siblings")),image.Element("plan"));if(plan==null)return true;
        // Historical completed source/owner effects authorize metadata only.
        // Later NPC reassignment, movement, player health or unrelated PDS rows
        // neither recreate financial permits nor block this terminal delivery.
        var original=new Original{Operation=operation,Id=id,Account=loaded.Account,Creation=(string)image.Attribute("creation"),Deaths=(int)image.Attribute("deaths"),Source=plan.Expected,Plan=plan,Text=text,Directory=directory,Save=loaded.Save};
        ObserveHistoricalTerminalAdoption(original,loaded);
        Record(original,"committed");
        if(!SendTerminalMetadata(loaded,operation,Purpose(original)))return true;
        ArchiveCompletedOriginal(original);RemoveNativeOwnerOriginal(loaded,original.Directory);return true;
    }
    private static void ObserveHistoricalTerminalAdoption(Original original,NpcOwnerLoad loaded)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&!ColdSourceAudit.ContainsKey(original.Operation)&&ColdSourceAudit.Count<64)
            ColdSourceAudit[original.Operation]=new ColdSourceReceipt{Process=System.Diagnostics.Process.GetCurrentProcess().Id,NativeOwnerBytes=loaded.Native.Length,HistoricalBodyBytes=ReadOriginalBody(ParseOriginal(original.Text).Element("nativeBody"),original.Id).Length,MetadataOnly=true};
#endif
    }
}
