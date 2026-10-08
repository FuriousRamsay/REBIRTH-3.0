using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Captures existing physical projections against canonical custody; never invents custody IDs.
internal static class RebirthNpcNativeReconstructionCapture
{
    private static string assemblyHash;
    internal static bool TryCapture(EntityRebirthHumanoidNPC npc, RebirthNpcPersistentRecord person,
        RebirthNpcNativeStackSet custody, Func<string,int,bool> supportsController,
        out RebirthNpcNativeReconstruction result, out string reason)
    {
        return TryCapture(npc,person,custody,custody?.Revision??0,supportsController,out result,out reason);
    }
    internal static bool TryCapture(EntityRebirthHumanoidNPC npc, RebirthNpcPersistentRecord person,
        RebirthNpcNativeStackSet custody,uint inventoryRevision,Func<string,int,bool> supportsController,
        out RebirthNpcNativeReconstruction result,out string reason)
    {
        result=null; reason="invalid-reconstruction-source";
        if(npc==null || npc.world==null || npc.world.IsRemote() || npc.RebirthRuntimeState==null ||
            person?.Identity==null || person.Profile==null || person.Presence==null ||
            person.Identity.StableNpcId!=npc.RebirthRuntimeState.StableId || person.Profile.ProfileId!=npc.RebirthRuntimeState.ProfileId ||
            person.Presence.EmbodimentGeneration==0 || npc.IsDead() || npc.Stats==null || npc.Buffs==null ||
            npc.inventory==null || npc.equipment==null || npc.bag==null) return false;
        try
        {
            if(!RebirthNpcNativeHeaderProjection.TryCapture(npc,out var header)){reason="native-header-unqualified";return false;}
            if(!RebirthNpcNativeGeometryProjection.TryCapture(npc,out var geometry)){reason="native-geometry-unqualified";return false;}
            foreach(var grid in new[]{npc.inventory.ItemGrid,npc.equipment.ItemGrid,npc.bag.ItemGrid})
                for(int index=0;index<grid.Length;index++)if(grid[index]!=null&&(grid[index].count<0||grid[index].count>65535))
                {reason="native-stack-count-not-losslessly-serializable";return false;}
            if(assemblyHash==null) assemblyHash=RebirthNpcNativeReconstruction.Digest(File.ReadAllBytes(typeof(Entity).Assembly.Location));
            if(custody!=null && (custody.Owner!=person.Identity.StableNpcId.ToString() || custody.Revision!=inventoryRevision)) return false;
            var root=new XElement("nativeReconstruction",new XAttribute("version",6),new XAttribute("required",1),
                new XAttribute("nativeVersion",EntityCreationData.FileVersion),new XAttribute("owner",person.Identity.StableNpcId),
                new XAttribute("profile",person.Profile.ProfileId),new XAttribute("generation",person.Presence.EmbodimentGeneration),
                new XAttribute("inventoryRevision",inventoryRevision),new XAttribute("assembly",assemblyHash),new XAttribute("complete",1));
            root.Add(Blob("actor",w=>npc.Write(w,StreamModeWrite.Persistency)),Blob("bodyDamage",w=>npc.bodyDamage.Write(w)),
                Blob("stats",w=>npc.Stats.Write(w)),Blob("buffs",w=>npc.Buffs.Write(w,false)),
                Blob("toolbelt",w=>npc.inventory.Write(w,StreamModeWrite.Persistency)),
                Blob("equipment",w=>npc.equipment.Write(w,StreamModeWrite.Persistency)),Blob("bag",w=>npc.bag.Write(w,StreamModeWrite.Persistency)));
            var slots=new XElement("slots"); var used=new HashSet<string>(StringComparer.Ordinal);
            var records=custody?.Write().Elements("nativeStack").ToArray()??new XElement[0];
            var previous=person.NativeReconstruction?.Write().Element("slots").Elements("slot").ToArray()??new XElement[0];
            foreach(var area in new[]{"toolbelt","equipment","bag"})
            {
                var grid=area=="toolbelt"?npc.inventory.ItemGrid:area=="equipment"?npc.equipment.ItemGrid:npc.bag.ItemGrid;
                if(grid.Length>RebirthNpcNativeReconstruction.MaximumSlots) return false;
                for(int i=0;i<grid.Length;i++)
                {
                    var stack=grid[i]; if(stack==null || stack.count<=0 || stack.itemValue==null || stack.itemValue.IsEmpty()) continue;
                    string payload=RebirthNativeItemCodec.Encode(stack.itemValue.Clone());
                    var candidates=records.Where(r=>(string)r.Attribute("payload")==payload && (int)r.Attribute("count")==stack.count &&
                        !used.Contains((string)r.Attribute("id"))).ToArray();
                    var prior=previous.SingleOrDefault(s=>(string)s.Attribute("area")==area && (int)s.Attribute("index")==i);
                    XElement record=prior==null?null:candidates.SingleOrDefault(r=>(string)r.Attribute("id")== (string)prior.Attribute("stack"));
                    if(record==null && candidates.Length==1) record=candidates[0];
                    if(record==null){reason="native-slot-custody-missing-or-ambiguous";return false;}
                    string id=(string)record.Attribute("id");used.Add(id);
                    byte[] bytes=Convert.FromBase64String(payload);
                    slots.Add(new XElement("slot",new XAttribute("area",area),new XAttribute("index",i),new XAttribute("stack",id),
                        new XAttribute("count",stack.count),new XAttribute("payload",payload),new XAttribute("sha256",RebirthNpcNativeReconstruction.Digest(bytes))));
                }
            }
            root.Add(slots); var controllers=new XElement("controllers");
            foreach(var f in person.ControllerFragments?.Fragments??new RebirthNpcControllerFragment[0])
            {
                if(f==null || f.Bytes==null || f.Bytes.Length==0 || f.Bytes.Length>RebirthNpcNativeReconstruction.MaximumBlobBytes) return false;
                controllers.Add(new XElement("fragment",new XAttribute("type",f.ControllerTypeId??""),new XAttribute("version",f.FragmentVersion),
                    new XAttribute("required",f.Required?1:0),new XAttribute("payload",Convert.ToBase64String(f.Bytes)),new XAttribute("sha256",f.FragmentChecksum??"")));
            }
            if(!RebirthNpcNativeHand.TryCapture(npc,slots,out var hand)){reason="native-original-hand-snapshot-unqualified";return false;}
            if(!RebirthNpcPreparedPhysicalHold.TryCaptureDesiredBodyPolicy(npc,person,out var bodyPolicy)){reason="native-original-body-policy-unqualified";return false;}
            root.Add(controllers);root.Add(header.Write());root.Add(geometry.Write());root.Add(hand.Write());root.Add(bodyPolicy.Write());
            if(!RebirthNpcNativeReconstruction.TryRead(root,out var captured) || !captured.Matches(person) ||
                !captured.TryValidateCustody(custody,inventoryRevision) || !captured.TryValidateControllers(person.ControllerFragments,supportsController)) return false;
            result=captured;reason="captured-existing-native-projection";return true;
        }
        catch(Exception ex) when(ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is InvalidOperationException || ex is FormatException || ex is System.Security.SecurityException)
        {reason="native-capture-failed:"+ex.GetType().Name;return false;}
    }
    internal static bool TryCaptureCheckpoint(RebirthNpcRuntimeState state,RebirthNpcPersistentRecord person)
    {
        var world=GameManager.Instance?.World;
        if(world==null || world.IsRemote() || state==null || person==null ||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out var id)) return false;
        var npc=world.GetEntity(id) as EntityRebirthHumanoidNPC;
        var live=npc?.RebirthRuntimeState;
        if(npc==null || !ReferenceEquals(npc.world,world) || live==null || live.StableId!=state.StableId ||
            live.Revision!=state.Revision || live.ProfileId!=state.ProfileId) return false;
        RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(state.StableId,out var custody,out var revision);
        // Required controller handlers are deliberately not inferred from fragment names.
        bool captured=TryCapture(npc,person,custody,revision,null,out var evidence,out _);
        RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(state.StableId,out var current,out var currentRevision);
        if(!ReferenceEquals(GameManager.Instance?.World,world) || !ReferenceEquals(world.GetEntity(id),npc) ||
            npc.RebirthRuntimeState?.Revision!=state.Revision || currentRevision!=revision || !ReferenceEquals(current,custody))
            throw new InvalidOperationException("NPC reconstruction source changed during checkpoint capture.");
        person.Vitals.CurrentHealth=npc.Health;
        person.Vitals.MaximumHealthAtSave=npc.GetMaxHealth();
        person.Vitals.DeathOrIncapacitationState=npc.IsDead()?"dead":"alive";
        person.Vitals.VitalsRevision=state.Revision;
        // An active changed embodiment must not retain stale reconstruction success.
        person.NativeReconstruction=captured?evidence:null;
        return captured;
    }
    private static XElement Blob(string name,Action<PooledBinaryWriter> write)
    {
        using(var stream=new BoundedCaptureStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
            {writer.SetBaseStream(stream);write(writer);writer.Flush();}
            byte[] bytes=stream.ToArray();return new XElement(name,new XAttribute("payload",Convert.ToBase64String(bytes)),
                new XAttribute("sha256",RebirthNpcNativeReconstruction.Digest(bytes)));
        }
    }
    private sealed class BoundedCaptureStream:MemoryStream
    {
        private void Bound(long length){if(length>RebirthNpcNativeReconstruction.MaximumBlobBytes)throw new InvalidDataException("NPC native reconstruction blob exceeds bound.");}
        public override void Write(byte[] buffer,int offset,int count){Bound(Position+count);base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){Bound(Position+1);base.WriteByte(value);}
        public override void SetLength(long value){Bound(value);base.SetLength(value);}
    }
}
