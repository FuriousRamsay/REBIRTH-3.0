// Actual original farming API/evidence constructor. Character repository/award
// sink/theory are doubles; this does not qualify native or remote publication.
using System;
using System.Collections.Generic;
class World {public bool Remote;public bool IsRemote()=>Remote;}
class EntityPlayer {public World world=new World();}
static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
enum RebirthSkillTrainingEvidenceMode {DiscreteAward}
class RebirthSkillTrainingEvidence {public string SkillId,SourceKey,ReferenceDescription,DurableReceiptId;public bool AuthoritativeSuccess;public RebirthSkillTrainingEvidenceMode Mode;public float CreditedWork,DiscreteRawAward;}
static class RebirthSkillAwardService {
 public static HashSet<string> Receipts=new HashSet<string>();public static int Awards;public static bool Durable=true;
 public static bool TryAwardMigratedTrainingEvidence(EntityPlayer player,RebirthSkillTrainingEvidence e,out object computation,out float gained,out float attribute){computation=null;gained=attribute=0;if(e.SkillId!="skill.farming"||!e.AuthoritativeSuccess||e.Mode!=RebirthSkillTrainingEvidenceMode.DiscreteAward||e.DiscreteRawAward!=0.25f||e.CreditedWork!=1||!e.DurableReceiptId.StartsWith("farming-original:"))throw new Exception("original profile evidence changed");if(Receipts.Add(e.DurableReceiptId))Awards++;return Durable;}
}
static class RebirthTheoryProgressionService {public static bool Accept;public static bool Earned;public static bool TryAwardInsight(EntityPlayer player,string key,string source,out object theory,out bool already){theory=null;already=Earned;if(Earned)return false;if(!Accept)return false;Earned=true;return true;}}
static class RebirthPhase8WorldOutputTrainingService {
 private static bool loaded=true;private static float farmingMatureGain=0.25f;private static void Load(){loaded=true;}private static bool IsPlayerCropHarvest(string crop)=>crop.EndsWith("3HarvestPlayer",StringComparison.OrdinalIgnoreCase);
// PRODUCTION_METHODS
}
static class Fixture {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static void Main(){var player=new EntityPlayer();string op=Guid.NewGuid().ToString("N");
  Check(!RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,op)&&RebirthSkillAwardService.Awards==1,"failed insight silently completed");
  RebirthTheoryProgressionService.Accept=true;Check(RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,op)&&RebirthSkillAwardService.Awards==1,"original retry duplicated farming reward");
  Check(RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,op)&&RebirthSkillAwardService.Awards==1,"completed operation not idempotent");
  player.world.Remote=true;Check(!RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,op),"remote owner awarded authority");player.world.Remote=false;
  Check(!RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,"bad"),"missing original identity accepted");
  Check(RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",0,Guid.NewGuid().ToString("N"))&&RebirthSkillAwardService.Awards==1,"zero original output granted farming skill");
  RebirthSurvivorMode.Enabled=false;Check(RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(player,"plantedCorn3HarvestPlayer",2,Guid.NewGuid().ToString("N"))&&RebirthSkillAwardService.Awards==1,"native mode gained Survivor reward or blocked retirement");
  Console.WriteLine("PASS actual original farming award API: authoritative original receipt evidence, retry/insight idempotence, remote/identity refusal, zero yield/native-mode controls; repository/award sink doubled");
 }
}
