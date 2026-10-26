using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace RebirthEntityStudio {
 // Evaluates authored joint targets with the same identity/composition/control
 // operations as the body surface. These are authoring anchors, not motion capture.
 public static class RebirthBodyAnatomicalJoints {
  public static Dictionary<string,Vector3> Evaluate(BodyTemplate template,BodyRecipe input){
   var recipe=JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(input));recipe.Validate(template);
   var skin=Enumerable.Range(0,template.positions.Length/3).Select(i=>new Vector3(template.positions[i*3],template.positions[i*3+1],template.positions[i*3+2])).ToArray();
   var joints=Enumerable.Range(0,template.jointPositions.Length/3).Select(i=>new Vector3(template.jointPositions[i*3],template.jointPositions[i*3+1],template.jointPositions[i*3+2])).ToArray();
   var skinTargets=template.morphs.ToDictionary(m=>m.id);var jointTargets=template.jointMorphs.ToDictionary(m=>m.id);var identity=new FaceRecipe{age=recipe.age,masculine=recipe.masculine};
   foreach(bool surface in new[]{true,false}){var points=surface?skin:joints;var targets=surface?skinTargets:jointTargets;RebirthFaceIdentity.Apply(points,targets,identity);RebirthBodyComposition.Apply(points,targets,recipe);foreach(var control in template.controls){float value=recipe.Get(control);foreach(string key in value<0?control.negative:control.positive)RebirthFaceGeneratorModel.Apply(points,targets[key],Mathf.Abs(value));}}
   // Authored foundations deform against the base-resolution control cage.
   if(!string.IsNullOrEmpty(recipe.authoredBodyGuid))recipe.subdivision=0;
   var uv=Enumerable.Range(0,skin.Length).Select(i=>new Vector2(template.uv[i*2],template.uv[i*2+1])).ToArray();int[] triangles;RebirthBodySubdivision.Evaluate(template,recipe,ref skin,ref uv,out triangles);
   float bottom=skin.Min(p=>p.y),scale=recipe.height/Mathf.Max(.1f,skin.Max(p=>p.y)-bottom);
   return template.jointNames.Select((name,i)=>new{name,position=new Vector3(joints[i].x*scale,(joints[i].y-bottom)*scale,joints[i].z*scale)}).ToDictionary(p=>p.name,p=>p.position);
  }
 }
}
