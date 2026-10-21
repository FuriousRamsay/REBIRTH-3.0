using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    private bool OwnedObstruction(Collider collider,Gun gun)
    {
        if(!collider||collider.isTrigger||!collider.enabled||!collider.gameObject.activeInHierarchy
            ||!collider.transform.IsChildOf(vehicle.RootTransform)
            ||RebirthCamperPhysicalTargets.SelectionProxy(collider,vehicle)
            ||collider.transform==gun.Yaw||collider.transform.IsChildOf(gun.Yaw))return false;
        var actor=collider.GetComponentInParent<Entity>();
        return actor==null||ReferenceEquals(actor,vehicle);
    }
    // Cache only shape reach at rig binding. Box/mesh bounds are transformed
    // from authored local space, including inactive body/plough geometry. Actual
    // admission still queries current active colliders at every candidate pose.
    private static float OwnedClearanceRadius(Transform root)
    {
        float radius=0;
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if(collider.isTrigger)continue;
            Bounds bounds;bool local;
            if(collider is BoxCollider box){bounds=new Bounds(box.center,box.size);local=true;}
            else if(collider is MeshCollider mesh&&mesh.sharedMesh){bounds=mesh.sharedMesh.bounds;local=true;}
            else{bounds=collider.bounds;local=false;}
            if(!local&&bounds.size.sqrMagnitude<.00000001f)continue;
            for(int i=0;i<8;i++)
            {
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                if(local)point=collider.transform.TransformPoint(point);
                radius=Mathf.Max(radius,root.InverseTransformPoint(point).magnitude);
            }
        }
        var scale=root.lossyScale;
        return radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
    }
}
