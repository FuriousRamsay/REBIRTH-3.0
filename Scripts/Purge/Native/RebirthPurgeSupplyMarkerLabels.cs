using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

// Optional map presentation only. Native cache owns existence/position; crate stamps own access.
internal static class RebirthPurgeSupplyMarkerLabels
{
    private sealed class Label { internal Vector3i Position; internal string Owner; }
    private static readonly Dictionary<int,Label> labels=new Dictionary<int,Label>();
    private static readonly Dictionary<Vector3i,string> positions=new Dictionary<Vector3i,string>();
    private static RebirthPoiWorldBinding binding;
    private static string path;
    private static bool reported,clearAfterWrite;
    internal static void Reset(){binding=null;path=null;labels.Clear();positions.Clear();reported=false;clearAfterWrite=false;}
    // Native WorldShuttingDown precedes the final WorldState.SetFrom/director
    // write. Keep optional names until that existing write, then release scope.
    internal static void PreserveUntilFinalWrite(){clearAfterWrite=true;}
    private static void Report(Exception error)
    {
        if(reported)return;reported=true;
        Log.Warning("[REBIRTH Purge] Supply marker labels unavailable: "+error.Message);
    }
    private static bool Safe()
    {
        if(binding==null||!binding.IsCurrent)return false;
        for(string at=Path.GetDirectoryName(path);!string.IsNullOrEmpty(at);at=Path.GetDirectoryName(at))
            if(Directory.Exists(at)&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)return false;
        foreach(string file in new[]{path,path+".tmp"})
            if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
        return true;
    }
    internal static void Capture(EntitySupplyCrate crate,string owner)
    {
        if(crate?.world==null||crate.world.IsRemote()||string.IsNullOrEmpty(owner)||labels.Count>=16384&&!labels.ContainsKey(crate.entityId))return;
        labels[crate.entityId]=new Label{Position=new Vector3i(crate.position),Owner=owner};
    }
    internal static void Bind(AIDirectorAirDropComponent director)
    {
        if(director==null)return;
        RebirthPoiWorldStore store;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))return;
        var current=store.Published?.Binding;
        if(current==null||!current.IsCurrent||ReferenceEquals(current,binding))return;
        if(binding!=null)Reset();
        binding=current;path=Path.Combine(binding.SaveDirectory,"RebirthData","Purge","Supplies","marker-labels.xml");
        try
        {
            if(!Safe())throw new IOException("Unsafe marker label path.");
            if(File.Exists(path))
            {
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304}))
                {
                    var root=XDocument.Load(reader).Root;
                    if(root==null||root.Name!="supply_marker_labels"||(string)root.Attribute("world")!=binding.NativeWorldId||(int?)root.Attribute("version")!=1)throw new FormatException("Marker label save differs from current world.");
                    int count=0;
                    var loaded=new Dictionary<int,Label>();
                    foreach(var node in root.Elements("label"))
                    {
                        if(++count>16384)throw new FormatException("Too many supply marker labels.");
                        int id=(int)node.Attribute("id");
                        string owner=RebirthPurgeDiscoveryNotification.CleanDisplayName((string)node.Attribute("owner"));
                        if(id<=0||string.IsNullOrEmpty(owner)||loaded.ContainsKey(id))throw new FormatException("Invalid supply marker label.");
                        loaded.Add(id,new Label{Owner=owner,Position=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z"))});
                    }
                    foreach(var pair in loaded)if(!labels.ContainsKey(pair.Key)&&labels.Count<16384)labels.Add(pair.Key,pair.Value);
                }
            }
        }
        catch(Exception error){Report(error);}
        try
        {
            Reindex(director,true);
            // Native refresh supplies the existing network delivery and unloaded fallback.
            director.RefreshCrates(-1);
        }
        catch(Exception error){Report(error);}
    }
    internal static void BeforeRefresh(AIDirectorAirDropComponent __instance)
    {
        // Landing changes native cache positions before RefreshCrates can build
        // unloaded markers. Do not wait for a disk save to publish owner names.
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||
           binding==null||!binding.IsCurrent||!ReferenceEquals(__instance?.Director?.World,binding.Scope))return;
        try {Reindex(__instance,false);}catch(Exception error){Report(error);}
    }
    internal static void AfterRemove(AIDirectorAirDropComponent __instance,bool __runOriginal)
    {
        // Prune presentation only after native removal. Never remove a crate,
        // nav object, observer or reward because an optional name is missing.
        if(__runOriginal)BeforeRefresh(__instance);
    }
    private static void Reindex(AIDirectorAirDropComponent director,bool restoring)
    {
        positions.Clear();var retained=new HashSet<int>();
        foreach(var cache in director.supplyCrates)
        {
            Label label;
            if(!labels.TryGetValue(cache.entityId,out label)||restoring&&!label.Position.Equals(cache.blockPos))continue;
            retained.Add(cache.entityId);label.Position=cache.blockPos;
            // Coincident crates with different owners must not display the wrong owner's name.
            string old;
            positions[cache.blockPos]=positions.TryGetValue(cache.blockPos,out old)&&old!=label.Owner?string.Empty:label.Owner;

        }
        // Resolve collisions before decorating existing positional markers too;
        // native can reuse one positional marker for coincident crates.
        foreach(var cache in director.supplyCrates)
        {
            Label label;if(!retained.Contains(cache.entityId)||!labels.TryGetValue(cache.entityId,out label))continue;
            var nav=NavObjectManager.Instance.GetNavObjectByEntityID(cache.entityId);
            if(nav!=null)RebirthPurgeSupplyMapLabel.SetName(nav,
                nav.TrackType==NavObject.TrackTypes.Entity?label.Owner:positions[cache.blockPos]);
        }
        var removed=new List<int>();foreach(int id in labels.Keys)if(!retained.Contains(id))removed.Add(id);
        foreach(int id in removed)labels.Remove(id);
    }
    internal static void AfterRegisterPosition(string __0,Vector3 __1,NavObject __result,bool __runOriginal)
    {
        string owner;
        if(__runOriginal&&__result!=null&&__0=="supply_drop"&&positions.TryGetValue(new Vector3i(__1),out owner))
            RebirthPurgeSupplyMapLabel.SetName(__result,owner);
    }
    internal static void AfterWrite(AIDirectorAirDropComponent __instance,bool __runOriginal)
    {
        if(!__runOriginal||binding==null||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        try
        {
            if(!Safe())return;
            Reindex(__instance,false);
            var root=new XElement("supply_marker_labels",new XAttribute("version",1),new XAttribute("world",binding.NativeWorldId));
            foreach(var pair in labels)root.Add(new XElement("label",new XAttribute("id",pair.Key),new XAttribute("x",pair.Value.Position.x),new XAttribute("y",pair.Value.Position.y),new XAttribute("z",pair.Value.Position.z),new XAttribute("owner",pair.Value.Owner)));
            Directory.CreateDirectory(Path.GetDirectoryName(path));if(!Safe())return;
            new XDocument(root).Save(path+".tmp");
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
        }
        catch(Exception error){Report(error);}
        finally {if(clearAfterWrite)Reset();}
    }
}
