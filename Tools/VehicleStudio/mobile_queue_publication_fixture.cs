// Exact production matcher/address/input-layout code is compiled by the runner.
// Save observation, admission policy and native items/queue are explicit doubles.
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class GameIO{public static string GetSaveGameDir()=>Path.GetFullPath("fixture-save");}
public class ItemValue{}
public class ItemStack{
 public int count;public ItemValue itemValue=new ItemValue();public ItemStack Clone()=>new ItemStack{count=count};
 public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;}
}
public class Recipe{public bool Marked=true;public string Job;}
public class RecipeQueueItem{public Recipe Recipe;public int Multiplier=1,StartingEntityId=42,Quality=1,AmountToRepair;public ItemValue RepairItem;public float CraftingTimeLeft=2,OneItemCraftTime=2;}
public static class RebirthStationGridQueue{
 public static bool IsMarked(Recipe r)=>r.Marked;
 public static bool TryGetJobId(Recipe r,out string id){id=r.Job;return Guid.TryParseExact(id,"N",out _);}
}
public class FixtureBlock{public string Name="forge";public string GetBlockName()=>Name;}
public class FixtureBlockValue{public FixtureBlock Block=new FixtureBlock();}
public class RebirthCamperStationState{
 public Guid InstallationId;public RecipeQueueItem[] Queue;public string[] MaterialNames=new string[0];
 public ItemStack[] Input=ItemStack.CreateArray(3);public int InputSlotCount=3;public FixtureBlockValue InstalledBlock=new FixtureBlockValue();
}
public class RebirthStationGridAdmission{ public bool IsMaterialPayment=false;
 public bool IsPublicationAttempted=true,PolicyAllowed=true;public RebirthCamperStationAddress Address;
 public string JobId=Guid.NewGuid().ToString("N"),CreationId="creation";
 public XElement Write()=>new XElement("admission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("stationAddress",Address.ToString()));
 public bool TryGetMobileAddress(out RebirthCamperStationAddress a){a=Address;return a.IsValid;}
 public bool MatchesQueuedQuality(Recipe r,int multiplier,int quality)=>PolicyAllowed&&multiplier==1&&quality==1;
 public bool MatchesInitialQueuedQuality(Recipe r,int multiplier,int quality)=>MatchesQueuedQuality(r,multiplier,quality);
 public XElement WritePaymentImage()=>Write();
 public bool MatchesQueuedRecipe(Recipe r,int multiplier)=>PolicyAllowed&&r.Job==JobId&&multiplier==1;
 public bool MatchesMobileQueuedObservation(string c,RebirthCamperStationAddress a,string block,IList<ItemStack> input,Recipe recipe,int multiplier)
 =>PolicyAllowed&&c==CreationId&&a.Equals(Address)&&block=="forge"&&input.Count==3&&input[0].count==1&&recipe.Job==JobId&&multiplier==1;
}
public static class RebirthCamperStationSaveEvidence{public class Publication{
 public RebirthCamperStationAddress Address;public string SaveDirectory=GameIO.GetSaveGameDir();public RebirthCamperStationState State;
 public string StationDigest=new string('A',64),VehicleEntityDigest=new string('B',64),VehicleBagDigest=new string('C',64);
 public int VehicleEntityId=10,VehicleClass=1;
 public string CanonicalDigest=new string('D',64);public string RecoveryDigest()=>CanonicalDigest;
 public bool Throw;public RebirthCamperStationState RestoreStationSnapshot(){if(Throw)throw new InvalidDataException();return State;}
}}
// Completion event/native output observation are explicit doubles; record code is actual.
public static class RebirthCamperStationCompletionCapture{
 public class FixtureEntity{public int entityId;}
 public class FixtureBinding{
  public RebirthCamperStationAddress Address;public RebirthStationGridAdmission Admission;
  public RebirthCamperStationPublicationRecord Publication;
  public FixtureEntity Vehicle=new FixtureEntity{entityId=10},Player=new FixtureEntity{entityId=42};
 }
 public class SuccessfulOutput{
  public FixtureBinding Binding;public bool Live=true,Saved=true;
  public bool MatchesLiveOutput()=>Live;public bool MatchesSavedOutput(RebirthCamperStationState state)=>Saved;
 }
}
// Training-plan creation is an explicit double; serialization and receipt hashing are production code.
public static class RebirthCraftTrainingRules{public struct Model{public float Raw,Difficulty,Seconds;public string RecipeId,SkillId;public int Outputs,IngredientTypes;public bool Valid;}}
public partial class RebirthCamperStationTrainingPlan{
 public RebirthCamperStationCompletionCapture.FixtureBinding Binding;
 public RebirthCamperStationCompletionRecord Publication;
 public string Skill,Source,Receipt;public int Cycles;public float PreparationMultiplier;
 public RebirthCraftTrainingRules.Model Model;public bool IsCurrent()=>true;
}
public static class MobileQueuePublicationFixture{
 static void Check(bool result,string label){if(!result)throw new Exception(label);}
 public static void Run(){
  var address=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());
  var admission=new RebirthStationGridAdmission{Address=address};
  var entry=new RecipeQueueItem{Recipe=new Recipe{Job=admission.JobId}};
  var state=new RebirthCamperStationState{InstallationId=address.Installation,Queue=new[]{entry}};state.Input[0].count=1;
  var proof=new RebirthCamperStationSaveEvidence.Publication{Address=address,State=state};
  Func<bool> match=()=>RebirthCamperQueuedPublicationMatch.Matches(proof,admission,42);
  Check(match(),"matching saved job");
  entry.StartingEntityId=43;Check(!match(),"wrong crafter");entry.StartingEntityId=42;
  state.Queue=new[]{entry,entry};Check(!match(),"duplicate job");state.Queue=new[]{entry};
  state.Input[0].count=2;Check(!match(),"changed remaining input");state.Input[0].count=1;
  admission.PolicyAllowed=false;Check(!match(),"admission policy refusal");admission.PolicyAllowed=true;
  admission.IsPublicationAttempted=false;Check(!match(),"unattempted job refusal");admission.IsPublicationAttempted=true;
  proof.Address=new RebirthCamperStationAddress(address.Vehicle,Guid.NewGuid());Check(!match(),"wrong station");proof.Address=address;
  proof.SaveDirectory=Path.GetFullPath("other-save");Check(!match(),"other save");proof.SaveDirectory=GameIO.GetSaveGameDir();
  entry.CraftingTimeLeft=float.NaN;Check(!match(),"invalid timer");entry.CraftingTimeLeft=2;
  state.Queue=new[]{entry,new RecipeQueueItem{Recipe=new Recipe{Job="bad"}}};Check(!match(),"malformed unrelated marked job");state.Queue=new[]{entry};
  proof.Throw=true;Check(!match(),"snapshot decode failure");proof.Throw=false;
  ThreadManager.Main=false;Check(!match(),"wrong thread");ThreadManager.Main=true;
  Check(match(),"valid observation still usable after refusals");
  Check(RebirthCamperStationPublicationRecord.TryCreate(admission,42,proof,out var receipt),"mobile receipt creation");
  Check(RebirthCamperStationPublicationRecord.TryRead(receipt.Clone().Write(),admission,out var parsed)&&XNode.DeepEquals(receipt.Write(),parsed.Write()),"receipt round trip");
  Check(receipt.MatchesSaveScope(GameIO.GetSaveGameDir())&&!receipt.MatchesSaveScope(Path.GetFullPath("other-save")),"receipt save isolation");
  var malformed=receipt.Write();malformed.SetAttributeValue("stationAddress",new RebirthCamperStationAddress(address.Vehicle,Guid.NewGuid()).ToString());
  Check(!RebirthCamperStationPublicationRecord.TryRead(malformed,admission,out _),"receipt installation isolation");
  malformed=receipt.Write();malformed.SetAttributeValue("entitySnapshot","bad");Check(!RebirthCamperStationPublicationRecord.TryRead(malformed,admission,out _),"receipt hash shape");
  malformed=receipt.Write();malformed.SetAttributeValue("unexpected",1);Check(!RebirthCamperStationPublicationRecord.TryRead(malformed,admission,out _),"receipt extra attribute refusal");
  var records=new Dictionary<string,RebirthCamperStationPublicationRecord>{{admission.JobId,receipt}};
  var admissions=new Dictionary<string,RebirthStationGridAdmission>{{admission.JobId,admission}};
  var section=RebirthCamperStationPublicationRecord.WriteAll(records,admissions);
  Check(RebirthCamperStationPublicationRecord.ReadAll(new XElement("progression",section),admissions,out var loaded)&&loaded.Count==1,"receipt collection round trip");
  Check(RebirthCamperStationPublicationRecord.ReadAll(new XElement("progression"),admissions,out loaded)&&loaded.Count==0,"legacy save without mobile section");
  Check(!RebirthCamperStationPublicationRecord.ReadAll(new XElement("progression",section,new XElement(section)),admissions,out loaded),"duplicate mobile sections");
  var duplicateSection=new XElement(section);duplicateSection.Add(receipt.Write());
  Check(!RebirthCamperStationPublicationRecord.ReadAll(new XElement("progression",duplicateSection),admissions,out loaded),"duplicate mobile jobs");
  Check(!RebirthCamperStationPublicationRecord.ReadAll(new XElement("progression",section),new Dictionary<string,RebirthStationGridAdmission>(),out loaded),"orphan receipt refusal");
  var completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput{Binding=new RebirthCamperStationCompletionCapture.FixtureBinding{Address=address,Admission=admission,Publication=receipt}};
  Check(RebirthCamperStationCompletionRecord.TryCreate(completed,proof,out var completion),"completion record creation with doubled original event");
  completed.Binding.Player.entityId=99;
  Check(RebirthCamperStationCompletionRecord.TryCreate(completed,proof,out var resumedCompletion)&&
   (int)resumedCompletion.Write().Attribute("completedOwner")==42,
   "current owner actor change preserves original native completion custody");
  completed.Binding.Player.entityId=42;
  Check(completion.HasRecoveryImage&&completion.MatchesRecoveryImage(proof,receipt),"version3 recovery binding");
  proof.CanonicalDigest=new string('E',64);Check(!completion.MatchesRecoveryImage(proof,receipt),"changed canonical station refused");proof.CanonicalDigest=new string('D',64);
  proof.VehicleEntityId=11;Check(completion.MatchesRecoveryImage(proof,receipt),"same persistent address permits numeric vehicle ID change in content correlation");
  completed.Binding.Vehicle.entityId=11;
  Check(RebirthCamperStationCompletionRecord.TryCreate(completed,proof,out var movedCompletion)&&
   (int)movedCompletion.Write().Attribute("vehicleId")==10&&(int)movedCompletion.Write().Attribute("nativeVehicleId")==11,
   "original lineage and current native ID retained separately");
  proof.VehicleEntityId=0;Check(!completion.MatchesRecoveryImage(proof,receipt),"invalid native ID refused");proof.VehicleEntityId=10;completed.Binding.Vehicle.entityId=10;
  proof.Address=new RebirthCamperStationAddress(Guid.NewGuid(),address.Installation);
  Check(!completion.MatchesRecoveryImage(proof,receipt),"different persistent truck refused despite equal station snapshot");proof.Address=address;
  proof.VehicleClass=2;Check(!completion.MatchesRecoveryImage(proof,receipt),"changed native vehicle class refused");proof.VehicleClass=1;
  malformed=completion.Write();malformed.SetAttributeValue("version",1);
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"version1 cannot claim new recovery field");
  malformed.Attribute("stationRecovery").Remove();
  malformed.Attribute("nativeVehicleId").Remove();
  Check(RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out var legacyCompletion)&&
    !legacyCompletion.HasRecoveryImage&&!legacyCompletion.MatchesRecoveryImage(proof,receipt),"legacy completion preserved without recovery admission");
  malformed=completion.Write();malformed.Attribute("stationRecovery").Remove();
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"version2 recovery field required");
  malformed=completion.Write();malformed.SetAttributeValue("stationRecovery","invalid");
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"malformed recovery digest refused");
  Check(RebirthCamperStationCompletionRecord.TryRead(completion.Clone().Write(),admission,receipt,out var readCompletion)&&
    XNode.DeepEquals(readCompletion.Write(),completion.Write()),"completion record round trip");
  foreach(var field in new[]{"completedOwner","vehicleId","vehicleClass"}){
   malformed=completion.Write();malformed.SetAttributeValue(field,999);
   Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"completion identity refusal "+field);
  }
  foreach(var field in new[]{"admission","queued","saveScope"}){
   malformed=completion.Write();malformed.SetAttributeValue(field,new string('D',64));
   Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"completion binding refusal "+field);
  }
  malformed=completion.Write();malformed.SetAttributeValue("stationAddress",new RebirthCamperStationAddress(address.Vehicle,Guid.NewGuid()).ToString());
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"completion station refusal");
  malformed=completion.Write();malformed.SetAttributeValue("extra",1);
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"completion shape refusal");
  malformed=completion.Write();malformed.SetAttributeValue("nativeVehicleId",0);
  Check(!RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out _),"invalid version3 native ID refused");
  malformed=completion.Write();malformed.Attribute("nativeVehicleId").Remove();malformed.SetAttributeValue("version",2);
  Check(RebirthCamperStationCompletionRecord.TryRead(malformed,admission,receipt,out var version2)&&version2.HasRecoveryImage,
   "version2 saved records retained without rewrite");
  completed.Saved=false;Check(!RebirthCamperStationCompletionRecord.TryCreate(completed,proof,out _),"failed saved-output adapter refusal");completed.Saved=true;
  var completions=new Dictionary<string,RebirthCamperStationCompletionRecord>{{admission.JobId,completion}};
  var completionSection=RebirthCamperStationCompletionRecord.WriteAll(completions,admissions,records);
  Check(RebirthCamperStationCompletionRecord.ReadAll(new XElement("progression",completionSection),admissions,records,out var savedCompletions)&&savedCompletions.Count==1,"completion collection round trip");
  Check(RebirthCamperStationCompletionRecord.ReadAll(new XElement("progression"),admissions,records,out savedCompletions)&&savedCompletions.Count==0,"legacy completion absence");
  Check(!RebirthCamperStationCompletionRecord.ReadAll(new XElement("progression",completionSection,new XElement(completionSection)),admissions,records,out savedCompletions),"duplicate completion sections");
  Check(!RebirthCamperStationCompletionRecord.ReadAll(new XElement("progression",completionSection),admissions,new Dictionary<string,RebirthCamperStationPublicationRecord>(),out savedCompletions),"orphan completion receipt refusal");
  var trainingPlan=new RebirthCamperStationTrainingPlan{Binding=completed.Binding,Publication=completion,Skill="skill.metalworking",Source="craft:skill.metalworking:forgeRecipe",Cycles=2,PreparationMultiplier=1f,Receipt=RebirthCamperStationTrainingPlan.ReceiptFor(admission),Model=new RebirthCraftTrainingRules.Model{Valid=true,Raw=.1f,Difficulty=5f,Seconds=3f,RecipeId="forgeRecipe",SkillId="skill.metalworking",Outputs=1,IngredientTypes=2}};
  Check(RebirthCamperStationTrainingRecord.TryCreate(trainingPlan,out var training),"training frozen record");
  Check(RebirthCamperStationTrainingRecord.TryRead(training.Clone().Write(),admission,receipt,completion,out _),"training clone round trip");
  var recovered=training.Clone().RestoreInputs();
  Check(recovered.HasTraining&&recovered.Skill==trainingPlan.Skill&&recovered.Source==trainingPlan.Source&&
    recovered.Receipt==trainingPlan.Receipt&&recovered.Cycles==2&&recovered.PreparationMultiplier==1f&&
    recovered.Model.Valid&&recovered.Model.Raw==.1f&&recovered.Model.Difficulty==5f&&recovered.Model.Seconds==3f&&
    recovered.Model.RecipeId=="forgeRecipe"&&recovered.Model.SkillId==trainingPlan.Skill&&recovered.Model.Outputs==1&&recovered.Model.IngredientTypes==2,"persisted exact award inputs");
  var editedImage=training.Write();editedImage.SetAttributeValue("cycles",99);Check(training.RestoreInputs().Cycles==2,"detached XML cannot rewrite frozen inputs");
  foreach(var field in new[]{"raw","difficulty","seconds","multiplier"})foreach(var invalid in new[]{"NaN","Infinity","-1"}){var node=training.Write();node.SetAttributeValue(field,invalid);Check(!RebirthCamperStationTrainingRecord.TryRead(node,admission,receipt,completion,out _),"training invalid number "+field+invalid);}
  foreach(var invalid in new[]{"0","10000"}){var node=training.Write();node.SetAttributeValue("cycles",invalid);Check(!RebirthCamperStationTrainingRecord.TryRead(node,admission,receipt,completion,out _),"training invalid cycles");}
  foreach(var field in new[]{"receipt","admission","completion","modelSkill","source"}){var node=training.Write();node.SetAttributeValue(field,"wrong");Check(!RebirthCamperStationTrainingRecord.TryRead(node,admission,receipt,completion,out _),"training wrong binding "+field);}
  admission.CreationId="replacement";Check(!RebirthCamperStationPublicationRecord.TryRead(receipt.Write(),admission,out _),"replacement character refusal");
  Console.WriteLine("PASS actual saved-queue matcher/address/input layout: valid match; crafter, duplicate, input, policy, attempt, station, save, timer, malformed job, decode and thread refusals. Native save/admission/items doubled; no gameplay proof.");
 }
}
public static class RebirthStationMaterialInputCodec {public static bool TryWrite(IList<ItemStack> s,int p,out XElement image){image=null;return false;}public static bool TryRead(XElement image,out ItemStack[] s,out int p){s=null;p=0;return false;}}
