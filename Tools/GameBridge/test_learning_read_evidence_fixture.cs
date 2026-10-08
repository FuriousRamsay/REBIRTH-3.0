using System;
using System.Collections.Generic;
// PRODUCTION_CLASS
public static class Check {
 static void A(bool ok,string name){if(!ok)throw new Exception(name);}
 public static void Main(){
 var seen=new HashSet<string>(StringComparer.Ordinal);var awarded=new List<string>();
 RebirthLearningReadEvidence.CreditVerified(new[]{"recipe.medicalFirstAidKit","rebirthTheoryCookingPrimer","literature.read.another_title","literature.recipe_read.recipe.foodHoboStew"},seen,s=>{awarded.Add(s);return true;});
 A(awarded.Count==0&&seen.Count==0,"possession/generic knowledge/other study cannot qualify");
 var exact=new[]{"literature.read.rebirthTheoryCookingPrimer","literature.recipe_read.recipe.medicalFirstAidKit","literature.recipe_read.recipe.resourceRepairKit"};
 RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>{awarded.Add(s);return true;});A(awarded.Count==3&&seen.Count==3&&awarded[0]=="rebirth.study.cooking_principles"&&awarded[1]=="rebirth.study.first_aid_recipe"&&awarded[2]=="rebirth.study.repair_manual","exact persisted receipts");
 RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>{awarded.Add(s);return true;});A(awarded.Count==3,"snapshot replay idempotent");
 seen.Clear();RebirthLearningReadEvidence.CreditVerified(exact,seen,null);A(seen.Count==0,"missing consumer retains retry");
 RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>{awarded.Add(s);return true;});A(awarded.Count==6,"reconnect saved receipts reconcile");
 seen.Clear();RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>false);A(seen.Count==0,"unacknowledged event retains retry");RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>true);A(seen.Count==3,"ready objective acknowledges retry");
 seen.Clear();bool failed=false;try{RebirthLearningReadEvidence.CreditVerified(exact,seen,s=>{throw new Exception("consumer");});}catch(Exception){failed=true;}A(failed&&seen.Count==0,"callback failure not marked credited");
 A(RebirthLearningReadEvidence.IsTutorialMarker(exact[0])&&!RebirthLearningReadEvidence.IsTutorialMarker("literature.read.other")&&!RebirthLearningReadEvidence.IsTutorialMarker(null),"bounded exact publication");
 Console.WriteLine("PASS actual reading evidence: exact completed receipts, no possession/discovery false positive, replay, reconnect and consumer retry");
 }
}
