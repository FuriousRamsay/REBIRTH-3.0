using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Main-map presentation only. Reads existing player map records; never requests
// chunks, reveals terrain, changes map storage, or touches minimap rendering.
internal sealed class RebirthMapOverview
{
    internal const int TextureSize=1024, MaxSlotsPerUpdate=2048, MaxStaticPixelsPerUpdate=16384;
    internal const float MetersPerUnit=.471910119f;
    private struct Region { internal Vector2i Position; internal MapChunkDatabaseByRegion.RegionData Data; }
    private readonly List<Region> regions=new List<Region>();
    private readonly List<KeyValuePair<int,ushort[]>> legacy=new List<KeyValuePair<int,ushort[]>>();
    private Texture2D texture;
    private Color32[] pixels;
    private byte[] visited;
    private readonly List<int> filled=new List<int>();
    private byte generation;
    private object world, player;
    private IMapChunkDatabase database;
    private Color32[] staticSource;
    private bool staticMode, active, building, decoding, changed, published;
    private int originX,originZ,span,regionIndex,slotIndex,legacyIndex,staticIndex,cleanupIndex;
    private float nextRefresh;
    private int displayedOriginX,displayedOriginZ,displayedSpan;

    internal static float NativeMaximum(int width,int height)=>2048f/336f*Mathf.Min(1f,(float)Mathf.Max(1,height)/Mathf.Max(1,width));
    internal static float MaximumZoom(int width,int height)
    {
        int shorter=Mathf.Max(1,Mathf.Min(width,height)),longer=Mathf.Max(1,Mathf.Max(width,height));
        return Mathf.Min(16384f/(shorter*MetersPerUnit),32768f/(longer*MetersPerUnit));
    }
    internal static bool Supported(XUiC_RebirthMapArea map)
    {
        if(map.showStaticData)return map.staticWorldTexture!=null;
        var db=map.localPlayer?.ChunkObserver?.mapDatabase;
        return db is MapChunkDatabaseByRegion||db is MapChunkDatabase;
    }
    internal static float Limit(XUiC_MapArea map)
    {
        var full=map as XUiC_RebirthMapArea;
        return full==null?6.15f:full.WorldFitMaximum(Supported(full)?MaximumZoom(full.ActiveCanvasWidth,full.ActiveCanvasHeight):NativeMaximum(full.ActiveCanvasWidth,full.ActiveCanvasHeight));
    }
    internal static int AtlasSpan(float widthMeters,float heightMeters)
    { return Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Max(widthMeters,heightMeters)*1.5f)),4096,65536); }

    internal void Update(XUiC_RebirthMapArea map)
    {
        if(!map.isOpen||!map.bMapInitialized||map.xuiTexture==null)return;
        var db=map.localPlayer?.ChunkObserver?.mapDatabase;
        object currentWorld=map.localPlayer?.world;
        if(!ReferenceEquals(world,currentWorld)||!ReferenceEquals(player,map.localPlayer)||!ReferenceEquals(database,db))
        { Close(map);world=currentWorld;player=map.localPlayer;database=db; }
        bool wanted=Supported(map)&&map.zoomScale>NativeMaximum(map.ActiveCanvasWidth,map.ActiveCanvasHeight);
        active=wanted;
        if(!Supported(map)){Bind(map,map.mapTexture);return;}
        if(texture==null)
        {
            texture=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,false){name="REBIRTH known-map overview",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
            pixels=new Color32[TextureSize*TextureSize];visited=new byte[pixels.Length];
        }
        float now=Time.realtimeSinceStartup;
        int desired=AtlasSpan(map.ActiveCanvasWidth*MetersPerUnit*map.zoomScale,map.ActiveCanvasHeight*MetersPerUnit*map.zoomScale);
        // Keep a generous cached margin. Panning inside it never snapshots or
        // rebuilds the atlas; zoom changes only reframe at power-of-two levels.
        float halfX=map.ActiveCanvasWidth*MetersPerUnit*map.zoomScale*.5f;
        float halfZ=map.ActiveCanvasHeight*MetersPerUnit*map.zoomScale*.5f;
        bool moved=span==0||map.mapMiddlePosPixel.x-halfX<originX||map.mapMiddlePosPixel.x+halfX>originX+span
            ||map.mapMiddlePosPixel.y-halfZ<originZ||map.mapMiddlePosPixel.y+halfZ>originZ+span;
        bool modeChanged=staticMode!=map.showStaticData||(map.showStaticData&&!ReferenceEquals(staticSource,map.staticWorldTexture));
        // A mode/source change may contain different permitted data. Never
        // retain static-world pixels when returning to the explored map.
        if(modeChanged)published=false;
        if((!building&&(moved||desired!=span))||modeChanged)
        {
            span=desired;
            // Center exactly on a chunk boundary; unlike truncation this also
            // preserves the correct side of zero for negative map coordinates.
            originX=Mathf.FloorToInt(map.mapMiddlePosPixel.x/16f)*16-span/2;
            originZ=Mathf.FloorToInt(map.mapMiddlePosPixel.y/16f)*16-span/2;
            Begin(map,true);
        }
        else if(!building&&now>=nextRefresh)Begin(map,false);
        
        if(building)Build(map);
        // Publish pixels and their coordinate frame together only after the
        // bounded rebuild completes. The existing GPU texture stays untouched
        // while the CPU buffer is cleared/rebuilt; no empty/progressive frames.
        if(!building&&(changed||!published))
        {
            texture.SetPixels32(pixels);texture.Apply(false,false);changed=false;
            displayedOriginX=originX;displayedOriginZ=originZ;displayedSpan=span;published=true;
        }
        var shown=wanted&&published?texture:map.mapTexture;
        Bind(map,shown);
    }

    private static void Bind(XUiC_RebirthMapArea map,Texture2D shown)
    {
        if(map.xuiTexture.Texture==shown)return;
        map.xuiTexture.Texture=shown;
        // Texture's native setter only marks dirty. Our update runs after the
        // child view update, so commit the actual UITexture before UV globals
        // change in OnPreRender. No second input/controller update is run.
        map.xuiTexture.updateData();
    }

    private void Begin(XUiC_RebirthMapArea map,bool reframe)
    {
        // Refresh existing content sparsely. Only a new frame clears the atlas;
        // stable known-map passes neither blank it nor upload identical pixels.
        if(reframe){Array.Clear(pixels,0,pixels.Length);Array.Clear(visited,0,visited.Length);filled.Clear();generation=0;}
        if(++generation==0){Array.Clear(visited,0,visited.Length);generation=1;}
        regions.Clear();legacy.Clear();regionIndex=slotIndex=legacyIndex=staticIndex=0;
        staticMode=map.showStaticData;staticSource=map.staticWorldTexture;
        if(!staticMode)
        {
            var regional=database as MapChunkDatabaseByRegion;
            if(regional!=null)
            {
                // Same lock as native GetMapColors/Contains. Copy references,
                // never clone chunk arrays or hold this lock while rendering.
                lock(regional.m_regionsLock)foreach(var pair in regional.m_regions)
                    if(Intersects(pair.Key.x*512,pair.Key.y*512,512))regions.Add(new Region{Position=pair.Key,Data=pair.Value});
            }
            else
            {
                var old=database as MapChunkDatabase;
                if(old!=null)lock(old.catalog)foreach(var pair in old.database.Dict)
                {int x,z;IMapChunkDatabase.FromChunkDBKey(pair.Key,out x,out z);if(Intersects(x*16,z*16,16))legacy.Add(pair);}
            }
        }
        building=decoding=true;cleanupIndex=0;changed|=reframe;

    }
    private bool Intersects(int x,int z,int size)=>x+size>originX&&z+size>originZ&&x<originX+span&&z<originZ+span;
    private void Build(XUiC_RebirthMapArea map)
    {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();int work=0;
        while(work++<(staticMode?MaxStaticPixelsPerUpdate:MaxSlotsPerUpdate))
        {
            if((work&31)==0&&(System.Diagnostics.Stopwatch.GetTimestamp()-started)>System.Diagnostics.Stopwatch.Frequency/1000)break;
            if(!decoding)
            {
                if(cleanupIndex>=filled.Count){Complete();break;}
                int index=filled[cleanupIndex];
                if(visited[index]!=generation)
                {pixels[index]=default;changed=true;filled[cleanupIndex]=filled[filled.Count-1];filled.RemoveAt(filled.Count-1);}
                else cleanupIndex++;
                continue;
            }
            if(staticMode)
            {
                if(staticIndex>=pixels.Length){decoding=false;continue;}
                int index=staticIndex++,px=index%TextureSize,pz=index/TextureSize;
                int x=originX+px*(span/TextureSize)+span/TextureSize/2,z=originZ+pz*(span/TextureSize)+span/TextureSize/2;
                if(px==0||pz==0||px==TextureSize-1||pz==TextureSize-1||staticSource==null)continue;
                if(x<map.staticMapLeft||x>map.staticMapRight||z<map.staticMapBottom||z>map.staticMapTop)continue;
                long source=(long)(z-map.staticMapBottom)*map.staticWorldWidth+x-map.staticMapLeft;
                if(source>=0&&source<staticSource.Length)
                {var color=staticSource[source];if(color.a>0)Write(px,pz,color);}
            }
            else if(database is MapChunkDatabaseByRegion)
            {
                if(regionIndex>=regions.Count){decoding=false;continue;}
                var region=regions[regionIndex];int sx=slotIndex%32,sz=slotIndex/32;
                var rows=region.Data?.m_regionData;
                var row=rows!=null&&sz<rows.Length?rows[sz]:null;
                var colors=row!=null&&sx<row.Length?row[sx]:null;
                DrawChunk(region.Position.x*512+sx*16,region.Position.y*512+sz*16,colors);
                if(++slotIndex==1024){slotIndex=0;regionIndex++;}
            }
            else
            {
                if(legacyIndex>=legacy.Count){decoding=false;continue;}
                var pair=legacy[legacyIndex++];int x,z;IMapChunkDatabase.FromChunkDBKey(pair.Key,out x,out z);DrawChunk(x*16,z*16,pair.Value);
            }
        }
    }
    private void Complete(){building=false;nextRefresh=staticMode?float.PositiveInfinity:Time.realtimeSinceStartup+2f;regions.Clear();legacy.Clear();}
    private void DrawChunk(int x,int z,ushort[] colors)
    {
        if(colors==null||colors.Length<256||!Intersects(x,z,16))return;
        int cell=span/TextureSize;
        if(cell<=16)
        {
            for(int dz=0;dz<16;dz+=cell)for(int dx=0;dx<16;dx+=cell)
                Write((x+dx-originX)/cell,(z+dz-originZ)/cell,Utils.FromColor5To32(colors[(dz+cell/2)*16+dx+cell/2]));
        }
        else Write(Mathf.FloorToInt((x-originX)/(float)cell),Mathf.FloorToInt((z-originZ)/(float)cell),Utils.FromColor5To32(colors[136]));
    }
    private void Write(int x,int z,Color32 color)
    {
        if(x<=0||z<=0||x>=TextureSize-1||z>=TextureSize-1)return;
        int index=z*TextureSize+x;if(visited[index]==generation)return;
        visited[index]=generation;var prior=pixels[index];
        if(prior.a==0)filled.Add(index);
        if(prior.r==color.r&&prior.g==color.g&&prior.b==color.b&&prior.a==255)return;
        pixels[index]=new Color32(color.r,color.g,color.b,255);changed=true;
    }
    internal bool TryGetUV(XUiC_RebirthMapArea map,out Vector4 uv)
    {
        uv=default;if(!active||!published||texture==null||displayedSpan==0)return false;
        float meters=MetersPerUnit*map.zoomScale,sx=map.ActiveCanvasWidth*meters/displayedSpan,sy=map.ActiveCanvasHeight*meters/displayedSpan;
        // Match native integer cTexMiddle even with odd canvas dimensions.
        uv=new Vector4((map.mapMiddlePosPixel.x-displayedOriginX-map.cTexMiddle.x*meters)/displayedSpan,
            (map.mapMiddlePosPixel.y-displayedOriginZ+(map.cTexMiddle.y-map.ActiveCanvasHeight)*meters)/displayedSpan,sx,sy);return true;
    }
    internal void Close(XUiC_RebirthMapArea map)
    {
        if(map.xuiTexture!=null&&map.xuiTexture.Texture==texture)Bind(map,map.mapTexture);
        if(texture!=null)UnityEngine.Object.Destroy(texture);
        texture=null;pixels=null;visited=null;regions.Clear();legacy.Clear();filled.Clear();filled.TrimExcess();world=player=null;database=null;staticSource=null;
        active=building=changed=published=false;span=displayedSpan=0;nextRefresh=0;
    }
}

