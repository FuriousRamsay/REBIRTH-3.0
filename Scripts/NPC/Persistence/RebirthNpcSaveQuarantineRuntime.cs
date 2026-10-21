using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcRuntimeRegistry
{
    // Server and projection-only client callers must supply their positively
    // captured original native scope. This adapter performs no host file access.
    internal static RebirthNpcSaveQuarantineDomain PrepareRuntimeQuarantine(World originalWorld,string originalDirectory)
    {
        lock(Sync)
        {
            if(originalWorld==null||(registryWorld!=null&&(!ReferenceEquals(registryWorld,originalWorld)||!string.Equals(registryDirectory,originalDirectory,StringComparison.OrdinalIgnoreCase)))||(registryWorld==null&&(ByEntityId.Count!=0||EntityByStableId.Count!=0)))throw new InvalidOperationException("Runtime registry original scope unresolved");
            if(ByEntityId.Count!=EntityByStableId.Count)throw new InvalidOperationException("Runtime identity index mismatch");
            var actors=new Dictionary<int,EntityRebirthNPC>();
            foreach(var pair in ByEntityId)
            {
                var actor=originalWorld.GetEntity(pair.Key) as EntityRebirthNPC;var state=pair.Value;
                if(actor==null||!actor.NativeIdentityReadable||state==null||state.StableId.IsEmpty||!actor.ObservedNativeStableIdentity.IsEmpty&&actor.ObservedNativeStableIdentity!=state.StableId||!ReferenceEquals(actor.world,originalWorld)||!ReferenceEquals(actor.RebirthRuntimeState,state)||!EntityByStableId.TryGetValue(state.StableId,out int id)||id!=pair.Key)throw new InvalidOperationException("Runtime original native identity unresolved");
                actors.Add(pair.Key,actor);
            }
            var byEntity=ByEntityId;var byStable=EntityByStableId;var world=registryWorld;string directory=registryDirectory;
            var nextEntity=new Dictionary<int,RebirthNpcRuntimeState>();var nextStable=new Dictionary<RebirthNpcStableId,int>();var doc=QuarantineRuntimeImage();
            CheckQuarantineNativeIdentity(actors,originalWorld);
            return new RebirthNpcSaveQuarantineDomain("runtime-registry",doc,
                ()=>{lock(Sync){if(!ReferenceEquals(ByEntityId,byEntity)||!ReferenceEquals(EntityByStableId,byStable)||!ReferenceEquals(registryWorld,world)||registryDirectory!=directory)return false;try{CheckQuarantineNativeIdentity(actors,originalWorld);return QuarantineRuntimeImage().OuterXml==doc.OuterXml;}catch{return false;}}},
                ()=>{lock(Sync){ByEntityId=nextEntity;EntityByStableId=nextStable;registryWorld=null;registryDirectory=null;}},
                ()=>{lock(Sync){ByEntityId=byEntity;EntityByStableId=byStable;registryWorld=world;registryDirectory=directory;}});
        }
    }
    private static void CheckQuarantineNativeIdentity(Dictionary<int,EntityRebirthNPC> actors,World world)
    {
        foreach(var pair in actors)if(!ReferenceEquals(world.GetEntity(pair.Key),pair.Value)||!pair.Value.NativeIdentityReadable||!ReferenceEquals(pair.Value.world,world)||!ByEntityId.TryGetValue(pair.Key,out var state)||!ReferenceEquals(pair.Value.RebirthRuntimeState,state)||!pair.Value.ObservedNativeStableIdentity.IsEmpty&&pair.Value.ObservedNativeStableIdentity!=state.StableId)throw new InvalidOperationException("Original native runtime changed during quarantine");
    }
    private static XmlDocument QuarantineRuntimeImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("runtimeRegistryQuarantine");doc.AppendChild(root);root.SetAttribute("directory",registryDirectory??string.Empty);
        var ids=new List<int>(ByEntityId.Keys);ids.Sort();
        foreach(int id in ids)
        {
            var state=ByEntityId[id];if(state==null)throw new InvalidOperationException("Runtime state missing");
            var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("dirty",state.Dirty?"1":"0");row.SetAttribute("prepared",state.PreparedRestorationPending?"1":"0");
            var actor=registryWorld?.GetEntity(id) as EntityRebirthNPC;if(actor==null||!actor.NativeIdentityReadable)throw new InvalidOperationException("Original native body identity unreadable");row.SetAttribute("nativeClass",actor.entityClass.ToString(CultureInfo.InvariantCulture));row.SetAttribute("observedNativeStable",actor.ObservedNativeStableIdentity.ToString());
            row.SetAttribute("guardX",state.GuardPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("guardY",state.GuardPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("guardZ",state.GuardPosition.z.ToString("R",CultureInfo.InvariantCulture));
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                RebirthNpcPersistenceCodec.Write(writer,state);writer.Flush();if(stream.Length>1024*1024)throw new InvalidDataException("Runtime quarantine record exceeds bound");row.InnerText=Convert.ToBase64String(stream.ToArray());
            }
        }
        return doc;
    }
}
