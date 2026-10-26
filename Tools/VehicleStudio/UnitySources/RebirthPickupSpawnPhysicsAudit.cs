using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Read-only source/bundle diagnostic. No prefab, material or bundle is saved.
public static class RebirthPickupSpawnPhysicsAudit
{
    const string Prefab="Assets/REBIRTHApprovedPickupProduction_20261020/rebirthCamper80s.prefab";
    const string Bundle="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Resources/rebirth_camper80s.unity3d";
    const string Output="E:/_Haven/shared/REBIRTHStaged/CamperCorrections_20261021/SpawnPhysicsAudit";
    static readonly StringBuilder report=new StringBuilder();
    static string V(Vector3 value)=>value.ToString("F5");
    static string PathOf(Transform t,Transform root)
    {string path=t.name;while(t.parent&&t.parent!=root){t=t.parent;path=t.name+"/"+path;}return path;}
    static void Travel(GameObject root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(new[]{"REBIRTHCamperBodyModule","REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders"}.Contains(t.name))t.gameObject.SetActive(false);
            if(t.name.StartsWith("REBIRTHInteraction_")){var c=t.GetComponent<Collider>();if(c)c.enabled=false;}
            if(t.name=="REBIRTHRearDoorPivot"){var c=t.GetComponent<Collider>();if(c)c.enabled=false;}
            if(t.name=="REBIRTHPickupOnlyBodywork"||t.name=="REBIRTHPickupOnlyPaint")t.gameObject.SetActive(true);
        }
    }
    static void Inventory(GameObject asset,string name)
    {
        var root=Object.Instantiate(asset);root.name=name;Travel(root);Physics.SyncTransforms();
        try
        {
            report.AppendLine("\nINVENTORY "+name);
            foreach(var r in root.GetComponentsInChildren<Rigidbody>(true))
                report.AppendLine("RB "+PathOf(r.transform,root.transform)+" mass="+r.mass+" position="+V(r.position)+" gravity="+r.useGravity+" kinematic="+r.isKinematic+" COM="+V(r.centerOfMass)+" autoCOM="+r.automaticCenterOfMass+" scale="+V(r.transform.lossyScale)+" drag="+r.drag+" angularDrag="+r.angularDrag);
            foreach(var c in root.GetComponentsInChildren<Collider>(true))
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy)continue;
                report.AppendLine("COLLIDER "+PathOf(c.transform,root.transform)+" type="+c.GetType().Name+" layer="+c.gameObject.layer+" trigger="+c.isTrigger+" attached="+(c.attachedRigidbody?PathOf(c.attachedRigidbody.transform,root.transform):"STATIC")+" bounds.min="+V(c.bounds.min)+" max="+V(c.bounds.max));
                if(c is BoxCollider b)report.AppendLine("  BOX center="+V(b.center)+" size="+V(b.size));
                if(c is WheelCollider w)report.AppendLine("  WHEEL center="+V(w.center)+" radius="+w.radius+" mass="+w.mass+" suspension="+w.suspensionDistance+" spring="+w.suspensionSpring.spring+" damper="+w.suspensionSpring.damper+" target="+w.suspensionSpring.targetPosition+" forcePoint="+w.forceAppPointDistance+" damping="+w.wheelDampingRate);
            }
            var player=new GameObject("StaticPlayerCapsuleApproximation");var capsule=player.AddComponent<CapsuleCollider>();capsule.center=new Vector3(0,.9f,0);capsule.height=1.8f;capsule.radius=.3f;
            Physics.SyncTransforms();
            foreach(var c in root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&!(c is WheelCollider)))
                if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,capsule,player.transform.position,player.transform.rotation,out var direction,out float distance))
                    report.AppendLine("PLAYER_OVERLAP "+PathOf(c.transform,root.transform)+" colliderSeparationDirection="+V(direction)+" penetration="+distance+" colliderType="+c.GetType().Name+" attached="+(c.attachedRigidbody?PathOf(c.attachedRigidbody.transform,root.transform):"STATIC"));
            Object.DestroyImmediate(player);
        }
        finally{Object.DestroyImmediate(root);}
    }
    static void Simulate(GameObject asset,bool player,bool detached,bool wheels)
    {
        var root=Object.Instantiate(asset);Travel(root);root.transform.position=Vector3.up*.5f;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="FlatGround";ground.transform.position=new Vector3(0,-.5f,0);ground.transform.localScale=new Vector3(200,1,200);
        var body=root.GetComponentInChildren<Rigidbody>();GameObject playerObject=null;
        try
        {
            report.AppendLine("\nSIM playerCapsule="+player+" detachedColliders="+detached+" wheels="+wheels+" rootAnchor="+V(root.transform.position));
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            // Native EntityVehicle.Init sets this automatic-COM override and uses
            // manual gravity. A force of mass*9.81 each step is the surrogate.
            if(body.automaticCenterOfMass)body.centerOfMass=new Vector3(0,.1f,0);
            body.isKinematic=false;body.useGravity=false;
            foreach(var c in root.GetComponentsInChildren<Collider>(true))
            {if(!detached&&c.attachedRigidbody!=body)c.enabled=false;if(!wheels&&c is WheelCollider)c.enabled=false;}
            if(player)
            {
                playerObject=new GameObject("StaticPlayerCapsuleApproximation");playerObject.transform.position=root.transform.position;
                var c=playerObject.AddComponent<CapsuleCollider>();c.center=new Vector3(0,.9f,0);c.height=1.8f;c.radius=.3f;
            }
            Physics.SyncTransforms();
            float peakY=body.position.y,peakSpeed=0;Vector3 start=body.position;
            for(int step=0;step<500;step++)
            {
                if(step==24&&playerObject){Object.DestroyImmediate(playerObject);playerObject=null;report.AppendLine("  player capsule removed t=.48s (seat-attachment approximation)");}
                body.AddForce(Vector3.down*9.81f*body.mass);Physics.Simulate(.02f);
                peakY=Mathf.Max(peakY,body.position.y);peakSpeed=Mathf.Max(peakSpeed,body.velocity.magnitude);
                if(step<25&&step%4==0||step%50==49)report.AppendLine("  t="+((step+1)*.02f).ToString("F2")+" position="+V(body.position)+" velocity="+V(body.velocity)+" angular="+V(body.angularVelocity));
            }
            report.AppendLine("RESULT delta="+V(body.position-start)+" peakY="+peakY+" peakSpeed="+peakSpeed+" final="+V(body.position));
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(ground);if(playerObject)Object.DestroyImmediate(playerObject);}
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        report.AppendLine("READ-ONLY Unity 2022.3 offline audit. Flat ground, authored wheel values, native automatic COM override, mass*9.81 force; no native vehicle drive/controller/CC code. Capsule is static radius.3 height1.8 center.9 at spawn anchor; removed at .48s. Not actual game qualification.");
        var old=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;AssetBundle bundle=null;
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(!source)throw new Exception("Source prefab unavailable");
            Inventory(source,"AuthoringPrefab");
            bundle=AssetBundle.LoadFromFile(Bundle);if(!bundle)throw new Exception("Installed bundle unavailable");
            var asset=bundle.LoadAsset<GameObject>("rebirthCamper80s");Inventory(asset,"InstalledBundle");
            foreach(bool detached in new[]{false,true})foreach(bool player in new[]{false,true})Simulate(asset,player,detached,true);
            Simulate(asset,false,false,false);
            File.WriteAllText(Output+"/physics-audit.txt",report.ToString());
            Debug.Log("SPAWN_PHYSICS_AUDIT_COMPLETE "+Output);
        }
        finally{Physics.simulationMode=old;if(bundle)bundle.Unload(true);File.WriteAllText(Output+"/physics-audit.txt",report.ToString());}
    }
}
