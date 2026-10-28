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
}
