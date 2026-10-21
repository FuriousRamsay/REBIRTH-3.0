using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool ValidateHistoricalRemoteOwnerDescriptor(RemoteHostOriginal source,XElement descriptor,int phase,int completed)
        =>ValidateHistoricalRemoteOwnerDescriptor(XElement.Parse(Encoding.UTF8.GetString(source.Image)),source.Operation,source.WorldKey,source.Account,descriptor,phase,completed);
    private static bool ValidateHistoricalRemoteOwnerDescriptor(XElement original,Guid operation,string world,string account,XElement descriptor,int phase,int completed)
    {
        if(descriptor?.Name!="actualRemoteOwnerSaved"||(string)descriptor.Attribute("operation")!=operation.ToString("N")||
            (int?)descriptor.Attribute("phase")!=phase||(int?)descriptor.Attribute("completed")!=completed)return false;
        string path=Path.GetFullPath((string)descriptor.Attribute("path"));string root=Path.GetFullPath(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"))+Path.DirectorySeparatorChar;
        long length=(long?)descriptor.Attribute("length")??0;
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith(operation.ToString("N")+".",StringComparison.Ordinal)||
            length<1||length>24L*1024*1024||RebirthWorkstationCompletionPublication.PublishedLength(path)!=length||RecyclerFileDigest(path)!=(string)descriptor.Attribute("digest")||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
        using(memory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=record?.Element("recyclerRemoteOwnerSaved");
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=operation.ToString("N")||
                (string)record.Attribute("world")!=world||!XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),original)||
                (int?)saved?.Attribute("phase")!=phase||(int?)saved.Attribute("completed")!=completed||(string)saved.Attribute("account")!=account)return false;
            byte[] native=ColdBytes(saved.Element("native"),16*1024*1024);
            return native!=null&&native.Length>5&&native[0]=='t'&&native[1]=='t'&&native[2]=='p'&&native[3]==0&&native[4]==PlayerDataFile.cFileVersion&&
                RebirthWorkstationCompletionPublication.PublishedLength(path)==length&&RecyclerFileDigest(path)==(string)descriptor.Attribute("digest");
        }
    }
    private static bool TryReadRemoteColdTerminal(RemoteHostOriginal source)
    {
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".terminal.xml");
        if(!new[]{path,path+".tmp",path+".bak"}.Any(ArtifactNodeExists))return true;
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(!source.AcceptedStation||source.ColdPhase!=3||source.ColdCompleted!=source.Slots.Length||length<1||length>24L*1024*1024||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
        try
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var terminal=record?.Element("recyclerPlacedTerminal");
            var owners=terminal?.Elements("actualRemoteOwnerSaved").ToArray();var station=terminal?.Element("actualStationSaved")?.Elements().SingleOrDefault();
            var native=station?.Element("recyclerPlacedStationSaved");var original=XElement.Parse(Encoding.UTF8.GetString(source.Image));
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=source.Operation.ToString("N")||(string)record.Attribute("world")!=source.WorldKey||
                !XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),original)||owners?.Length!=2||
                !ValidateHistoricalRemoteOwnerDescriptor(source,owners[0],1,0)||!ValidateHistoricalRemoteOwnerDescriptor(source,owners[1],3,source.Slots.Length)||
                station?.Name!="workstationCompletionIntent"||(string)station.Attribute("operation")!=source.Operation.ToString("N")||
                !XNode.DeepEquals(station.Element("before")?.Elements().SingleOrDefault(),original)||(string)native?.Attribute("stage")!="after"||
                !ColdBytes(native.Element("native"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(source.AfterNative)||source.ColdStation?.Current()!=true)return false;
            byte[] bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes?.LongLength!=length)return false;
            source.Terminal=bytes;source.TerminalPath=path;source.TerminalMemory=memory;memory=null;return true;
        }
        finally{memory?.Dispose();}
    }
}
