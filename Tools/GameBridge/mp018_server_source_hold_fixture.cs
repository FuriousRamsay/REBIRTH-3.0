// AUTHORED_NOT_RUN. Actual remote source custody, purpose journal and real WAL.
// Native peer/authentication, item codec and metabolism repository are adapters.
internal sealed class ItemValue {internal string Data;internal bool IsEmpty()=>string.IsNullOrEmpty(Data);internal object ItemClass=>this;}
internal sealed class ItemStack {internal int count;internal ItemValue itemValue;internal ItemStack(){}internal ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}internal static ItemStack Empty=>new ItemStack();internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue==null?null:new ItemValue{Data=itemValue.Data}};internal bool IsEmpty()=>count==0;}
internal sealed class RebirthMetabolismState {internal string SurvivorCreationId;internal string HydrationGenerationId="";internal int Revision=8;internal ItemStack HydrationSlotItem=new ItemStack();internal RebirthHydrationTransferOriginal HydrationTransferReceipt;internal void Touch(){Revision++;}}
internal static class RebirthMetabolismStateRepository {internal static RebirthMetabolismState State;internal static bool SourceSaved=true;internal static bool TrySaveRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)=>SourceSaved;internal static bool TryGet(EntityPlayer player,out RebirthMetabolismState state){state=State;return state!=null;}}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue item)=>item.Data;internal static bool TryDecode(string data,out ItemValue item){item=new ItemValue{Data=data};return !string.IsNullOrEmpty(data);}}
internal sealed class Definition {internal bool IsDrink=true,HydrationEquippable=true;}
internal static class RebirthConsumableResolver {internal static Definition Value=new Definition();internal static bool TryResolve(ItemValue item,out Definition definition){definition=Value;return definition!=null;}}
internal sealed class Support {internal Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>();}
internal sealed class Character {internal Support Support=new Support();}
internal static class RebirthWorldCharacterService {internal static Character Value=new Character();internal static bool TryGet(EntityPlayer player,out Character character){character=Value;return character!=null;}}
internal static class RebirthSurvivorGearService {internal const string SupportSlotId="support";}
internal static class RebirthBackpackLibraryReservation {internal static bool Held;internal static bool BlocksResourceUse(EntityPlayer player)=>Held;}
internal static class Program {
 private static EntityPlayer player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static Stack[] Cells(int length){var cells=new Stack[length];for(int i=0;i<length;i++)cells[i]=new Stack();return cells;}
 private static void Fresh(){
  var world=new World();player=new EntityPlayer{world=world};world.Actor=player;GameManager.Instance.World=world;ThreadManager.Main=true;
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=player.Creation};RebirthMetabolismStateRepository.SourceSaved=true;RebirthBackpackLibraryReservation.Held=false;RebirthConsumableResolver.Value=new Definition();RebirthWorldCharacterService.Value=new Character();
  var bag=Cells(44);bag[3]=new Stack{Count=3,ItemData=Convert.ToBase64String(new byte[]{7,9,3,25})};
  Need(RebirthHydrationTransferPlan.TryCreate(bag,Cells(20),null,4,new Stack(),true,3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),player.Owner,player.Creation,2,8,4,null,plan,out original),"fixture source original unavailable");
 }
 public static void Main(){
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out var lease)&&lease.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty(),"qualified source hold mutated or refused");
  Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out var retained)&&ReferenceEquals(lease,retained),"same source hold changed identity");
  RebirthMetabolismStateRepository.State.Revision++;Need(lease.Current(),"unrelated timer revision revoked unchanged source");
  SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();Need(!lease.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"replacement peer inherited permission or cleared custody");
  var replacement=new EntityPlayer{world=player.world,Owner=player.Owner};player.world.Actor=replacement;
  Need(RebirthHydrationServerSourceHold.HoldsPlayer(replacement)&&!RebirthHydrationServerSourceHold.Lease.TryAcquire(replacement,original,out _),"replacement creation/body bypassed account source custody");
  var capturedWorld=player.world;player.world=new World();Need(RebirthHydrationServerSourceHold.HoldsWorld(capturedWorld),"mutated actor world erased captured source scope");
  for(int variant=0;variant<10;variant++){
   Fresh();switch(variant){case 0:ThreadManager.Main=false;break;case 1:player.Dead=true;break;case 2:player.Spawned=false;break;case 3:SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;break;case 4:SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer.loginDone=false;break;case 5:SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer.Owner="foreign";break;case 6:RebirthMetabolismStateRepository.State.Revision++;break;case 7:RebirthBackpackLibraryReservation.Held=true;break;case 8:RebirthConsumableResolver.Value.HydrationEquippable=false;break;case 9:RebirthWorldCharacterService.Value.Support.EquippedGearBySlot["support"]="occupied";break;}
   Need(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out _),"unqualified remote source acquired "+variant);
  }
  Fresh();Action<string,RebirthCamperTransferJournal.Record,bool> fault=(path,record,published)=>{if(published)throw new IOException("unknown prepared publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"unknown WAL publication discarded source custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease)&&lease.Current(),"same original could not recover prepared metadata");
  Fresh();RebirthMetabolismStateRepository.SourceSaved=false;Need(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"failed saved source checkpoint released custody or granted lease");
  RebirthMetabolismStateRepository.SourceSaved=true;Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease)&&lease.Current(),"saved source checkpoint could not retry same original");
  // SOURCE_CHECKPOINT_CASES
  Console.WriteLine("PASS actual remote source hold/purpose journal/WAL; native topology, transport, grant and terminal remain NOT RUN");
 }
}
