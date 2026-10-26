using System;
using System.Collections.Generic;
using UnityEngine;

// B08 concealed-block addressing. Native repair acts on the first block a ray
// hits, so a block hidden behind others can only be repaired from a position
// that sees it. This picks such a standing cell near the target and asks the
// shared native movement adapter to walk there; it never repairs through
// geometry and never changes the exact-first-hit rule in RebirthNpcSingleRepairPolicy.
internal enum RebirthP9RepairApproach{Visible,Moving,Unreachable}

internal static class RebirthP9RepairWorkPosition
{
    private const int Radius=4,Down=3,Up=1,MaxRays=24,MaxStates=256;
    private sealed class State{internal readonly HashSet<Vector3i> Rejected=new HashSet<Vector3i>();internal Vector3i Chosen;internal bool HasChosen;}
    private static readonly Dictionary<ulong,State> States=new Dictionary<ulong,State>();

    private static bool Solid(World world,Vector3i p)
    {
        var bv=world.GetBlock(p);
        return !bv.isair&&bv.Block!=null&&bv.Block.IsCollideMovement&&!bv.Block.IsTerrainDecoration;
    }
    private static bool Standable(World world,Vector3i foot)=>
        Solid(world,new Vector3i(foot.x,foot.y-1,foot.z))&&!Solid(world,foot)&&!Solid(world,new Vector3i(foot.x,foot.y+1,foot.z));

    // Same ray the repair policy uses at the action boundary, from any eye point.
    internal static bool HasLine(World world,Vector3 eye,Vector3i target)
    {
        var delta=target.ToVector3()+new Vector3(.5f,.5f,.5f)-eye;float distance=delta.magnitude;
        if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<.5f||distance>Constants.cDigAndBuildDistance)return false;
        var direction=delta/distance;var ray=new Ray(eye+direction*.5f,direction);
        return Voxel.Raycast(world,ray,Constants.cDigAndBuildDistance,-538480645,4095,0f)&&
            Voxel.voxelRayHitInfo.bHitValid&&Voxel.voxelRayHitInfo.hit.blockPos==target;
    }

    internal static RebirthP9RepairApproach Approach(EntityRebirthHumanoidNPC npc,ulong assignment,Vector3i target,out string detail)
    {
        detail=string.Empty;
        try
        {
            var world=npc.world;
            if(HasLine(world,npc.GetLookRay().origin,target)){States.Remove(assignment);return RebirthP9RepairApproach.Visible;}
            var adapter=RebirthNpcMovementAdapterRegistry.Current;
            if(adapter==null){detail="Shared native movement adapter is unavailable.";return RebirthP9RepairApproach.Moving;}
            if(!States.TryGetValue(assignment,out var state))
            {
                if(States.Count>=MaxStates)States.Clear();
                States[assignment]=state=new State();
            }
            float eyeHeight=npc.GetEyeHeight();
            for(int attempt=0;attempt<3;attempt++)
            {
                if(!state.HasChosen&&!Choose(world,npc,target,eyeHeight,state)){state.Rejected.Clear();detail="No clear work position can see the damaged block.";return RebirthP9RepairApproach.Unreachable;}
                var stand=new Vector3(state.Chosen.x+.5f,state.Chosen.y,state.Chosen.z+.5f);
                var result=adapter.RequestMove(npc,stand,.6f,out string moveDetail);
                if(result==RebirthNpcMovementRequestResult.Accepted||result==RebirthNpcMovementRequestResult.AlreadySatisfied)
                {detail="Moving to a position that sees the damaged block.";return RebirthP9RepairApproach.Moving;}
                state.Rejected.Add(state.Chosen);state.HasChosen=false;detail=moveDetail??string.Empty;
            }
            detail="No reachable position sees the damaged block.";return RebirthP9RepairApproach.Unreachable;
        }
        catch{detail="Work position search failed.";return RebirthP9RepairApproach.Unreachable;}
    }

    private static bool Choose(World world,EntityRebirthHumanoidNPC npc,Vector3i target,float eyeHeight,State state)
    {
        var candidates=new List<KeyValuePair<float,Vector3i>>();
        // The physical arrival rule measures from the block corner; measure the same way.
        // RequestMove stops within .6 m of the cell, so keep cell+stop inside the 2.5 m arrival rule.
        float limit=RebirthNpcWorkRuntime.WorkRadius-.6f-.1f;limit*=limit;
        for(int dy=-Down;dy<=Up;dy++)
        for(int dx=-Radius;dx<=Radius;dx++)
        for(int dz=-Radius;dz<=Radius;dz++)
        {
            var foot=new Vector3i(target.x+dx,target.y+dy,target.z+dz);
            if(state.Rejected.Contains(foot)||foot.Equals(target))continue;
            var center=new Vector3(foot.x+.5f,foot.y,foot.z+.5f);
            var offset=center-target.ToVector3();
            if(offset.sqrMagnitude>limit||!Standable(world,foot))continue;
            candidates.Add(new KeyValuePair<float,Vector3i>((center-npc.position).sqrMagnitude,foot));
        }
        candidates.Sort((a,b)=>a.Key.CompareTo(b.Key));
        for(int i=0;i<candidates.Count&&i<MaxRays;i++)
        {
            var foot=candidates[i].Value;
            if(!HasLine(world,new Vector3(foot.x+.5f,foot.y+eyeHeight,foot.z+.5f),target))continue;
            state.Chosen=foot;state.HasChosen=true;return true;
        }
        return false;
    }
}
