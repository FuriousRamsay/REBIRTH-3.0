// AUTHORED_NOT_RUN. Actual Settle + actual complete Outbox injected by runner.
internal sealed class EntityPlayer {internal World world;}
internal sealed class Players {internal System.Collections.Generic.List<EntityPlayer> list=new System.Collections.Generic.List<EntityPlayer>();}
internal sealed partial class World {internal Players Players=new Players();}
internal sealed class Recipe {}
internal sealed partial class RebirthWorkstationCompletionOriginal {
 internal string RecipeName="native-original";
 internal bool MatchesOwner(EntityPlayer player)=>player!=null&&ReferenceEquals(player.world,GameManager.Instance.World);
 internal Recipe ResolveRecipe()=>new Recipe();
}
internal static class RebirthWorkstationCompletionFixture {internal static string Held;internal static bool Hold(string stage,string operation)=>Held==stage;}
internal static class RebirthServiceCraftSkillService {internal static string ClassifyRecipe(Recipe recipe)=>"skill.original";}
internal static class RebirthCraftTrainingRules {internal static int BuildModel(EntityPlayer player,Recipe recipe,string skill)=>1;}
internal sealed class Progression {internal System.Collections.Generic.HashSet<string> SkillAwardReceipts=new System.Collections.Generic.HashSet<string>();}
internal sealed class Character {internal Progression Progression=new Progression();}
internal static class RebirthSkillAwardService {
 internal enum WorkstationCraftAwardOutcome {Accepted,RefusedBeforeAwardEffect,Unknown}
 internal static Character Current=new Character();internal static int Effects,Invocations;internal static WorkstationCraftAwardOutcome Outcome=WorkstationCraftAwardOutcome.Accepted;
 internal static bool TryGetEligible(EntityPlayer player,out object identity,out Character character){identity=new object();character=Current;return true;}
 internal static WorkstationCraftAwardOutcome TryAwardWorkstationOriginal(EntityPlayer player,RebirthWorkstationCompletionOriginal original,string skill,int model){
  Invocations++;string receipt="wc2:"+original.Operation+":"+original.Digest;
  if(Current.Progression.SkillAwardReceipts.Contains(receipt))return WorkstationCraftAwardOutcome.Accepted;
  if(Outcome==WorkstationCraftAwardOutcome.RefusedBeforeAwardEffect)return Outcome;
  Effects++;if(Outcome==WorkstationCraftAwardOutcome.Accepted)Current.Progression.SkillAwardReceipts.Add(receipt);return Outcome;
 }
}
internal sealed class Statistics {internal System.Collections.Generic.Dictionary<string,string> WorkstationOriginalReceipts=new System.Collections.Generic.Dictionary<string,string>();}
internal static class RebirthStatisticsRepository {internal static Statistics Current=new Statistics();internal static Statistics GetOrCreate(EntityPlayer player)=>Current;}
internal static class RebirthStatisticsService {
 internal static int Effects;internal static bool Allow=true;
 internal static bool RecordWorkstationCraftOriginal(EntityPlayer player,RebirthWorkstationCompletionOriginal original){
  var receipts=RebirthStatisticsRepository.Current.WorkstationOriginalReceipts;if(!receipts.ContainsKey(original.Operation)){Effects++;receipts.Add(original.Operation,original.Digest);}return Allow;
 }
}
internal static partial class RebirthWorkstationReagentOriginal {internal static bool Allow=true;internal static int Calls;internal static bool Settle(RebirthWorkstationCompletionOutbox.State state,EntityPlayer owner){Calls++;state.Decision=true;return Allow;}}
internal static class RebirthWorkstationCompletionPipeline {
// ACTUAL_SETTLE
 internal static void Exercise(RebirthWorkstationCompletionOutbox.State state)=>Settle(state);
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthWorkstationCompletionOutbox.State Fresh(){
  ThreadManager.Main=true;var world=new World();GameManager.Instance.World=world;world.Players.list.Add(new EntityPlayer{world=world});
  RebirthSkillAwardService.Current=new Character();RebirthSkillAwardService.Effects=RebirthSkillAwardService.Invocations=0;RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Accepted;
  RebirthStatisticsRepository.Current=new Statistics();RebirthStatisticsService.Effects=0;RebirthStatisticsService.Allow=true;RebirthWorkstationReagentOriginal.Allow=true;RebirthWorkstationReagentOriginal.Calls=0;
  RebirthWorkstationCompletionPublication.Writes=RebirthWorkstationCompletionPublication.FailAt=0;RebirthWorkstationCompletionFixture.Held=null;
  return RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"server");
 }
 private static void Run(RebirthWorkstationCompletionOutbox.State state)=>RebirthWorkstationCompletionPipeline.Exercise(state);
 private static RebirthWorkstationCompletionOutbox.State Cold(RebirthWorkstationCompletionOutbox.State state){
  string operation=state.Original.Operation;
  var field=typeof(RebirthWorkstationCompletionOutbox).GetField("active",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
  ((System.Collections.IDictionary)field.GetValue(null)).Clear();
  return RebirthWorkstationCompletionOutbox.Pending("server").Single(value=>value.Original.Operation==operation);
 }
 public static void Main(){
  var state=Fresh();Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1&&RebirthStatisticsService.Effects==1&&RebirthWorkstationReagentOriginal.Calls==1,"full original settlement missing");Run(state);Need(RebirthSkillAwardService.Effects==1&&RebirthStatisticsService.Effects==1,"terminal settlement replayed rewards");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=2;Run(state);Need(RebirthSkillAwardService.Effects==0&&!state.Saved,"failed attempted publication entered skill");RebirthWorkstationCompletionPublication.FailAt=0;Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1,"same attempted publication retry lost");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=3;Run(state);Need(state.Skill&&!state.Saved&&RebirthStatisticsService.Effects==0,"failed skill outcome publication advanced statistics");RebirthWorkstationCompletionPublication.FailAt=0;Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1,"skill outcome publication retry awarded twice");
  state=Fresh();RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.RefusedBeforeAwardEffect;Run(state);Need(state.SkillRefusedBeforeEffect&&state.Saved&&RebirthSkillAwardService.Effects==0,"positive pre-effect refusal not saved");RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Accepted;Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1,"positive saved refusal cannot resume original");
  state=Fresh();RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Unknown;Run(state);Run(state);Need(RebirthSkillAwardService.Invocations==1&&RebirthSkillAwardService.Effects==1&&!state.Terminal&&RebirthStatisticsService.Effects==0,"unknown native award replayed or advanced");
  state=Fresh();RebirthStatisticsService.Allow=false;Run(state);Need(!state.Statistics&&RebirthStatisticsService.Effects==1,"unknown statistics result not retained");RebirthStatisticsService.Allow=true;Run(state);Need(state.Terminal&&RebirthStatisticsService.Effects==1&&RebirthSkillAwardService.Effects==1,"statistics receipt recovery repeated count/skill");
  state=Fresh();var changed=RebirthWorkstationCompletionPublication.ReadPrimary(state.Path);changed.SetAttributeValue("decision",true);changed.Save(state.Path);Run(state);Need(RebirthSkillAwardService.Effects==0,"changed saved head entered award");
  state=Fresh();ThreadManager.Main=false;Run(state);Need(RebirthSkillAwardService.Effects==0&&!state.Settling,"worker settlement entered stage");ThreadManager.Main=true;state.Settling=true;Run(state);Need(RebirthSkillAwardService.Effects==0,"nested original settlement entered stage");
  state=Fresh();RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Unknown;Run(state);state=Cold(state);Run(state);Need(RebirthSkillAwardService.Invocations==1&&!state.Terminal&&RebirthStatisticsService.Effects==0,"cold unknown skill original invoked again");
  state=Fresh();RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.RefusedBeforeAwardEffect;Run(state);state=Cold(state);RebirthSkillAwardService.Outcome=RebirthSkillAwardService.WorkstationCraftAwardOutcome.Accepted;Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1,"cold saved positive no-effect refusal lost original retry");
  state=Fresh();RebirthStatisticsService.Allow=false;Run(state);state=Cold(state);RebirthStatisticsService.Allow=true;Run(state);Need(state.Terminal&&RebirthSkillAwardService.Effects==1&&RebirthStatisticsService.Effects==1,"cold native statistics receipt recovery counted original twice");
  Console.WriteLine("PASS actual completion settlement + outbox; reward/native/crash services are explicit adapters, topology NOT RUN");
 }
}
