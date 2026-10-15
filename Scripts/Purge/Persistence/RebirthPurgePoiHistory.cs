using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// Display history only. Never used as discovery, clearance or reward authority.
internal static class RebirthPurgePoiHistory
{
    internal sealed class Entry
    {
        internal long Epoch;
        internal string Discoverer="",Contributors="";
        internal ulong DiscoveredAt,PurgedAt;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static RebirthPoiWorldBinding binding;
    private static bool dirty;
    private static bool loadFailed;
    private static float nextSave;
    internal static long Revision {get;private set;}
    internal static void Reset()
    {
        if(dirty&&binding!=null&&binding.IsCurrent)Save(false);
        binding=null;entries.Clear();dirty=false;loadFailed=false;nextSave=0;Revision++;
    }
    internal static void Ensure()
    {
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote())return;
        if(binding!=null&&binding.IsCurrent)
        {
            if(dirty&&UnityEngine.Time.realtimeSinceStartup>=nextSave)Save(false);
            return;
        }
        Reset();if(!RebirthPoiWorldBinding.TryCapture(world,out binding))return;
        try
        {
            string path=Path.Combine(binding.Directory,"poi_history.xml");if(!File.Exists(path))return;
            if(new FileInfo(path).Length>32*1024*1024)throw new FormatException("POI history exceeds display budget.");
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
            {
                var root=XDocument.Load(reader).Root;
                if(root==null||(string)root.Attribute("world")!=binding.NativeWorldId)throw new FormatException("POI history world differs.");
                foreach(var node in root.Elements("poi"))
                {
                    if(entries.Count>=RebirthPoiClearanceLedger.MaximumRecords)throw new FormatException("Too many POI history entries.");
                    string key=(string)node.Attribute("key"),discoverer=(string)node.Attribute("discoverer")??"",contributors=(string)node.Attribute("contributors")??"";
                    long epoch=(long)node.Attribute("epoch");
                    if(string.IsNullOrEmpty(key)||key.Length>1024||epoch<0||discoverer.Length>80||contributors.Length>8192||entries.ContainsKey(key))throw new FormatException("Invalid POI history.");
                    entries.Add(key,new Entry{Epoch=epoch,Discoverer=discoverer,Contributors=contributors,DiscoveredAt=(ulong?)node.Attribute("discovered")??0,PurgedAt=(ulong?)node.Attribute("purged")??0});
                }
            }
        }
        catch(Exception error){entries.Clear();loadFailed=true;Log.Warning("[REBIRTH Purge] POI display history unavailable; original file preserved until reload: "+error.Message);}
    }
    internal static Entry Get(RebirthPoiClearanceRecord record)
    {
        Ensure();Entry entry;if(entries.TryGetValue(record.Identity.Key,out entry)&&entry.Epoch==record.Epoch)return entry;
        // Existing clearance witnesses can recover real purge time and names;
        // old discovery names were never stored and remain explicitly unknown.
        if(record.Clear==null)return null;
        string names=RebirthPurgeClearNotification.ContributorNames(record);
        if(names.Length>8192)return null;
        entry=new Entry{Epoch=record.Epoch,Contributors=names,PurgedAt=record.Clear.WorldTime};entries[record.Identity.Key]=entry;Revision++;return entry;
    }
    internal static void Discover(RebirthPoiIdentity identity,string name)
    {
        Ensure();RebirthPoiWorldStore store;RebirthPoiClearanceRecord record;
        if(binding==null||!binding.IsCurrent||!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)||store.Published==null||!store.Published.TryGet(identity,out record))return;
        Entry entry;if(!entries.TryGetValue(identity.Key,out entry)||entry.Epoch!=record.Epoch)entries[identity.Key]=entry=new Entry{Epoch=record.Epoch};
        if(entry.DiscoveredAt!=0)return;
        entry.Discoverer=RebirthPurgeDiscoveryNotification.CleanDisplayName(name);entry.DiscoveredAt=GameManager.Instance.World.worldTime;Save();
    }
    internal static void Purged(RebirthPoiClearanceRecord record,string contributors)
    {
        Ensure();if(binding==null||!binding.IsCurrent)return;
        Entry entry;if(!entries.TryGetValue(record.Identity.Key,out entry)||entry.Epoch!=record.Epoch)entries[record.Identity.Key]=entry=new Entry{Epoch=record.Epoch};
        if((contributors??"").Length>8192){Log.Warning("[REBIRTH Purge] POI contributor display exceeds limit.");return;}
        entry.Contributors=contributors??"";entry.PurgedAt=GameManager.Instance.World.worldTime;Save();
    }
    private static void Save(bool changed=true)
    {
        if(changed)Revision++;
        // Never replace an unreadable original with a partial in-memory history.
        // New display observations may still be shown, but persistence waits for
        // a fresh world load that can validate the original history successfully.
        if(loadFailed)return;
        dirty=true;nextSave=UnityEngine.Time.realtimeSinceStartup+5f;
        try
        {
            if(!binding.IsCurrent)return;
            string path=Path.Combine(binding.Directory,"poi_history.xml");
            for(string at=binding.Directory;!string.IsNullOrEmpty(at);at=Path.GetDirectoryName(at))
                if(Directory.Exists(at)&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new IOException("POI history path is a link.");
            foreach(string file in new[]{path,path+".tmp"})if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("POI history file is a link.");
            Directory.CreateDirectory(binding.Directory);var root=new XElement("poi_history",new XAttribute("version",1),new XAttribute("world",binding.NativeWorldId));
            foreach(var pair in entries)root.Add(new XElement("poi",new XAttribute("key",pair.Key),new XAttribute("epoch",pair.Value.Epoch),new XAttribute("discoverer",pair.Value.Discoverer),new XAttribute("contributors",pair.Value.Contributors),new XAttribute("discovered",pair.Value.DiscoveredAt),new XAttribute("purged",pair.Value.PurgedAt)));
            new XDocument(root).Save(path+".tmp");if(!binding.IsCurrent)return;
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
            dirty=false;
        }
        catch(Exception error){Log.Warning("[REBIRTH Purge] POI display history could not save: "+error.Message);}
    }
}
