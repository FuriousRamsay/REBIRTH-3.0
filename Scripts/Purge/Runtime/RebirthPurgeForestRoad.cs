using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Reads native authored biome/surface/height providers before destination chunks
// exist. No terrain painting or settlement relocation is performed.
internal static class RebirthPurgeForestRoad
{
    private static World cachedWorld;private static SpawnPosition cached;private static bool found;
    private static WorldGridCompressedData<byte> roads;
    private static readonly List<Rect> exclusions=new List<Rect>();
    // A bounded failed attempt is retried by the starter coordinator's five-
    // second admission timer, rather than caching failure for the entire world.
    internal static void RetryFailedSearch(World world)
    {if(ReferenceEquals(cachedWorld,world)&&!found)cachedWorld=null;}
    internal static bool Find(World world,out SpawnPosition position)
    {
        position=SpawnPosition.Undef;
        if(ReferenceEquals(cachedWorld,world)){position=cached;return found;}
        cachedWorld=world;found=false;cached=SpawnPosition.Undef;
        var provider=world?.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        var biomes=provider?.GetBiomeProvider() as WorldBiomeProviderFromImage;
        var terrain=provider?.m_TerrainGenerator;
        roads=(provider as ChunkProviderGenerateWorldFromRaw)?.poiFromImage?.m_Poi;
        if(biomes==null||terrain==null||roads==null||biomes.worldSize<512){Log.Warning("[PurgeTruckStart] step=road.providers waiting biome="+(biomes!=null)+" terrain="+(terrain!=null)+" roads="+(roads!=null));cachedWorld=null;return false;}
        var timer=System.Diagnostics.Stopwatch.StartNew();int forest=0,road=0,settlement=0,run=0,slope=0,width=0;
        Log.Out("[PurgeTruckStart] step=road.search.begin size="+biomes.worldSize+" provider="+provider.GetType().Name);
        var buildings=exclusions;buildings.Clear();
        var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
        if(decorator?.allPrefabs!=null)foreach(var prefab in decorator.allPrefabs)
        {
            var size=prefab.boundingBoxSize;if(size.x<8||size.z<8||size.y<5)continue;
            var at=prefab.boundingBoxPosition;buildings.Add(new Rect(at.x-200,at.z-200,size.x+400,size.z+400));
        }
        int margin=192,half=biomes.worldSizeHalf;var random=new System.Random();
        for(int attempt=0;attempt<500000;attempt++)
        {
            int x=random.Next(-half+margin,half-margin),z=random.Next(-half+margin,half-margin);
            if((attempt&255)==0&&timer.ElapsedMilliseconds>3000)break;
            if(!Forest(biomes,x,z))continue;forest++;
            if(!Road(biomes,x,z))continue;road++;
            if(buildings.Any(b=>b.Contains(new Vector2(x,z)))){settlement++;continue;}
            for(int angle=0;angle<8;angle++)
            {
                float radians=angle*Mathf.PI/8;var forward=new Vector2(Mathf.Sin(radians),Mathf.Cos(radians));var side=new Vector2(forward.y,-forward.x);
                if(!RoadRun(biomes,new Vector2(x,z),forward,30))continue;run++;
                float left=Width(biomes,new Vector2(x,z),-side),right=Width(biomes,new Vector2(x,z),side);
                if(left+right<3||left+right>20){width++;continue;}
                var centre=new Vector2(x,z)+side*(right-left)*.5f;
                if(buildings.Any(b=>b.Contains(centre)))continue;
                bool clear=true;float low=float.MaxValue,high=float.MinValue;
                for(int along=-6;along<=6;along+=3)for(int across=-2;across<=2;across+=2)
                {
                    var at=centre+forward*along+side*(across*.6f);int px=Mathf.RoundToInt(at.x),pz=Mathf.RoundToInt(at.y);
                    if(!Road(biomes,px,pz)||!Forest(biomes,px,pz)){clear=false;break;}
                    float height=terrain.GetTerrainHeightAt(px,pz);low=Mathf.Min(low,height);high=Mathf.Max(high,height);
                }
                if(!clear||high-low>.45f){slope++;continue;}
                // Surroundings must remain forest beyond both ends and road shoulders.
                foreach(var offset in new[]{forward*50,-forward*50,side*25,-side*25})
                    if(!Forest(biomes,Mathf.RoundToInt(centre.x+offset.x),Mathf.RoundToInt(centre.y+offset.y)))clear=false;
                if(!clear)continue;
                float y=terrain.GetTerrainHeightAt(Mathf.RoundToInt(centre.x),Mathf.RoundToInt(centre.y));
                var candidate=new SpawnPosition(new Vector3(centre.x,y+.5f,centre.y),angle*22.5f);
                if(!PlayerAnchor(world,candidate,out _))continue;
                cached=candidate;found=true;position=cached;
                Log.Out("[PurgeTruckStart] step=road.search.selected position="+cached.position+" heading="+cached.heading+" elapsedMs="+timer.ElapsedMilliseconds+" attempts="+(attempt+1));return true;
            }
        }
        Log.Error("[PurgeTruckStart] step=road.search.failed elapsedMs="+timer.ElapsedMilliseconds+" forest="+forest+" road="+road+" settlementRejected="+settlement+" straightRuns="+run+" widthRejected="+width+" footprintRejected="+slope);return false;
    }
    internal static bool Nearby(World world,Vector3 origin,float heading,float offset,out SpawnPosition candidate)
    {
        candidate=SpawnPosition.Undef;
        if(!ReferenceEquals(cachedWorld,world)||roads==null)return false;
        var provider=world.ChunkCache.ChunkProvider as ChunkProviderGenerateWorld;
        var map=provider?.GetBiomeProvider() as WorldBiomeProviderFromImage;
        var forward=Quaternion.Euler(0,heading,0)*Vector3.forward;var point=origin+forward*offset;
        if(map==null||exclusions.Any(b=>b.Contains(new Vector2(point.x,point.z))))return false;
        for(int along=-6;along<=6;along+=2)for(int across=-1;across<=1;across++)
        {var at=point+forward*along+Quaternion.Euler(0,heading,0)*Vector3.right*across*1.4f;
            if(!Forest(map,Mathf.RoundToInt(at.x),Mathf.RoundToInt(at.z))||!Road(map,Mathf.RoundToInt(at.x),Mathf.RoundToInt(at.z)))return false;}
        point.y=provider.m_TerrainGenerator.GetTerrainHeightAt(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.z))+.5f;
        candidate=new SpawnPosition(point,heading);return true;
    }
    internal static bool PlayerAnchor(World world,SpawnPosition truck,out SpawnPosition player)
    {
        player=SpawnPosition.Undef;
        var provider=world?.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        var map=provider?.GetBiomeProvider() as WorldBiomeProviderFromImage;
        if(map==null||provider.m_TerrainGenerator==null)return false;
        // The truck occupies +/-7m along the qualified road. Keep the unseated
        // character completely outside it while native spawn/creation finishes.
        var point=truck.position-(Quaternion.Euler(0,truck.heading,0)*Vector3.forward)*10;
        float low=float.MaxValue,high=float.MinValue;
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
        {
            int px=Mathf.RoundToInt(point.x)+x,pz=Mathf.RoundToInt(point.z)+z;
            if(!Forest(map,px,pz)||!Road(map,px,pz))return false;
            float height=provider.m_TerrainGenerator.GetTerrainHeightAt(px,pz);
            low=Mathf.Min(low,height);high=Mathf.Max(high,height);
        }
        if(high-low>.45f)return false;
        point.y=provider.m_TerrainGenerator.GetTerrainHeightAt(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.z))+.5f;
        player=new SpawnPosition(point,truck.heading);return true;
    }
    private static bool Forest(WorldBiomeProviderFromImage map,int x,int z)=>map.GetBiomeAt(x,z)?.m_sBiomeName=="pine_forest"&&map.GetRadiationAt(x,z)<=0;
    private static bool Road(WorldBiomeProviderFromImage map,int x,int z)
    {
        // Native terrain splats describe ground materials, not road overlays.
        // The native POI map is what paints asphalt/gravel during chunk generation.
        string name=roads!=null&&roads.Contains(x,z)?cachedWorld.Biomes.getPoiForColor(roads.GetData(x,z))?.m_BlockValue.Block?.GetBlockName():null;
        return name!=null&&(name.IndexOf("asphalt",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("gravel",StringComparison.OrdinalIgnoreCase)>=0);
    }
    private static bool RoadRun(WorldBiomeProviderFromImage map,Vector2 centre,Vector2 direction,int length)
    {for(int step=-length;step<=length;step+=3){var at=centre+direction*step;if(!Road(map,Mathf.RoundToInt(at.x),Mathf.RoundToInt(at.y)))return false;}return true;}
    private static float Width(WorldBiomeProviderFromImage map,Vector2 centre,Vector2 direction)
    {for(int step=1;step<=16;step++){var at=centre+direction*step;if(!Road(map,Mathf.RoundToInt(at.x),Mathf.RoundToInt(at.y)))return step-.5f;}return 17;}
}
