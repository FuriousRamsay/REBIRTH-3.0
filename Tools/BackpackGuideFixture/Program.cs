using System;using System.IO;using System.Linq;using System.Collections.Generic;
class Program{
 static int checks;static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks++;Console.WriteLine("PASS "+n);}
 static bool Both()=>RebirthJournalGuideService.Entries.Count(e=>e.Id=="BackpackReadingMaterials"||e.Id=="BackpackForSale")==2;
 static void Main(){string temp=Path.Combine(Path.GetTempPath(),"rebirth-backpack-guides-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);RebirthJournalStore.PathValue=Path.Combine(temp,"owner.xml");
 var p=new EntityPlayer{world=new World()};p.world.Player=p;RebirthJournalGuideService.Reset();RebirthJournalGuideService.Poll(p,true);Check(!Both(),"no equip does not unlock backpack guides");
 RebirthWorldCharacterService.Record.Support.EquippedGearBySlot["backpack"]="pack";RebirthWorldCharacterService.Record.IsComplete=false;RebirthJournalGuideService.ObserveBackpackEquip(p);Check(!Both(),"uncommitted character does not unlock");
 RebirthWorldCharacterService.Record.IsComplete=true;RebirthJournalGuideService.ObserveBackpackEquip(p);Check(Both(),"first host backpack unlocks both guides immediately");
 Check(File.Exists(RebirthJournalStore.PathValue+".guides.xml"),"guide state saved using production persistence");
 int rev=RebirthJournalGuideService.Revision;RebirthJournalGuideService.ObserveBackpackEquip(p);Check(rev==RebirthJournalGuideService.Revision,"duplicate equip idempotent");
 var entry=RebirthJournalGuideService.Entries.Single(e=>e.Id=="BackpackReadingMaterials");Check(RebirthJournalGuideService.Mark(entry,true),"read preference saved");
 RebirthWorldCharacterService.Record.Support.EquippedGearBySlot.Clear();RebirthJournalGuideService.Reset();RebirthJournalGuideService.Poll(p,true);Check(Both(),"guides survive reload and unequip");
 Check(RebirthJournalGuideService.IsRead(RebirthJournalGuideService.Entries.Single(e=>e.Id==entry.Id)),"read preference survives reload");
 RebirthJournalStore.PathValue=Path.Combine(temp,"remote.xml");RebirthJournalGuideService.Reset();p.world.Remote=true;RebirthSurvivorClientState.Item="";RebirthJournalGuideService.Poll(p,true);Check(!Both(),"remote without projected gear stays locked");
 RebirthSurvivorClientState.Item="pack";RebirthJournalGuideService.ObserveBackpackEquip(p);Check(Both(),"authenticated remote gear projection unlocks guides");
 RebirthJournalStore.PathValue=Path.Combine(temp,"legacy.xml");RebirthJournalGuideService.Reset();p.world.Remote=false;RebirthWorldCharacterService.Record.Support.EquippedGearBySlot["backpack"]="pack";RebirthJournalGuideService.Poll(p,true);Check(Both(),"older equipped-backpack save receives new guides");
 Check(RebirthJournalGuideService.Entries.Count(e=>e.Id=="welcome")==1,"existing journal definitions retained");Console.WriteLine("RESULT "+checks+" PASS; production journal and atomic persistence, native gear/projection doubles; temp data only; no game launch.");}}
public class RebirthJournalEntry{public string Id,Title,Type,Body,Created,Image;public bool Authored;}
public static class RebirthJournalStore{public static string PathValue;public static string ResolvePath()=>PathValue;}
public class World{public bool Remote;public EntityPlayer Player;public bool IsRemote()=>Remote;public EntityPlayer GetPrimaryPlayer()=>Player;}
public class EntityPlayer{public World world;public ChallengeJournal challengeJournal;}
public class ChallengeJournal{public List<Challenge> Challenges=new();}public class Challenge{public ChallengeClass ChallengeClass;public List<Objective> ObjectiveList;}public class ChallengeClass{public string Name;}public class Objective{public int Current;public bool Complete;}
public static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld()=>true;}
public static class RebirthSurvivorGearService{public const string BackpackSlotId="backpack";}
public class RebirthWorldCharacterRecord{public bool IsComplete=true;public Support Support=new();}public class Support{public Dictionary<string,string> EquippedGearBySlot=new();}
public static class RebirthWorldCharacterService{public static RebirthWorldCharacterRecord Record=new();public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return true;}}
public static class RebirthSurvivorClientState{public static string Item;public static string GetProjectedGearItem(EntityPlayer p,string s)=>Item;}
public static class RebirthBackpackLibraryPolicy{public static int CapacityForBackpack(string id)=>id=="pack"?10:0;}
public class RebirthMetabolismSnapshot{public float StomachFluidMl,StomachSolidMl,IntestinalFluidMl,IntestinalSolidMl;}
public static class RebirthMetabolismClientState{public static bool TryGet(out RebirthMetabolismSnapshot m){m=null;return false;}}
public static class Localization{public static string Get(string key)=>key;}public static class Log{public static void Warning(string s)=>Console.WriteLine(s);}
namespace UnityEngine{public static class Time{public static float realtimeSinceStartup;}}