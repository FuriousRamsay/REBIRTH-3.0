using System;using System.Collections.Generic;
// Actual production scope, ownership and outgoing checkpoint; native items/save adapters doubled.
class EntityPlayer{public int entityId=1;}
class Origin{public string CreationId;}
class Cassette{public string SlotId,ItemId,ItemData;}
class Pending{public bool IsAudiobook;public int Operation;public string AudiobookSlotId;}
class Support{public Pending PendingMusicTransfer;public List<Cassette>AudiobookCassettes=new List<Cassette>();}
class Progression{public Dictionary<string,float>LiteratureStudyProgress=new Dictionary<string,float>();}
class RebirthWorldCharacterRecord{public Progression Progression=new Progression();public void Touch(string reason){}public bool IsComplete=true;public Origin Origin=new Origin();public Support Support=new Support();}
class Session{public int EntityId=1;public float SaveAccumulator;public string ItemId,CompletionMessage;public string CreationId,StoredSlotId,StoredItemData,AudiobookItemId;public int CassetteItemType;public ushort CassetteSeed;public bool CompletedAwaitingSave;}
static class RebirthLiteratureService{public static bool HasMatchingInventoryItem(EntityPlayer p,int type,ushort seed){return true;}}
static class Localization{public static string Get(string key){return key;}}
static class RebirthWorldCharacterService{public static RebirthWorldCharacterRecord Record;public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
static class RebirthStudyHudNetworkService{public static int Clears;public static void SendClear(EntityPlayer p){Clears++;}}
// SCOPE
class Checks{
static Dictionary<int,Session> Active=new Dictionary<int,Session>();static bool SaveResult=true;static int Saves;
static bool Persist(EntityPlayer player,RebirthWorldCharacterRecord record,Session s){Saves++;return SaveResult;}
static int Cancels;static void Cancel(EntityPlayer player,Session session,string message,bool save){if(save)throw new Exception("unexpected second save");Cancels++;Active.Remove(player.entityId);}
// SWITCHES
static int CompletionMessages;static void Send(EntityPlayer p,bool ok,string message){CompletionMessages++;}static bool SaveDirty(EntityPlayer p,string reason){Saves++;return SaveResult;}
// COMPLETION
// OWNERSHIP
static bool PrepareOutgoing(EntityPlayer player,RebirthWorldCharacterRecord record,out string message){message="";
// OUTGOING
return true;}
static void A(bool value,string label){if(!value)throw new Exception(label);}
static void Main(){var player=new EntityPlayer();string creation=Guid.NewGuid().ToString("N");var record=new RebirthWorldCharacterRecord{Origin=new Origin{CreationId=creation}};var session=new Session{CreationId=creation,CassetteItemType=1};
A(HasSessionCassette(player,record,session),"current carried cassette accepted");record.Origin.CreationId=Guid.NewGuid().ToString("N");A(!HasSessionCassette(player,record,session),"carried cassette cannot retain prior character session");record.Origin.CreationId=creation;record.IsComplete=false;A(!HasSessionCassette(player,record,session),"incomplete character refused");record.IsComplete=true;
session.StoredSlotId="slot";session.StoredItemData="exact";session.AudiobookItemId="audio";record.Support.AudiobookCassettes.Add(new Cassette{SlotId="slot",ItemId="audio",ItemData="exact"});A(HasSessionCassette(player,record,session),"current stored cassette accepted");record.Support.PendingMusicTransfer=new Pending{IsAudiobook=true,Operation=2,AudiobookSlotId="slot"};A(!HasSessionCassette(player,record,session),"stored cassette removal refused");record.Support.PendingMusicTransfer=null;record.Support.AudiobookCassettes[0].ItemData="changed";A(!HasSessionCassette(player,record,session),"stored metadata replacement refused");
Active[player.entityId]=session;SaveResult=false;string message;A(!PrepareOutgoing(player,record,out message)&&Saves==1&&ReferenceEquals(Active[player.entityId],session),"save failure prevents outgoing replacement");SaveResult=true;A(PrepareOutgoing(player,record,out message)&&Saves==2,"saved outgoing session permits title switch");session.CompletedAwaitingSave=true;A(!PrepareOutgoing(player,record,out message)&&Saves==2,"completion awaiting save cannot be replaced");
session.CompletedAwaitingSave=false;RebirthWorldCharacterService.Record=record;
foreach(var stop in new Func<EntityPlayer,bool>[] {StopAudio,StopReading}){
Active[player.entityId]=session;SaveResult=false;int before=Cancels;A(!stop(player)&&Cancels==before&&ReferenceEquals(Active[player.entityId],session),"failed medium checkpoint preserves session");SaveResult=true;A(stop(player)&&Cancels==before+1&&!Active.ContainsKey(player.entityId),"successful medium checkpoint cancels once without second save");A(stop(player)&&Cancels==before+1,"no outgoing session admits mode switch");}
Console.WriteLine("PASS 6 actual reading/audio switch assertions: failed save preserves outgoing session, successful checkpoint removes once, empty session admits handoff; native persistence/cancel adapters doubled.");var completed=new Session{EntityId=player.entityId,ItemId="book",CreationId=creation};Active[player.entityId]=completed;record.Progression.LiteratureStudyProgress["book"]=.9f;SaveResult=false;int beforeSave=Saves;
BeginCompletionSave(player,record,completed,"completed");A(completed.CompletedAwaitingSave&&Active.ContainsKey(player.entityId)&&!record.Progression.LiteratureStudyProgress.ContainsKey("book")&&CompletionMessages==0,"failed completion save retains pending completion without restoring partial fraction");
A(RetryCompletedSave(player,completed,.3f)&&Saves==beforeSave+1,"completion retry throttled below one second");
A(RetryCompletedSave(player,completed,.7f)&&Saves==beforeSave+2&&Active.ContainsKey(player.entityId)&&CompletionMessages==0,"failed retry retains completion without notification");
A(!StopReading(player)&&Active.ContainsKey(player.entityId),"pending physical completion blocks medium replacement");
SaveResult=true;A(RetryCompletedSave(player,completed,1f)&&!Active.ContainsKey(player.entityId)&&CompletionMessages==1&&RebirthStudyHudNetworkService.Clears==1,"successful retry finishes once and clears HUD");
A(!RetryCompletedSave(player,new Session(),1f),"ordinary session does not enter completion retry");
Console.WriteLine("PASS 6 actual physical completion checkpoint assertions: retained failed completion, bounded retries, handoff refusal, successful completion and ordinary-session bypass; native reward application/save/HUD adapters not qualified.");Console.WriteLine("PASS 9 actual audiobook session ownership/outgoing checkpoint assertions; carried/stored character scope, pending removal, metadata, failed/successful save and completion hold. Native item/save adapters doubled; complete playback loop not executed.");}}