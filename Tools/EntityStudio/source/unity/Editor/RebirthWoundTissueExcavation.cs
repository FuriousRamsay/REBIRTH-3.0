using System;
using System.IO;
using UnityEngine;
namespace RebirthEntityStudio {
 // The existing skin-sampled lining is the cut floor. Remove overlying muscle
 // against that same piecewise-linear surface, without changing bones or skin.
 internal static class RebirthWoundTissueExcavation {
  internal const int Columns=49,Rows=97;
  internal static Texture2D Build(Mesh lining,Material frame){
   var points=lining.vertices;if(points.Length!=Columns*Rows)throw new InvalidDataException("Tissue excavation requires the matching wound lining grid");
   Vector3 center=frame.GetVector("_DeepCenter0"),z=frame.GetVector("_DeepZ0");
   if(z.sqrMagnitude<1e-12f)throw new InvalidDataException("Invalid tissue excavation depth frame");
   var values=new float[points.Length];for(int i=0;i<points.Length;i++){values[i]=Vector3.Dot(points[i]-center,z);if(float.IsNaN(values[i])||float.IsInfinity(values[i]))throw new InvalidDataException("Nonfinite tissue excavation floor");}
   var texture=new Texture2D(Columns,Rows,TextureFormat.RFloat,false,true){name=lining.name+"_TissueCutFloor",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
   try{texture.SetPixelData(values,0);texture.Apply(false,false);return texture;}catch{UnityEngine.Object.DestroyImmediate(texture);throw;}
  }
  internal static void Apply(Material material,Texture2D floor){
   if(!material.HasProperty("_ExcavateTissue")||!material.HasProperty("_TissueCutFloor"))throw new InvalidOperationException("Install the depth-aware tissue shader");
   if(floor==null)throw new ArgumentNullException("floor");material.SetTexture("_TissueCutFloor",floor);material.SetFloat("_ExcavateTissue",1);
  }
 }
}
