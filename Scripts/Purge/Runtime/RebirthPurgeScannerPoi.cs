using System;
using System.Collections.Generic;
using UnityEngine;

// Scanner and Purge visit selection share the containing-XZ policy. Native
// location naming and strict manifest/actor/reset/clear-proof rules are unchanged.
internal static class RebirthPurgeScannerPoi
{
    private static readonly FastTags<TagGroup.Poi> Hidden=FastTags<TagGroup.Poi>.Parse("hideui");
    private static readonly FastTags<TagGroup.Poi> Street=FastTags<TagGroup.Poi>.Parse("streettile,rwgonly");
    [ThreadStatic] private static List<PrefabInstance> candidates;
    internal static PrefabInstance Resolve(World world,Vector3 position)
    {
        if(world==null)return null;
        // Native indexed point query; never enumerate the world's prefab list.
        var list=candidates??(candidates=new List<PrefabInstance>(16));
        list.Clear();
        PrefabInstance best=null;bool bestStreet=true;
        double bestArea=double.MaxValue,bestDistance=double.MaxValue;
        try
        {
            // Indexed bounds are block coordinates. Truncation moves negative
            // fractional positions outside the last block of a valid footprint.
            int x=Mathf.FloorToInt(position.x),z=Mathf.FloorToInt(position.z);
            world.GetPOIsAtXZ(x,x,z,z,list);
            foreach(var candidate in list)
            {
                var authored=candidate?.prefab;
                if(authored==null||authored.Tags.Test_AnySet(Hidden))continue;
                var origin=candidate.boundingBoxPosition;var size=candidate.boundingBoxSize;
                if(size.x<1||size.z<1||position.x<origin.x||position.x>(double)origin.x+size.x||
                    position.z<origin.z||position.z>(double)origin.z+size.z)continue;
                double dx=origin.x+size.x*.5-position.x,dz=origin.z+size.z*.5-position.z;
                // Legacy exact authored-center/origin-Y shortcut precedes ranking.
                if(dx==0&&dz==0&&position.y==origin.y)return candidate;
                bool street=authored.Tags.Test_AnySet(Street);
                double area=(double)size.x*size.z,distance=dx*dx+dz*dz;
                if(best==null||(bestStreet&&!street)||bestStreet==street&&
                    (area<bestArea||area==bestArea&&distance<bestDistance))
                {best=candidate;bestStreet=street;bestArea=area;bestDistance=distance;}
            }
            return best;
        }
        finally {list.Clear();}
    }
}