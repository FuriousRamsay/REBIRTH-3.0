// Actual immutable original codec, client store and fsync WAL; native identity,
// World, body/peer transport, gear snapshot and generation are explicit adapters.
internal sealed class IndexAdapter {
 internal RebirthCamperTransferJournal.Record FindPending(Guid group)=>null;
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
 internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid group,string creation)=>new RebirthCamperTransferJournal.Record[0];
}
internal static class RebirthCamperTransferHistoryScope {internal static bool TryGet(RebirthCamperTransferJournal journal,out IndexAdapter index){index=null;return false;}}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static ItemStack[] Cells(int count){return Enumerable.Range(0,count).Select(_=>new ItemStack()).ToArray();}
 private static void Fresh(){var world=new World{Remote=true};player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};
  world.Actor=world.Primary=player;GameManager.Instance.World=world;ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=false};RebirthStablePlayerIdentity.Owner="account";
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&
    RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,player.bag.LockedSlots,4,new Stack(),true,3,out var plan)&&
    RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),"account",player.Creation,player.Died,8,4,player.bag.LockedSlots,plan,out original),"original fixture unavailable");
 }
 public static void Main(){
  Fresh();Need(RebirthHydrationClientOriginals.TryRead(player,out var absent)&&absent==null,"new client namespace not empty");
  Need(RebirthHydrationClientOriginals.Prepare(player,original)&&RebirthHydrationClientOriginals.TryRead(player,out var saved)&&saved.Digest==original.Digest,"actual immutable client original not saved/read");
  Need(RebirthHydrationClientOriginals.Prepare(player,original)&&player.bag.ItemGrid.items[3].count==3,"duplicate description altered native inventory");
  var immutable=original;var priorBody=player;Fresh();player.world.Guid=immutable.World.ToString("N");player.Creation=immutable.Creation;player.Died=immutable.Deaths;original=immutable;
  Need(!ReferenceEquals(priorBody,player)&&RebirthHydrationClientOriginals.TryRead(player,out saved)&&saved.Operation==immutable.Operation&&saved.Digest==immutable.Digest,"new native body/peer lost original description");
  Need(RebirthHydrationClientOriginals.Close(player,immutable)&&RebirthHydrationClientOriginals.TryRead(player,out saved)&&saved==null,"actual terminal description did not retire pending metadata");
  Fresh();Action<string,RebirthCamperTransferJournal.Record,bool> fault=(path,record,published)=>{if(published)throw new IOException("unknown original publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationClientOriginals.Prepare(player,original),"unknown publication reported positive");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(RebirthHydrationClientOriginals.TryRead(player,out saved)&&saved.Digest==original.Digest&&player.bag.ItemGrid.items[3].count==3,"published unknown original lost custody or changed native source");
  Fresh();var captured=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];fault=(path,record,published)=>{if(published)SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new Peer();};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationClientOriginals.Prepare(player,original),"changed native peer inherited publication return");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(!ReferenceEquals(captured,SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0])&&RebirthHydrationClientOriginals.TryRead(player,out saved)&&saved.Digest==original.Digest,"changed peer erased immutable description");
  Fresh();Need(RebirthHydrationClientOriginals.Prepare(player,original),"life original unavailable");player.Died++;
  Need(RebirthHydrationClientOriginals.TryRead(player,out saved)&&saved.Deaths==original.Deaths&&!RebirthHydrationClientOriginals.Close(player,original),"ended life adopted or erased original metadata");
  Fresh();ThreadManager.Main=false;Need(!RebirthHydrationClientOriginals.Prepare(player,original),"worker published client original");
  Fresh();SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0].Disconnected=true;Need(!RebirthHydrationClientOriginals.Prepare(player,original),"disconnected transport published client original");
  Console.WriteLine("PASS actual client original/WAL reentry; native inventory application and host settlement have separate cohorts, native endpoints NOT RUN");
 }
}
