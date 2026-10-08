using System;using System.Xml.Linq;
public class RebirthStablePlayerIdentity{public string StorageKey="owner",CanonicalId="canonical";public static bool TryFromClientInfo(ClientInfo c,out RebirthStablePlayerIdentity i){i=ServerDoubles.Peer;return i!=null;}}
public class RebirthWorldCharacterRecord{public RebirthWorldSupportState Support;public Origin Origin=new();public bool IsComplete=true;public int Touches;public void Touch(string reason){Touches++;}}
public class Origin{public string CreationId;}
public static partial class Program{
static bool serverAuthority=true, migrated,loaderOk=true,cacheOk=true,loaderThrows,ownerMatches=true,worldMatches=true;static string fixturePath="path";static RebirthWorldCharacterRecord cached,saved;static readonly object gate=new();
static string GetPath(string key)=>fixturePath;
static object GetWriteLock(string key)=>gate;
static bool TryGetCurrentCached(RebirthStablePlayerIdentity i,out RebirthWorldCharacterRecord r){r=cached;return cacheOk;}
static bool TryLoadValidatedRecord(string path,RebirthStablePlayerIdentity i,out RebirthWorldCharacterRecord r,out bool migration,out string a,out string b){if(loaderThrows)throw new Exception("loader");r=saved;migration=migrated;a=b="";return loaderOk&&ownerMatches&&worldMatches;}
static void WitnessTests(RebirthWorldSupportState state,string creation,string marker,RebirthGearPreparationRefusal refusal){
var identity=new RebirthStablePlayerIdentity();
void Reset(){serverAuthority=true;migrated=false;loaderOk=cacheOk=true;loaderThrows=false;ownerMatches=worldMatches=true;fixturePath="path";cached=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};saved=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};}
bool Base()=>HasSavedUnpreparedGearBase(identity,cached,marker);
bool Final()=>HasSavedGearPreparationRefusal(identity,cached.Support.PendingGearPreparationRefusal);
Reset();Check(Base()&&Final(),"both witness positive");
saved.Support.PendingGearPreparationRefusal=null;Check(Base()&&!Final(),"base accepts absent saved refusal only");
Reset();cached.Support.PendingGearPreparationRefusal=null;Check(Base()&&!Final(),"base accepts absent cached refusal only");
Reset();var other=cached;cached=new(){Support=state.Clone(),Origin=new(){CreationId=creation}};Check(!HasSavedUnpreparedGearBase(identity,other,marker),"base exact cached reference");
foreach(var stage in new[]{"authority","path","loader","cache","migrated","savedIncomplete","cachedIncomplete","foreignCreation","loaderException","ownerMismatch","worldMismatch"}){
Reset();switch(stage){case "authority":serverAuthority=false;break;case "path":fixturePath=null;break;case "loader":loaderOk=false;break;case "cache":cacheOk=false;break;case "migrated":migrated=true;break;case "savedIncomplete":saved.IsComplete=false;break;case "cachedIncomplete":cached.IsComplete=false;break;case "foreignCreation":saved.Origin.CreationId=Guid.NewGuid().ToString("N");break;case "loaderException":loaderThrows=true;break;case "ownerMismatch":ownerMatches=false;break;case "worldMismatch":worldMatches=false;break;}
Check(!Base()&&!Final(),"witness refuses "+stage);}
Reset();RebirthGearPreparationRefusal.TryRead(new XElement("support",refusal.Write()),out var equivalent);cached.Support.PendingGearPreparationRefusal=equivalent;Check(!HasSavedGearPreparationRefusal(identity,refusal),"final exact cached refusal reference");
Reset();RebirthGearPreparationRefusal.TryCreateStale(marker,4,out var olderRefusal);saved.Support.PendingGearPreparationRefusal=olderRefusal;Check(Base()&&!Final(),"base strips same original refusal observed stage only");Reset();saved.Support.GearRevision++;Check(!Base()&&!Final(),"revision mismatch");
foreach(Action<RebirthWorldSupportState> mutate in new Action<RebirthWorldSupportState>[]{
x=>x.MusicRevision++,x=>x.AudiobookRevision++,x=>x.MusicShuffle=!x.MusicShuffle,
x=>x.EquippedGearBySlot["belt"]="other",x=>x.EquippedGearItemDataBySlot["belt"]="BA==",
x=>x.MusicCassettes[0].ItemData="BA==",x=>x.AudiobookCassettes[0].ItemData="BA==",
x=>x.Entries["a"].Stacks++,x=>x.Entries["a"].GraceRemainingActiveSeconds++}){
Reset();mutate(saved.Support);Check(!Base()&&!Final(),"full sibling equality");}
Reset();saved.Support.PendingMusicTransfer=new();Check(!Base()&&!Final(),"pending music witness");
Reset();saved.Support.PendingLibraryTransfer=new();Check(!Base()&&!Final(),"pending library witness");
Reset();Check(!HasSavedUnpreparedGearBase(null,cached,marker)&&!HasSavedGearPreparationRefusal(null,refusal),"null identity");
Reset();Check(!HasSavedUnpreparedGearBase(identity,cached,"different-marker"),"marker mismatch");
}
}
