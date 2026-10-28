// AUTHORED_NOT_RUN. Actual complete Outbox/NativeScrap/Settle composition.
// Native frozen-original admission and primitive XML publisher are adapters.
internal static class ScrapNoAwardsCases{
 private static int checks;private static void Need(bool value,string text){if(!value)throw new Exception(text);checks++;}
 private static void Cold(){var field=typeof(RebirthWorkstationCompletionOutbox).GetField("active",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);((System.Collections.IDictionary)field.GetValue(null)).Clear();}
 private static RebirthWorkstationCompletionOutbox.State Fresh(bool scrap=true){
  ThreadManager.Main=true;var world=new World();GameManager.Instance.World=world;world.Players.list.Add(new EntityPlayer{world=world});
  RebirthSkillAwardService.Effects=RebirthSkillAwardService.Invocations=RebirthStatisticsService.Effects=RebirthWorkstationReagentOriginal.Calls=0;
  RebirthWorkstationCompletionPublication.Writes=RebirthWorkstationCompletionPublication.FailAt=0;
  return RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal{NativeAdditionalScrap=scrap},"server");
 }
 public static void Main(){
  var state=Fresh();var original=state.Original;RebirthWorkstationCompletionPipeline.Exercise(state);
  Need(state.NoCanonicalAwards&&state.Terminal&&state.Saved,"typed native scrap has explicit saved terminal disposition");
  Need(!state.Skill&&!state.Statistics&&!state.Decision&&!state.SkillAttempted&&!state.StatisticsAttempted&&!state.ReagentAttempted,"no synthetic canonical effect flags");
  Need(RebirthSkillAwardService.Invocations==0&&RebirthStatisticsService.Effects==0&&RebirthWorkstationReagentOriginal.Calls==0,"native-only scrap invokes no canonical services");
  Need(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(state.Path))=="archive"&&System.IO.File.Exists(state.Path),"positive terminal archives exact original");
  Cold();Need(RebirthWorkstationCompletionOutbox.TryReadSaved(original,out var loaded)&&loaded.NoCanonicalAwards&&loaded.Terminal&&!loaded.Skill,"cold saved disposition remains distinct from award");
  Need(RebirthWorkstationCompletionOutbox.HasTerminal(original.Operation,original.Digest)&&!RebirthWorkstationCompletionOutbox.HasStage(original,"skill"),"terminal lookup never fabricates skill receipt");
  state=Fresh();state.SkillAttempted=true;Need(RebirthWorkstationCompletionOutbox.Save(state),"entered canonical stage saved for retained case");RebirthWorkstationCompletionPipeline.Exercise(state);Need(!state.NoCanonicalAwards&&!state.Terminal,"entered old canonical stage cannot become no-effect disposition");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=RebirthWorkstationCompletionPublication.Writes+1;RebirthWorkstationCompletionPipeline.Exercise(state);Need(!state.Saved&&System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(state.Path))=="pending","failed disposition save retains pending custody");
  original=state.Original;Cold();RebirthWorkstationCompletionPublication.FailAt=0;Need(RebirthWorkstationCompletionOutbox.TryReadSaved(original,out loaded)&&!loaded.NoCanonicalAwards&&!loaded.Terminal,"cold after failed save reads only positive initial metadata");
  RebirthWorkstationCompletionPipeline.Exercise(loaded);Need(loaded.NoCanonicalAwards&&loaded.Terminal&&loaded.Saved&&RebirthSkillAwardService.Invocations==0,"cold continuation publishes metadata without canonical replay");
  state=Fresh(false);var ordinary=RebirthWorkstationCompletionPublication.ReadPrimary(state.Path);Need(ordinary.Attribute("noCanonicalAwards")==null&&!state.NoCanonicalAwards,"legacy/default schema preserves ordinary disposition");
  RebirthWorkstationCompletionPipeline.Exercise(state);Need(state.Skill&&state.Statistics&&state.Decision&&state.Terminal&&RebirthSkillAwardService.Invocations==1&&RebirthWorkstationReagentOriginal.Calls==1,"ordinary actual settlement unchanged");
  state=Fresh(false);state.NoCanonicalAwards=state.Terminal=true;Need(!RebirthWorkstationCompletionOutbox.Save(state)&&!state.Saved,"ordinary original cannot claim native-only disposition");
  state=Fresh();state.NoCanonicalAwards=state.Terminal=state.Acknowledged=true;Need(!RebirthWorkstationCompletionOutbox.Save(state),"server no-awards terminal cannot borrow client acknowledgement");
  Console.WriteLine("PASS "+checks+" actual native scrap no-awards persistence/cold/archive assertions");
 }
}
