using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#nullable disable

public enum RebirthOreSenseType : byte { Unknown=0,Iron=1,Lead=2,Coal=3,Nitrate=4,OilShale=5 }

public sealed class RebirthOreSenseEntry
{
    public Vector3i Position;public RebirthOreSenseType Type;public string BlockName=string.Empty;public float Distance;
}

/// <summary>Local presentation-only Miner Ore Sense. No entity/container/cave query is performed.</summary>
public static class RebirthOreSenseService
{
    public const string BonusId="background_bonus.ore_sense";
    private const int ScanBudgetPerFrame=96;
    private const int MaxVisualMarkers=64;
    private static readonly List<Vector3i> Offsets=new List<Vector3i>();
    private static readonly Dictionary<Vector3i,RebirthOreSenseEntry> Cache=new Dictionary<Vector3i,RebirthOreSenseEntry>();
    private static readonly List<LineRenderer> Markers=new List<LineRenderer>();
    private static readonly Vector3[] CubePositions=new Vector3[16];
    private static Material lineMaterial;
    private static bool installed,active,offsetsReady,hasCenter;
    private static Vector3i center;
    private static int scanIndex,scanPasses,blocksScanned,cacheHits;
    private static float nextVisualRefresh,nextScanCycle;
    private static string nearestType=string.Empty;
    private static float nearestDistance;

