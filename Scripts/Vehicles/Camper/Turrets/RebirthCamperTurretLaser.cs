using UnityEngine;
using Object=UnityEngine.Object;

// Native junk-turret mesh/material; firing admission stays server-authoritative.
internal sealed class RebirthCamperTurretLaser
{
    private static readonly System.Collections.Generic.List<RebirthCamperTurretLaser> instances=new System.Collections.Generic.List<RebirthCamperTurretLaser>();
    private readonly GameObject root;
    private readonly Vector3 nativeBasis;
    private readonly float meshLength;
    private readonly float diameter;
    private float beamLength;
    private Vector3 beamDirection,savedPosition,savedScale;
    private bool clipped;
    internal RebirthCamperTurretLaser(Transform parent)
    {
        var prefab=DataLoader.LoadAsset<GameObject>("@:Other/Items/Weapons/Ranged/JunkTurret/junkTurret_Prefab.prefab");
        var source=prefab?prefab.transform.FindInChilds("turret_laser",false):null;
        if(!source)throw new System.InvalidOperationException("Native junk turret laser unavailable.");
        var mesh=source.GetComponent<MeshFilter>()?.sharedMesh;
        if(!mesh||mesh.bounds.size.z<=0)throw new System.InvalidOperationException("Native junk turret laser mesh unavailable.");
        root=Object.Instantiate(source.gameObject,parent,false);
        root.name="Camper native junk turret laser";
        // This object is nested inside the native weapon prefab. localScale
        // alone drops the native rig/import scale and produces a broad ribbon.
        nativeBasis=new Vector3(source.TransformVector(Vector3.right).magnitude,
            source.TransformVector(Vector3.up).magnitude,source.TransformVector(Vector3.forward).magnitude);
        meshLength=mesh.bounds.size.z;
        diameter=Mathf.Max(mesh.bounds.size.x*nativeBasis.x,mesh.bounds.size.y*nativeBasis.y);
        root.SetActive(false);
        instances.Add(this);
    }
    internal void Update(World world,Transform muzzle,bool visible,float range)
    {
        if(!root)return;
        Restore();
        root.SetActive(visible);if(!visible)return;
        var origin=muzzle.position-muzzle.up*.025f;
        bool hit=Voxel.Raycast(world,new Ray(origin+Origin.position,muzzle.forward),range,-538750989,8,0);
        float distance=hit?Mathf.Clamp(Mathf.Sqrt(Voxel.voxelRayHitInfo.hit.distanceSq),0,range):range;
        // Native vertices extend along -Z from zero to -.7874 metres.
        root.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(-muzzle.forward,muzzle.up));
        // The beam rotates with pitch/yaw relative to its parent. Measuring its
        // actual rotated basis avoids treating parent XYZ scales as beam XYZ.
        root.transform.localScale=Vector3.one;
        var basis=new Vector3(root.transform.TransformVector(Vector3.right).magnitude,
            root.transform.TransformVector(Vector3.up).magnitude,root.transform.TransformVector(Vector3.forward).magnitude);
        if(!RebirthCamperLaserScale.Try(nativeBasis,basis,meshLength,distance,out var scale)){root.SetActive(false);return;}
        root.transform.localScale=scale;
        beamLength=distance;beamDirection=muzzle.forward;
    }
    internal static void BeginCamera(Camera camera)
    {
        EndCamera();
        if(!camera)return;
        foreach(var beam in instances)beam.Clip(camera);
    }
    private void Clip(Camera camera)
    {
        if(!root||!root.activeInHierarchy||beamLength<=.001f)return;
        var offset=root.transform.position-camera.transform.position;
        // Only the camera at this turret's muzzle needs a near-field correction.
        if(offset.sqrMagnitude>.25f)return;
        float start=RebirthCamperLaserNearField.Start(diameter,camera.pixelHeight,camera.fieldOfView,
            Vector3.Dot(offset,camera.transform.forward),Vector3.Dot(beamDirection,camera.transform.forward),beamLength);
        if(start<=0)return;
        savedPosition=root.transform.position;savedScale=root.transform.localScale;clipped=true;
        root.transform.position=savedPosition+beamDirection*start;
        var scale=savedScale;scale.z*= (beamLength-start)/beamLength;root.transform.localScale=scale;
    }
    internal static void EndCamera(){foreach(var beam in instances)beam.Restore();}
    private void Restore(){if(!clipped)return;clipped=false;if(root){root.transform.position=savedPosition;root.transform.localScale=savedScale;}}
    internal void Destroy(){Restore();instances.Remove(this);if(root)Object.Destroy(root);}
}
