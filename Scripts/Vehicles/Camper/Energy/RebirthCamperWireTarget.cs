using System.Linq;
using UnityEngine;

internal sealed class RebirthCamperWireTarget:MonoBehaviour
{
    internal EntityVehicle Vehicle;internal int Kind,Slot;
    internal static void Attach(GameObject go,EntityVehicle vehicle,int kind,int slot,Bounds? bounds=null)
    {
        var target=go.GetComponent<RebirthCamperWireTarget>()??go.AddComponent<RebirthCamperWireTarget>();
        target.Vehicle=vehicle;target.Kind=kind;target.Slot=slot;
        if(bounds.HasValue){var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.center=bounds.Value.center;box.size=bounds.Value.size;}
    }
    internal Vector3 ReachPoint(EntityPlayer player)
    {
        var collider=GetComponent<Collider>();
        return collider?collider.ClosestPoint(player.position+Vector3.up*player.GetEyeHeight()-Origin.position):transform.position;
    }
    internal static RebirthCamperWireTarget Focus(EntityPlayerLocal player)
    {
        var ray=player.GetLookRay();ray.origin-=Origin.position;
        var hits=Physics.RaycastAll(ray,Constants.cDigAndBuildDistance,Physics.AllLayers,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
        foreach(var hit in hits)
        {
            if(!hit.collider||hit.collider.transform.IsChildOf(player.RootTransform))continue;
            var target=hit.collider.GetComponent<RebirthCamperWireTarget>();
            if(!target||!target.Vehicle||!target.gameObject.activeInHierarchy)continue;
            bool blocked=hits.Any(h=>h.distance<hit.distance-.005f&&h.collider&&!h.collider.isTrigger
                &&!h.collider.transform.IsChildOf(player.RootTransform)&&!RebirthCamperPhysicalTargets.SelectionProxy(h.collider,target.Vehicle));
            if(!blocked)return target;
        }
        return null;
    }
}
