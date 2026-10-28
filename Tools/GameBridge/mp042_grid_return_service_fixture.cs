// AUTHORED_NOT_RUN. Shares explicit service adapters with cancellation fixture.
internal static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>"creation";}
internal static partial class RebirthCookingRemoteAdmission {
 internal static bool TryOperation(ItemValue item,out Guid id){id=Guid.Empty;return item!=null&&item.Metadata.TryGetValue("operation",out var text)&&Guid.TryParseExact(text,"N",out id);}
}
internal static partial class RebirthCookingRemoteAdmissionBackend {
 internal static bool StationAccess=true;
 private static RebirthCamperTransferJournal admission,returns;private static ItemValue carrier;private static Guid originalId;private static bool adopted;
 private static RebirthCamperTransferJournal Journal()=>admission;private static RebirthCamperTransferJournal Returns()=>returns;
 private static void Install(){}private static void Scope(){}private static bool Adopted(Guid operation)=>adopted;
// ACTUAL_OWED
// ACTUAL_RETURN_GRID
 private static void ResetReturn(bool refused=false){
  Model.Reset();GameManager.Instance.World=Model.Player.world;RebirthCookingOverflowOriginal.Saved=false;RebirthCookingOverflowOriginal.Advances=0;admission=new RebirthCamperTransferJournal();returns=new RebirthCamperTransferJournal();adopted=false;originalId=Guid.NewGuid();
  var root=new XElement("original",new XAttribute("world",Model.Player.world.worldState.Guid),new XAttribute("owner","storage-owner"),new XAttribute("location","station"),
   new XElement("beforeGrid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(new ItemValue(),7)}))),new XElement("afterGrid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(new ItemValue(),5)}))));
  byte[] image=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));var record=admission.Prepare(originalId,Guid.NewGuid(),"creation",0,image,image);record.State=refused?RebirthCamperTransferJournal.Phase.Rejected:RebirthCamperTransferJournal.Phase.Applied;
  carrier=new ItemValue();carrier.Metadata["operation"]=originalId.ToString("N");
 }
 private static string CallReturn(string expected="")=>ReturnGrid(Model.Player,"creation","station",carrier,expected);
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 internal static void ExerciseReturn(){
  foreach(bool refused in new[]{false,true}){
   ResetReturn(refused);var source=admission.Read(originalId);byte[] original=(byte[])source.Before.Clone();
   Need(CallReturn().StartsWith("return:0:")&&returns.Prepared==1&&Model.PlannedRefund==(refused?7:5),"wrong retained before/after grid refund");
   Guid refund=returns.Rows.Single().Operation;Model.OwnerPhase=1;Need(CallReturn().StartsWith("return:1:")&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"return skipped saved phase one");
   Model.OwnerPhase=2;string result=CallReturn();Need(result.StartsWith("returned:")&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Applied,"return skipped saved phase two");
   Need(CallReturn()==result&&returns.Prepared==1&&returns.Rows.Single().Operation==refund&&source.Before.SequenceEqual(original)&&admission.Prepared==1,"terminal replay altered source original or repeated refund");
  }
  ResetReturn();StationAccess=false;Need(CallReturn().StartsWith("return:0:")&&returns.Prepared==1,"closing native station lease cannot strand this owner's saved unpaid grid entitlement");StationAccess=true;
  ResetReturn();CallReturn();Model.Source="changed-source";Need(CallReturn().StartsWith("return:0:")&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared,"failed no-start observation rejected original");
  Model.NoStart=true;Need(CallReturn().StartsWith("retry-return:")&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Rejected,"positive rejection unavailable");
  Need(CallReturn().StartsWith("retry-return:")&&returns.Prepared==1,"cold missing hint bypassed unacknowledged rejection");Model.RejectionAcknowledged=true;
  string retainedHint=returns.Rows.Single().Operation.ToString("N");
  Need(CallReturn(retainedHint).StartsWith("retry-return:")&&returns.Prepared==1,"acknowledged hinted rejection issued replacement before client retired hint");
  Need(CallReturn().StartsWith("return:0:")&&returns.Prepared==2,"acknowledged rejection failed to replan after hint retirement");
  ResetReturn();Model.Room=false;Need(CallReturn().StartsWith("return:0:")&&returns.Prepared==1&&RebirthCookingOverflowOriginal.Advances==0,"full owner bypassed saved admission");
  Model.OwnerPhase=1;Need(CallReturn().StartsWith("return:1:")&&RebirthCookingOverflowOriginal.Advances==0,"overflow preceded saved owner credit");
  Model.OwnerPhase=2;Need(CallReturn()==RebirthCookingRemoteAdmission.Pending&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"unsaved remainder completed return");
  RebirthCookingOverflowOriginal.Saved=true;Need(CallReturn().StartsWith("returned:")&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Applied,"saved remainder failed to complete return");
  for(int variant=0;variant<7;variant++){
   ResetReturn();var record=admission.Read(originalId);var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
   if(variant==0)adopted=true;if(variant==1)record.State=RebirthCamperTransferJournal.Phase.Prepared;if(variant==2)root.SetAttributeValue("owner","foreign");
   if(variant==3)root.SetAttributeValue("world","foreign");if(variant==4)root.SetAttributeValue("location","foreign");if(variant==5)record.OwnerCreation="foreign";if(variant==6)Model.Current=false;
   record.Before=record.After=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
   Need(CallReturn()==RebirthCookingRemoteAdmission.Pending&&returns.Prepared==0,"unqualified source authorized grid refund "+variant);
  }
  ResetReturn();CallReturn();Model.Player.Died++;Model.OwnerPhase=1;Need(CallReturn()==RebirthCookingRemoteAdmission.Pending&&returns.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared,"foreign life advanced pending grid return");
  ResetReturn();CallReturn();Need(CallReturn(Guid.NewGuid().ToString("N"))==RebirthCookingRemoteAdmission.Pending&&returns.Prepared==1,"foreign refund hint created replacement");
  Console.WriteLine("PASS actual complete ReturnGrid/Owed server service; native saves, codecs and genuine remote topology remain adapters/NOT RUN");
 }
}
internal static class Program {public static void Main()=>RebirthCookingRemoteAdmissionBackend.ExerciseReturn();}
