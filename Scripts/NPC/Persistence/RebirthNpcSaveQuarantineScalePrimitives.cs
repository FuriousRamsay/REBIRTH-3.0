using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcSpatialIndex
{
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){if(Entries.Count>8192)throw new InvalidOperationException("Spatial original identity bound exceeded");foreach(var row in Entries.Values){if(row==null)throw new InvalidOperationException("Spatial original missing");RebirthNpcSaveQuarantineIds.Add(ids,row.StableId);}}}
    private static RebirthNpcAncillaryOriginalScope spatialScope;
    private static bool UseSpatialScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref spatialScope,Entries.Count==0&&Cells.Count==0,true);}
    private static bool SpatialActorCurrent(Entry entry,EntityRebirthNPC actor)
    {return actor!=null&&ReferenceEquals(entry.Actor,actor)&&ReferenceEquals(actor.world,spatialScope?.World)&&ReferenceEquals(actor.RebirthRuntimeState,entry.Runtime)&&actor.NativeIdentityReadable&&entry.Runtime!=null&&entry.Runtime.StableId==entry.StableId;}
    internal static bool CanRetireEmptySpatial{get{lock(Sync)return ThreadManager.IsMainThread()&&Entries.Count==0&&Cells.Count==0;}}
    private static XmlDocument SpatialImage()
    {
        if(Entries.Count>8192||Cells.Count>8192)throw new InvalidOperationException("Original spatial evidence bound exceeded");
        var doc=RebirthNpcAncillaryOriginalScope.Image("spatialIndex",spatialScope,new[]{queries,candidates,hits,updates,removes});var ids=new List<int>(Entries.Keys);ids.Sort();
        foreach(int id in ids){var entry=Entries[id];if(entry==null||entry.EntityId!=id||entry.Actor==null||entry.Runtime==null||entry.StableId.IsEmpty||!SpatialActorCurrent(entry,entry.Actor)||entry.Cell!=CellKey(entry.X,entry.Z)||float.IsNaN(entry.X)||float.IsInfinity(entry.X)||float.IsNaN(entry.Y)||float.IsInfinity(entry.Y)||float.IsNaN(entry.Z)||float.IsInfinity(entry.Z))throw new InvalidOperationException("Original spatial record invalid");var row=doc.CreateElement("actor");row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",entry.StableId.ToString());row.SetAttribute("x",entry.X.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",entry.Y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",entry.Z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("cell",entry.Cell.ToString(CultureInfo.InvariantCulture));doc.DocumentElement.AppendChild(row);}
        var keys=new List<long>(Cells.Keys);keys.Sort();long total=0;foreach(long key in keys){var values=Cells[key];if(values==null||(total+=values.Count)>8192)throw new InvalidOperationException("Original spatial cell graph invalid");var row=doc.CreateElement("cell");row.SetAttribute("key",key.ToString(CultureInfo.InvariantCulture));var ordered=new List<int>(values);ordered.Sort();row.InnerText=string.Join(",",ordered);foreach(int id in ordered)if(!Entries.TryGetValue(id,out var entry)||entry.Cell!=key)throw new InvalidOperationException("Original spatial cell conflict");doc.DocumentElement.AppendChild(row);}if(total!=Entries.Count)throw new InvalidOperationException("Original spatial cell membership incomplete");return doc;
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareSpatialQuarantine(RebirthNpcSaveQuarantineScope scope,RebirthNpcRecoveryPrimitiveCaches.Context transition=null)
    {
        lock(Sync)
        {
            if(transition==null)RebirthNpcAncillaryOriginalScope.RequireScope(scope,spatialScope);else transition.RequireOriginal(scope,spatialScope);if(spatialScope==null&&(Entries.Count!=0||Cells.Count!=0))throw new InvalidOperationException("Original spatial binding unknown");var records=Entries;var cells=Cells;var original=spatialScope;var doc=SpatialImage();var refs=new Dictionary<int,Entry>(Entries);long a=queries,b=candidates,c=hits,d=updates,e=removes;var next=new Dictionary<int,Entry>();var nextCells=new Dictionary<long,HashSet<int>>();
            return new RebirthNpcSaveQuarantineDomain("spatial-index",doc,
                ()=>{lock(Sync){if(!ReferenceEquals(Entries,records)||!ReferenceEquals(Cells,cells)||!ReferenceEquals(spatialScope,original)||SpatialImage().OuterXml!=doc.OuterXml)return false;foreach(var row in refs)if(!Entries.TryGetValue(row.Key,out var current)||!ReferenceEquals(current,row.Value))return false;return true;}},
                ()=>{lock(Sync){Entries=next;Cells=nextCells;spatialScope=null;queries=candidates=hits=updates=removes=0;}},
                ()=>{lock(Sync){Entries=records;Cells=cells;spatialScope=original;queries=a;candidates=b;hits=c;updates=d;removes=e;}});
        }
    }
}
public static partial class RebirthNpcPathBudgetCoordinator
{
    private static RebirthNpcAncillaryOriginalScope pathScope;
    private static bool UsePathScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref pathScope,epoch<0&&accepted==0&&deferred==0,true);}
    internal static bool CanRetirePathScope{get{lock(Sync)return ThreadManager.IsMainThread()&&pathScope==null&&accepted==0&&deferred==0;}}
    private static XmlDocument PathImage()=>RebirthNpcAncillaryOriginalScope.Image("pathBudget",pathScope,new[]{epoch,accepted,deferred,epochs,(long)Remaining[0],Remaining[1],Remaining[2]});
    internal static RebirthNpcSaveQuarantineDomain PreparePathQuarantine(RebirthNpcSaveQuarantineScope scope,RebirthNpcRecoveryPrimitiveCaches.Context transition=null)
    {
        lock(Sync)
        {
            if(transition==null)RebirthNpcAncillaryOriginalScope.RequireScope(scope,pathScope);else transition.RequireOriginal(scope,pathScope);if((pathScope==null&&(epoch>=0||accepted!=0||deferred!=0||epochs!=0))||Remaining==null||Remaining.Length!=3)throw new InvalidOperationException("Original path budget invalid");for(int i=0;i<Remaining.Length;i++)if(Remaining[i]<0||Remaining[i]>Limits[i])throw new InvalidOperationException("Original path remaining budget invalid");var original=pathScope;var values=Remaining;var next=new int[3];long a=epoch,b=accepted,c=deferred,d=epochs;var doc=PathImage();
            return new RebirthNpcSaveQuarantineDomain("path-budget",doc,
                ()=>{lock(Sync)return ReferenceEquals(pathScope,original)&&ReferenceEquals(Remaining,values)&&PathImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){pathScope=null;Remaining=next;epoch=-1;accepted=deferred=epochs=0;}},
                ()=>{lock(Sync){pathScope=original;Remaining=values;epoch=a;accepted=b;deferred=c;epochs=d;}});
        }
    }
}