    public static string Install()
    {
        if(installed)return "[REBIRTH Ore Sense] already installed";installed=true;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Ore Sense] installed budget="+ScanBudgetPerFrame+" markers="+MaxVisualMarkers;
    }

    public static bool Active=>active;
    public static int CachedCount=>Cache.Count;
    public static int BlocksScanned=>blocksScanned;
    public static string NearestType=>nearestType;
    public static float NearestDistance=>nearestDistance;
    public static float Range=>Mathf.Clamp(RebirthResourceSignatureService.GetTuning(BonusId,"range_meters",10f),8f,12f);

    public static bool TryToggle(EntityPlayerLocal player)
    {
        if(player==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,BonusId)){Deactivate();return false;}
        active=!active;if(!active)ClearVisuals();else ResetScan(World.worldToBlockPos(player.position));return true;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(GameManager.Instance==null||GameManager.Instance.World==null)return;World world=GameManager.Instance.World;EntityPlayerLocal player=world.GetPrimaryPlayer();
        if(player==null||!active)return;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,BonusId)){Deactivate();return;}
        EnsureOffsets();Vector3i p=World.worldToBlockPos(player.position);if(!hasCenter||DistanceSq(center,p)>=4)ResetScan(p);
        float now=Time.realtimeSinceStartup;float range=Range;float r2=range*range;
        int budget=now>=nextScanCycle?ScanBudgetPerFrame:0;
        while(budget-->0&&scanIndex<Offsets.Count)
        {
            Vector3i at=center+Offsets[scanIndex++];blocksScanned++;BlockValue bv=world.GetBlock(at);RebirthOreSenseType type;string name;
            if(TryClassify(bv,out type,out name)){float d=(at.ToVector3()+new Vector3(.5f,.5f,.5f)-player.position).magnitude;if(d<=range){Cache[at]=new RebirthOreSenseEntry{Position=at,Type=type,BlockName=name,Distance=d};cacheHits++;}}
        }
        if(scanIndex>=Offsets.Count){scanPasses++;scanIndex=0;nextScanCycle=now+0.75f;Prune(world,player.position,r2);}
        if(now>=nextVisualRefresh){nextVisualRefresh=now+0.12f;RefreshVisuals(world,player.position,range);}
    }

    private static void EnsureOffsets()
    {
        if(offsetsReady)return;offsetsReady=true;int r=12;for(int y=-r;y<=r;y++)for(int z=-r;z<=r;z++)for(int x=-r;x<=r;x++){int d=x*x+y*y+z*z;if(d<=r*r)Offsets.Add(new Vector3i(x,y,z));}
        Offsets.Sort((a,b)=>(a.x*a.x+a.y*a.y+a.z*a.z).CompareTo(b.x*b.x+b.y*b.y+b.z*b.z));
    }
    private static void ResetScan(Vector3i p){center=p;hasCenter=true;scanIndex=0;nextScanCycle=0f;Cache.Clear();nearestType=string.Empty;nearestDistance=0f;ClearVisuals();}
    private static int DistanceSq(Vector3i a,Vector3i b){int x=a.x-b.x,y=a.y-b.y,z=a.z-b.z;return x*x+y*y+z*z;}
    private static void Prune(World world,Vector3 pos,float r2){List<Vector3i> remove=new List<Vector3i>();foreach(KeyValuePair<Vector3i,RebirthOreSenseEntry> kv in Cache){Vector3 d=kv.Key.ToVector3()+new Vector3(.5f,.5f,.5f)-pos;if(d.sqrMagnitude>r2){remove.Add(kv.Key);continue;}RebirthOreSenseType t;string n;if(!TryClassify(world.GetBlock(kv.Key),out t,out n))remove.Add(kv.Key);}for(int i=0;i<remove.Count;i++)Cache.Remove(remove[i]);}

    private static bool TryClassify(BlockValue bv,out RebirthOreSenseType type,out string name)
    {
        type=RebirthOreSenseType.Unknown;name=string.Empty;if(bv.isair||bv.Block==null)return false;name=bv.Block.GetBlockName()??string.Empty;string n=name.ToLowerInvariant();
        bool oreWord=n.Contains("ore")||n.Contains("oilshale")||n.Contains("oil_shale")||n.Contains("shale");if(!oreWord)return false;
        if(n.Contains("iron"))type=RebirthOreSenseType.Iron;else if(n.Contains("lead"))type=RebirthOreSenseType.Lead;else if(n.Contains("coal"))type=RebirthOreSenseType.Coal;else if(n.Contains("nitrate")||n.Contains("potassium"))type=RebirthOreSenseType.Nitrate;else if(n.Contains("shale")||n.Contains("oil"))type=RebirthOreSenseType.OilShale;else return false;return true;
    }

    private static void RefreshVisuals(World world,Vector3 playerPos,float range)
    {
        List<RebirthOreSenseEntry> entries=new List<RebirthOreSenseEntry>();foreach(RebirthOreSenseEntry e in Cache.Values){e.Distance=(e.Position.ToVector3()+new Vector3(.5f,.5f,.5f)-playerPos).magnitude;if(e.Distance<=range)entries.Add(e);}entries.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
        nearestType=entries.Count>0?Friendly(entries[0].Type):string.Empty;nearestDistance=entries.Count>0?entries[0].Distance:0f;int count=Math.Min(MaxVisualMarkers,entries.Count);EnsureMarkerCount(count);
        for(int i=0;i<Markers.Count;i++){LineRenderer lr=Markers[i];bool show=i<count;if(lr==null)continue;lr.enabled=show;if(!show)continue;RebirthOreSenseEntry e=entries[i];float edge=Mathf.Clamp01((range-e.Distance)/Mathf.Max(1f,range*.35f));Color c=ColorFor(e.Type);c.a=Mathf.Lerp(.10f,.82f,edge);lr.startColor=c;lr.endColor=c;SetCube(lr,e.Position.ToVector3()+new Vector3(.5f,.5f,.5f),.46f);}
    }
    private static void EnsureMarkerCount(int count){while(Markers.Count<count){GameObject go=new GameObject("REBIRTH Ore Sense Marker");UnityEngine.Object.DontDestroyOnLoad(go);LineRenderer lr=go.AddComponent<LineRenderer>();lr.useWorldSpace=true;lr.positionCount=16;lr.startWidth=.025f;lr.endWidth=.025f;lr.numCapVertices=0;lr.numCornerVertices=0;if(lineMaterial==null){Shader s=Shader.Find("Hidden/Internal-Colored");if(s==null)s=Shader.Find("Sprites/Default");if(s!=null){lineMaterial=new Material(s);lineMaterial.hideFlags=HideFlags.HideAndDontSave;if(lineMaterial.HasProperty("_SrcBlend"))lineMaterial.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);if(lineMaterial.HasProperty("_DstBlend"))lineMaterial.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);if(lineMaterial.HasProperty("_Cull"))lineMaterial.SetInt("_Cull",(int)UnityEngine.Rendering.CullMode.Off);if(lineMaterial.HasProperty("_ZWrite"))lineMaterial.SetInt("_ZWrite",0);if(lineMaterial.HasProperty("_ZTest"))lineMaterial.SetInt("_ZTest",(int)UnityEngine.Rendering.CompareFunction.Always);}}if(lineMaterial!=null)lr.material=lineMaterial;Markers.Add(lr);}}
    private static void SetCube(LineRenderer l,Vector3 c,float h)
    {
        // GameUpdate owns this scratch array; SetPositions submits the complete geometry.
        CubePositions[0]=c+new Vector3(-h,-h,-h);
        CubePositions[1]=c+new Vector3(h,-h,-h);
        CubePositions[2]=c+new Vector3(h,-h,h);
        CubePositions[3]=c+new Vector3(-h,-h,h);
        CubePositions[4]=c+new Vector3(-h,-h,-h);
        CubePositions[5]=c+new Vector3(-h,h,-h);
        CubePositions[6]=c+new Vector3(h,h,-h);
        CubePositions[7]=c+new Vector3(h,-h,-h);
        CubePositions[8]=c+new Vector3(h,h,-h);
        CubePositions[9]=c+new Vector3(h,h,h);
        CubePositions[10]=c+new Vector3(h,-h,h);
        CubePositions[11]=c+new Vector3(h,h,h);
        CubePositions[12]=c+new Vector3(-h,h,h);
        CubePositions[13]=c+new Vector3(-h,-h,h);
        CubePositions[14]=c+new Vector3(-h,h,h);
        CubePositions[15]=c+new Vector3(-h,h,-h);
        l.SetPositions(CubePositions);
    }
    private static Color ColorFor(RebirthOreSenseType t){switch(t){case RebirthOreSenseType.Iron:return new Color(.68f,.34f,.18f,1f);case RebirthOreSenseType.Lead:return new Color(.42f,.52f,.66f,1f);case RebirthOreSenseType.Coal:return new Color(.24f,.24f,.27f,1f);case RebirthOreSenseType.Nitrate:return new Color(.78f,.72f,.48f,1f);case RebirthOreSenseType.OilShale:return new Color(.34f,.22f,.38f,1f);default:return Color.white;}}
    private static string Friendly(RebirthOreSenseType t){switch(t){case RebirthOreSenseType.Iron:return "Iron";case RebirthOreSenseType.Lead:return "Lead";case RebirthOreSenseType.Coal:return "Coal";case RebirthOreSenseType.Nitrate:return "Nitrate";case RebirthOreSenseType.OilShale:return "Oil Shale";default:return string.Empty;}}
    private static void ClearVisuals(){for(int i=0;i<Markers.Count;i++)if(Markers[i]!=null)Markers[i].enabled=false;}
    private static void Deactivate(){active=false;hasCenter=false;Cache.Clear();scanIndex=0;nextScanCycle=0f;nearestType=string.Empty;nearestDistance=0f;ClearVisuals();}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){Deactivate();DestroyMarkers();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){Deactivate();DestroyMarkers();}
    private static void DestroyMarkers(){for(int i=0;i<Markers.Count;i++)if(Markers[i]!=null)UnityEngine.Object.Destroy(Markers[i].gameObject);Markers.Clear();if(lineMaterial!=null)UnityEngine.Object.Destroy(lineMaterial);lineMaterial=null;}

    public static string BuildDebugReport(){StringBuilder b=new StringBuilder();b.Append("active=").Append(active).Append(" range=").Append(Range.ToString("0.0",CultureInfo.InvariantCulture)).Append(" cached=").Append(Cache.Count).Append(" scanIndex=").Append(scanIndex).Append('/').Append(Offsets.Count).Append(" passes=").Append(scanPasses).Append(" blocksScanned=").Append(blocksScanned).Append(" cacheHits=").Append(cacheHits).Append(" markers=").Append(Markers.Count).Append(" nearest=").Append(nearestType).Append('@').Append(nearestDistance.ToString("0.0",CultureInfo.InvariantCulture));return b.ToString();}
}