// Native input routing, sounds, popup closing and clamps remain in place.
// Only our full map uses proportional progression and log-space easing.
[HarmonyPatch]
internal static class RebirthMapOverviewZoom
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(XUiC_MapArea),"onMapScrolled");yield return AccessTools.Method(typeof(XUiC_MapArea),"Update");}
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
    {
        int count=0,gamepad=0,easing=0;bool update=__originalMethod.Name=="Update";
        var code=new List<CodeInstruction>(instructions);
        for(int i=0;i<code.Count;i++)
        {
            var instruction=code[i];
            if(instruction.opcode==OpCodes.Ldc_R4&&instruction.operand is float&&(float)instruction.operand==6.15f)
            {
                count++;var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(instruction.labels);load.blocks.AddRange(instruction.blocks);
                yield return load;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthMapOverview),nameof(RebirthMapOverview.Limit)));
            }
            else if(update&&instruction.opcode==OpCodes.Add&&i>=4&&code[i-1].opcode==OpCodes.Mul
                &&code[i-2].opcode==OpCodes.Ldarg_1&&code[i-4].opcode==OpCodes.Ldfld
                &&Equals(code[i-4].operand,AccessTools.Field(typeof(XUiC_MapArea),"targetZoomScale")))
            {
                gamepad++;var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(instruction.labels);load.blocks.AddRange(instruction.blocks);
                yield return load;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthMapZoomInput),nameof(RebirthMapZoomInput.GamepadTarget)));
            }
            else if(update&&instruction.opcode==OpCodes.Call&&Equals(instruction.operand,AccessTools.Method(typeof(Mathf),"Lerp",new[]{typeof(float),typeof(float),typeof(float)})))
            {
                easing++;var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(instruction.labels);load.blocks.AddRange(instruction.blocks);
                yield return load;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthMapZoomInput),nameof(RebirthMapZoomInput.Ease)));
            }
            else yield return instruction;
        }
        if(count!=1||(update&&(gamepad!=1||easing!=1)))throw new InvalidOperationException("Main-map zoom input native contract changed.");
    }
}

