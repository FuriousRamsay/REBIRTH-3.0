from pathlib import Path
root=Path(__file__).resolve().parents[3]
out=Path(__file__).resolve().parent
names=['Clear','RegisterFarmPlot','UnregisterFarmPlot','PublishPendingSnapshot','RebuildCoarseInfluenceSnapshotLocked','MakeChunkKey','SamePosition']
def extract(s,name):
    import re
    m=re.search(r'    (?:public|private) static [^\n]+ '+name+r'\(',s)
    start=m.start(); brace=s.index('{',m.end()); depth=1; end=brace+1
    while depth:
        depth += (s[end]=='{')-(s[end]=='}'); end+=1
    return s[start:end]
for label,path in [('Before','_Documentation/PerformanceAudit_20261010/before_farm_chunk_expansion/AdvancedFarmingActiveAreaRegistry.cs'),('After','Scripts/AdvancedFarming/AdvancedFarmingActiveAreaRegistry.cs')]:
    s=(root/path).read_text(encoding='utf-8')
    fields=s[s.index('    private static readonly object Sync'):s.index('    public static bool HasAnyFarmPlots')]
    body='\n'.join(extract(s,n) for n in names)
    access='public static HashSet<long> Coarse => s_coarseInfluenceChunks; public static HashSet<Vector3i> Positions => s_farmPlotPositionSnapshot; public static void Rebuild()=>RebuildCoarseInfluenceSnapshotLocked();'
    (out/(label+'.cs')).write_text('using System; using System.Collections.Generic;\nstatic class '+label+' {\n'+fields+body+access+'}',encoding='utf-8')
