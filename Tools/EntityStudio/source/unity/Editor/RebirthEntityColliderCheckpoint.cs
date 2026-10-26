using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityColliderCheckpoint
    {
        public static string GeometryHash(SkinnedMeshRenderer renderer)
        {
            var mesh=renderer.sharedMesh;
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                var vertices=mesh.vertices;writer.Write(vertices.Length);foreach(var v in vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                foreach(var w in mesh.boneWeights){writer.Write(w.boneIndex0);writer.Write(w.boneIndex1);writer.Write(w.boneIndex2);writer.Write(w.boneIndex3);writer.Write(w.weight0);writer.Write(w.weight1);writer.Write(w.weight2);writer.Write(w.weight3);}
                writer.Write(mesh.subMeshCount);for(int i=0;i<mesh.subMeshCount;i++){var indices=mesh.GetTriangles(i);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                foreach(var matrix in mesh.bindposes)for(int i=0;i<16;i++)writer.Write(matrix[i]);
                foreach(var bone in renderer.bones){writer.Write(bone.name);for(int i=0;i<16;i++)writer.Write(bone.localToWorldMatrix[i]);}
                for(int i=0;i<16;i++)writer.Write(renderer.transform.localToWorldMatrix[i]);
                writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static void Apply(RebirthEntityRigWorkspace workspace,SkinnedMeshRenderer renderer,string path,string geometryHash,List<GameObject> created)
        {
            var report=JsonUtility.FromJson<RebirthEntityArmRemeshExchange.CandidateColliders>(File.ReadAllText(path));
            if(report==null||string.IsNullOrEmpty(report.geometrySha256)||report.geometrySha256!=geometryHash)throw new InvalidDataException("Collider checkpoint does not match this exact mesh, skin, bind poses and rest skeleton.");
            if(report.shapes==null||report.shapes.Any(s=>s==null||string.IsNullOrWhiteSpace(s.name))||report.shapes.Select(s=>s.name).Distinct().Count()!=report.shapes.Length)throw new InvalidDataException("Collider checkpoint names are missing or duplicated.");
            var original=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).ToDictionary(s=>s.name);
            if(original.Keys.Any(name=>!report.shapes.Any(s=>s.name==name)))throw new InvalidDataException("Checkpoint omits an original body shape.");
            foreach(var record in report.shapes)
            {
                if(!Finite(record.radius)||!Finite(record.height)||record.radius<=0||record.height<=0||record.direction<0||record.direction>2||!Finite(record.center.x)||!Finite(record.center.y)||!Finite(record.center.z))throw new InvalidDataException("Invalid checkpoint dimensions.");
                var q=record.localRotation;float length=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
                if(!Finite(length)||Mathf.Abs(length-1)>.001f)throw new InvalidDataException("Invalid checkpoint rotation.");
                CapsuleCollider shape;
                bool isOriginal=original.TryGetValue(record.name,out shape);
                if(!isOriginal)
                {
                    var match=Regex.Match(record.name,"^EntityStudio_Compound_(Left|Right)(Thumb|Index|Middle|Ring|Pinky)FingerHit([12])$");
                    if(!match.Success)throw new InvalidDataException("Unsupported extra checkpoint collider: "+record.name);
                    string side=match.Groups[1].Value,parent=side+"Hand"+match.Groups[2].Value+match.Groups[3].Value;
                    if(record.parent!=parent)throw new InvalidDataException("Finger checkpoint parent differs from its chain.");
                    var reference=original["EntityStudio_Compound_"+side+"HandHit"];var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,parent);
                    if(bone==null)throw new InvalidDataException("Checkpoint finger chain is missing.");
                    var go=new GameObject(record.name);created.Add(go);go.transform.SetParent(bone,false);go.tag=reference.tag;go.layer=reference.gameObject.layer;
                    shape=go.AddComponent<CapsuleCollider>();shape.sharedMaterial=reference.sharedMaterial;shape.isTrigger=reference.isTrigger;shape.enabled=reference.enabled;
                    if(shape.attachedRigidbody!=reference.attachedRigidbody)throw new InvalidDataException("Checkpoint changed finger body ownership.");
                }
                if(shape.transform.parent.name!=record.parent||shape.tag!=record.tag||shape.gameObject.layer!=record.layer)throw new InvalidDataException("Checkpoint body hierarchy/tag/layer contract changed: "+record.name);
                if(isOriginal&&shape.direction!=record.direction)throw new InvalidDataException("Checkpoint changes an original collider axis: "+record.name);
                if(renderer.bones.Contains(shape.transform)&&Quaternion.Angle(shape.transform.localRotation,record.localRotation)>.001f)throw new InvalidDataException("Checkpoint cannot rotate a mesh-deforming body bone.");
                shape.direction=record.direction;shape.center=record.center;shape.radius=record.radius;shape.height=record.height;shape.transform.localRotation=record.localRotation;
            }
            Debug.Log("COLLIDER CHECKPOINT REPLAYED: geometrySha256="+geometryHash+" shapes="+report.shapes.Length+"; actual posed coverage still requires verification.");
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