internal static class RebirthMapZoomInput
{
    internal static float WheelTarget(float pending,float current,float delta,bool shift,float maximum)
    {
        // A reversal cancels easing still travelling the other way, while
        // successive same-direction events keep accumulating their targets.
        if((delta>0&&pending>current)||(delta<0&&pending<current))pending=current;
        return Mathf.Clamp(Mathf.Max(.7f,pending)*Mathf.Exp(-delta*(shift?4.700036f:2.2314355f)),.7f,maximum);
    }
    internal static float GamepadTarget(float pending,float nativeDelta,XUiC_MapArea map)
    {
        if(!(map is XUiC_RebirthMapArea))return pending+nativeDelta;
        if(nativeDelta!=0)((XUiC_RebirthMapArea)map).Interaction.Zoom();
        if((nativeDelta>0&&pending<map.zoomScale)||(nativeDelta<0&&pending>map.zoomScale))pending=map.zoomScale;
        return Mathf.Max(.7f,pending)*Mathf.Exp(nativeDelta*.4f);
    }
    internal static float Ease(float current,float target,float nativeBlend,XUiC_MapArea map)
    {
        if(!(map is XUiC_RebirthMapArea))return Mathf.Lerp(current,target,nativeBlend);
        if(current!=target)((XUiC_RebirthMapArea)map).Interaction.Zoom();
        current=Mathf.Max(.7f,current);target=Mathf.Max(.7f,target);
        float next=Mathf.Exp(Mathf.Lerp(Mathf.Log(current),Mathf.Log(target),1f-Mathf.Exp(-Mathf.Max(0,nativeBlend)*2f)));
        return Mathf.Abs(Mathf.Log(next/target))<.001f?target:next;
    }
}

