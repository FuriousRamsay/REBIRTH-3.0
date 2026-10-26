using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;

// Called only from the read-only avatar probe's disposable batch scene.
public static class RebirthCamperSeatingReview
{
    [Serializable] public class Occupant
    {
        public int seat,pose,vertices;public string body,bedClip,authoredPoseClip;public Vector3 position,rotation,boundsCenter,boundsSize,hips,head,leftKnee,leftFoot,rightKnee,rightFoot;
        public string[] activeClips;public bool genericAvatar,headMeshPresent,nativeIKApplied=false;
        public float pelvisBottomY,thighTopY;public FootContact[] footContacts;public TargetRecord[] ikTargets;
    }
    [Serializable] public class TargetRecord {public string name;public Vector3 modelPosition,modelRotation;}
    [Serializable] public class FootContact {public string side;public int sampledVertices,unsupportedVertices;public float minimumClearance;}
    [Serializable] public class FurnitureFace {public string mesh,region;public int triangle;public Vector3 a,b,c;public float normalY,area;}
    [Serializable] public class DecodedSubmesh {public int[] indices;}
    [Serializable] public class DecodedMesh {public string name;public Vector3[] vertices,normals;public Vector4[] tangents;public Vector2[] uv;public DecodedSubmesh[] submeshes;}
    [Serializable] public class DecodedMeshes {public string bundleSha256;public DecodedMesh[] meshes;}
    [Serializable] public class BenchIndices {public int[] vertexIds;}
    [Serializable] public class Report
    {
        public string status="FAIL",error,prefab,xml,bedClipOverride,savedCandidate,bundle;
        public string scope="Actual native default male/female skin and generic poses at current XML anchors; native steering IK when recorded; not game, multiplayer or comfort acceptance";
        public Occupant[] occupants;public string[] images,coarseBodyBoundsOverlaps;
        public FurnitureFace[] measuredFurniture;public float reviewOnlyTableRaise;
    }
    public static void Review(string prefabPath,string xmlPath,string output,GameObject male,GameObject female)
    {
        Directory.CreateDirectory(output);var report=new Report{prefab=prefabPath,xml=xmlPath};var owned=new List<UnityEngine.Object>();var graphs=new List<PlayableGraph>();
        var evidence=new List<Occupant>();var images=new List<string>();var bodies=new List<Bounds>();
        var previousAmbientMode=RenderSettings.ambientMode;Color previousAmbient=RenderSettings.ambientLight;AssetBundle reviewBundle=null;
        bool useNativeProfile=Arg("-rebirthAvatarUseNativeProfile","0")=="1";bool defaultOnly=useNativeProfile||Arg("-rebirthAvatarDefaultOnly","0")=="1";Report nativeProfile=null;
        try
        {
            if(!Application.isBatchMode || Application.isPlaying)throw new Exception("Dedicated non-playing batch scene required.");
            if(male==null || female==null || !File.Exists(xmlPath))throw new Exception("Actual bound male/female templates and explicit XML path required.");
            report.bundle=Arg("-rebirthAvatarCamperBundle","");
            GameObject prefab;
            if(string.IsNullOrEmpty(report.bundle))prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            else
            {
                if(Arg("-rebirthAvatarSaveCandidate","0")=="1"||Arg("-rebirthAvatarAuthorFront","0")=="1"||Arg("-rebirthAvatarAuthorCabin","0")=="1"||Arg("-rebirthAvatarCamperReauthorBed","0")=="1")throw new Exception("Bundle review must not reauthor or save source");
                reviewBundle=AssetBundle.LoadFromFile(report.bundle);if(!reviewBundle)throw new Exception("Candidate bundle unavailable");
                prefab=reviewBundle.LoadAsset<GameObject>("rebirthCamper80s");
            }
            if(prefab==null)throw new Exception("Candidate camper prefab unavailable.");
            if(useNativeProfile){nativeProfile=JsonUtility.FromJson<Report>(File.ReadAllText(Path.GetDirectoryName(prefabPath)+"/NativeSeatIKProfile.json"));if(nativeProfile.occupants.Length!=6)throw new Exception("Saved native profile incomplete");}
            var camper=UnityEngine.Object.Instantiate(prefab);owned.Add(camper);camper.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            if(reviewBundle)
            {
                // Read-only review copies of the actual bundle's decoded buffers.
                // Keep original package intact; permit CPU collision/fit inspection.
                string decodedPath=Arg("-rebirthAvatarDecodedMeshes","");
                var decoded=JsonUtility.FromJson<DecodedMeshes>(File.ReadAllText(decodedPath));
                using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(report.bundle))if(decoded.bundleSha256!=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant())throw new Exception("Decoded preview buffers belong to a different bundle");
                foreach(var filter in camper.GetComponentsInChildren<MeshFilter>(true))
                {
                    var row=decoded.meshes.SingleOrDefault(m=>m.name==filter.sharedMesh.name);if(row==null&&new[]{"Cube","Cylinder"}.Contains(filter.sharedMesh.name)&&AssetDatabase.GetAssetPath(filter.sharedMesh).Contains("unity default resources"))continue;if(row==null)throw new Exception("Decoded mesh missing: ["+filter.sharedMesh.name+"] at "+AnimationUtility.CalculateTransformPath(filter.transform,camper.transform)+"; available="+string.Join(",",decoded.meshes.Select(m=>m.name)));
                    var mesh=new Mesh{name=row.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};owned.Add(mesh);
                    mesh.vertices=row.vertices;mesh.normals=row.normals;mesh.uv=row.uv;if(row.tangents!=null&&row.tangents.Length>0)mesh.tangents=row.tangents;
                    mesh.subMeshCount=row.submeshes.Length;for(int i=0;i<row.submeshes.Length;i++)mesh.SetTriangles(row.submeshes[i].indices,i);mesh.RecalculateBounds();filter.sharedMesh=mesh;
                }
                File.WriteAllText(Path.Combine(output,"decoded-package-preview.txt"),"CPU-readable review copies reconstructed from actual package vertex, normal, tangent, UV and submesh buffers: "+decodedPath+". Original asset bundle unchanged. No source-prefab mesh fallback.");
            }
            var model=camper.transform.Find("GameObject")??camper.transform;foreach(var rb in camper.GetComponentsInChildren<Rigidbody>(true))rb.isKinematic=true;
            if(Arg("-rebirthAvatarNativeBenchSupport","0")=="1")
            {
                if(Arg("-rebirthAvatarSaveCandidate","0")=="1")throw new Exception("Native bench review is not a saved candidate");
                var filter=model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="interior");var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);owned.Add(mesh);filter.sharedMesh=mesh;var vertices=mesh.vertices;
                var plan=JsonUtility.FromJson<BenchIndices>(File.ReadAllText("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-cushion-plan.json"));if(plan.vertexIds.Length!=76)throw new Exception("Measured bench isolation missing");int back=0,cushion=0;
                foreach(int index in plan.vertexIds){var point=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[index]));if(point.z<-.15f){point.z-=.08f;back++;}else if(point.y<.9f){point.y-=point.x>0?.025f:.015f;cushion++;}vertices[index]=filter.transform.InverseTransformPoint(model.TransformPoint(point));}
                mesh.vertices=vertices;mesh.RecalculateBounds();File.WriteAllText(Path.Combine(output,"native-bench-review.txt"),"Review-only isolated padding adjustment to accommodate native recline: back="+back+" vertices -80mmZ; cushion="+cushion+" vertices -15mmY driver/-25mmY passenger. Original source unchanged.");
            }
            if(Arg("-rebirthAvatarCampStations","0")=="1"){ foreach(var t in camper.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="REBIRTHCampInteriorStations"||t.name=="REBIRTHCampRoofStations"||t.name=="REBIRTHCampPowerEquipment"))t.gameObject.SetActive(true); } report.measuredFurniture=MeasureFurniture(model);
            float tableRaise=float.Parse(Arg("-rebirthAvatarReviewTableRaise","0"),CultureInfo.InvariantCulture);
            if(tableRaise<0 || tableRaise>.35f)throw new Exception("Review-only table raise outside measured calibration range");
            if(tableRaise>0)
            {
                var filter=model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="camper_interior");
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);owned.Add(mesh);filter.sharedMesh=mesh;
                var selected=new HashSet<Vector3>();var vertices=mesh.vertices;var indices=mesh.triangles;
                foreach(var face in report.measuredFurniture.Where(f=>f.region=="table"&&f.mesh=="camper_interior"))for(int corner=0;corner<3;corner++)selected.Add(vertices[indices[face.triangle*3+corner]]);
                if(selected.Count<4)throw new Exception("Measured table vertices unavailable");
                var delta=filter.transform.InverseTransformVector(model.TransformVector(Vector3.up*tableRaise));
                // Include the measured tabletop's bevel and duplicate seam
                // vertices; moving horizontal-face vertices alone tears edges.
                for(int vertex=0;vertex<vertices.Length;vertex++)
                {
                    var point=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[vertex]));
                    if(point.x>.1f&&point.x<1f&&point.z>-2f&&point.z<-1f&&point.y>1.33f&&point.y<1.45f)vertices[vertex]+=delta;
                }
                mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();
                if(Arg("-rebirthAvatarCampStations","0")=="1")foreach(var t in camper.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"rusted camping stove","wooden toolbox","steampunk distillation","steampunk notebook"}.Contains(t.name)))t.position+=Vector3.up*tableRaise; report.reviewOnlyTableRaise=tableRaise;report.measuredFurniture=MeasureFurniture(model,tableRaise);
            }
            foreach(var light in camper.GetComponentsInChildren<Light>(true))light.enabled=false;
            var document=new XmlDocument();document.Load(xmlPath);var vehicle=document.SelectSingleNode("/configs/append/vehicle[@name='rebirthCamper80s']");if(vehicle==null)throw new Exception("Exact camper XML vehicle absent.");
            var seats=vehicle.SelectNodes("property[starts-with(@class,'seat')]");if(seats.Count!=6)throw new Exception("Expected exactly six authored camper positions.");
            string bedOverride=Arg("-rebirthAvatarCamperBedClip","");report.bedClipOverride=bedOverride;
            AnimationClip bedClip=null;
            if(Arg("-rebirthAvatarCamperReauthorBed", "0")=="1")
            {
                var author=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperBedPoseAuthoring",false)).FirstOrDefault(t=>t!=null);
                if(author==null)throw new Exception("Bed authoring helper unavailable");
                var holderRoot=new GameObject("MeasuredBedPoseReview");owned.Add(holderRoot);
                new GameObject("M").transform.SetParent(holderRoot.transform,false);
                string folder="Assets/REBIRTHCamperBedReviewGenerated/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                bedClip=(AnimationClip)author.GetMethod("Apply").Invoke(null,new object[]{holderRoot,folder});
                bedOverride=AssetDatabase.GetAssetPath(bedClip);report.bedClipOverride=bedOverride;
            }
            if(!string.IsNullOrEmpty(bedOverride))bedClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(bedOverride);
            else{var holder=model.Find("M/BedPoseAsset");var animator=holder==null?null:holder.GetComponent<Animator>();bedClip=animator?.runtimeAnimatorController?.animationClips.SingleOrDefault(c=>c.name=="rebirthCamperBedLying");}
            if(!defaultOnly&&(bedClip==null || bedClip.humanMotion || bedClip.legacy || AnimationUtility.GetCurveBindings(bedClip).Any(b=>b.type!=typeof(Transform))))
                throw new Exception("A corrected nonlegacy generic skeletal bed clip is required; obsolete humanoid candidate clips are refused.");

            var authoredCabin=new Dictionary<int,AnimationClip>();
            if(Arg("-rebirthAvatarAuthorCabin","0")=="1")
            {
                var author=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperCabinPoseAuthoring",false)).FirstOrDefault(t=>t!=null);
                if(author==null)throw new Exception("Generic cabin pose authoring helper unavailable");
                string folder="Assets/REBIRTHCamperCabinReviewGenerated/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var authored=(AnimationClip[])author.GetMethod("Apply").Invoke(null,new object[]{camper,folder,xmlPath});
                foreach(int slot in new[]{2,3,5})authoredCabin[slot]=authored.Single(c=>c.name=="rebirthCamperCabinSeat"+slot);
            }
            else foreach(int slot in new[]{0,1,2,3,5})
            {
                var holder=model.Find("M/CabinPoseAssets/Seat"+slot);if(holder==null)continue;
                var asset=holder.GetComponent<Animator>();
                var authored=asset?.runtimeAnimatorController?.animationClips.Where(c=>c.name=="rebirthCamperCabinSeat"+slot).Distinct().ToArray();
                if(asset==null||asset.enabled||holder.Find("NativeRigLayout")==null||authored==null||authored.Length!=1||authored[0].humanMotion||authored[0].legacy)
                    throw new Exception("Saved authored cabin dependency invalid for slot "+slot);
                authoredCabin[slot]=authored[0];
            }
            if(Arg("-rebirthAvatarAuthorFront","0")=="1")
            {
                string folder="Assets/REBIRTHCamperFrontReviewGenerated/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var author=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperFrontPoseAuthoring",false)).First(t=>t!=null);
                var clips=(AnimationClip[])author.GetMethod("Apply").Invoke(null,new object[]{camper,folder});
                foreach(int slot in new[]{0,1})authoredCabin[slot]=clips.Single(c=>c.name=="rebirthCamperCabinSeat"+slot);
            }
            if(Arg("-rebirthAvatarSaveCandidate","0")=="1")
            {
                if(authoredCabin.Count<3)throw new Exception("Saving requires all authored cabin poses");
                var saver=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperSeatingCandidate",false)).FirstOrDefault(t=>t!=null);
                if(saver==null)throw new Exception("Seating candidate saver unavailable");
                report.savedCandidate=(string)saver.GetMethod("Save").Invoke(null,new object[]{camper,bedClip});
            }
            if(defaultOnly)
            {
                if(Arg("-rebirthAvatarSaveCandidate","0")=="1")throw new Exception("Default-pose comparison cannot save an authored candidate");
                authoredCabin.Clear();
                report.scope="Default native vehicle poses first, current seat anchors, optional native steering targets; no authored clips applied. Seat4 is a seated baseline, not accepted sleeping pose. Unity comparison only.";
            }
            if(defaultOnly&&Arg("-rebirthAvatarSaveNative","0")=="1")
            {
                var saver=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperSeatingCandidate",false)).First(t=>t!=null);report.savedCandidate=(string)saver.GetMethod("SaveNative").Invoke(null,new object[]{camper,prefabPath});
            }
            GameObject driverBody=null;AnimationClip driverClip=null;Dictionary<string,Transform> driverTargets=null;
            for(int seat=0;seat<6;seat++)
            {
                var node=vehicle.SelectSingleNode("property[@class='seat"+seat+"']");if(node==null)throw new Exception("Missing seat "+seat);
                var anchor=new GameObject("NativeAttachmentModel_Seat"+seat);owned.Add(anchor);anchor.transform.SetParent(model,false);anchor.transform.localPosition=Vec(Value(node,"position"));anchor.transform.localEulerAngles=Vec(Value(node,"rotation"));
                if(useNativeProfile){var profile=nativeProfile.occupants.Single(r=>r.seat==seat);anchor.transform.localPosition=profile.position;anchor.transform.localEulerAngles=profile.rotation;report.scope=nativeProfile.scope+"; reopened saved source/profile; no review-only support edits.";}
                if(defaultOnly&&Arg("-rebirthAvatarNativeSupportIK","0")=="1")
                {
                    anchor.transform.position+=anchor.transform.forward*(seat<2?0:seat==5?.12f:seat==2||seat==3?.08f:0);
                    if(seat==2||seat==3)anchor.transform.position-=model.right*(seat==2?.13f:.08f);
                    if(seat==5)anchor.transform.position+=model.up*.015f;
                    if(seat==2||seat==5)anchor.transform.position+=model.up*.01f;else if(seat==3)anchor.transform.position+=model.up*.02f;
                }
                bool useMale=(seat%2==0)^(Arg("-rebirthAvatarSwapBodies","0")=="1");
                var body=UnityEngine.Object.Instantiate(useMale?male:female);owned.Add(body);body.name="ActualNativeOccupant_"+seat;body.SetActive(true);
                foreach(var behavior in body.GetComponentsInChildren<MonoBehaviour>(true))if(behavior!=null)behavior.enabled=false;
                body.transform.SetParent(anchor.transform,false);body.transform.localPosition=Vector3.zero;body.transform.localRotation=Quaternion.identity;
                AttachActualHead(body,useMale,owned);
                var animator=body.GetComponent<Animator>();if(animator==null || animator.avatar==null || !animator.avatar.isValid || animator.avatar.isHuman)throw new Exception("Actual native generic rig expected for seat "+seat);
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
                int pose=int.Parse(Value(node,"pose"),CultureInfo.InvariantCulture);
                if(useNativeProfile)pose=nativeProfile.occupants.Single(r=>r.seat==seat).pose;
                bool nativeBed=defaultOnly&&seat==4&&Arg("-rebirthAvatarNativeBed","0")=="1";if(nativeBed)pose=0;
                bool nativeReview=defaultOnly||(((seat<2&&!authoredCabin.ContainsKey(seat))||Arg("-rebirthAvatarNativeCabin","0")=="1")&&seat!=4);
                string cabinPose=Arg("-rebirthAvatarCabinPose", "");
                if(nativeReview && seat>=2 && seat!=4 && !string.IsNullOrEmpty(cabinPose))
                {
                    int candidate=int.Parse(cabinPose,CultureInfo.InvariantCulture);
                    if(candidate<41 || candidate>45)throw new Exception("Cabin comparison requires a native 4x4 passenger pose 41..45.");
                    pose=candidate;
                }
                var clips=new List<string>();
                Dictionary<string,Transform> vehicleTargets=null;
                if(seat==0 && Arg("-rebirthAvatarSteeringIK","0")=="1")
                {
                    vehicleTargets=new Dictionary<string,Transform>();
                    var steering=vehicle.SelectSingleNode("property[@class='handlebars']");
                    if(steering==null)throw new Exception("Native steering properties missing");
                    var joint=model.Find(Value(steering,"transform"));
                    if(joint==null)throw new Exception("Native steering transform missing");
                    foreach(string hand in new[]{"IKHandL","IKHandR"})
                    {
                        var target=new GameObject(hand);owned.Add(target);
                        target.transform.SetParent(joint,false);
                        target.transform.localPosition=Vec(Value(steering,hand+"Position"));
                        target.transform.localEulerAngles=Vec(Value(steering,hand+"Rotation"));
                        vehicleTargets.Add(hand,target.transform);
                    }
                }
                if(nativeReview)clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,vehicleTargets));
                else{animator.SetInteger("VehiclePose",pose);for(int step=0;step<60;step++)animator.Update(1f/30f);for(int layer=0;layer<animator.layerCount;layer++)clips.AddRange(animator.GetCurrentAnimatorClipInfo(layer).Select(c=>c.clip.name+" weight="+c.weight));}
                if(useNativeProfile)
                {
                    vehicleTargets=new Dictionary<string,Transform>();var profile=nativeProfile.occupants.Single(r=>r.seat==seat);
                    foreach(var record in profile.ikTargets)
                    {
                        var target=new GameObject(record.name);owned.Add(target);var parent=seat==0&&record.name.StartsWith("IKHand")?model.Find(Value(vehicle.SelectSingleNode("property[@class='handlebars']"),"transform")):model;
                        target.transform.SetParent(parent,false);target.transform.position=model.TransformPoint(record.modelPosition);target.transform.rotation=model.rotation*Quaternion.Euler(record.modelRotation);vehicleTargets.Add(record.name,target.transform);
                    }
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,vehicleTargets));
                }
                if(useNativeProfile&&seat==4&&Arg("-rebirthAvatarRelaxedBed","0")=="1")
                {
                    // Re-evaluate the native idle before fitting limbs; discard the rejected curled targets.
                    anchor.transform.localRotation=Quaternion.Euler(0,25,0)*Quaternion.Euler(0,0,90)*Quaternion.Euler(0,90,0);
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,0,null));
                    var bones=body.GetComponentsInChildren<Transform>(true);var hips=bones.Single(t=>t.name=="Hips");
                    anchor.transform.position+=model.TransformPoint(new Vector3(0,2.035f,-.08f))-hips.position;
                    var along=new Vector3(.9063078f,0,-.4226183f);var across=Vector3.Cross(Vector3.up,along);
                    vehicleTargets=new Dictionary<string,Transform>();
                    foreach(string side in new[]{"Left","Right"})
                    {
                        string suffix=side=="Left"?"L":"R";float sign=side=="Left"?1:-1;
                        var upper=bones.Single(t=>t.name==side+"UpLeg");var knee=bones.Single(t=>t.name==side+"Leg");var foot=bones.Single(t=>t.name==side+"Foot");
                        float length=Vector3.Distance(upper.position,knee.position)+Vector3.Distance(knee.position,foot.position);
                        var footPoint=model.InverseTransformPoint(upper.position)+along*(length*.94f);footPoint.y=1.99f;
                        foreach(string kind in new[]{"IKFoot","IKKnee","IKHand"})
                        {
                            var go=new GameObject(kind+suffix);owned.Add(go);go.transform.SetParent(model,false);
                            go.transform.localPosition=kind=="IKFoot"?footPoint:kind=="IKKnee"?model.InverseTransformPoint(upper.position)+along*.4f+Vector3.up*.3f:new Vector3(0,2.005f,-.08f)-along*.06f+across*(sign*.23f);
                            go.transform.rotation=kind=="IKFoot"?foot.rotation:bones.Single(t=>t.name==side+"Hand").rotation;
                            vehicleTargets.Add(go.name,go.transform);
                        }
                    }
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,0,vehicleTargets));
                    report.scope="Relaxed diagonal bed trial: native idle first, then limb IK; rejected curled targets discarded. Source unchanged; visual acceptance required.";
                }
                if(useNativeProfile&&seat==4&&Arg("-rebirthAvatarRestingBed","0")=="1")
                {
                    var bones=body.GetComponentsInChildren<Transform>(true);var pelvis=model.InverseTransformPoint(bones.Single(t=>t.name=="Hips").position);
                    foreach(string side in new[]{"Left","Right"})
                    {
                        string suffix=side=="Left"?"L":"R";float sign=side=="Left"?1:-1;
                        // Retain bent knees and foot positions; relax the sole direction away from rigid dorsiflexion.
                        var relaxedFoot=RebirthStandardVehicleSeatingReview.LevelFootRotation(body,model,side,model.TransformDirection(new Vector3(1,.85f,sign*.08f).normalized));
                        vehicleTargets["IKFoot"+suffix].rotation=Quaternion.Slerp(vehicleTargets["IKFoot"+suffix].rotation,relaxedFoot,.35f);
                        vehicleTargets["IKHand"+suffix].localPosition=new Vector3(pelvis.x+.12f,1.925f,pelvis.z+sign*.24f);
                        var hint=new GameObject("IKElbow"+suffix);owned.Add(hint);hint.transform.SetParent(model,false);hint.transform.localPosition=new Vector3(pelvis.x-.22f,1.94f,pelvis.z+sign*.28f);vehicleTargets.Add(hint.name,hint.transform);
                    }
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,0,vehicleTargets));
                    report.scope="Owner-directed bed trial: bent knees retained; relaxed foot orientation and lowered hand targets with elbow hints for mattress-supported arms. Native pose first; source unchanged.";
                    if(Arg("-rebirthAvatarPlantBedFeet","0")=="1")
                    {
                        // Keep accepted arm targets; plant soles for the bent-knee resting posture.
                        foreach(string side in new[]{"Left","Right"})vehicleTargets[side=="Left"?"IKFootL":"IKFootR"].rotation=RebirthStandardVehicleSeatingReview.LevelFootRotation(body,model,side,model.right);
                        clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,0,vehicleTargets));
                        var skin=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="feet");var mesh=new Mesh();
                        try
                        {
                            skin.BakeMesh(mesh);var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
                            foreach(bool left in new[]{true,false})
                            {
                                var selected=points.Where(p=>left?body.transform.InverseTransformPoint(p).x<0:body.transform.InverseTransformPoint(p).x>0).Select(p=>model.InverseTransformPoint(p)).ToArray();
                                if(selected.Length<50)throw new Exception("Actual foot sole samples missing");
                                var target=vehicleTargets[left?"IKFootL":"IKFootR"];var position=target.localPosition;position.y+=1.9208545f-selected.Min(p=>p.y);target.localPosition=position;
                            }
                        }
                        finally{UnityEngine.Object.DestroyImmediate(mesh);}
                        clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,0,vehicleTargets));
                        report.scope+=" Feet only: level soles and native IK ankle heights solved from actual skin minimum to mattress top+2mm; arm targets unchanged.";
                    }
                }
                if(nativeBed)
                {
                    var reference=JsonUtility.FromJson<Report>(File.ReadAllText(Arg("-rebirthAvatarSupportReference",""))).occupants.Single(r=>r.seat==4);
                    anchor.transform.localRotation=Quaternion.AngleAxis(-3,Vector3.forward)*Quaternion.Euler(0,0,90)*Quaternion.Euler(0,90,0);
                    var hips=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hips");anchor.transform.position+=model.TransformPoint(reference.hips)-hips.position;
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,vehicleTargets));
                    report.scope="Native controller poses first with native IK; bed trial uses native idle pose0, whole attachment supine placement and limb targets, no custom skeletal clips. Unity only; fit not automatically accepted.";
                }
                if(defaultOnly&&(seat!=4||nativeBed)&&Arg("-rebirthAvatarNativeSupportIK","0")=="1")
                {
                    var measured=JsonUtility.FromJson<Report>(File.ReadAllText(Arg("-rebirthAvatarSupportReference",""))).occupants.Single(r=>r.seat==seat);
                    if(vehicleTargets==null)vehicleTargets=new Dictionary<string,Transform>();
                    foreach(string side in new[]{"Left","Right"})
                    {
                        var bones=body.GetComponentsInChildren<Transform>(true);var foot=bones.Single(t=>t.name==side+"Foot");var target=new GameObject(side=="Left"?"IKFootL":"IKFootR");owned.Add(target);target.transform.SetParent(model,false);
                        target.transform.localPosition=side=="Left"?measured.leftFoot:measured.rightFoot;
                        var direction=anchor.transform.forward;
                        if(seat<2)direction=(direction+model.up*.5f).normalized;
                        if((seat==0&&side=="Right")||(seat==1&&side=="Left"))direction=Quaternion.AngleAxis(seat==0?-30:30,model.up)*direction;
                        target.transform.rotation=nativeBed?foot.rotation:RebirthStandardVehicleSeatingReview.LevelFootRotation(body,model,side,direction);
                        vehicleTargets.Add(target.name,target.transform);
                        var hint=new GameObject(side=="Left"?"IKKneeL":"IKKneeR");owned.Add(hint);hint.transform.SetParent(model,false);hint.transform.localPosition=side=="Left"?measured.leftKnee:measured.rightKnee;vehicleTargets.Add(hint.name,hint.transform);
                        if(seat!=0)
                        {
                            var hand=bones.Single(t=>t.name==side+"Hand");var handTarget=new GameObject(side=="Left"?"IKHandL":"IKHandR");owned.Add(handTarget);handTarget.transform.SetParent(anchor.transform,false);handTarget.transform.localPosition=new Vector3(side=="Left"?-.16f:.16f,1.12f,.1f);if(nativeBed)handTarget.transform.position=model.TransformPoint(measured.hips+new Vector3(.18f,.09f,side=="Left"?.16f:-.16f));handTarget.transform.rotation=hand.rotation;vehicleTargets.Add(handTarget.name,handTarget.transform);
                        }
                    }
                    clips.Clear();clips.AddRange(RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,vehicleTargets));
                    if(seat==0&&Arg("-rebirthAvatarCalibrateGrip","0")=="1")RebirthCamperHandGripCalibration.Calibrate(body,vehicleTargets,()=>RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,vehicleTargets),output);
                }
                if(defaultOnly&&seat==0){driverBody=body;driverTargets=vehicleTargets;}
                AnimationClip authoredClip=seat==4&&!defaultOnly?bedClip:null;
                if(authoredCabin.ContainsKey(seat))authoredClip=authoredCabin[seat];
                if(seat!=4&&!defaultOnly)
                {
                    string path=Arg("-rebirthAvatarCabinSeat"+seat,"");
                    if(!string.IsNullOrEmpty(path))
                    {
                        authoredClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if(authoredClip==null || authoredClip.humanMotion || authoredClip.legacy || AnimationUtility.GetCurveBindings(authoredClip).Any(c=>c.type!=typeof(Transform)))
                            throw new Exception("Explicit generic cabin pose is missing or incompatible for seat "+seat);
                    }
                }
                if(authoredClip!=null && seat<2)
                {
                    RebirthCamperAvatarProbe.EvaluateAuthoredPoseSnapshot(body,authoredClip,vehicleTargets);
                    if(seat==0){driverBody=body;driverClip=authoredClip;driverTargets=vehicleTargets;}
                }
                else if(authoredClip!=null)
                {
                    if(nativeReview)throw new Exception("Native cabin review refuses authored clip override");
                    var graph=PlayableGraph.Create("CamperSeatGenericReview"+seat);graphs.Add(graph);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip=AnimationClipPlayable.Create(graph,authoredClip);clip.SetApplyFootIK(false);clip.SetApplyPlayableIK(false);clip.SetTime(.5);clip.SetSpeed(0);
                    var destination=AnimationPlayableOutput.Create(graph,"ActualNativeAuthoredBody",animator);destination.SetSourcePlayable(clip);graph.Play();graph.Evaluate(0);
                }
                int vertices;var bounds=BakedBounds(body,out vertices);bodies.Add(bounds);
                var row=new Occupant{seat=seat,pose=pose,vertices=vertices,body=useMale?"native male default skin":"native female default skin",genericAvatar=true,headMeshPresent=true,nativeIKApplied=vehicleTargets!=null,
                    bedClip=seat==4&&authoredClip!=null?bedClip.name:null,authoredPoseClip=authoredClip==null?null:AssetDatabase.GetAssetPath(authoredClip),activeClips=authoredClip!=null?new[]{"generic clip playable: "+authoredClip.name}:clips.ToArray(),position=anchor.transform.localPosition,rotation=anchor.transform.localEulerAngles,
                    boundsCenter=model.InverseTransformPoint(bounds.center),boundsSize=bounds.size,hips=Point(body,model,"Hips"),head=Point(body,model,"Head"),leftKnee=Point(body,model,"LeftLeg"),leftFoot=Point(body,model,"LeftFoot"),rightKnee=Point(body,model,"RightLeg"),rightFoot=Point(body,model,"RightFoot")};
                MeasureSkinBands(body,model,row);
                if(vehicleTargets!=null)row.ikTargets=vehicleTargets.Select(t=>new TargetRecord{name=t.Key,modelPosition=model.InverseTransformPoint(t.Value.position),modelRotation=(Quaternion.Inverse(model.rotation)*t.Value.rotation).eulerAngles}).ToArray();
                if(seat==2 || seat==3 || seat==5)row.footContacts=MeasureFootContact(body,model,report.measuredFurniture);
                evidence.Add(row);report.occupants=evidence.ToArray();
            }
            if(Arg("-rebirthAvatarShinAudit","0")=="1")
            {
                var auditor=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperShinCollisionReview",false)).FirstOrDefault(t=>t!=null);
                if(auditor==null)throw new Exception("Exact shin collision reviewer unavailable");
                auditor.GetMethod("Review").Invoke(null,new object[]{model,Path.Combine(output,"shin_collision")});
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.white*.7f;
            var lightGo=new GameObject("SeatingReviewLight");owned.Add(lightGo);var lamp=lightGo.AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.2f;lightGo.transform.rotation=Quaternion.Euler(45,25,0);
            var cameraGo=new GameObject("SeatingReviewCamera");owned.Add(cameraGo);var camera=cameraGo.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.2f,.24f);camera.nearClipPlane=.01f;camera.farClipPlane=50;
            View(camera,model,new Vector3(-7,3.5f,4),new Vector3(0,1.25f,-.8f),false,3.3f,output,"all_six_exterior",images);
            var camperRenderers=camper.GetComponentsInChildren<Renderer>(true).Where(r=>!r.GetComponentInParent<Animator>() || !r.GetComponentInParent<Animator>().name.StartsWith("ActualNativeOccupant_")).ToArray();
            var originalRendererStates=camperRenderers.Select(r=>r.enabled).ToArray();
            // Renderer selection applies only to the camper, never the cloned skins.
            var actualSkins=camper.GetComponentsInChildren<SkinnedMeshRenderer>(true);var furniture=camperRenderers.Where(r=>!actualSkins.Contains(r as SkinnedMeshRenderer)&&r.sharedMaterials.Any(m=>m!=null&&m.name.IndexOf("interior",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            if(furniture.Length==0)throw new Exception("No candidate interior material geometry was identified.");
            foreach(var renderer in camperRenderers)if(!(renderer is SkinnedMeshRenderer))renderer.enabled=furniture.Contains(renderer)||(Arg("-rebirthAvatarCampStations","0")=="1"&&renderer.GetComponentsInParent<Transform>(true).Any(t=>t.name=="REBIRTHCampInteriorStations"));
            View(camera,model,new Vector3(0,8,-1),new Vector3(0,1.2f,-1),true,2.6f,output,"all_six_cutaway_top",images);
            View(camera,model,new Vector3(-6,3.4f,-1),new Vector3(0,1.3f,-1),true,2.8f,output,"all_six_cutaway_left",images);
            View(camera,model,new Vector3(6,3.4f,-1),new Vector3(0,1.3f,-1),true,2.8f,output,"all_six_cutaway_right",images);
            View(camera,model,new Vector3(-.5f,2,-3),new Vector3(.65f,1.2f,-1.5f),false,1,output,"dinette_and_sink",images);
            View(camera,model,new Vector3(0,2.8f,-1.6f),new Vector3(0,2,-.1f),false,1,output,"bed_from_cabin",images);
            if(Arg("-rebirthAvatarRestingBed","0")=="1")
            {
                View(camera,model,new Vector3(0,2.18f,-1.4f),new Vector3(0,2.1f,-.1f),false,1,output,"bed_low_support",images);
                View(camera,model,new Vector3(.9f,2.7f,-1.2f),new Vector3(0,2.05f,-.1f),false,1,output,"bed_foot_oblique",images);
            }
            View(camera,model,new Vector3(-2.3f,1.5f,.6f),new Vector3(0,.8f,.2f),false,1,output,"driver_front_passenger",images);
            var cab=camperRenderers.Where(r=>r.name=="interior").ToArray();foreach(var renderer in cab)renderer.enabled=false;
            View(camera,model,new Vector3(-.9f,1.5f,.9f),new Vector3(-.413f,1.15f,.34f),true,.38f,output,"front_hands_cutaway",images);
            foreach(var renderer in cab)renderer.enabled=true;
            if(Arg("-rebirthAvatarSteeringSweep","0")=="1")
            {
                if(driverBody==null||driverTargets==null)throw new Exception("Evaluated native driver required for steering sweep");
                var joint=driverTargets.Values.First().parent;var neutral=joint.localRotation;
                foreach(float angle in new[]{-60f,60f})
                {
                    joint.localRotation=neutral;
                    if(driverClip!=null)RebirthCamperAvatarProbe.EvaluateAuthoredPoseSnapshot(driverBody,driverClip,driverTargets,angle);
                    else{joint.localRotation=neutral*Quaternion.AngleAxis(angle,Vector3.up);RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(driverBody,40,driverTargets);}
                    string label=angle<0?"steer_left":"steer_right";
                    var auditor=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperShinCollisionReview",false)).First(t=>t!=null);
                    var previewStates=camperRenderers.Select(r=>r.enabled).ToArray();for(int i=0;i<camperRenderers.Length;i++)camperRenderers[i].enabled=originalRendererStates[i];
                    auditor.GetMethod("Review").Invoke(null,new object[]{model,Path.Combine(output,label+"_collision")});
                    for(int i=0;i<camperRenderers.Length;i++)camperRenderers[i].enabled=previewStates[i];
                    View(camera,model,new Vector3(-2.3f,1.5f,.6f),new Vector3(0,.8f,.2f),false,1,output,label+"_cab",images);
                    foreach(var renderer in cab)renderer.enabled=false;
                    View(camera,model,new Vector3(-.9f,1.5f,.9f),new Vector3(-.413f,1.15f,.34f),true,.38f,output,label+"_hands",images);
                    foreach(var renderer in cab)renderer.enabled=true;
                }
                joint.localRotation=neutral;if(driverClip!=null)RebirthCamperAvatarProbe.EvaluateAuthoredPoseSnapshot(driverBody,driverClip,driverTargets);else RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(driverBody,40,driverTargets);
            }
            var overlaps=new List<string>();for(int a=0;a<bodies.Count;a++)for(int b=a+1;b<bodies.Count;b++)if(bodies[a].Intersects(bodies[b]))overlaps.Add("seat"+a+"/seat"+b+": coarse world bounds overlap, not a mesh collision determination");
            report.coarseBodyBoundsOverlaps=overlaps.ToArray();report.images=images.ToArray();report.status="PASS_SIX_NATIVE_BODIES_RENDERED_NOT_COMFORT_ACCEPTED";
            if(defaultOnly&&!string.IsNullOrEmpty(report.savedCandidate))
            {
                var saver=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RebirthCamperSeatingCandidate",false)).First(t=>t!=null);saver.GetMethod("AttachNativeProfile").Invoke(null,new object[]{report.savedCandidate,JsonUtility.ToJson(report)});
            }
        }
        catch(Exception error){report.error=error.ToString();throw;}
        finally
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));foreach(var graph in graphs)if(graph.IsValid())graph.Destroy();
            foreach(var obj in owned.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientLight=previousAmbient;
            if(reviewBundle)reviewBundle.Unload(true);
        }
    }
    static FootContact[] MeasureFootContact(GameObject body,Transform model,FurnitureFace[] faces)
    {
        var results=new List<FootContact>();
        foreach(string side in new[]{"Left","Right"})
        {
            var contact=new FootContact{side=side,minimumClearance=float.PositiveInfinity};
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var weights=skin.sharedMesh.boneWeights;if(weights.Length==0)continue;
                var baked=new Mesh();try{skin.BakeMesh(baked);var vertices=baked.vertices;
                    for(int n=0;n<vertices.Length;n++)
                    {
                        var weight=weights[n];int[] indices={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] values={weight.weight0,weight.weight1,weight.weight2,weight.weight3};float footWeight=0;
                        for(int j=0;j<4;j++)if(values[j]>0 && skin.bones[indices[j]]!=null && (skin.bones[indices[j]].name==side+"Foot"||skin.bones[indices[j]].name.StartsWith(side+"Toe")))footWeight+=values[j];
                        if(footWeight<.5f)continue;var point=model.InverseTransformPoint(skin.transform.TransformPoint(vertices[n]));float floor=float.NegativeInfinity;
                        foreach(var face in faces)
                        {
                            var a=face.a;var b=face.b;var c=face.c;float yMean=(a.y+b.y+c.y)/3;if(yMean<.7f||yMean>1.3f)continue;
                            float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.000001f)continue;
                            float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/d;float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/d;
                            if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;floor=Mathf.Max(floor,u*a.y+v*b.y+(1-u-v)*c.y);
                        }
                        contact.sampledVertices++;if(float.IsNegativeInfinity(floor)){contact.unsupportedVertices++;continue;}contact.minimumClearance=Mathf.Min(contact.minimumClearance,point.y-floor);
                    }
                }finally{UnityEngine.Object.DestroyImmediate(baked);}
            }
            if(contact.sampledVertices==0 || float.IsInfinity(contact.minimumClearance))throw new Exception("Actual foot skin/floor evidence unavailable for "+side);
            results.Add(contact);
        }
        return results.ToArray();
    }
    public static void AttachActualHead(GameObject body,bool male,List<UnityEngine.Object> owned)
    {
        string asset="Assets/AssetBundles/Player/"+(male?"Male":"Female")+"/Heads/White/01/Meshes/player"+(male?"Male":"Female")+"White01.fbx";
        var bundle=AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b=>b.GetAllAssetNames().Any(n=>n.Equals(asset,StringComparison.OrdinalIgnoreCase)));
        if(bundle==null)throw new Exception("Required real default head asset dependency was not loaded: "+asset);
        var source=bundle.LoadAsset<GameObject>(asset);if(source==null)throw new Exception("Native head asset failed to load");
        var head=UnityEngine.Object.Instantiate(source);owned.Add(head);
        var catalog=body.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.ToArray());
        Func<Transform,Transform> map=null;map=old=>
        {
            if(old==null)throw new Exception("Null native head bone");Transform[] matches;
            if(catalog.TryGetValue(old.name,out matches)){if(matches.Length!=1)throw new Exception("Ambiguous native head bone "+old.name);return matches[0];}
            if(old.parent==null || old==head.transform)throw new Exception("Missing native head parent chain: "+old.name);
            var parent=map(old.parent);var added=new GameObject(old.name).transform;added.SetParent(parent,false);added.localPosition=old.localPosition;added.localRotation=old.localRotation;added.localScale=old.localScale;catalog[old.name]=new[]{added};return added;
        };
        var skins=head.GetComponentsInChildren<SkinnedMeshRenderer>(true);if(skins.Length==0)throw new Exception("Native head has no actual skinned mesh");
        foreach(var skin in skins)
        {
            var bones=skin.bones.Select(map).ToArray();var root=map(skin.rootBone);skin.transform.SetParent(body.transform,false);skin.bones=bones;skin.rootBone=root;skin.updateWhenOffscreen=true;
            var materials=skin.sharedMaterials;for(int m=0;m<materials.Length;m++){var copy=new Material(Shader.Find("Standard"));owned.Add(copy);copy.color=new Color(.63f,.51f,.43f);if(materials[m]!=null&&materials[m].HasProperty("_MainTex"))copy.mainTexture=materials[m].GetTexture("_MainTex");materials[m]=copy;}skin.sharedMaterials=materials;
        }
        head.SetActive(false);
    }
    static FurnitureFace[] MeasureFurniture(Transform model,float tableRaise=0)
    {
        var faces=new List<FurnitureFace>();foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="camper_interior"||f.name=="camper_body"||f.name=="Rear entry floor"))
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int n=0;n<triangles.Length;n+=3){var a=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[n]]));var b=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[n+1]]));var c=model.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[n+2]]));var cross=Vector3.Cross(b-a,c-a);if(cross.magnitude<.00002f || Mathf.Abs(cross.normalized.y)<.85f)continue;var center=(a+b+c)/3;string region="other_horizontal";
                if(center.x>.2f&&center.x<1&&center.y>1.2f&&center.y<1.34f){if(center.z>-1.1f&&center.z<-.55f)region="front_cushion";if(center.z>-2.4f&&center.z<-1.9f)region="rear_cushion";}
                if(center.x>.1f&&center.x<1&&center.z>-1.9f&&center.z<-1.1f&&center.y>1.33f+tableRaise&&center.y<1.45f+tableRaise)region="table";
                if(center.x>-.8f&&center.x<.8f&&center.z>-.5f&&center.z<.28f&&center.y>1.85f&&center.y<2)region="bed";
                if(center.x>-.9f&&center.x<-.25f&&center.z>-1.2f&&center.z<-.55f&&center.y>1.45f&&center.y<1.6f)region="sink_seat";
                faces.Add(new FurnitureFace{mesh=filter.name,region=region,triangle=n/3,a=a,b=b,c=c,normalY=cross.normalized.y,area=cross.magnitude*.5f});}
        }
        if(faces.Count==0)throw new Exception("Actual prefab furniture mesh measurement failed");return faces.ToArray();
    }
    static void MeasureSkinBands(GameObject body,Transform model,Occupant row)
    {
        float pelvis=float.PositiveInfinity,thigh=float.NegativeInfinity;foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {var mesh=new Mesh();try{skin.BakeMesh(mesh);foreach(var vertex in mesh.vertices){var p=model.InverseTransformPoint(skin.transform.TransformPoint(vertex));var horizontal=new Vector2(p.x-row.hips.x,p.z-row.hips.z);if(horizontal.magnitude<.16f&&Mathf.Abs(p.y-row.hips.y)<.22f)pelvis=Mathf.Min(pelvis,p.y);if(Mathf.Abs(p.y-row.hips.y)<.22f&&(Distance(p,row.hips,row.leftKnee)<.14f||Distance(p,row.hips,row.rightKnee)<.14f))thigh=Mathf.Max(thigh,p.y);}}finally{UnityEngine.Object.DestroyImmediate(mesh);}}
        if(float.IsInfinity(pelvis)||float.IsInfinity(thigh))throw new Exception("Actual pelvis/thigh vertex evidence unavailable");row.pelvisBottomY=pelvis;row.thighTopY=thigh;
    }
    static float Distance(Vector3 point,Vector3 a,Vector3 b){var line=b-a;float t=line.sqrMagnitude==0?0:Mathf.Clamp01(Vector3.Dot(point-a,line)/line.sqrMagnitude);return Vector3.Distance(point,a+t*line);}
    static string Value(XmlNode seat,string name){var property=seat.SelectSingleNode("property[@name='"+name+"']");if(property==null)throw new Exception("Seat property missing: "+name);return property.Attributes["value"].Value;}
    static Vector3 Vec(string text){var parts=text.Split(',');if(parts.Length!=3)throw new Exception("Invalid seat vector");return new Vector3(float.Parse(parts[0],CultureInfo.InvariantCulture),float.Parse(parts[1],CultureInfo.InvariantCulture),float.Parse(parts[2],CultureInfo.InvariantCulture));}
    static Vector3 Point(GameObject body,Transform model,string name){var bones=body.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();if(bones.Length!=1)throw new Exception("Native fit bone absent/ambiguous: "+name);return model.InverseTransformPoint(bones[0].position);}
    static Bounds BakedBounds(GameObject body,out int count)
    {
        var bounds=new Bounds();bool started=false;count=0;foreach(var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {var mesh=new Mesh();try{renderer.BakeMesh(mesh);foreach(var vertex in mesh.vertices){var point=renderer.transform.TransformPoint(vertex);if(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.z))throw new Exception("Nonfinite native skin vertex");if(!started){bounds=new Bounds(point,Vector3.zero);started=true;}else bounds.Encapsulate(point);count++;}}finally{UnityEngine.Object.DestroyImmediate(mesh);}}
        if(!started)throw new Exception("No actual native skin geometry available");return bounds;
    }
    static void View(Camera camera,Transform model,Vector3 from,Vector3 at,bool orthographic,float size,string output,string label,List<string> images)
    {
        camera.orthographic=orthographic;camera.orthographicSize=size;camera.fieldOfView=55;camera.transform.position=model.TransformPoint(from);var direction=model.TransformPoint(at)-camera.transform.position;var up=Mathf.Abs(Vector3.Dot(direction.normalized,model.up))>.99f?model.forward:model.up;camera.transform.rotation=Quaternion.LookRotation(direction,up);
        var previous=RenderTexture.active;var target=new RenderTexture(1600,1200,24);var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);
        // Match the collision audit's CPU-evaluated native skin. Batch rendering
        // can reuse stale GPU skinning after manually evaluating the preview graph.
        // These temporary snapshots never enter the saved source or bundle.
        var snapshots=new List<GameObject>();var meshes=new List<Mesh>();var hidden=new List<SkinnedMeshRenderer>();
        foreach(var skin in model.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.enabled&&s.gameObject.activeInHierarchy))
        {
            var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
            var snapshot=new GameObject("EvaluatedNativeSkin_RenderOnly");snapshots.Add(snapshot);
            snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);snapshot.transform.localScale=skin.transform.lossyScale;
            snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;snapshot.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            hidden.Add(skin);skin.enabled=false;
        }
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();string path=Path.Combine(output,label+".png");File.WriteAllBytes(path,texture.EncodeToPNG());images.Add(path);}
        finally{foreach(var snapshot in snapshots)UnityEngine.Object.DestroyImmediate(snapshot);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);foreach(var skin in hidden)skin.enabled=true;camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);}
    }
    static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:fallback;}
}












