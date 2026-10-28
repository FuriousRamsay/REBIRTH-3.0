// Actual immutable original store/WAL, client conductor, native owner reentry,
// saved receipt parser, actual remote private grant/native apply and Seen/Closed
// release plus refusal receiver/retirement. Network couriers below only record
// scheduling; authenticated receiver calls are the explicit transport boundary.
internal static class RebirthHydrationIntentClient {internal static int Calls;internal static bool TrySend(RebirthHydrationProposalClient.Intent intent){Calls++;return intent.Current();}}
internal static class RebirthHydrationDeclineClient {internal static int Calls;internal static bool TrySend(RebirthHydrationProposalClient.Intent intent){Calls++;return intent.Current();}}
internal static class RebirthHydrationPreparedClient {internal static int Calls;internal static bool TrySend(RebirthHydrationProposalClient.Intent intent){Calls++;return intent.Current();}}
internal static class RebirthHydrationAcknowledgmentClient {internal static int Calls;internal static bool TrySend(EntityPlayerLocal player,Guid operation,string digest){Calls++;return RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out _);}}
internal static class RebirthHydrationRemoteClientDispatch {internal static bool TryRequestConfirmation(EntityPlayerLocal player)=>false;}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static ItemStack[] Cells(int count)=>Enumerable.Range(0,count).Select(_=>new ItemStack()).ToArray();
 private static void Fresh(){var world=new World{Remote=true};player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};
  world.Actor=world.Primary=player;GameManager.Instance.World=world;ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=false};RebirthStablePlayerIdentity.Owner="account";GameManager.Instance.PersistentOwner="account";RebirthRemoteResourceOwnerAction.Allowed=true;
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};NativeStore.Suppress=NativeStore.ThrowAfterSave=false;RebirthGearNativePlayerFile.AfterRead=null;
 }
 private static RebirthHydrationProposalClient.Intent Accepted(){Fresh();
  Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out var intent),"client original intent unavailable");
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,player.bag.LockedSlots,4,new Stack(),true,3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(intent.Operation,Guid.Parse(player.world.Guid),"account",player.Creation,player.Died,8,4,player.bag.LockedSlots,plan,out original)&&intent.Receive(original),"authenticated exact original not durably accepted");return intent;
 }
 private static void Reconnect(){var file=NativeStore.File;var buffs=new Dictionary<string,float>(player.Buffs.Values);var world=new World{Remote=true,Guid=original.World.ToString("N")};
  player=new EntityPlayerLocal{world=world,Creation=original.Creation,Died=original.Deaths,bag=new Bag{ItemGrid=new Grid{items=NativeStore.Copy(file.Bag)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=NativeStore.Copy(file.Belt)}}};foreach(var pair in buffs)player.Buffs.Values[pair.Key]=pair.Value;
  world.Actor=world.Primary=player;GameManager.Instance.World=world;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=false};
 }
 public static void Main(){
  RebirthHydrationProposalClient.Intent recovered=null;RebirthHydrationRemoteClientGrant grant=null;var prior=Accepted();var old=player;Reconnect();Need(!prior.Current()&&RebirthHydrationProposalClient.Intent.TryBegin(player,true,9,false,out recovered)&&recovered.Operation==original.Operation&&recovered.Original.Digest==original.Digest,"changed selection replaced retained original on new body");
  int intro=RebirthHydrationIntentClient.Calls;Need(recovered.Advance(1)&&RebirthHydrationIntentClient.Calls==intro+1&&player.bag.ItemGrid.items[3].count==3&&RebirthHydrationOwnerHold.IsHeld(player)&&RebirthHydrationOwnerHold.BlocksInventory(old.bag,true),"same original reconnect did not qualify real saved native Before without debit");
  Need(recovered.Receive(original)&&recovered.Advance(2)&&RebirthHydrationPreparedClient.Calls==1&&NativeWrites.Calls==0,"reconfirmed original failed prepared schedule or wrote item before grant");
  Need(RebirthHydrationClientPreparedRejection.Receive(player.world,player.entityId,original.Operation,original.Digest)&&!RebirthHydrationOwnerHold.IsHeld(player)&&RebirthHydrationClientOriginals.TryRead(player,out var closed)&&closed==null,"authenticated rejection did not durably close real owner/native description");
  Accepted();old=player;Reconnect();Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out recovered)&&recovered.Advance(1)&&recovered.Receive(original)&&RebirthHydrationRemoteClientGrant.Receive(player.world,player.entityId,original.Operation,original.Digest)&&RebirthHydrationRemoteClientGrant.TryGet(player,out grant),"same original authenticated reconnect grant not issued");
  int writes=NativeWrites.Calls;RebirthHydrationOwnerApply.Apply(grant);
  Need(NativeWrites.Calls==writes+1&&player.bag.ItemGrid.items[3].count==2&&RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied),"actual remote grant did not apply/checkpoint exactly one native owner debit");
  RebirthHydrationOwnerApply.Apply(grant);Need(NativeWrites.Calls==writes+1,"duplicate native grant replayed debit");
  Need(RebirthHydrationRemoteClientTerminal.Receive(player.world,player.entityId,original.Operation,original.Digest)&&!RebirthHydrationOwnerHold.IsHeld(player)&&!RebirthHydrationOwnerHold.BlocksInventory(old.bag,true),"actual Seen terminal failed joint native hold release");
  // A released owner may legitimately move inventory before an ACK retry.
  player.bag.ItemGrid.items[3].count=1;NativeStore.Save(player);Reconnect();
  Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out recovered),"historical Seen original not reconstructed");int acks=RebirthHydrationAcknowledgmentClient.Calls;
  Need(recovered.Advance(1)&&RebirthHydrationAcknowledgmentClient.Calls==acks+1&&NativeWrites.Calls==writes+1&&!RebirthHydrationOwnerHold.IsHeld(player),"historical Seen reconnect replayed debit or failed ACK scheduling after legitimate inventory change");
  Need(RebirthHydrationRemoteClientTerminal.ReceiveClosed(player.world,player.entityId,original.Operation,original.Digest)&&RebirthHydrationClientOriginals.TryRead(player,out closed)&&closed==null,"authenticated Closed failed exact immutable original metadata retirement");
  Accepted();player.Buffs.Values[RebirthHydrationRefusedCheckpoint.Marker(original)]=1;NativeStore.Save(player);old=player;Reconnect();
  Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out recovered),"saved refusal original not reconstructed");int decline=RebirthHydrationDeclineClient.Calls;
  Need(recovered.Advance(1)&&RebirthHydrationDeclineClient.Calls==decline+1&&!RebirthHydrationOwnerHold.IsHeld(player)&&player.bag.ItemGrid.items[3].count==3,"saved refusal reconnect acquired grant/custody instead of declining original");
  Need(RebirthHydrationClientPreparedRejection.Receive(player.world,player.entityId,original.Operation,original.Digest)&&!RebirthHydrationOwnerHold.BlocksInventory(old.bag,true)&&RebirthHydrationClientOriginals.TryRead(player,out closed)&&closed==null,"authenticated historical refusal retained old native custody/original metadata");
  Fresh();Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out prior)&&prior.Original==null,"intro seed failed");SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new Peer();
  Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out recovered)&&recovered.Operation!=prior.Operation&&!prior.Current()&&recovered.Current(),"revoked unaccepted intro prevented a new user request");
  Accepted();Reconnect();player.Died++;Need(!RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out _),"new life adopted prior financial original");
  Accepted();Reconnect();player.bag.ItemGrid.items[3].Data=Convert.ToBase64String(new byte[]{99});Need(RebirthHydrationProposalClient.Intent.TryBegin(player,true,3,false,out recovered)&&!recovered.Advance(1)&&player.bag.ItemGrid.items[3].count==3,"changed native rich source was replayed on reconnect");
  Console.WriteLine("PASS actual client original/native Before/grant/apply/Seen/Closed/refusal reentry; native engine/network endpoints NOT RUN");
 }
}
