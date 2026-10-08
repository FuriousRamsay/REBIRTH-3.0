using System;
class RebirthSkillRuntimeState {public float Value,Progress;}
class Check {
 const float MinimumInstructorSkill=10f;
 // SOURCE
 static void Main(){
  if(HasInstructorPracticalSkill(null))throw new Exception("null accepted");
  float[] denied={-50f,0f,9f,9.5f,9.999f,float.NaN,float.NegativeInfinity,float.PositiveInfinity};
  foreach(float value in denied)foreach(float progress in new[]{0f,0.75f,0.999999f})
   if(HasInstructorPracticalSkill(new RebirthSkillRuntimeState{Value=value,Progress=progress}))throw new Exception("unfinished/invalid practical level accepted: "+value+"+"+progress);
  foreach(float value in new[]{10f,10.5f,100f})foreach(float progress in new[]{0f,0.75f,0.999999f})
   if(!HasInstructorPracticalSkill(new RebirthSkillRuntimeState{Value=value,Progress=progress}))throw new Exception("earned threshold rejected");
  Console.WriteLine("PASS actual shared instructor practical requirement: null/nonfinite refusal, below10 including fractional saved levels with pending progress, exact10 and higher allowed; progress cannot bypass earned-level threshold.");
 }
}