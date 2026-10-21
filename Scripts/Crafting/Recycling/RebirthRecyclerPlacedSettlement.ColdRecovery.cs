using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool ArtifactNodeExists(string path)
    {
        try{File.GetAttributes(path);return true;}
        catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}
        catch{return true;}
    }
    private static bool TryReadColdTerminal(Original original,LoadedOwner owner,LoadedStation station,out string path,out byte[] bytes,out IDisposable memory)
    {
        path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".terminal.xml");bytes=null;memory=null;
        if(station.Stage!="after"||!owner.MatchesOriginalCheckpoint(original,3,original.Slots.Length))return false;
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out memory))return false;
        bool retained=false;
        try
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var before=XElement.Parse(Encoding.UTF8.GetString(original.Image));
            var terminal=record?.Element("recyclerPlacedTerminal");var savedOwner=terminal?.Element("actualOwnerSaved")?.Elements().SingleOrDefault();
            var ownerEvent=savedOwner?.Element("recyclerNativeOwnerSaved")??savedOwner?.Element("recyclerNativeOwnerLoaded");
            var savedStation=terminal?.Element("actualStationSaved")?.Elements().SingleOrDefault();var native=savedStation?.Element("recyclerPlacedStationSaved");
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=original.Operation.ToString("N")||
                (string)record.Attribute("world")!=original.WorldKey||!XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),before)||
                savedOwner?.Name!="workstationCompletionIntent"||(string)savedOwner.Attribute("operation")!=original.Operation.ToString("N")||
                !XNode.DeepEquals(savedOwner.Element("before")?.Elements().SingleOrDefault(),before)||ownerEvent==null||
                (int?)ownerEvent.Attribute("phase")!=3||(int?)ownerEvent.Attribute("completed")!=original.Slots.Length||
                ColdBytes(ownerEvent.Element("native"),16*1024*1024)==null||savedStation?.Name!="workstationCompletionIntent"||
                (string)savedStation.Attribute("operation")!=original.Operation.ToString("N")||
                !XNode.DeepEquals(savedStation.Element("before")?.Elements().SingleOrDefault(),before)||native==null||(string)native.Attribute("stage")!="after"||
                !ColdBytes(native.Element("native"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(original.AfterNative))return false;
            bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            retained=bytes!=null&&bytes.LongLength==length&&owner.MatchesOriginalCheckpoint(original,3,original.Slots.Length)&&station.Current();return retained;
        }
        finally{if(!retained){bytes=null;memory?.Dispose();memory=null;}}
    }
    private static void RecoverAuthorityCold(World world)
    {
        if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||authorityOriginals.Count>=4||
            !TryObserveLoadedRecyclerOwner(world.GetPrimaryPlayer(),out var owner))return;
        LoadedStation[] candidates;lock(coldStationGate)candidates=loadedRecyclerStations.Where(value=>!value.Adopted&&value.SameWorld(world)).ToArray();
        foreach(var station in candidates)
        {
            Original original=null;StationPublication native=null;OwnerPublication publication=null;AuthorityOriginal root=null;IDisposable terminalMemory=null;
            try
            {
                if(authorityOriginals.Any(value=>value.Source.Operation==station.Operation)||!station.Current())continue;
                original=Original.FromActualCold(owner,station);if(original==null||!TryClassifyLoadedOwner(owner,station,original,out _,out _,out _))continue;
                string terminalPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".terminal.xml");
                bool hasTerminal=new[]{terminalPath,terminalPath+".tmp",terminalPath+".bak"}.Any(ArtifactNodeExists);byte[] terminal=null;
                if(hasTerminal&&!TryReadColdTerminal(original,owner,station,out terminalPath,out terminal,out terminalMemory))continue;
                native=StationPublication.FromActualCold(original,station);if(native==null)continue;
                publication=OwnerPublication.FromActualCold(original,owner,station,native);if(publication==null)continue;
                var access=AccessHold.Capture(original,native);if(access==null)continue;
                root=new AuthorityOriginal{Source=original,Station=native,Owner=publication,Access=access,Reentry=station,
                    Terminal=terminal,TerminalPath=hasTerminal?terminalPath:null,TerminalAllocation=terminalMemory};
                if(hasTerminal){if(!publication.OwnerSaved||!native.SavedAfter())continue;access.ObserveTerminal(publication,native);}
                authorityOriginals.Add(root);station.Adopted=true;original=null;native=null;publication=null;terminalMemory=null;root=null;
                // The new private original/checkpoint now owns the exact copied
                // native evidence. Dropping the observation releases no slots.
                owner.Dispose();return;
            }
            catch{/* Conflicting or incomplete actual native loads retain custody. */}
            finally{if(root!=null){root.Reentry=null;root.Dispose();original=null;native=null;publication=null;terminalMemory=null;}
                publication?.Dispose();native?.Dispose();original?.Dispose();terminalMemory?.Dispose();}
        }
    }
}
