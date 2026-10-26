using System;using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthBodyIdentity {
  public static void InitializeFromHead(BodyRecipe body,string faceSettings){
   if(!body.inheritHeadAge&&!body.inheritHeadShape)return;var head=JsonUtility.FromJson<FaceRecipe>(faceSettings);if(head==null)throw new InvalidOperationException("Invalid saved head settings");head.Validate(RebirthFaceGeneratorWindow.ReadTemplate());if(body.inheritHeadAge)body.age=head.age;if(body.inheritHeadShape)body.masculine=head.masculine;
  }
 }
}