[HarmonyPatch(typeof(XUiC_MapArea),"onMapScrolled")]
internal static class RebirthMapResponsiveWheel
{
    private static void Prefix(XUiC_MapArea __instance,out float __state)
    {__state=__instance.targetZoomScale>0?__instance.targetZoomScale:__instance.zoomScale;if(__instance is XUiC_RebirthMapArea map)map.Interaction.Zoom();}
    private static void Postfix(XUiC_MapArea __instance,float _delta,float __state)
    {
        if(__instance is XUiC_RebirthMapArea)
            __instance.targetZoomScale=RebirthMapZoomInput.WheelTarget(__state,__instance.zoomScale,_delta,InputUtils.ShiftKeyPressed,RebirthMapOverview.Limit(__instance));
    }
}

[HarmonyPatch(typeof(XUiC_MapArea),"teleportPlayerOnMap")]
internal static class RebirthMapTeleportBounds
{
    internal const string OutsideWorldKey="xuiRebirthMapTeleportOutsideWorld";
    private static bool Prefix(XUiC_MapArea __instance,Vector3 __0)
    {
        var map=__instance as XUiC_RebirthMapArea;
        if(map==null)return true;
        if(map.SuppressMapHover)return false;
        if(PrefabEditModeManager.Instance?.IsActive()==true)return true;
        var world=GameManager.Instance?.World;
        if(world==null||map.localPlayer==null)return true;
        // Respawn applies this exact native clamp after teleport. Reject an
        // outside-world click instead of silently moving it to a different X/Z.
        var requested=map.screenPosToWorldPos(__0,false);requested.y=180f;
        var valid=world.ClampToValidWorldPos(requested);
        if(valid.x==requested.x&&valid.z==requested.z)return true;
        var local=map.localPlayer as EntityPlayerLocal;
        if(local!=null)GameManager.ShowTooltip(local,Localization.Get(OutsideWorldKey),string.Empty,"ui_denied");
        return false;
    }
}




