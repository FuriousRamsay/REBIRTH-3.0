// Actual host source lease/reentry, native full receipt parser/owner witness,
// source final-file reads/writes, grant, financial WAL, terminal and cold ACK.
// Native peer/body/PDS loader and physiology schema are explicit adapters.
// PROGRAM_PREFIX
 private static RebirthHydrationRemoteGrant Warm(){Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out var lease),"warm source original unavailable");WitnessFile.Set(original,false);Need(RebirthHydrationRemoteGrant.TryIssue(lease,out var grant),"warm original grant unavailable");return grant;}
 private static void Reconnect(){var old=player;var previous=RebirthMetabolismStateRepository.State;
  var world=new World{Guid=original.World.ToString("N")};player=new EntityPlayer{world=world,Creation=original.Creation,Owner=original.Owner,Died=original.Deaths};world.Actor=player;GameManager.Instance.World=world;
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo{Owner=original.Owner};
  RebirthMetabolismStateRepository.State=new RebirthMetabolismState{SurvivorCreationId=original.Creation,Revision=0,HydrationSlotItem=previous.HydrationSlotItem.Clone(),HydrationTransferReceipt=previous.HydrationTransferReceipt};
  Need(!ReferenceEquals(old,player),"reconnect did not create distinct native body");
 }
 private static void Seen(){WitnessFile.Set(original,true);var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){
  writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)3);writer.Write("rbGear_"+original.Operation.ToString("N"));writer.Write(1f);writer.Write("rbHydration_"+original.Operation.ToString("N")+"_"+original.Digest);writer.Write(1f);writer.Write("rbHydrationSeen_"+original.Operation.ToString("N")+"_"+original.Digest);writer.Write(1f);writer.Flush();}
  RebirthGearNativePlayerFile.File.buffData=stream;
 }
 public static void Main(){
  RebirthHydrationServerSourceHold.Lease lease=null;RebirthHydrationRemoteGrant grant=null;
  var old=Warm();Reconnect();Need(!old.Current()&&RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out lease)&&lease.Reentered&&RebirthHydrationRemoteGrant.TryIssue(lease,out grant)&&grant.Current(),"saved original Before did not reenter exact new native peer/body");
  WitnessFile.Set(original,true);Need(RebirthHydrationTransferJournal.RemoteTerminal.TryPublish(lease,out var terminal)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1,"reentered owner Applied did not settle only original source");
  Seen();RebirthGearNativePlayerFile.File.Bag[3].count=99;Need(RebirthHydrationTransferJournal.TryPersistColdAcknowledgment(player,original.Operation,original.Digest)&&RebirthHydrationServerSourceHold.RetireAcknowledgedHistory(player,original.Operation,original.Digest)&&!grant.Current()&&!RebirthHydrationServerSourceHold.HoldsPlayer(player),"historical native Seen did not retire original after legitimate owner inventory change");
  old=Warm();WitnessFile.Set(original,true);Reconnect();Need(RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out lease)&&RebirthHydrationRemoteGrant.TryIssue(lease,out grant),"already saved native owner result could not reenter");
  Need(RebirthHydrationTransferJournal.RemoteTerminal.TryPublish(lease,out terminal),"already saved native result lacked source terminal");int revision=RebirthMetabolismStateRepository.State.Revision;
  Seen();Action<string,RebirthCamperTransferJournal.Record,bool> fault=(path,record,published)=>{if(path.Contains("HydrationAcknowledgments")&&record.State==RebirthCamperTransferJournal.Phase.Applied&&published)throw new IOException("unknown ACK publication");};
  RebirthCamperTransferJournal.PublicationFinished+=fault;try{Need(!RebirthHydrationTransferJournal.TryPersistColdAcknowledgment(player,original.Operation,original.Digest)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"unknown ACK publication lost original custody");}finally{RebirthCamperTransferJournal.PublicationFinished-=fault;}
  Need(RebirthHydrationTransferJournal.TryPersistColdAcknowledgment(player,original.Operation,original.Digest)&&RebirthHydrationServerSourceHold.RetireAcknowledgedHistory(player,original.Operation,original.Digest)&&RebirthMetabolismStateRepository.State.Revision==revision,"known saved ACK retry repeated original source mutation");
  old=Warm();Reconnect();RebirthGearNativePlayerFile.File.Bag[3].itemValue.Data=Convert.ToBase64String(new byte[]{99});Need(!RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out _)&&!old.Current()&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"wrong rich native saved source inherited grant or lost custody");
  WitnessFile.Set(original,false);Need(RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out lease),"same original could not retry exact saved owner proof");
  var peer=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer;peer.netConnection[0]=new NativeHydrationTransport();Need(!lease.Current(),"same ClientInfo with replaced native channel inherited original permission");
  Need(RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out var renewed)&&!ReferenceEquals(lease,renewed)&&renewed.Current(),"exact saved original did not qualify replacement native channel through reentry");
  old=Warm();Reconnect();player.Died++;Need(!RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out _)&&!old.Current(),"ended life inherited original remote grant");
  old=Warm();Reconnect();RebirthMetabolismStateRepository.AfterRead=()=>GameManager.Instance.World=new World();Need(!RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out _),"source-read World replacement granted reentry");
  Console.WriteLine("PASS actual host native original reentry/source/grant/terminal/ACK; owning setters are unavailable here, native topology NOT RUN");
 }
}
