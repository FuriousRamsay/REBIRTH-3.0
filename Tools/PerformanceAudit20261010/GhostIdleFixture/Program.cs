using System;using System.Collections.Generic;
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new();}
class EntityPlayer {}
class World {public bool Remote;public bool IsRemote()=>Remote;public object GetEntity(int id)=>new EntityPlayer();}
class GameManager {public static GameManager Instance=new();public World World=new();}
class Log {public static void Warning(string s){}}
class RebirthDogChunkObserverService {public static void RefreshForPlayer(EntityPlayer p){}}
class Program {
 class GhostRepairRetry {public int PlayerEntityId,AttemptsRemaining;public long NextAttemptUtcTicks;public bool OwnerProjectionReconciled;}
 static readonly object GhostRepairSync=new();static readonly Dictionary<int,GhostRepairRetry> GhostRepairRetries=new();
 static bool Ready=true,Missing=false;static int Projections,Repairs;
 static bool TryResolveOwnerId(EntityPlayer p,out string id){id="owner";return Ready;}
 static void ReconcileOwnerProjectionForPlayer(EntityPlayer p){Projections++;}
 static void RepairMissingActiveDogsForPlayer(World w,EntityPlayer p,string trigger){Repairs++;}
 static bool HasMissingActiveFollowDog(World w,string id)=>Missing;
    public static void TickGhostRepairRetries()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance?.World;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote()) return;

        long now = DateTime.UtcNow.Ticks;
        List<GhostRepairRetry> due = null;
        lock (GhostRepairSync)
            foreach (GhostRepairRetry retry in GhostRepairRetries.Values)
                if (retry != null && retry.NextAttemptUtcTicks <= now)
                {
                    if (due == null) due = new List<GhostRepairRetry>();
                    due.Add(retry);
                }
        if (due == null) return;

        foreach (GhostRepairRetry retry in due)
        {
            EntityPlayer player = world.GetEntity(retry.PlayerEntityId) as EntityPlayer;
            bool ready = false, pending = true;
            try
            {
                string ownerId;
                ready = player != null && TryResolveOwnerId(player, out ownerId);
                if (ready)
                {
                    // The immediate spawn call can precede owner indexing. Do the complete
                    // projection once when the persistent owner is actually resolvable.
                    if (!retry.OwnerProjectionReconciled)
                    {
                        RebirthDogChunkObserverService.RefreshForPlayer(player);
                        ReconcileOwnerProjectionForPlayer(player);
                        retry.OwnerProjectionReconciled = true;
                    }
                    else RepairMissingActiveDogsForPlayer(world, player, "spawn-retry");
                    string resolvedOwner;
                    pending = !TryResolveOwnerId(player, out resolvedOwner) || HasMissingActiveFollowDog(world, resolvedOwner);
                }
            }
            catch (Exception ex)
            {
                if (retry.AttemptsRemaining == 30)
                    Log.Warning("[REBIRTH Dog] reconciliation waiting player=" + retry.PlayerEntityId + " reason=" + ex.Message);
            }
            retry.AttemptsRemaining--;
            bool remove = (ready && !pending) || retry.AttemptsRemaining <= 0;
            if (remove)
            {
                lock (GhostRepairSync) GhostRepairRetries.Remove(retry.PlayerEntityId);
                if (pending) Log.Warning("[REBIRTH Dog] reconciliation still pending player=" + retry.PlayerEntityId +
                    "; use rbdog ownership then rbdog reconcile. No ownership or inventory was discarded.");
            }
            else retry.NextAttemptUtcTicks = now + TimeSpan.TicksPerSecond;
        }
    }


 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static GhostRepairRetry Add(int id,long time,int attempts=30){var r=new GhostRepairRetry{PlayerEntityId=id,NextAttemptUtcTicks=time,AttemptsRemaining=attempts};GhostRepairRetries[id]=r;return r;}
 static void Main(){
  for(int i=0;i<1000;i++)TickGhostRepairRetries();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)TickGhostRepairRetries();Check(GC.GetAllocatedBytesForCurrentThread()==before,"empty allocation");
  var future=Add(1,long.MaxValue);before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)TickGhostRepairRetries();Check(GC.GetAllocatedBytesForCurrentThread()==before&&future.AttemptsRemaining==30,"future allocation/attempts");
  Add(2,0);TickGhostRepairRetries();Check(!GhostRepairRetries.ContainsKey(2)&&Projections==1,"successful recovery");
  Ready=false;var r=Add(3,0,2);TickGhostRepairRetries();Check(r.AttemptsRemaining==1&&r.NextAttemptUtcTicks>DateTime.UtcNow.Ticks,"retry scheduled");r.NextAttemptUtcTicks=0;TickGhostRepairRetries();Check(!GhostRepairRetries.ContainsKey(3),"attempts exhausted");
  Ready=true;Missing=true;r=Add(4,0);TickGhostRepairRetries();Check(r.OwnerProjectionReconciled,"projection marked");r.NextAttemptUtcTicks=0;TickGhostRepairRetries();Check(Repairs==1,"later repair path");
  r.NextAttemptUtcTicks=0;int n=r.AttemptsRemaining;GameManager.Instance.World.Remote=true;TickGhostRepairRetries();Check(r.AttemptsRemaining==n,"remote gate");GameManager.Instance.World.Remote=false;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;TickGhostRepairRetries();Check(r.AttemptsRemaining==n,"server gate");
  Console.WriteLine("PASS actual extracted callback: 10000 empty and 10000 future-only calls allocate zero; success, retry deadline/exhaustion, projection/repair and authority gates. Native collaborators are doubles.");
 }
}
