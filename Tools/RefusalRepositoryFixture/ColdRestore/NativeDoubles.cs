using System;using System.Collections.Generic;using System.Linq;
public class RebirthGearInventorySnapshot{public RebirthGearInventoryPlan.Stack[] Bag,Belt;public int OwnedBeltSlots;public bool IsUsableSource(bool b,int i)=>i>=0&&i<(b?Bag.Length:OwnedBeltSlots);public static bool TryCapture(object b,object t,int o,out RebirthGearInventorySnapshot s){s=null;if(!D.Capture||!(b is RebirthGearInventoryPlan.Stack[] bag)||!(t is RebirthGearInventoryPlan.Stack[] belt))return false;s=new(){Bag=bag,Belt=belt,OwnedBeltSlots=o};return true;}}
public class EntityPlayerLocal{public int entityId=7;public World world;public Buffs Buffs=new();public Bag bag=new();public Bag inventory=new();public PlayerUI PlayerUI=new();public bool Dead,Spawned=true;public bool IsSpawned()=>Spawned;public bool IsDead()=>Dead;}
public class Bag{public Grid ItemGrid=new();public bool IsHoldingItemActionRunning()=>Cold.Action;}public class Grid{public RebirthGearInventoryPlan.Stack[] items;}
public class Buffs{public Dictionary<string,float> CVars=new();public float GetCustomVar(string k)=>CVars.TryGetValue(k,out var v)?v:0;public void SetCustomVar(string k,float v,bool sync)=>CVars[k]=v;public void RemoveCustomVar(string k)=>CVars.Remove(k);}
public class World{public WorldState worldState=new();public EntityPlayerLocal Player,Entity;public bool Remote=true;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Entity;}
public class WorldState{public string Guid;}
public class GameManager{public static GameManager Instance;public World World;public Persistent getPersistentPlayerID(object x)=>new(){CombinedString=D.Canonical};public void SaveLocalPlayerData(){D.Saves++;D.SaveAction?.Invoke();if(D.ThrowSave)throw new Exception("save");D.Retired=D.Publish&&!World.Player.Buffs.CVars.ContainsKey(D.Marker);D.Rejected=D.Publish&&World.Player.Buffs.CVars.ContainsKey(D.Marker);D.SavedPhase=-1;}}
public class Persistent{public string CombinedString;}public class SingletonMonoBehaviour<T>{public static T Instance;}public class Peer{public bool Dead;public bool IsDisconnected()=>Dead;}
public class ConnectionManager{public bool IsServer;public Peer[] connectionToServer;public void SendToServer(object p){D.Sends++;D.SendAction?.Invoke();if(D.ThrowSend)throw new Exception("send");}}
public static class ThreadManager{public static bool IsMainThread()=>D.Main;}public enum EnumGamePrefs{GameGuidClient}
public static class GamePrefs{public static string GetString(EnumGamePrefs p)=>D.WorldGuid;}public static class GameIO{public static string GetPlayerDataDir()=>D.Root;}
public static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld()=>D.Mode;}
public static class RebirthCharacterCreationHoldService{public static bool IsHeld(EntityPlayerLocal p)=>D.CreationHold;}
public static class RebirthSurvivorClientState{public static string GetProjectedCreationId(EntityPlayerLocal p)=>D.Creation;}
public class RebirthStablePlayerIdentity{public string CanonicalId="owner";public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity o){o=new();return D.Owner;}}
public static class D{
public static bool Main=true,Mode=true,Owner=true,Hold=true,Offer,Rejected,Capture=true,File=true,Retired,Publish=true,ThrowSave,CreationHold;
public static string Canonical="owner",WorldGuid,Root=System.IO.Path.GetTempPath(),Creation,Marker;
public static RebirthGearPreparationIntent Intent;public static RebirthGearInventorySnapshot Image,SavedImage;public static float SavedPhase;public static int Saves,Sends,RetiredCalls;public static bool ThrowSend,ThrowMatch;public static Action SaveAction,SendAction,RetiredAction;}
