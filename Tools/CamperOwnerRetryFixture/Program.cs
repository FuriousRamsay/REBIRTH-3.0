using System;
using System.Collections;
using System.Collections.Generic;

class Program
{
    static int passed;
    static void Check(bool yes,string name){if(!yes)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        var player=new EntityPlayerLocal();var vehicle=new EntityVehicle();var world=new World{Player=player,Vehicle=vehicle};vehicle.world=world;
        var journal=new RebirthCamperTransferJournal();var operation=Guid.NewGuid();bool current=true;
        Func<bool> session=()=>current;
        Func<bool> eligible=()=>RebirthCamperOwnerSaveRetry.Eligible(journal,operation,vehicle,player,session);
        Check(eligible(),"complete candidate admitted");
        player.Buffs.Marker=2;Check(!eligible(),"partial marker refused");player.Buffs.Marker=0;Check(!eligible(),"missing marker refused");player.Buffs.Marker=1;
        current=false;Check(!eligible(),"changed character refused");current=true;
        world.Remote=true;Check(!eligible(),"remote world refused");world.Remote=false;
        world.Player=new EntityPlayerLocal();Check(!eligible(),"different owner refused");world.Player=player;
        world.Vehicle=new EntityVehicle();Check(!eligible(),"replaced vehicle refused");world.Vehicle=vehicle;
        RebirthCamperTransferSnapshot.Live=new byte[]{2};Check(!eligible(),"changed candidate refused");RebirthCamperTransferSnapshot.Live=new byte[]{1};
        journal.Receipt.State=RebirthCamperTransferJournal.Phase.Applied;Check(!eligible(),"terminal receipt refused");journal.Receipt.State=RebirthCamperTransferJournal.Phase.Uncertain;
        RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,session);
        RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,session);
        Check(GameManager.Instance.Coroutines.Count==1,"duplicate observer suppressed");
        var loop=GameManager.Instance.Coroutines[0];
        Check(loop.MoveNext() && RebirthCamperSavedCursor.Attempts==1,"failed save yields without acknowledgement");
        Check(RebirthCamperSaveAcknowledgement.Starts==0,"failed save cannot apply");
        RebirthCamperSavedCursor.Succeeds=true;
        Check(!loop.MoveNext() && RebirthCamperSaveAcknowledgement.Starts==1,"retry success delegates vehicle acknowledgement");
        operation=Guid.NewGuid();RebirthCamperSavedCursor.Succeeds=false;
        RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,session);loop=GameManager.Instance.Coroutines[1];
        current=false;int attempts=RebirthCamperSavedCursor.Attempts;
        Check(!loop.MoveNext() && attempts==RebirthCamperSavedCursor.Attempts,"character switch stops before save");current=true;
        operation=Guid.NewGuid();RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,session);loop=GameManager.Instance.Coroutines[2];
        Check(loop.MoveNext(),"timeout observer started");UnityEngine.Time.realtimeSinceStartup=31;
        Check(!loop.MoveNext() && RebirthCamperSaveAcknowledgement.Starts==1,"timeout preserves pending receipt");
        Console.WriteLine(passed+" checks passed; actual retry code, simulated save/world/snapshot dependencies.");
    }
}
public static class ThreadManager{public static bool IsMainThread()=>true;}
public class World{public bool Remote;public EntityPlayerLocal Player;public EntityVehicle Vehicle;public bool IsRemote()=>Remote;public object GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>Vehicle;}
public class EntityPlayerLocal{public Buffs Buffs=new Buffs();public bool IsDead()=>false;}
public class Buffs{public float Marker=1;public float GetCustomVar(string key)=>Marker;}
public class EntityVehicle{public World world;public int entityId=1;public Bag bag=new Bag();}
public class Bag{public Grid ItemGrid=new Grid();}
public class Grid{public object items=new object();}
public class RebirthCamperTransferJournal
{public enum Phase{Uncertain,Applied}public class Record{public Guid Vehicle=RebirthCamperModuleRuntime.Value.Identity;public Phase State=Phase.Uncertain;public byte[] After={1};}public Record Receipt=new Record();public Record Read(Guid id)=>Receipt;}
public class RebirthCamperModuleRuntime
{public static RebirthCamperModuleRuntime Value=new RebirthCamperModuleRuntime();public Guid Identity=Guid.NewGuid();public object Storage=new object(),Stations=new object();public static RebirthCamperModuleRuntime For(EntityVehicle v)=>Value;}
public class RebirthCamperTransferSnapshot
{public static byte[] Live={1};public object Cursor=new object();public int BackpackSlot=-1;public static RebirthCamperTransferSnapshot Restore(Guid id,byte[] bytes)=>new RebirthCamperTransferSnapshot();public static byte[] Capture(object a,object b,object c,object d,int slot=-1)=>Live;}
public static class RebirthCamperSavedCursor
{public static int Attempts;public static bool Succeeds;public static bool SaveAndVerifyPublishedLocal(EntityPlayerLocal p,Guid id,Func<bool> current){Attempts++;return Succeeds;}}
public static class RebirthCamperSaveAcknowledgement
{public static int Starts;public static void Start(RebirthCamperTransferJournal j,Guid id,EntityVehicle v,EntityPlayerLocal p,Func<bool> c,bool resume){Starts++;}}
public static class RebirthCamperTrace{public static void Write(string s,EntityVehicle v,string d){}}
public class GameManager{public static GameManager Instance=new GameManager();public List<IEnumerator> Coroutines=new List<IEnumerator>();public void StartCoroutine(IEnumerator e)=>Coroutines.Add(e);}
namespace UnityEngine{public static class Time{public static float realtimeSinceStartup;}public class WaitForSecondsRealtime{public WaitForSecondsRealtime(float seconds){}}}
