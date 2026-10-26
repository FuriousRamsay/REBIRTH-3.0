using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    /// <summary>Imports the transform/bone subset of a saved Game Bridge /entitymodel response.</summary>
    public static class RebirthEntityBridgeSkeletonImporter
    {
#pragma warning disable 0649 // Unity's JsonUtility populates these public fields.
        [Serializable] private sealed class Inspection
        {
            public BridgeTransform[] transforms;
            public BridgeComponent[] modelComponents;
        }
        [Serializable] private sealed class BridgeTransform
        {
            public int index;
            public int parent;
            public string name;
            public string path;
            public string unityTag;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }
        [Serializable] private sealed class BridgeComponent
        {
            public string kind;
            public BridgeBone[] bones;
        }
        [Serializable] private sealed class BridgeBone { public string path; }
#pragma warning restore 0649

        public static void ImportIntoProfile(RebirthEntityRigProfile profile)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            string path=EditorUtility.OpenFilePanel("Import Game Bridge entity model JSON","","json");
            if(string.IsNullOrEmpty(path))return;
            ImportJsonIntoProfile(File.ReadAllText(path),profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        public static int ImportJsonIntoProfile(string json,RebirthEntityRigProfile profile)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            if(string.IsNullOrWhiteSpace(json))throw new ArgumentException("Game Bridge JSON is empty.","json");
            Inspection report=JsonUtility.FromJson<Inspection>(json);
            if(report==null||report.transforms==null||report.transforms.Length==0)
                throw new InvalidDataException("The JSON has no Game Bridge transforms array.");

            int origin=-1;
            for(int i=0;i<report.transforms.Length;i++)
            {
                BridgeTransform node=report.transforms[i];
                if(node==null||string.IsNullOrEmpty(node.path))
                    throw new InvalidDataException("The Game Bridge transforms array contains a missing entry at "+i+".");
                if(node.index!=i||node.parent>=i||node.parent<-1)
                    throw new InvalidDataException("Transform indices/parents are not in Game Bridge preorder at entry "+i+".");
                if(string.Equals(node.name,"Origin",StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(node.unityTag,"E_BP_BipedRoot",StringComparison.Ordinal))origin=i;
            }
            if(origin<0)throw new InvalidDataException("No transform named Origin tagged E_BP_BipedRoot was found in the inspection.");

            var include=new HashSet<int>{origin};
            var rendererBonePaths=new HashSet<string>(StringComparer.Ordinal);
            if(report.modelComponents!=null)
                foreach(BridgeComponent component in report.modelComponents)
                {
                    if(component==null||!string.Equals(component.kind,"SkinnedMeshRenderer",StringComparison.Ordinal)||component.bones==null)continue;
                    foreach(BridgeBone bone in component.bones)
                        if(bone!=null&&!string.IsNullOrEmpty(bone.path))rendererBonePaths.Add(bone.path);
                }

            // Keep every transform below Origin so unweighted animation/helper nodes are not
            // lost. Only renderer bone paths (above) are allowed to receive skin weights.
            for(int i=0;i<report.transforms.Length;i++)
            {
                int at=i;
                for(int steps=0;at>=0&&steps<=report.transforms.Length;steps++)
                {
                    if(at==origin){include.Add(i);break;}
                    if(at<origin)break;
                    at=report.transforms[at].parent;
                }
            }
            foreach(string bonePath in rendererBonePaths)
            {
                bool found=false;
                for(int i=origin;i<report.transforms.Length;i++)
                    if(include.Contains(i)&&string.Equals(report.transforms[i].path,bonePath,StringComparison.Ordinal)){found=true;break;}
                if(!found)throw new InvalidDataException("Renderer bone is outside the imported Origin hierarchy: "+bonePath);
            }

            var positions=new Dictionary<int,Matrix4x4>();
            positions[origin]=Matrix4x4.identity;
            for(int i=origin+1;i<report.transforms.Length;i++)
            {
                if(!include.Contains(i))continue;
                BridgeTransform node=report.transforms[i];
                if(node.parent<origin||!positions.TryGetValue(node.parent,out Matrix4x4 parentMatrix))
                    throw new InvalidDataException("The imported rig is not a preorder hierarchy beneath Origin at "+node.path);
                Quaternion rotation=Normalize(node.localRotation);
                Vector3 scale=IsZero(node.localScale)?Vector3.one:node.localScale;
                positions[i]=parentMatrix*Matrix4x4.TRS(node.localPosition,rotation,scale);
            }

            var points=new List<Vector3>();
            foreach(int index in include)if(positions.TryGetValue(index,out Matrix4x4 matrix))points.Add(matrix.MultiplyPoint3x4(Vector3.zero));
            if(points.Count<2)throw new InvalidDataException("The inspection did not contain a renderer bone hierarchy beneath Origin.");
            Bounds bounds=new Bounds(points[0],Vector3.zero);
            for(int i=1;i<points.Count;i++)bounds.Encapsulate(points[i]);
            Vector3 size=bounds.size;
            if(size.x<0.0001f)size.x=0.0001f;if(size.y<0.0001f)size.y=0.0001f;if(size.z<0.0001f)size.z=0.0001f;

            var names=new Dictionary<int,string>();
            var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(int index in include)
            {
                BridgeTransform node=report.transforms[index];
                if(index==origin){names[index]="Origin";unique.Add("Origin");continue;}
                string name=string.IsNullOrWhiteSpace(node.name)?"Bone_"+index:node.name;
                if(!unique.Add(name))
                    throw new InvalidDataException("The inspected renderer hierarchy has duplicate bone name '"+name+"'. Rename/map that hierarchy explicitly before using it as a rig profile.");
                names[index]=name;
            }

            var specs=new List<EntityBoneSpec>();
            BridgeTransform originNode=report.transforms[origin];
            specs.Add(new EntityBoneSpec{name="Origin",parent="",gameTag="E_BP_BipedRoot",normalizedPosition=new Vector3(.5f,0f,.5f),deformsMesh=rendererBonePaths.Contains(originNode.path)});
            for(int i=origin+1;i<report.transforms.Length;i++)
            {
                if(!include.Contains(i)||!positions.TryGetValue(i,out Matrix4x4 matrix))continue;
                BridgeTransform node=report.transforms[i];
                if(!names.TryGetValue(node.parent,out string parentName))
                    throw new InvalidDataException("Missing imported parent for "+node.path);
                Vector3 local=matrix.MultiplyPoint3x4(Vector3.zero);
                Vector3 normalized=new Vector3((local.x-bounds.min.x)/size.x,(local.y-bounds.min.y)/size.y,(local.z-bounds.min.z)/size.z);
                bool rendererBone=rendererBonePaths.Contains(node.path);
                specs.Add(new EntityBoneSpec
                {
                    name=names[i],parent=parentName,
                    gameTag=string.IsNullOrEmpty(node.unityTag)||node.unityTag=="Untagged"||node.unityTag=="<undefined-tag>"?"":node.unityTag,
                    normalizedPosition=normalized,
                    referenceLocalEuler=Normalize(node.localRotation).eulerAngles,
                    deformsMesh=rendererBone,
                    required=true
                });
            }
            profile.bones=specs;
            profile.boneDeformFlagsMigrated=true;
            profile.reviewedSkeletonMapping=false;
            profile.reviewedColliderFit=false;
            profile.reviewedLods=false;
            profile.reviewedMaterials=false;
            return specs.Count;
        }

        private static bool IsZero(Vector3 value){return value.sqrMagnitude<0.0000001f;}
        private static Quaternion Normalize(Quaternion value)
        {return value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w<0.0000001f?Quaternion.identity:value.normalized;}
    }
}
