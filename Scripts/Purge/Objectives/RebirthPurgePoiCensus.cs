using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Xml;

// Server-only authored census. It never creates or activates native sleeper rooms.
// An incomplete census is not a denominator and must not be displayed as 100%.
internal sealed class RebirthPurgePoiCensus
{
    private static readonly FastTags<TagGroup.Poi> RwgOnly = FastTags<TagGroup.Poi>.Parse("rwgonly");
    private static readonly FastTags<TagGroup.Poi> StreetTile = FastTags<TagGroup.Poi>.Parse("streettile");
    internal static readonly RebirthPurgePoiCensus Instance = new RebirthPurgePoiCensus();
    internal sealed class Entry
    {
        internal readonly RebirthPoiIdentity Identity;
        internal readonly int Tier;
        internal Entry(RebirthPoiIdentity identity,int tier) { Identity=identity; Tier=tier; }
    }
    private XmlReader source;
    private readonly Dictionary<string,int> sourceKeys=new Dictionary<string,int>(StringComparer.Ordinal);
    private string sourcePath;
    private long sourceLength;
    private DateTime sourceWriteTime;
    private bool sourceRoot;
    private int sourceCount;
    private bool sourceComplete;
    private World world;
    private DynamicPrefabDecorator decorator;
    private int cursor, expectedCount;
    private bool dirty = true;
    private double nextPulse;
    private Dictionary<string,Entry> staging = new Dictionary<string,Entry>(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string,Entry> Published { get; private set; }
    internal int Unresolved { get; private set; }
    internal bool Ready { get; private set; }
    internal long Generation { get; private set; }
    private string lastFailure;
    internal string Status => "Census ready="+Ready+", native="+expectedCount+", source="+sourceCount+
        ", processed="+cursor+", eligible="+staging.Count+", unresolved="+Unresolved+
        ", unmatched="+sourceKeys.Count+", sourceComplete="+sourceComplete+
        (lastFailure==null?string.Empty:", failure="+lastFailure);
    private static string SourceKey(string name,int x,int y,int z,int rotation,bool ground)
    { return name.Length+":"+name+":"+x+":"+y+":"+z+":"+rotation+":"+ground; }
    private bool ConsumeSource(string key)
    {
        int count;
        if(!sourceKeys.TryGetValue(key,out count))return false;
        if(count==1)sourceKeys.Remove(key);else sourceKeys[key]=count-1;
        return true;
    }
    private void Changed(PrefabInstance ignored) { Invalidate(); }
    private void Invalidate()
    {
        if(source!=null)source.Dispose(); source=null; sourcePath=null; sourceRoot=false; sourceCount=0; sourceComplete=false; sourceKeys.Clear();
        dirty=true; Ready=false; Published=null; cursor=0; Unresolved=0; lastFailure=null;
        staging=new Dictionary<string,Entry>(StringComparer.Ordinal); Generation++;
    }
    internal void Reset()
    {
        if(decorator!=null)
        {
            decorator.OnPrefabLoaded-=Changed; decorator.OnPrefabChanged-=Changed;
            decorator.OnPrefabRemoved-=Changed;
        }
        decorator=null; world=null; nextPulse=0; Invalidate();
    }
    internal void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge) { if(world!=null)Reset(); return; }
        if(!RebirthPurgePoiEligibility.Available) { if(Ready)Invalidate(); return; }
        if(!ThreadManager.IsMainThread()) return;
        var game=GameManager.Instance;
        if(game==null || game.World==null || game.World.IsRemote() || game.IsStartingGame) return;
        RebirthPoiWorldStore store;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)) return;
        var native=game.GetDynamicPrefabDecorator();
        if(native==null) return;
        if(!ReferenceEquals(world,game.World) || !ReferenceEquals(decorator,native))
        {
            Reset(); world=game.World; decorator=native;
            decorator.OnPrefabLoaded+=Changed; decorator.OnPrefabChanged+=Changed;
            decorator.OnPrefabRemoved+=Changed;
        }
        double now=(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
        if(now<nextPulse)return; nextPulse=now+0.1;
        lock(decorator.listsLock)
        {
            if(decorator.allPrefabs==null) { Invalidate(); return; }
            if(decorator.allPrefabs.Count>200000) { Invalidate(); Unresolved=1; dirty=false; return; }
            if(expectedCount!=decorator.allPrefabs.Count) { Invalidate(); expectedCount=decorator.allPrefabs.Count; }
            if(!dirty)return;
            if(!sourceComplete)
            {
                try
                {
                    if(source==null)
                    {
                        var provider=world.ChunkCache==null?null:world.ChunkCache.ChunkProvider as ChunkProviderGenerateWorld;
                        if(provider==null || !ReferenceEquals(provider.prefabDecorator,decorator))return;
                        
                        sourcePath=Path.Combine(provider.worldLocation.FullPath,"prefabs.xml");
                        var sourceInfo=new FileInfo(sourcePath); sourceLength=sourceInfo.Length; sourceWriteTime=sourceInfo.LastWriteTimeUtc;
                        source=XmlReader.Create(new FileStream(sourcePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=64000000, CloseInput=true });
                    }
                    var slice=Stopwatch.StartNew();
                    int nodes=0;
                    while(nodes++<128 && (nodes==1 || slice.ElapsedMilliseconds<4))
                    {
                        if(!source.Read()) { if(!sourceRoot)throw new FormatException(); sourceComplete=true; source.Dispose(); source=null; break; }
                        if(source.NodeType==XmlNodeType.Element && source.Depth==0)
                        { if(sourceRoot || source.Name!="prefabs" || source.NamespaceURI.Length!=0)throw new FormatException(); sourceRoot=true; }
                        if(source.NodeType==XmlNodeType.Element && source.Name=="decoration")
                        {
                            if(source.Depth!=1 || source.NamespaceURI.Length!=0)throw new FormatException();
                            string name=RebirthPoiIdentity.Canonical(source.GetAttribute("name"),256);
                            var position=Vector3i.Parse(source.GetAttribute("position"));
                            byte rotation=0; string raw=source.GetAttribute("rotation");
                            if(raw!=null && !byte.TryParse(raw,out rotation))throw new FormatException();
                            if(rotation>3)throw new FormatException();
                            bool ground=false; raw=source.GetAttribute("y_is_groundlevel");
                            if(raw!=null && !bool.TryParse(raw,out ground))throw new FormatException();
                            // RWG can repeat road decorations. Match every native occurrence,
                            // rather than rejecting the entire world's POI census.
                            string key=SourceKey(name,position.x,position.y,position.z,rotation,ground);
                            int count;sourceKeys.TryGetValue(key,out count);sourceKeys[key]=count+1;
                            if(++sourceCount>200000)throw new FormatException();
                        }
                    }
                    var currentSource=new FileInfo(sourcePath);
                    if(!currentSource.Exists || currentSource.Length!=sourceLength || currentSource.LastWriteTimeUtc!=sourceWriteTime)throw new IOException("Prefab source changed during census.");
                    if(!sourceComplete)return;
                    // Native loader may skip malformed/missing definitions. Never turn
                    // such an incomplete load into a smaller successful denominator.
                    if(sourceCount!=expectedCount) { lastFailure="Source/native prefab count mismatch"; Unresolved++; dirty=false; return; }
                }
                catch(Exception error) { lastFailure=error.GetType().Name+": "+error.Message; if(source!=null)source.Dispose(); source=null; Unresolved++; dirty=false; return; }
            }
            var nativeSlice=Stopwatch.StartNew();
            int first=cursor;
            int stop=Math.Min(expectedCount,cursor+64);
            for(;cursor<stop && (cursor==first || nativeSlice.ElapsedMilliseconds<4);cursor++)
            {
                var instance=decorator.allPrefabs[cursor];
                if(instance==null || instance.prefab==null) { lastFailure="Missing native prefab definition"; Unresolved++; continue; }
                var definition=instance.prefab;
                var nativeOrigin=instance.boundingBoxPosition;
                string nativeName;
                try { nativeName=RebirthPoiIdentity.Canonical(definition.PrefabName,256); }
                catch { lastFailure="Invalid native prefab name"; Unresolved++; continue; }
                bool direct=ConsumeSource(SourceKey(nativeName,nativeOrigin.x,nativeOrigin.y,nativeOrigin.z,(int)instance.rotation,false));
                bool ground=ConsumeSource(SourceKey(nativeName,nativeOrigin.x,nativeOrigin.y-definition.yOffset,nativeOrigin.z,(int)instance.rotation,true));
                if(direct==ground) { lastFailure="Source identity mismatch: "+nativeName; Unresolved++; continue; }
                if(!RebirthPurgePoiEligibility.Allows(definition.PrefabName, definition.bTraderArea, definition.Tags.Test_AnySet(RwgOnly), definition.Tags.Test_AnySet(StreetTile)))continue;
                var rooms=definition.SleeperVolumeList;
                if(rooms==null || rooms.Count>4096) { lastFailure="Missing/oversized authored rooms: "+nativeName; Unresolved++; continue; }
                bool combat=false;
                for(int n=0;n<rooms.Count;n++)
                    if(rooms[n].spawnCountMax!=0 || !string.IsNullOrWhiteSpace(rooms[n].minScript)) { combat=true; break; }
                if(!combat)continue;
                try
                {
                    var origin=instance.boundingBoxPosition; var size=instance.boundingBoxSize;
                    // World.GetBiome reads only loaded chunks. The authored world map
                    // resolves the complete census without forcing distant chunks to load.
                    var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
                    var biome=provider?.GetBiomeProvider()?.GetBiomeAt(origin.x+size.x/2,origin.z+size.z/2);
                    if(biome==null) { lastFailure="Missing biome: "+nativeName; Unresolved++; continue; }
                    var identity=new RebirthPoiIdentity(definition.PrefabName,origin.x,origin.y,origin.z,(int)instance.rotation,size.x,size.y,size.z,biome.m_sBiomeName);
                    if(staging.ContainsKey(identity.Key)) { lastFailure="Duplicate eligible identity: "+nativeName; Unresolved++; continue; }
                    staging.Add(identity.Key,new Entry(identity,definition.DifficultyTier));
                }
                catch(Exception error) { lastFailure="Identity "+nativeName+": "+error.GetType().Name+": "+error.Message; Unresolved++; }
            }
            try { var info=new FileInfo(sourcePath); if(!info.Exists || info.Length!=sourceLength || info.LastWriteTimeUtc!=sourceWriteTime) { Unresolved++; dirty=false; return; } }
            catch { Unresolved++; dirty=false; return; }
            if(cursor!=expectedCount)return;
            dirty=false;
            // Empty manifest can mean absent/missing prefabs.xml. Do not claim completion.
            if(Unresolved!=0 || expectedCount==0 || sourceKeys.Count!=0)return;
            Published=new ReadOnlyDictionary<string,Entry>(staging); Ready=true;
        }
    }

    // Finish the first loaded-world census before exposing its HUD. Normal
    // later updates retain their existing sliced budgets; no new save/cache.
    internal void InitializeLoadedWorld()
    {
        var start=Stopwatch.StartNew();
        while(!Ready&&start.ElapsedMilliseconds<100)
        {
            int beforeCursor=cursor,beforeSource=sourceCount;
            nextPulse=0;
            Pulse();
            if(!dirty||beforeCursor==cursor&&beforeSource==sourceCount)break;
        }
    }
}
