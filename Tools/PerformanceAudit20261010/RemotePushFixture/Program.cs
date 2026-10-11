class EntityPlayer { public int entityId=1; }
class EntityPlayerLocal:EntityPlayer {}
class RemoteResourceAvailabilitySnapshot {}
static class RemoteResourceSnapshotCache {
 public static int invalidates, builds;
 public static void InvalidatePlayer(int id){invalidates++;}
 public static RemoteResourceAvailabilitySnapshot Get(EntityPlayer p){builds++;return new();}
}
class Fixture {
 class RequestScope { public EntityPlayer Player; }
 static object Sync=new(); static Dictionary<int,RequestScope> Requests=new(); static int localCalls,sends;
 static void DispatchLocalUi(EntityPlayerLocal p){localCalls++;}
 static void Run(EntityPlayer input) { foreach(var player in new[]{input}) {
            RemoteResourceSnapshotCache.InvalidatePlayer(player.entityId); // Also discovers newly eligible sources.
            EntityPlayerLocal local = player as EntityPlayerLocal;
            RequestScope scope = null;
            if (local == null)
            {
                lock (Sync) Requests.TryGetValue(player.entityId, out scope);
                // Keep invalidation above even without a subscriber; transaction reads
                // rebuild on demand. Do not discover/clone inventories for an unused push.
                if (scope == null || !ReferenceEquals(scope.Player, player)) continue;
            }
            RemoteResourceAvailabilitySnapshot snapshot = RemoteResourceSnapshotCache.Get(player);
            if (local != null) { DispatchLocalUi(local); continue; }
sends++;
 }}
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Main(){
  var remote=new EntityPlayer();Run(remote);
  Check(RemoteResourceSnapshotCache.invalidates==1 && RemoteResourceSnapshotCache.builds==0 && sends==0,"unsubscribed invalidates without rebuilding");
  Requests[1]=new(){Player=new EntityPlayer()};Run(remote);
  Check(RemoteResourceSnapshotCache.invalidates==2 && RemoteResourceSnapshotCache.builds==0 && sends==0,"recycled entity identity cannot trigger unused rebuild");
  Requests[1]=new(){Player=remote};Run(remote);
  Check(RemoteResourceSnapshotCache.invalidates==3 && RemoteResourceSnapshotCache.builds==1 && sends==1,"matching remote subscriber rebuilds and reaches send");
  Requests.Clear();Run(new EntityPlayerLocal());
  Check(RemoteResourceSnapshotCache.invalidates==4 && RemoteResourceSnapshotCache.builds==2 && localCalls==1 && sends==1,"local player preserves rebuild and UI notification without remote send");
  Console.WriteLine("Production dispatch block extracted; cache/network services doubled, not a multiplayer integration test.");
 }
}
