// AUTHORED_NOT_RUN. Actual final-character receipt predicate, explicit repository/native adapters.
using System;
using System.Collections.Generic;
// ACTUAL_TRAINING_READBACK
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
sealed class RebirthStablePlayerIdentity {internal string StorageKey="owner";}
static class RebirthSurvivorRequestScope {internal static bool Matches(string a,string b)=>a==b;}
sealed class Origin {internal string CreationId="creation";}
sealed class Progression {internal HashSet<string> SkillAwardReceipts=new HashSet<string>();}
sealed class SavedRecord {internal Origin Origin=new Origin();internal Progression Progression=new Progression();}
public static partial class RebirthWorldCharacterRepository {
 private static bool serverAuthority=true;
 internal static bool Loaded=true,Migrated,Throw,ReplacePath,RevokeServer;
 internal static string Path="final";internal static SavedRecord Saved;
 static string GetPath(string owner)=>Path;
 static object GetWriteLock(string owner)=>typeof(RebirthWorldCharacterRepository);
 static bool TryLoadValidatedRecord(string path,RebirthStablePlayerIdentity identity,out SavedRecord saved,out bool migrated,out object first,out object second){
  saved=Saved;migrated=Migrated;first=second=null;
  if(Throw)throw new Exception("injected failed final read");
  if(ReplacePath)Path="other";if(RevokeServer)serverAuthority=false;return Loaded;
 }
 internal static void Reset(Guid operation){
  serverAuthority=true;ThreadManager.Main=true;Loaded=true;Migrated=Throw=ReplacePath=RevokeServer=false;Path="final";
  Saved=new SavedRecord();Saved.Progression.SkillAwardReceipts.Add("electrical-action:"+operation.ToString("N"));
 }
}
static class Program {
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  var operation=Guid.NewGuid();var owner=new RebirthStablePlayerIdentity();
  for(int variant=0;variant<10;variant++){
   RebirthWorldCharacterRepository.Reset(operation);
   Need(RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,"creation",operation),"exact saved receipt refused");
   if(variant==0)RebirthWorldCharacterRepository.Loaded=false;
   if(variant==1)RebirthWorldCharacterRepository.Migrated=true;
   if(variant==2)RebirthWorldCharacterRepository.Saved.Progression.SkillAwardReceipts.Clear();
   if(variant==3)RebirthWorldCharacterRepository.Saved.Origin.CreationId="other";
   if(variant==4)RebirthWorldCharacterRepository.Throw=true;
   if(variant==5)RebirthWorldCharacterRepository.ReplacePath=true;
   if(variant==6)RebirthWorldCharacterRepository.RevokeServer=true;
   if(variant==7)RebirthWorldCharacterRepository.Saved.Progression=null;
   if(variant==8)RebirthWorldCharacterRepository.Saved=null;
   if(variant==9)ThreadManager.Main=false;
   Need(!RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,"creation",operation),"unqualified final receipt accepted variant "+variant);
  }
  RebirthWorldCharacterRepository.Reset(operation);
  Need(!RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,"creation",Guid.NewGuid()),"foreign operation receipt accepted");
  Need(!RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(null,"creation",operation),"missing owner accepted");
  Need(!RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,"",operation),"missing creation accepted");
  Need(!RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,"creation",Guid.Empty),"empty operation accepted");
  Console.WriteLine("PASS actual final-character training receipt predicate; repository decoding/native saved publication NOT RUN");
 }
}
