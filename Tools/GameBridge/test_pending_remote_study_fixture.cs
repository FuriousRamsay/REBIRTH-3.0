using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {public static class Time {public static float unscaledTime;}}
public class World {public EntityPlayer Player;public bool IsRemote(){return false;}public object GetEntity(int id){return Player;}}
public class GameManager {public static GameManager Instance=new GameManager();public World World;}
public class EntityPlayer {public int entityId=1;public World world;public Inventory inventory=new Inventory();public bool Dead;public bool IsDead(){return Dead;}}
public class EntityPlayerLocal:EntityPlayer {}
public class Inventory {public ItemValue holdingItemItemValue;}
public class ItemValue {public int type=1;public ushort Seed;public ItemClass ItemClass=new ItemClass();}
public class ItemClass {public string GetItemName(){return "book";}}
public class ItemStack {public ItemValue itemValue=new ItemValue();public bool IsEmpty(){return false;}}
public class Origin {public string CreationId="one";}
public class RebirthWorldCharacterRecord {public bool IsComplete=true;public Origin Origin=new Origin();}
public static class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return true;}}
public static class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true;}
public class Options {public bool RequireTimedReading=true;}
public static class RebirthSandboxOptionManager {public static Options Current=new Options();}
public static class RebirthCharacterCreationHoldService {public static bool IsHeld(EntityPlayer p){return false;}}
public static class RebirthBackpackLibraryReservation {public static bool Blocked;public static bool BlocksResourceUse(EntityPlayer p){return Blocked;}}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b){return a==b;}}
public class RebirthLiteratureDefinition {}
public static class RebirthProgressionRuntimeConfig {public static bool TryGetLiterature(string id,out RebirthLiteratureDefinition d){d=new RebirthLiteratureDefinition();return true;}}
public static class RebirthLiteratureService {
 public static int Starts;public static bool Owned=true;
 public static bool TryFindMatchingInventoryStackPublic(EntityPlayer p,int type,ushort seed,out ItemStack s){s=new ItemStack();return Owned;}
 public static bool TryReadMatchingInventoryItem(EntityPlayer p,int type,ushort seed,out string message){message="result";if(!Owned||RebirthBackpackLibraryReservation.Blocked)return false;Starts++;return true;}
}
public static class Localization {public static string Get(string key){return key;}}
public class ConnectionManager {public bool IsServer=true;public int Results;public bool LastSuccess;public void SendPackage(NetPackageRebirthSurvivorSupportActionResult p,int _attachedToEntityId){Results++;LastSuccess=p.Success;}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
public class NetPackageRebirthSurvivorSupportActionResult {public bool Success;public NetPackageRebirthSurvivorSupportActionResult Setup(bool ok,string message){Success=ok;return this;}}
// ACTUAL_PENDING_CLASS
public static class PendingStudyChecks {
 static EntityPlayer Setup(){
  RebirthPendingRemoteStudy.Clear();Time.unscaledTime=10;
  RebirthLiteratureService.Starts=0;RebirthLiteratureService.Owned=true;
  RebirthBackpackLibraryReservation.Blocked=false;RebirthSandboxOptionManager.Current.RequireTimedReading=true;
  RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
  var w=new World();var p=new EntityPlayer{world=w};w.Player=p;GameManager.Instance.World=w;return p;
 }
 static void A(bool yes,string message){if(!yes)throw new Exception(message);}
 public static void Run(){
  var p=Setup();A(RebirthPendingRemoteStudy.TryDefer(p,1,0,"one"),"admission");
  RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==0,"no premature start");
  p.inventory.holdingItemItemValue=new ItemValue();Time.unscaledTime=10.2f;RebirthPendingRemoteStudy.Pump();
  RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==1,"one held arrival starts once");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");Time.unscaledTime=13;
  p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();
  A(RebirthLiteratureService.Starts==0&&!SingletonMonoBehaviour<ConnectionManager>.Instance.LastSuccess,"deadline rejects late arrival");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");Time.unscaledTime=12;
  A(RebirthPendingRemoteStudy.TryDefer(p,1,0,"one"),"duplicate accepted without extension");
  Time.unscaledTime=13;RebirthPendingRemoteStudy.Pump();A(SingletonMonoBehaviour<ConnectionManager>.Instance.Results==1,"duplicate keeps deadline");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");RebirthLiteratureService.Owned=false;
  A(!RebirthPendingRemoteStudy.TryDefer(p,2,0,"one"),"new invalid request refused");
  RebirthLiteratureService.Owned=true;p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();
  A(RebirthLiteratureService.Starts==0,"new invalid request cancels old");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");RebirthWorldCharacterService.Record.Origin.CreationId="two";
  p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==0,"character replacement");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");RebirthPendingRemoteStudy.Cancel(p);
  p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==0,"explicit cancellation");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");RebirthSandboxOptionManager.Current.RequireTimedReading=false;
  p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==0,"option change cancels");
  p=Setup();RebirthPendingRemoteStudy.TryDefer(p,1,0,"one");RebirthBackpackLibraryReservation.Blocked=true;
  p.inventory.holdingItemItemValue=new ItemValue();RebirthPendingRemoteStudy.Pump();A(RebirthLiteratureService.Starts==0,"ordinary custody gate retained");
 }
}