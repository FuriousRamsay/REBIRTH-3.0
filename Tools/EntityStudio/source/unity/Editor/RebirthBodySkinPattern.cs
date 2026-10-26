using System;using System.IO;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class BodySkinPatternSettings {
  public float strength=0,scale=1;public int seed;
  public void Validate(){foreach(float v in new[]{strength,scale})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Invalid face skin pattern setting");strength=Mathf.Clamp(strength,0,1.5f);scale=Mathf.Clamp(scale,.5f,2);seed=Mathf.Max(0,seed);}
 }
 public static class RebirthBodySkinPattern {
  public static BodySkinPatternSettings Draw(BodySkinPatternSettings input){input.Validate();EditorGUI.BeginChangeCheck();float strength=EditorGUILayout.Slider("Experimental pigmentation transfer",input.strength,0,1.5f),scale=EditorGUILayout.Slider("Skin pattern size",input.scale,.5f,2);int seed=EditorGUILayout.IntField("Skin pattern seed",input.seed);EditorGUILayout.LabelField("Experimental colour synthesis can repeat facial blemishes across the body. Leave at zero for normal generation. Face-derived skin tone remains active; this control does not generate anatomical detail.",EditorStyles.wordWrappedMiniLabel);if(!EditorGUI.EndChangeCheck())return null;var value=new BodySkinPatternSettings{strength=strength,scale=scale,seed=seed};value.Validate();return value;}
 }
}
