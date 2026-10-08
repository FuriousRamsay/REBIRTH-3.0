using System;
// OPTION TYPES
class EntityPlayer {}
class ItemValue {public int type;public ushort Seed;public ItemClass ItemClass=new ItemClass();}
class RebirthSandboxOptionManager {public static RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();public bool RequireTimedReading=true;}
class RebirthBackpackLibraryReservation {public static bool Held;public static bool BlocksResourceUse(EntityPlayer p){return Held;}}
class Localization {public static string Get(string key){return key;}}
class RebirthLiteratureStudySessionService {public static int Calls;public static bool TryBegin(EntityPlayer p,int type,ushort seed,out string message){Calls++;message="timed";return true;}}
class ItemClass {public string GetItemName(){return "testBook";}}
class ItemStack {public ItemValue itemValue=new ItemValue();public bool IsEmpty(){return false;}}
class RebirthLiteratureDefinition {}
class RebirthAudiobookDefinition {public string SourceLiteratureId="testBook";}
class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true;}
class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
class RebirthCharacterCreationHoldService {public static bool IsHeld(EntityPlayer player){return false;}}
class RebirthProgressionRuntimeConfig {public static int AudioLookups;public static bool TryGetAudiobook(string id,out RebirthAudiobookDefinition audio){AudioLookups++;audio=new RebirthAudiobookDefinition();return true;}public static bool TryGetLiterature(string id,out RebirthLiteratureDefinition definition){definition=new RebirthLiteratureDefinition();return true;}}
class ActualCompletion {
public static int Lookups,Grants;
public static bool TryFindMatchingInventoryStackPublic(EntityPlayer player,int type,ushort seed,out ItemStack stack){Lookups++;stack=new ItemStack();return true;}
public static bool TryApplyDefinition(EntityPlayer player,RebirthLiteratureDefinition definition,string source,string medium,out string message){Grants++;message="granted";return true;}
// COMPLETION METHOD
// AUDIO COMPLETION METHOD
}
class Check {
static int CompleteCalls;static bool CompletionAllowed=true;
static bool TryCompleteStudy(EntityPlayer player,int type,ushort seed,out string message){CompleteCalls++;message="quick";return CompletionAllowed;}
static bool clientTracking;static int clientItemType;static ushort clientSeed;static bool clientLastSlow;static float clientHeartbeatRemaining;
// TRACKING METHOD
// METHODS
static void Assert(bool pass,string label){if(!pass)throw new Exception(label);}
static void Main(){var player=new EntityPlayer();string message;
ReadingState decoded; var setting=new ReadingState();Assert(setting.RequireTimedReading,"default setting is timed");
Assert(ReadingCodec.Encode(setting)==""&&ReadingCodec.Decode("",out decoded)&&decoded.RequireTimedReading,"omitted old setting stays timed");
setting.RequireTimedReading=false;string fragment=ReadingCodec.Encode(setting);Assert(fragment=="CRA"&&ReadingCodec.Decode(fragment,out decoded)&&!decoded.RequireTimedReading,"quick persisted option roundtrip");
Assert(ReadingCodec.Decode("CRB",out decoded)&&decoded.RequireTimedReading,"explicit timed setting accepted");
Assert(!ReadingCodec.Decode("CRC",out decoded)&&!ReadingCodec.Decode("CRZ",out decoded)&&!ReadingCodec.Decode("CR?",out decoded),"invalid persisted booleans rejected");
Assert(TryReadMatchingInventoryItem(player,1,2,out message)&&RebirthLiteratureStudySessionService.Calls==1&&CompleteCalls==0,"default routes through held timed session");
BeginClientTracking(player,new ItemValue{type=1,Seed=2});Assert(clientTracking&&clientItemType==1&&clientSeed==2&&!clientLastSlow&&clientHeartbeatRemaining==0f,"timed tracking records exact item");
RebirthSandboxOptionManager.Current.RequireTimedReading=false;
Assert(TryReadMatchingInventoryItem(player,1,2,out message)&&CompleteCalls==1&&RebirthLiteratureStudySessionService.Calls==1,"quick routes through authoritative completion without timed session");
BeginClientTracking(player,new ItemValue{type=3,Seed=4});Assert(!clientTracking&&clientItemType==1&&clientSeed==2,"quick mode clears tracking without replacing held item");
CompletionAllowed=false;Assert(!TryReadMatchingInventoryItem(player,1,2,out message)&&CompleteCalls==2,"quick preserves completion refusal");
RebirthBackpackLibraryReservation.Held=true;Assert(!TryReadMatchingInventoryItem(player,1,2,out message)&&CompleteCalls==2,"quick cannot bypass library custody");
Assert(!ActualCompletion.TryCompleteStudy(player,1,2,out message)&&ActualCompletion.Lookups==0&&ActualCompletion.Grants==0&&message=="xuiRebirthLibraryTransferPending","actual timed completion refuses custody before lookup or grant");
RebirthBackpackLibraryReservation.Held=false;Assert(ActualCompletion.TryCompleteStudy(player,1,2,out message)&&ActualCompletion.Lookups==1&&ActualCompletion.Grants==1,"actual unreserved completion reaches authoritative definition");
string sourceId;RebirthBackpackLibraryReservation.Held=true;
Assert(!ActualCompletion.TryCompleteAudiobook(player,"testAudio",out sourceId,out message)&&sourceId==string.Empty&&RebirthProgressionRuntimeConfig.AudioLookups==0&&ActualCompletion.Grants==1,"actual audiobook completion refuses custody before definition or grant");
RebirthBackpackLibraryReservation.Held=false;
Assert(ActualCompletion.TryCompleteAudiobook(player,"testAudio",out sourceId,out message)&&sourceId=="testBook"&&RebirthProgressionRuntimeConfig.AudioLookups==1&&ActualCompletion.Grants==2,"actual unreserved audiobook preserves source study routing");
Console.WriteLine("PASS actual reading-option codec branches and routing/tracking: omitted default, quick roundtrip, explicit timed, invalid bool refusal, timed/quick completion and custody; full codec framing/native adapters not exercised");}
}