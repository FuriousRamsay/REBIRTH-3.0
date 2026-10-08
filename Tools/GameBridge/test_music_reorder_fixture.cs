using System;
using System.Collections.Generic;
class EntityPlayer {}
class RebirthMusicCassetteState {public string ItemId,ItemData;}
class RebirthWorldSupportState {
 public long MusicRevision=5;
 public object PendingGearTransfer,PendingMusicTransfer,PendingLibraryTransfer;
 public List<RebirthMusicCassetteState> MusicCassettes=new List<RebirthMusicCassetteState>();
}
class RebirthWorldCharacterRecord {public RebirthWorldSupportState Support=new RebirthWorldSupportState();public int Touches;public void Touch(string reason){Touches++;}}
class RebirthStablePlayerIdentity {
 public static bool Valid=true;
 public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity id){id=Valid?new RebirthStablePlayerIdentity():null;return Valid;}
}
class RebirthWorldCharacterRepository {
 public static RebirthWorldCharacterRecord Owned;public static bool SaveOk=true,ThrowSave;public static int Saves;
 public static bool TryGet(RebirthStablePlayerIdentity id,out RebirthWorldCharacterRecord r){r=Owned;return r!=null;}
 public static bool SaveIfDirty(RebirthStablePlayerIdentity id,string reason){Saves++;if(ThrowSave)throw new Exception("disk");return SaveOk;}
}
class RebirthSurvivorNetworkService {public static bool SendOk=true;public static int Sends;public static bool SendOwnerState(EntityPlayer p,long r,bool f,string reason){Sends++;return SendOk;}}
class RebirthSkillAwardService {public static int Queued;public static void QueueOwnerPublication(EntityPlayer p){Queued++;}}
class Localization {public static string Get(string key){return key;}}
class Check {
 const int Capacity=24;
 // SOURCE
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 static RebirthWorldCharacterRecord Setup(){
  var r=new RebirthWorldCharacterRecord();
  for(int i=0;i<24;i++)r.Support.MusicCassettes.Add(new RebirthMusicCassetteState{ItemId="same-song",ItemData="full-payload-"+i});
  RebirthWorldCharacterRepository.Owned=r;RebirthWorldCharacterRepository.SaveOk=true;RebirthWorldCharacterRepository.ThrowSave=false;RebirthWorldCharacterRepository.Saves=0;
  RebirthSurvivorNetworkService.SendOk=true;RebirthSurvivorNetworkService.Sends=0;RebirthSkillAwardService.Queued=0;RebirthStablePlayerIdentity.Valid=true;
  return r;
 }
 static void Main(){
  var p=new EntityPlayer();string msg;
  for(int from=0;from<24;from++)for(int to=0;to<24;to++){
   var r=Setup();var before=r.Support.MusicCassettes.ToArray();
   bool ok=TryReorder(p,r,from*24+to,5,out msg);
   Assert(ok==(from!=to),"same slot");
   for(int i=0;i<24;i++)Assert(object.ReferenceEquals(r.Support.MusicCassettes[i],before[i==from?to:i==to?from:i]),"payload reference");
   Assert(r.Support.MusicRevision==(from==to?5:6),"revision");
   Assert(RebirthWorldCharacterRepository.Saves==(from==to?0:1),"save count");
  }
  for(int mode=0;mode<9;mode++){
   var r=Setup();var first=r.Support.MusicCassettes[0];var last=r.Support.MusicCassettes[23];
   if(mode==0)r.Support.PendingGearTransfer=new object();
   if(mode==1)r.Support.PendingMusicTransfer=new object();
   if(mode==2)r.Support.PendingLibraryTransfer=new object();
   if(mode==3)RebirthStablePlayerIdentity.Valid=false;
   if(mode==4)RebirthWorldCharacterRepository.Owned=new RebirthWorldCharacterRecord();
   if(mode==5)RebirthWorldCharacterRepository.SaveOk=false;
   if(mode==6)RebirthWorldCharacterRepository.ThrowSave=true;
   Assert(!TryReorder(p,r,mode==7?576:23,mode==8?4:5,out msg),"reject mode"+mode);
   Assert(object.ReferenceEquals(r.Support.MusicCassettes[0],first)&&object.ReferenceEquals(r.Support.MusicCassettes[23],last)&&r.Support.MusicRevision==5,"rollback");
   Assert(RebirthSurvivorNetworkService.Sends==0&&RebirthSkillAwardService.Queued==0,"failed publication");
  }
  var saved=Setup();RebirthSurvivorNetworkService.SendOk=false;
  Assert(TryReorder(p,saved,23,5,out msg)&&RebirthSkillAwardService.Queued==1,"saved send retry");
  Assert(!TryReorder(p,saved,23,5,out msg)&&RebirthWorldCharacterRepository.Saves==1,"replay");
  Console.WriteLine("PASS actual reorder:576pairs preserve full record references; custody/identity/revision/bounds/save failure and publication retry");
 }
}