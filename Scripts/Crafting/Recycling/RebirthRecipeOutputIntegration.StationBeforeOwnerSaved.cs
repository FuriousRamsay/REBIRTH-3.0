using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class StationBeforeOwnerCapture
    {
        internal StationOutputBefore[] Sources;internal string Hash,Root,Name;internal int Length;
    }
    private sealed class StationBeforeOwnerSave
    {
        internal StationBeforeOwnerCapture Capture;internal byte[] Native;internal string Path;internal IDisposable Memory;
    }
    private static readonly ConditionalWeakTable<PlayerDataFile,StationBeforeOwnerCapture> stationBeforeOwnerCaptures=
        new ConditionalWeakTable<PlayerDataFile,StationBeforeOwnerCapture>();
    private static bool PublishStationOutputOwnerNative(string path,byte[] native)
    {
        if(PublishUiOriginal(path,native))return true;
        RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
        return !File.Exists(path+".tmp")&&!File.Exists(path+".bak")&&File.Exists(path)&&
            new FileInfo(path).Length==native.Length&&File.ReadAllBytes(path).SequenceEqual(native);
    }
    // This observes the existing FromPlayer/Save chain. A matching inventory
    // replica or a successful Save call without exact final bytes is insufficient.
    private static void ObserveStationOutputBeforeOwnerFromPlayer(PlayerDataFile data,EntityPlayerLocal actor)
    {
        stationBeforeOwnerCaptures.Remove(data);
        var sources=stationOutputBefores.Where(value=>ReferenceEquals(value.Actor,actor)&&value.ReturnedUpdate&&value.SyncReturned&&
            value.OwnerSaved==null&&StationOutputBeforeCurrent(value)).ToArray();
        if(sources.Length==0||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return;
            var bag=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true));
            var belt=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false));var equipment=data.equipmentData?.ToArray();
            sources=sources.Where(value=>value.Owner==owner.StorageKey&&data.id==value.Actor.entityId&&data.deaths==value.Deaths&&
                value.Bag.SequenceEqual(bag)&&value.Belt.SequenceEqual(belt)&&equipment!=null&&value.Equipment.SequenceEqual(equipment)&&
                RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,value.OwnerFrame)&&
                StationOutputBeforeCurrent(value)).ToArray();
            if(sources.Length==0)return;
            var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
            if(native.Length<6||native.Length>16*1024*1024)return;
            stationBeforeOwnerCaptures.Add(data,new StationBeforeOwnerCapture{Sources=sources,Hash=UiOriginalHash(native),Length=native.Length,
                Root=Path.GetFullPath(GameIO.GetPlayerDataDir()),Name=owner.CanonicalId});
        }
    }
    private static void EnterStationOutputBeforeOwnerSave(PlayerDataFile data,string directory,string playerName,bool ran,out object state)
    {
        state=null;IDisposable memory=null;
        try
        {
            if(!ran||!stationBeforeOwnerCaptures.TryGetValue(data,out var capture))return;
            string root=Path.GetFullPath(directory),path=Path.GetFullPath(Path.Combine(root,playerName+"."+PlayerDataFile.EXT));
            if(root!=capture.Root||playerName!=capture.Name||Path.GetDirectoryName(path)!=root||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*capture.Length+65536),out memory))return;
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
            if(native.Length!=capture.Length||UiOriginalHash(native)!=capture.Hash)return;
            state=new StationBeforeOwnerSave{Capture=capture,Native=native,Path=path,Memory=memory};memory=null;
        }
        catch{/* The native save remains independent of this purpose observer. */}
        finally{memory?.Dispose();}
    }
    private static void ObserveStationOutputBeforeOwnerSave(bool ran,object state)
    {
        if(state is not StationBeforeOwnerSave save)return;
        try
        {
            if(!ran||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))return;
            foreach(var source in save.Capture.Sources)
            {
                if(source.OwnerSaved!=null)continue;
                // Archive precisely the native image positively observed on
                // disk. It is evidence for this original, never a player writer.
                if(!PublishStationOutputOwnerNative(source.Path+".owner.native",save.Native))continue;
                byte[] frame=UiOriginalBytes(source.OwnerFrame);
                if(!PublishPersonalColdBinding(source.Path+".owner-frame",frame))continue;
                var proof=new XElement("recipeOutputStationOwnerBeforeSaved",new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),
                    new XAttribute("entity",source.Actor.entityId),new XAttribute("playerFile",save.Path),
                    new XAttribute("nativeDigest",save.Capture.Hash),new XAttribute("nativeLength",save.Native.Length),new XAttribute("frameDigest",UiOriginalHash(frame)));
                if(!PublishPersonalColdBinding(source.Path+".owner-saved",UiOriginalBytes(proof)))continue;
                source.OwnerSaved=proof;
            }
        }
        catch{/* Neither an absent file nor a partial archive qualifies Before. */}
        finally{save.Memory?.Dispose();save.Memory=null;save.Native=null;}
    }
}
