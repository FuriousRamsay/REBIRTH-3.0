using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Prefab cell storage is shared by rotated instances. Read only allocated cells,
// then retain lamp offsets in the instance's own (rotated) coordinate system.
internal static class RebirthCamperAuthoredLightIndex
{
    private sealed class Entry
    {
        internal object Cells, Rows, Pool;
        internal Vector3i Size;
        internal int Rotation;
        internal Vector3i[] Lights;
    }
    private static readonly ConditionalWeakTable<Prefab,Entry> entries=new ConditionalWeakTable<Prefab,Entry>();
    internal static void Invalidate(Prefab prefab)=>entries.Remove(prefab);
    internal static Vector3i[] Lights(Prefab prefab)
    {
        if(prefab?.blockCells==null)return Array.Empty<Vector3i>();
        var entry=entries.GetValue(prefab,_=>new Entry());
        var rows=prefab.blockCells.a;
        if(entry.Lights!=null&&ReferenceEquals(entry.Cells,prefab.blockCells)&&ReferenceEquals(entry.Rows,rows)&&
            ReferenceEquals(entry.Pool,prefab.cellPool)&&entry.Size==prefab.size&&entry.Rotation==prefab.localRotation)
            return entry.Lights;
        var lights=new List<Vector3i>();
        if(rows!=null)for(int y=0;y<rows.Length;y++)
        {
            var zCells=rows[y]?.a;if(zCells==null)continue;
            for(int zi=0;zi<zCells.Length;zi++)
            {
                var xCells=zCells[zi]?.a;if(xCells==null)continue;
                for(int xi=0;xi<xCells.Length;xi++)
                {
                    var cell=xCells[xi];if(!cell.HasData)continue;
                    for(int i=0;i<16;i++)
                    {
                        var value=new BlockValue(cell.Get(prefab.cellPool,i));
                        if(value.ischild||value.Block is not BlockLight lamp||!lamp.HasTileEntity)continue;
                        int rawX=xi*4+(i&3),rawZ=zi*4+(i>>2);
                        int x=rawX,z=rawZ;
                        // Inverse of the installed Prefab.RotateCoords. GetBlock
                        // remains the authority for the value/rotation at use time.
                        switch(prefab.localRotation)
                        {
                            case 1:x=prefab.size.x-rawZ-1;z=rawX;break;
                            case 2:x=prefab.size.x-rawX-1;z=prefab.size.z-rawZ-1;break;
                            case 3:x=rawZ;z=prefab.size.z-rawX-1;break;
                        }
                        if(x>=0&&x<prefab.size.x&&y<prefab.size.y&&z>=0&&z<prefab.size.z)
                            lights.Add(new Vector3i(x,y,z));
                    }
                }
            }
        }
        entry.Cells=prefab.blockCells;entry.Rows=rows;entry.Pool=prefab.cellPool;
        entry.Size=prefab.size;entry.Rotation=prefab.localRotation;return entry.Lights=lights.ToArray();
    }
}

[HarmonyPatch(typeof(Prefab),nameof(Prefab.SetBlock),new[]{typeof(int),typeof(int),typeof(int),typeof(BlockValue)})]
internal static class RebirthCamperAuthoredLightIndexChanged
{
    private static void Postfix(Prefab __instance)=>RebirthCamperAuthoredLightIndex.Invalidate(__instance);
}
