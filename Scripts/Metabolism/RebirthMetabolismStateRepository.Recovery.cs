using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthMetabolismStateRepository
{
    private const long MaximumStoreBytes=64L*1024*1024;
    // Versions 1..9 use the same named outer container. Existing row decoder
    // supplies explicit historical gastric/intestinal/meal-credit migrations.
    private static Dictionary<string,RebirthMetabolismState> ReadCandidate(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<=0||stream.Length>MaximumStoreBytes)throw new InvalidDataException("Metabolism store size invalid.");
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumStoreBytes};
            using(var reader=XmlReader.Create(stream,settings))return DecodeStore(XDocument.Load(reader,LoadOptions.None));
        }
    }
    internal static Dictionary<string,RebirthMetabolismState> DecodeStore(XDocument document)
    {
        var root=document?.Root;
        if(document?.DocumentType!=null||root==null||root.Name!="rebirthMetabolismPlayers"||root.Attributes().Any(a=>a.Name!="version")||
            !int.TryParse((string)root.Attribute("version"),out int version)||version<1||version>RebirthMetabolismState.CurrentVersion)
            throw new InvalidDataException("Unsupported metabolism store schema.");
        var result=new Dictionary<string,RebirthMetabolismState>(StringComparer.Ordinal);
        foreach(var source in root.Elements())
        {
            if(source.Name!="player")throw new InvalidDataException("Unknown metabolism store row.");
            string id=(string)source.Attribute("id");
            if(string.IsNullOrWhiteSpace(id)||id.Length>4096||id!=id.Trim()||result.ContainsKey(id))throw new InvalidDataException("Missing or duplicate metabolism identity.");
            var row=new XElement(source);
            if(row.Attribute("version")==null)row.SetAttributeValue("version",version);
            if(!int.TryParse((string)row.Attribute("version"),out int rowVersion)||rowVersion<1||rowVersion>RebirthMetabolismState.CurrentVersion)
                throw new InvalidDataException("Unsupported metabolism player version.");
            if(rowVersion==RebirthMetabolismState.CurrentVersion)
            {
                // Later optional same-schema controls retain their documented
                // defaults; missing core persisted physiology is not a player.
                foreach(string attribute in new[]{"initialized","digestiveHealth","energy","lastWorldTime","nextEntryId"})
                    if(row.Attribute(attribute)==null)throw new InvalidDataException("Incomplete current metabolism player: "+attribute);
                if(row.Element("ingestionEntries")==null||row.Element("timedEffects")==null)throw new InvalidDataException("Incomplete current metabolism sections.");
            }
            var groups=new HashSet<XName>();
            foreach(var child in row.Elements())
            {
                if((child.Name!="hydrationSlot"&&child.Name!="hydrationTransferReceipt"&&child.Name!="ingestionEntries"&&child.Name!="timedEffects")||!groups.Add(child.Name))
                    throw new InvalidDataException("Unknown or duplicate metabolism player section.");
                if(child.Name=="ingestionEntries"&&child.Elements().Any(x=>x.Name!="entry")||child.Name=="timedEffects"&&child.Elements().Any(x=>x.Name!="effect"))
                    throw new InvalidDataException("Unknown metabolism section row.");
            }
            var state=DeserializePlayer(row);ValidateStateForPersistence(state);
            var ids=new HashSet<int>();int largest=0;
            foreach(var entry in state.IngestionEntries)
            {
                if(entry.EntryId<=0||!ids.Add(entry.EntryId))throw new InvalidDataException("Invalid ingestion identity.");
                largest=Math.Max(largest,entry.EntryId);
            }
            if(state.NextEntryId<=largest)
            {
                if(rowVersion>=RebirthMetabolismState.CurrentVersion||largest==int.MaxValue)throw new InvalidDataException("Invalid next ingestion identity.");
                state.NextEntryId=largest+1;state.Dirty=true;
            }
            result.Add(id,state);
        }
        return result;
    }
    internal static Dictionary<string,RebirthMetabolismState> ReadStoreCandidates(string path,out bool usedBackup)
    {
        path=Path.GetFullPath(path);
        usedBackup=false;Exception primaryError=null;
        if(File.Exists(path))try{return ReadCandidate(path);}catch(Exception error){primaryError=error;}
        if(File.Exists(path+".bak"))
        {
            var recovered=ReadCandidate(path+".bak");usedBackup=true;
            foreach(var state in recovered.Values)state.Dirty=true;
            return recovered;
        }
        string directory=Path.GetDirectoryName(path);
        bool retained=Directory.Exists(directory)&&Directory.EnumerateFiles(directory,Path.GetFileName(path)+".*",SearchOption.TopDirectoryOnly).Any();
        if(primaryError!=null||retained)throw new InvalidDataException("Metabolism candidates require recovery; no new empty store.",primaryError);
        return new Dictionary<string,RebirthMetabolismState>(StringComparer.Ordinal);
    }
    private static void PreserveLoadEvidence(string path,bool usedBackup)
    {
        // A good backup must not be replaced by the corrupt primary on the next
        // ordinary write. Retain that primary and leave the good backup in place.
        if(usedBackup&&File.Exists(path))File.Move(path,path+".corrupt."+Guid.NewGuid().ToString("N"));
        if(File.Exists(path+".tmp"))File.Move(path+".tmp",path+".candidate."+Guid.NewGuid().ToString("N"));
    }
}
