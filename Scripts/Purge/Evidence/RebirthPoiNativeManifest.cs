using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class RebirthPoiNativeManifest
{
    private static readonly FastTags<TagGroup.Poi> RwgOnly = FastTags<TagGroup.Poi>.Parse("rwgonly");
    private static readonly FastTags<TagGroup.Poi> StreetTile = FastTags<TagGroup.Poi>.Parse("streettile");
    public readonly World World;
    public readonly PrefabInstance Prefab;
    public readonly RebirthPoiIdentity Identity;
    public readonly SleeperVolume[] Volumes;
    private readonly HashSet<SleeperVolume> volumeSet;
    private RebirthPoiNativeManifest(World world,PrefabInstance prefab,RebirthPoiIdentity identity,SleeperVolume[] volumes)
    { World=world; Prefab=prefab; Identity=identity; Volumes=volumes; volumeSet=new HashSet<SleeperVolume>(volumes); }
    // Biome metadata must not discard a native spawn/death witness merely
    // because the POI centre chunk is unloaded. Callers retain fail-closed
    // exception handling and every native room/actor/proof validation.
    private static BiomeDefinition ResolveBiome(World world,Vector3i origin,Vector3i size)
    {
        var biome=world.GetBiome(origin.x+size.x/2,origin.z+size.z/2);
        if(biome!=null)return biome;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        return provider?.GetBiomeProvider()?.GetBiomeAt(origin.x+size.x/2,origin.z+size.z/2);
    }
    public static bool TryResolve(World world,PrefabInstance prefab,out RebirthPoiNativeManifest manifest)
    {
        manifest=null;
        try
        {
            if(!ThreadManager.IsMainThread() || world==null || world.IsRemote() || GameManager.Instance==null ||
                !ReferenceEquals(GameManager.Instance.World,world) || prefab==null || prefab.prefab==null || prefab.prefab.bTraderArea) return false;
            var authored = prefab.prefab;
            if (RebirthSandboxOptionManager.Current.IsPurge && !RebirthPurgePoiEligibility.Allows(authored.PrefabName, authored.bTraderArea, authored.Tags.Test_AnySet(RwgOnly), authored.Tags.Test_AnySet(StreetTile))) return false;
            var definitions=prefab.prefab.SleeperVolumeList;
            if(definitions==null || definitions.Count<1 || definitions.Count>4096) return false;
            var mapped=new List<SleeperVolume>(); var distinct=new HashSet<SleeperVolume>();
            for(int i=0;i<definitions.Count;i++)
            {
                var definition=definitions[i];
                // Explicit no-combat role only when authoring cannot request actors/script.
                if(definition.spawnCountMax==0 && string.IsNullOrWhiteSpace(definition.minScript)) continue;
                var minimum=prefab.boundingBoxPosition+definition.startPos;
                var maximum=minimum+definition.size;
                int id=world.FindSleeperVolume(minimum,maximum); if(id<0) return false;
                var volume=world.GetSleeperVolume(id);
                if(volume==null || !ReferenceEquals(volume.prefabInstance,prefab) || !volume.BoxMin.Equals(minimum) || !volume.BoxMax.Equals(maximum) ||
                    volume.spawnCountMin!=definition.spawnCountMin || volume.spawnCountMax!=definition.spawnCountMax || volume.flags!=definition.flags ||
                    !distinct.Add(volume)) return false;
                mapped.Add(volume);
            }
            if(mapped.Count==0) return false;
            // Extra runtime combat volumes cannot be ignored merely because absent in definition.
            foreach(var volume in prefab.sleeperVolumes)
                if(volume!=null && (volume.spawnCountMax!=0 || volume.minScript!=null) && !distinct.Contains(volume)) return false;
            var origin=prefab.boundingBoxPosition; var size=prefab.boundingBoxSize;
            var biome=ResolveBiome(world,origin,size);
            if(biome==null) return false;
            var identity=new RebirthPoiIdentity(prefab.prefab.PrefabName,origin.x,origin.y,origin.z,(int)prefab.rotation,size.x,size.y,size.z,biome.m_sBiomeName);
            manifest=new RebirthPoiNativeManifest(world,prefab,identity,mapped.ToArray()); return true;
        }
        catch { return false; }
    }
    // Native integer IDs alone cannot distinguish a reconstructed/replacement room.
    public bool TryGetDescriptor(SleeperVolume volume,out string descriptor)
    {
        descriptor=null;
        try
        {
            if(volume==null||!Volumes.Contains(volume)||!StillMatches())return false;
            var definitions=Prefab.prefab.SleeperVolumeList;int matches=0;string script=string.Empty;
            for(int i=0;i<definitions.Count;i++){var d=definitions[i];if((Prefab.boundingBoxPosition+d.startPos).Equals(volume.BoxMin)&&(Prefab.boundingBoxPosition+d.startPos+d.size).Equals(volume.BoxMax)){matches++;script=d.minScript??string.Empty;}}
            if(matches!=1||script.Length>4096)return false;var f=System.Globalization.CultureInfo.InvariantCulture;
            string shape=Identity.Key+"|"+volume.BoxMin.x.ToString(f)+","+volume.BoxMin.y.ToString(f)+","+volume.BoxMin.z.ToString(f)+"|"+volume.BoxMax.x.ToString(f)+","+volume.BoxMax.y.ToString(f)+","+volume.BoxMax.z.ToString(f)+"|"+volume.flags.ToString(f)+"|"+volume.spawnCountMin.ToString(f)+"|"+volume.spawnCountMax.ToString(f)+"|"+script.Length.ToString(f)+":"+script;
            using(var hash=System.Security.Cryptography.SHA256.Create())descriptor=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(shape))).Replace("-","").ToLowerInvariant();return true;
        }
        catch{return false;}
    }
    public bool StillMatches()
    {
        // Validate against the retained mapping instead of allocating another
        // manifest, identity key, list, set and array on each scanner pulse.
        try
        {
            if(!ThreadManager.IsMainThread()||World==null||World.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,World)||Prefab?.prefab==null||Prefab.prefab.bTraderArea)return false;
            var authored=Prefab.prefab;
            if(RebirthSandboxOptionManager.Current.IsPurge&&!RebirthPurgePoiEligibility.Allows(authored.PrefabName,
                authored.bTraderArea,authored.Tags.Test_AnySet(RwgOnly),authored.Tags.Test_AnySet(StreetTile)))return false;
            var origin=Prefab.boundingBoxPosition;var size=Prefab.boundingBoxSize;
            if(origin.x!=Identity.X||origin.y!=Identity.Y||origin.z!=Identity.Z||Prefab.rotation!=Identity.Rotation||
                size.x!=Identity.SizeX||size.y!=Identity.SizeY||size.z!=Identity.SizeZ||
                RebirthPoiIdentity.Canonical(authored.PrefabName,256)!=Identity.Prefab)return false;
            var biome=ResolveBiome(World,origin,size);
            if(biome==null||RebirthPoiIdentity.Canonical(biome.m_sBiomeName,64)!=Identity.Biome)return false;
            var definitions=authored.SleeperVolumeList;
            if(definitions==null||definitions.Count<1||definitions.Count>4096)return false;
            int mapped=0;
            for(int i=0;i<definitions.Count;i++)
            {
                var definition=definitions[i];
                if(definition.spawnCountMax==0&&string.IsNullOrWhiteSpace(definition.minScript))continue;
                if(mapped>=Volumes.Length)return false;
                var minimum=origin+definition.startPos;var maximum=minimum+definition.size;
                int id=World.FindSleeperVolume(minimum,maximum);if(id<0)return false;
                var volume=World.GetSleeperVolume(id);
                if(!ReferenceEquals(volume,Volumes[mapped++])||volume==null||!ReferenceEquals(volume.prefabInstance,Prefab)||
                    !volume.BoxMin.Equals(minimum)||!volume.BoxMax.Equals(maximum)||
                    volume.spawnCountMin!=definition.spawnCountMin||volume.spawnCountMax!=definition.spawnCountMax||
                    volume.flags!=definition.flags)return false;
            }
            if(mapped==0||mapped!=Volumes.Length)return false;
            foreach(var volume in Prefab.sleeperVolumes)
                if(volume!=null&&(volume.spawnCountMax!=0||volume.minScript!=null)&&!volumeSet.Contains(volume))return false;
            return true;
        }
        catch{return false;}
    }
    // A certificate freezes the complete authoring but proves native registration
    // one room at a time. It never establishes whole-POI completion.
    internal sealed class RoomCertificate
    {
        private sealed class Shape
        {
            internal object Definition;internal Vector3i Start,Size;internal int Flags,Group;
            internal short Minimum,Maximum;internal string Script,GroupName;internal bool Used;
            internal bool Combat=>Maximum!=0||!string.IsNullOrWhiteSpace(Script);
        }
        internal readonly World World;internal readonly PrefabInstance Prefab;
        private readonly object authored,list;private readonly Vector3i origin,size;private readonly int rotation;
        private readonly string name;private readonly Shape[] shapes;
        internal RebirthPoiIdentity Identity {get;private set;}
        internal Guid Seal {get;private set;}
        internal int CombatRooms=>shapes.Count(s=>s.Combat);
        private RoomCertificate(World world,PrefabInstance prefab)
        {
            World=world;Prefab=prefab;authored=prefab.prefab;list=prefab.prefab.SleeperVolumeList;
            origin=prefab.boundingBoxPosition;size=prefab.boundingBoxSize;rotation=(int)prefab.rotation;
            name=prefab.prefab.PrefabName;
            shapes=new Shape[prefab.prefab.SleeperVolumeList.Count];
            for(int i=0;i<shapes.Length;i++)
            {
                var d=prefab.prefab.SleeperVolumeList[i];
                shapes[i]=new Shape {Definition=d,Start=d.startPos,Size=d.size,
                    Flags=d.flags,Group=d.groupId,Minimum=d.spawnCountMin,Maximum=d.spawnCountMax,
                    Script=d.minScript??string.Empty,GroupName=d.groupName,Used=d.Used};
            }
        }
        internal static bool TryCapture(World world,PrefabInstance prefab,out RoomCertificate certificate)
        {
            certificate=null;
            try
            {
                if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
                    prefab?.prefab==null||prefab.prefab.bTraderArea||prefab.prefab.SleeperVolumeList==null||
                    prefab.prefab.SleeperVolumeList.Count<1||prefab.prefab.SleeperVolumeList.Count>4096)return false;
                if(RebirthSandboxOptionManager.Current.IsPurge&&!RebirthPurgePoiEligibility.Allows(prefab.prefab.PrefabName,false,
                    prefab.prefab.Tags.Test_AnySet(RwgOnly),prefab.prefab.Tags.Test_AnySet(StreetTile)))return false;
                var value=new RoomCertificate(world,prefab);
                if(!value.AuthoredCurrent()||value.shapes.Any(s=>s.Script.Length>4096||(s.GroupName?.Length??0)>512))return false;
                RebirthPoiIdentity.Canonical(value.name,256);
                foreach(var shape in value.shapes){System.Xml.XmlConvert.VerifyXmlChars(shape.Script);if(shape.GroupName!=null)System.Xml.XmlConvert.VerifyXmlChars(shape.GroupName);}
                certificate=value;return true;
            }
            catch{return false;}
        }
        internal bool AuthoredCurrent()
        {
            try
            {
                if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,World)||World.IsRemote()||
                    !ReferenceEquals(Prefab.prefab,authored)||!ReferenceEquals(Prefab.prefab.SleeperVolumeList,list)||
                    !Prefab.boundingBoxPosition.Equals(origin)||!Prefab.boundingBoxSize.Equals(size)||(int)Prefab.rotation!=rotation||
                    Prefab.prefab.PrefabName!=name||Prefab.prefab.bTraderArea||Prefab.prefab.SleeperVolumeList.Count!=shapes.Length)return false;
                if(RebirthSandboxOptionManager.Current.IsPurge&&!RebirthPurgePoiEligibility.Allows(name,false,
                    Prefab.prefab.Tags.Test_AnySet(RwgOnly),Prefab.prefab.Tags.Test_AnySet(StreetTile)))return false;
                for(int i=0;i<shapes.Length;i++)
                {
                    var d=Prefab.prefab.SleeperVolumeList[i];var s=shapes[i];
                    if(!ReferenceEquals(d,s.Definition)||!d.startPos.Equals(s.Start)||!d.size.Equals(s.Size)||d.flags!=s.Flags||
                        d.spawnCountMin!=s.Minimum||d.spawnCountMax!=s.Maximum||d.groupId!=s.Group||d.groupName!=s.GroupName||
                        (d.minScript??string.Empty)!=s.Script||d.Used!=s.Used)return false;
                }
                return true;
            }
            catch{return false;}
        }
        internal bool BindIdentity()
        {
            if(!AuthoredCurrent())return false;
            try
            {
                var biome=ResolveBiome(World,origin,size);if(biome==null)return false;
                var current=new RebirthPoiIdentity(name,origin.x,origin.y,origin.z,rotation,size.x,size.y,size.z,biome.m_sBiomeName);
                if(Identity!=null)return current.Key==Identity.Key&&current.Biome==Identity.Biome;
                var text=new System.Text.StringBuilder("rb-observed-authoring-1|").Append(current.Key);
                var f=System.Globalization.CultureInfo.InvariantCulture;
                for(int index=0;index<shapes.Length;index++)
                {
                    var shape=shapes[index];
                    text.Append('|').Append(index.ToString(f)).Append('|').Append(Point(shape.Start)).Append('|').Append(Point(shape.Size))
                        .Append('|').Append(shape.Flags.ToString(f)).Append('|').Append(shape.Minimum.ToString(f)).Append('|').Append(shape.Maximum.ToString(f))
                        .Append('|').Append(shape.Group.ToString(f)).Append('|').Append(shape.Used?"1":"0")
                        .Append('|').Append((shape.GroupName?.Length??-1).ToString(f)).Append(':').Append(shape.GroupName)
                        .Append('|').Append(shape.Script.Length.ToString(f)).Append(':').Append(shape.Script);
                }
                Guid seal;
                using(var hash=System.Security.Cryptography.SHA256.Create())
                {
                    var bytes=hash.ComputeHash(new System.Text.UTF8Encoding(false,true).GetBytes(text.ToString()));var guid=new byte[16];Array.Copy(bytes,guid,16);
                    guid[7]=(byte)((guid[7]&15)|128);guid[8]=(byte)((guid[8]&63)|128);seal=new Guid(guid);
                }
                Identity=current;Seal=seal;
                return true;
            }
            catch{return false;}
        }
        private static string Point(Vector3i point){var f=System.Globalization.CultureInfo.InvariantCulture;return point.x.ToString(f)+","+point.y.ToString(f)+","+point.z.ToString(f);}
        internal static bool IsSealed(Guid generation)=>((generation.ToByteArray()[7]>>4)&15)==8;
        internal bool ValidateGeneration(RebirthPoiPartialObservation saved)
        {
            if(saved==null||!BindIdentity())return saved==null;
            // Historical random GUID full journals remain valid, never rekeyed.
            return IsSealed(saved.Generation)?saved.Generation==Seal:
                ((saved.Generation.ToByteArray()[7]>>4)&15)==4&&saved.Volumes.Count==CombatRooms;
        }
        private string Descriptor(Shape s)
        {
            var minimum=origin+s.Start;var maximum=minimum+s.Size;var f=System.Globalization.CultureInfo.InvariantCulture;
            string shape=Identity.Key+"|"+minimum.x.ToString(f)+","+minimum.y.ToString(f)+","+minimum.z.ToString(f)+"|"+
                maximum.x.ToString(f)+","+maximum.y.ToString(f)+","+maximum.z.ToString(f)+"|"+s.Flags.ToString(f)+"|"+
                s.Minimum.ToString(f)+"|"+s.Maximum.ToString(f)+"|"+s.Script.Length.ToString(f)+":"+s.Script;
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(shape))).Replace("-","").ToLowerInvariant();
        }
        internal bool KnownDescriptor(string descriptor)
        {return BindIdentity()&&shapes.Count(s=>s.Combat&&Descriptor(s)==descriptor)==1;}
        internal bool TryRoom(SleeperVolume volume,out int id,out string descriptor)
        {
            descriptor=null;id=-1;
            if(!AuthoredCurrent()||volume==null||!ReferenceEquals(volume.prefabInstance,Prefab))return false;
            var matching=shapes.Where(s=>s.Combat&&(origin+s.Start).Equals(volume.BoxMin)&&(origin+s.Start+s.Size).Equals(volume.BoxMax)).ToArray();
            if(matching.Length!=1)return false;var shape=matching[0];
            if(volume.flags!=shape.Flags||volume.spawnCountMin!=shape.Minimum||volume.spawnCountMax!=shape.Maximum||
                (id=World.FindSleeperVolume(volume.BoxMin,volume.BoxMax))<0||!ReferenceEquals(World.GetSleeperVolume(id),volume))return false;
            if(BindIdentity())descriptor=Descriptor(shape);
            return true;
        }
        // Registration was positively captured by NativeEvidence before this call.
        // Later biome readiness may bind its frozen descriptor even after unloading.
        internal bool TryCapturedDescriptor(SleeperVolume volume,out string descriptor)
        {
            descriptor=null;
            if(!BindIdentity()||volume==null||!ReferenceEquals(volume.prefabInstance,Prefab))return false;
            var matching=shapes.Where(s=>s.Combat&&(origin+s.Start).Equals(volume.BoxMin)&&(origin+s.Start+s.Size).Equals(volume.BoxMax)&&
                volume.flags==s.Flags&&volume.spawnCountMin==s.Minimum&&volume.spawnCountMax==s.Maximum).ToArray();
            if(matching.Length!=1)return false;descriptor=Descriptor(matching[0]);return true;
        }
    }
}


