using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    private sealed class InventoryOwnerFile
    {internal InventoryOriginal Original;internal byte[] Native;internal string Path,Phase;internal bool Accepted,Saved,Archived;internal int Workers;}
    private sealed class InventoryOwnerSave{internal InventoryOwnerFile File;internal byte[] Native;internal string Path;internal bool Finished;}
    private static readonly object inventoryOwnerGate=new object();
    private static readonly ConditionalWeakTable<PlayerDataFile,InventoryOwnerFile> inventoryOwnerData=new ConditionalWeakTable<PlayerDataFile,InventoryOwnerFile>();
    private static readonly Dictionary<InventoryOriginal,Dictionary<string,InventoryOwnerFile>> inventoryOwnerFiles=new Dictionary<InventoryOriginal,Dictionary<string,InventoryOwnerFile>>();
    internal static string InventoryBeforeReceipt(Guid operation)=>"rbPlayerConsumeBefore_"+operation.ToString("N");
    internal static string InventorySourceReceipt(Guid operation)=>"rbPlayerConsumeSource_"+operation.ToString("N");
    internal static string InventorySecondaryReceipt(Guid operation)=>"rbPlayerConsumeSecondary_"+operation.ToString("N");
    internal static string InventoryUseReceipt(Guid operation)=>"rbPlayerConsumeUse_"+operation.ToString("N");
    private static void CaptureInventoryOwner(InventoryOriginal original,PlayerDataFile data,bool accepted)
    {
        if(!original.Current()||!original.BeforeIssued||data==null||data.id!=original.Actor.entityId||data.deaths!=original.Deaths||data.bDead||data.buffData?.Length>1048576||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventoryBeforeReceipt(original.Operation),out float before,out bool beforePresent)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventorySourceReceipt(original.Operation),out float source,out bool sourcePresent)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventorySecondaryReceipt(original.Operation),out float secondary,out _)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,InventoryUseReceipt(original.Operation),out float use,out _))return;
        int prefix=source>=100f&&source<1000f&&(float)(int)source==source?(int)source-100:-1;
        bool retired=sourcePresent&&source==3f&&before==0f&&secondary==0f&&use==0f&&original.Committed;
        if(!retired&&(!beforePresent||before!=1f))return;
        string phase=retired?"retired":prefix>=0?"prefix-"+prefix:sourcePresent&&source==1f?(use==1f?"after":secondary==1f?"secondary":"source"):"before";
        if(phase!="before"&&!original.SourceIssued)return;
        if((phase=="secondary"||phase=="after")&&(!original.CanonicalSaved||!original.DeferredReturned||!original.SecondaryEntered)||
            phase=="after"&&(!original.SecondarySaved||!original.NativeEntered))return;
        if(phase=="before"&&(source!=0f||secondary!=0f||use!=0f)||prefix>=0&&(secondary!=0f||use!=0f)||phase=="source"&&(secondary!=0f||use!=0f)||phase=="secondary"&&use!=0f||phase=="after"&&secondary!=1f)return;
        var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
        if(!(prefix>=0?original.Inventory.MatchesPrefix(bag,belt,prefix):phase=="before"?original.Inventory.MatchesBefore(bag,belt):original.Inventory.MatchesAfter(bag,belt)))return;
        string path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),original.Owner+"."+PlayerDataFile.EXT));
        if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
        var bytes=RebirthNpcMetabolismCustody.CapturePlayerConsumptionNative(data);
        if(!inventoryOwnerFiles.TryGetValue(original,out var phases))inventoryOwnerFiles.Add(original,phases=new Dictionary<string,InventoryOwnerFile>());
        if(phases.TryGetValue(phase,out var previous)&&(previous.Archived||previous.Workers!=0))return;
        var capture=new InventoryOwnerFile{Original=original,Native=bytes,Path=path,Phase=phase,Accepted=accepted};phases[phase]=capture;
        inventoryOwnerData.Remove(data);inventoryOwnerData.Add(data,capture);
    }
    internal static void ObserveInventoryOwnerFromPlayer(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        if(!run||!ThreadManager.IsMainThread())return;
        lock(inventoryOwnerGate)try
        {
            foreach(var original in inventoryOriginals.Values)
                if(ReferenceEquals(original.Actor,actor)&&ReferenceEquals(original.World.GetPrimaryPlayer(),actor)&&
                    RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==original.Owner){CaptureInventoryOwner(original,data,true);return;}
        }catch{}
    }
    internal static void EnterInventoryAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null)return;
        lock(inventoryOwnerGate)try
        {
            if(!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner))return;
            foreach(var original in inventoryOriginals.Values)
                if(original.Actor.entityId==peer.entityId&&original.Owner==owner.CanonicalId)
                {CaptureInventoryOwner(original,data,false);if(inventoryOwnerData.TryGetValue(data,out var capture))state=capture;return;}
        }catch{}
    }
    internal static void ObserveInventoryAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,object state)
    {
        if(!ThreadManager.IsMainThread()||state is not InventoryOwnerFile file)return;
        lock(inventoryOwnerGate)try
        {
            if(run&&file.Original?.Current()==true&&ReferenceEquals(peer.latestPlayerData,data)&&
                ReferenceEquals(peer,SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(file.Original.Actor.entityId))&&
                file.Native.SequenceEqual(RebirthNpcMetabolismCustody.CapturePlayerConsumptionNative(data)))file.Accepted=true;
        }catch{}
    }
    internal static void EnterInventoryOwnerSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null)return;
        lock(inventoryOwnerGate)try
        {
            if(!inventoryOwnerData.TryGetValue(data,out var file)||file.Original==null||file.Original.Terminal||file.Archived||file.Workers>=2)return;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));
            if(path!=file.Path||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||
                !file.Native.SequenceEqual(RebirthNpcMetabolismCustody.CapturePlayerConsumptionNative(data)))return;
            file.Workers++;state=new InventoryOwnerSave{File=file,Native=file.Native,Path=path};
        }catch{}
    }
    internal static void ObserveInventoryOwnerSave(bool run,object state)
    {
        if(state is not InventoryOwnerSave save)return;
        lock(inventoryOwnerGate)
        {
            if(save.Finished)return;save.Finished=true;
            // Worker completion compares only its exact captured native file.
            // Main-thread actor/session scope is rechecked at archive entry.
            try{if(run&&save.File.Original!=null&&!save.File.Original.Terminal&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))save.File.Saved=true;}
            catch{}
            finally
            {
                var original=save.File.Original;save.File.Workers--;if(save.File.Archived&&save.File.Workers==0)save.File.Native=null;
                save.File=null;save.Native=null;if(original?.Terminal==true||original?.ObservationRetired==true)ReleaseInventoryOriginalMemory(original);
            }
        }
    }
    internal static bool ArchiveInventoryOwner(InventoryOriginal original,string phase)
    {
        if(!original.Current())return false;
        // The purpose callback owns native Save/Upload. Polling its completed
        // witness must not start another full owner save every game update.
        lock(inventoryOwnerGate)
        {
            if(!inventoryOwnerFiles.TryGetValue(original,out var phases)||!phases.TryGetValue(phase,out var file)||!file.Accepted||!file.Saved)return false;
            if(!file.Archived)
            {
                original.Record("owner-"+phase+"-saved",new XElement("native",Convert.ToBase64String(file.Native)));
                file.Archived=true;if(file.Workers==0)file.Native=null;
            }
            return original.Current();
        }
    }
    private static void ReleaseInventoryOriginalMemory(InventoryOriginal original)
    {
        lock(inventoryOwnerGate)
        {
            if(original==null||!original.Terminal&&!original.ObservationRetired||original.MemoryReleased)return;
            if(inventoryOwnerFiles.TryGetValue(original,out var phases))
            {
                if(phases.Values.Any(value=>value.Workers!=0))return;
                inventoryOwnerFiles.Remove(original);foreach(var file in phases.Values){file.Native=null;file.Original=null;}
            }
            original.MemoryReleased=true;original.Memory.Dispose();
        }
    }
}
