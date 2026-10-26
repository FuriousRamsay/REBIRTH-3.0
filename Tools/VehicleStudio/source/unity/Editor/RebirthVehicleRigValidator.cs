using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleRigValidator
    {
        public sealed class Finding
        {
            public string severity, code, message;
            public Finding(string severity, string code, string message)
            { this.severity=severity; this.code=code; this.message=message; }
            public override string ToString() { return severity+" ["+code+"] "+message; }
        }
        public static List<Finding> Inspect(Transform root, RebirthVehicleRigProfile profile, bool forExport=false)
        {
            var findings = new List<Finding>();
            if(root==null || profile==null)
            {
                findings.Add(new Finding("ERROR","NO_WORKSPACE","Prefab root or rig profile is null."));
                return findings;
            }
            // Scene-only rig workspace components are stripped by SaveCleanPrefab.
            // Unrecognized behaviours and missing scripts must NEVER ship in the
            // bundle because the 7DTD game would not necessarily have those DLLs.
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null)
                    findings.Add(new Finding("ERROR","MISSING_SCRIPT",
                        "A prefab GameObject has a missing MonoBehaviour script."));
                else if (script is RebirthVehicleRigWorkspace)
                {
                    if (forExport)
                        findings.Add(new Finding("ERROR","AUTHORING_SCRIPT_IN_BUNDLE",
                            "Remove the REBIRTH Editor authoring workspace before packaging the prefab."));
                }
                else
                    findings.Add(new Finding("ERROR","UNVERIFIED_COMPONENT",
                        "Custom MonoBehaviour would require a matching game runtime DLL: "+script.GetType().FullName));
            }
            bool anyGeometry = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    anyGeometry = true;
                    foreach (var mat in renderer.sharedMaterials)
                        if (mat == null || mat.shader == null)
                            findings.Add(new Finding("WARNING","MISSING_MATERIAL",
                                "A renderer has a missing material/shader: "+renderer.name));
                }
            }
            if (!anyGeometry)
                findings.Add(new Finding("ERROR","NO_VISUAL_GEOMETRY",
                    "Vehicle prefab contains no renderable mesh geometry."));
            if(profile.sourceXmlStatus != "ok")
                findings.Add(new Finding("WARNING","XML_UNBOUND","The imported capture XML reference was not verified."));
            var ids = new HashSet<int>();
            foreach(var seat in profile.seats)
            {
                if(seat==null) continue;
                if(seat.index<0 || seat.index>5 || !ids.Add(seat.index))
                    findings.Add(new Finding("ERROR","BAD_SEAT","Seat index invalid or duplicated: "+seat.index));
                if(string.IsNullOrWhiteSpace(seat.poseId))
                    findings.Add(new Finding("WARNING","NO_POSE","seat"+seat.index+" has no pose ID."));
                if(seat.exits==null || seat.exits.Count==0)
                    findings.Add(new Finding("WARNING","NO_EXIT","seat"+seat.index+" has no exit attempts."));
                if(!Finite(seat.position) || !Finite(seat.rotationEuler))
                    findings.Add(new Finding("ERROR","BAD_SEAT_VECTOR","seat"+seat.index+" has invalid coordinates."));
                if(seat.exits!=null)
                    foreach(var exit in seat.exits)
                        if(!Finite(exit)) findings.Add(new Finding("ERROR","BAD_EXIT_VECTOR",
                            "seat"+seat.index+" has a nonfinite exit coordinate."));
            }
            if(!ids.Contains(0))
                findings.Add(new Finding("ERROR","NO_DRIVER","Driver seat0 missing."));
            foreach(var link in profile.links)
            {
                if(link==null || string.IsNullOrEmpty(link.transformPath)) continue;
                var node=RebirthVehicleRigBuilder.FindPath(root,link.transformPath);
                if(node==null)
                    findings.Add(new Finding("ERROR","MISSING_PATH",link.partClass+"."+link.propertyName+
                        " references missing "+link.transformPath));
                else if(link.authoredPlaceholder)
                    findings.Add(new Finding("WARNING","CHECK_PLACEHOLDER","Reference was created as an empty joint: "+
                        link.transformPath+". Verify precise pivot position and axes."));
            }
            // Wide sanity bounds only: actual animations and occupant fit must be
            // observed inside 7DTD, not inferred from two static points in Unity.
            VehicleSeatAnchor driver=null;
            foreach(var seat in profile.seats)
                if(seat!=null && seat.index==0) {driver=seat;break;}
            foreach(var ik in profile.ik)
            {
                if(ik==null) continue;
                var pivot=RebirthVehicleRigBuilder.FindPath(root,ik.pivotPath);
                if(pivot==null)
                    findings.Add(new Finding("ERROR","IK_PIVOT_MISSING",ik.partClass+
                        " pivot unresolved: "+ik.pivotPath));
                if ((ik.hasLeftHand && (!Finite(ik.leftHandPosition) || !Finite(ik.leftHandRotationEuler))) ||
                    (ik.hasRightHand && (!Finite(ik.rightHandPosition) || !Finite(ik.rightHandRotationEuler))) ||
                    (ik.hasLeftFoot && (!Finite(ik.leftFootPosition) || !Finite(ik.leftFootRotationEuler))) ||
                    (ik.hasRightFoot && (!Finite(ik.rightFootPosition) || !Finite(ik.rightFootRotationEuler))))
                    findings.Add(new Finding("ERROR","IK_BAD_VECTOR",ik.partClass+" has nonfinite IK target data."));
                if(driver!=null && pivot!=null)
                {
                    Vector3 driverWorld=root.TransformPoint(driver.position);
                    if(ik.hasLeftHand && Vector3.Distance(pivot.TransformPoint(ik.leftHandPosition),driverWorld)>3f)
                        findings.Add(new Finding("WARNING","IK_HAND_REACH",
                            "Left hand marker is over 3 m from driver seat; verify animation calibration."));
                    if(ik.hasRightHand && Vector3.Distance(pivot.TransformPoint(ik.rightHandPosition),driverWorld)>3f)
                        findings.Add(new Finding("WARNING","IK_HAND_REACH",
                            "Right hand marker is over 3 m from driver seat; verify animation calibration."));
                    if(ik.hasLeftFoot && Vector3.Distance(pivot.TransformPoint(ik.leftFootPosition),driverWorld)>4f)
                        findings.Add(new Finding("WARNING","IK_FOOT_REACH",
                            "Left foot marker is over 4 m from driver seat."));
                    if(ik.hasRightFoot && Vector3.Distance(pivot.TransformPoint(ik.rightFootPosition),driverWorld)>4f)
                        findings.Add(new Finding("WARNING","IK_FOOT_REACH",
                            "Right foot marker is over 4 m from driver seat."));
                }
                if(ik.partClass!="pedals" && (ik.hasLeftFoot || ik.hasRightFoot))
                    findings.Add(new Finding("WARNING","FOOT_IK_UNVERIFIED",
                        "Non-pedal foot IK may not be supported by 7DTD: "+ik.partClass));
            }
            foreach(var wheel in profile.wheels)
            {
                if(wheel==null) continue;
                if(!string.IsNullOrWhiteSpace(wheel.steerTransformPath) &&
                    RebirthVehicleRigBuilder.FindPath(root,wheel.steerTransformPath)==null)
                    findings.Add(new Finding("ERROR","STEER_MISSING",wheel.partClass+": "+wheel.steerTransformPath));
                if(!string.IsNullOrWhiteSpace(wheel.tireTransformPath))
                {
                    var tire=RebirthVehicleRigBuilder.FindPath(root,wheel.tireTransformPath);
                    if(tire==null)
                        findings.Add(new Finding("ERROR","TIRE_MISSING",wheel.partClass+": "+wheel.tireTransformPath));
                    else if(tire.GetComponentsInChildren<Renderer>(true).Length==0)
                        findings.Add(new Finding("WARNING","TIRE_EMPTY",wheel.partClass+
                            " tire node has no renderer children. Attach actual wheel visuals."));
                }
            }
            findings.Add(new Finding("INFO","NOT_GAME_PROVEN",
                "Unity hierarchy validation is not a 7DTD runtime/seat-animation test."));
            return findings;
        }
        private static bool Finite(Vector3 p)
        {
            return Finite(p.x) && Finite(p.y) && Finite(p.z);
        }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    }
}
