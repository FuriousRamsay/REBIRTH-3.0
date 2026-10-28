// AUTHORED_NOT_RUN. Native PDS file IO and slot decoder are adapters; receipt
// bytes are decoded by the actual native buff parser with zero active buffs.
internal sealed class NativeFile {internal int deaths;internal MemoryStream buffData;internal ItemStack[] Bag,Belt;}
internal static class RebirthGearNativePlayerFile {internal static NativeFile File;internal static int Reads;internal static Action AfterRead;internal static bool TryRead(RebirthStablePlayerIdentity identity,out NativeFile file){Reads++;file=File;AfterRead?.Invoke();return file!=null;}}
internal static class RebirthGearPlayerDataInventory {internal static ItemStack[] ReadSlots(NativeFile file,bool bag)=>bag?file.Bag:file.Belt;}
internal sealed class RebirthGearInventorySnapshot {
 internal Stack[] Bag,Belt;
 internal static bool TryCapture(ItemStack[] bag,ItemStack[] belt,int owned,out RebirthGearInventorySnapshot snapshot){snapshot=null;if(bag==null||belt==null)return false;
  if(!RebirthGearEncodedSnapshot.TryCopy(bag.Select(cell=>new Stack{Count=cell.count,ItemData=cell.IsEmpty()?"":cell.itemValue.Data}).ToArray(),belt.Select(cell=>new Stack{Count=cell.count,ItemData=cell.IsEmpty()?"":cell.itemValue.Data}).ToArray(),owned,out var b,out var t))return false;
  snapshot=new RebirthGearInventorySnapshot{Bag=b,Belt=t};return true;}
}
internal static class WitnessFile {
 internal static void Set(RebirthHydrationTransferOriginal original,bool applied){
  if(!original.TryGetPlan(out var plan))throw new Exception("fixture original unavailable");
  var bag=Enumerable.Range(0,44).Select(_=>new ItemStack()).ToArray();var belt=Enumerable.Range(0,20).Select(_=>new ItemStack()).ToArray();
  var change=plan.Changes[0];var cell=applied?change.After:change.Before;(change.IsBag?bag:belt)[change.Index]=new ItemStack{count=cell.Count,itemValue=new ItemValue{Data=cell.ItemData}};
  var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){
   writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)2);writer.Write("rbGear_"+original.Operation.ToString("N"));writer.Write(applied?1f:2f);writer.Write("rbHydration_"+original.Operation.ToString("N")+"_"+original.Digest);writer.Write(applied?1f:2f);writer.Flush();}
  RebirthGearNativePlayerFile.File=new NativeFile{deaths=original.Deaths,buffData=stream,Bag=bag,Belt=belt};RebirthGearNativePlayerFile.AfterRead=null;
 }
}
internal static class EntityBuffs {internal const int Version=3;}
internal sealed class BuffValue {internal void Read(PooledReader reader,int version){throw new InvalidDataException("Fixture native buffer adapter supports zero active buffs only");}}
internal sealed class PooledReader:IDisposable {private BinaryReader reader;internal void SetBaseStream(Stream stream){reader=new BinaryReader(stream);}internal byte ReadByte()=>reader.ReadByte();internal ushort ReadUInt16()=>reader.ReadUInt16();internal string ReadString()=>reader.ReadString();internal float ReadSingle()=>reader.ReadSingle();public void Dispose(){reader?.Dispose();}}
internal sealed class Pool {internal PooledReader AllocSync(bool reset)=>new PooledReader();}
internal static class MemoryPools {internal static Pool poolBinaryReader=new Pool();}
// WITNESS_CASES
  Fresh();Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"witness source lease unavailable");
  WitnessFile.Set(original,false);Need(RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease)&&!RebirthHydrationRemoteOwnerWitness.HasApplied(lease),"exact applying Before refused or treated as applied");
  RebirthGearNativePlayerFile.File.Bag[3].count=2;Need(!RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease),"applying postimage minted new Before grant witness");
  WitnessFile.Set(original,true);int reads=RebirthGearNativePlayerFile.Reads;Need(RebirthHydrationRemoteOwnerWitness.HasApplied(lease)&&RebirthGearNativePlayerFile.Reads==reads+1,"remote saved receipts/grids did not use one file observation");
  RebirthGearNativePlayerFile.File.deaths++;Need(!RebirthHydrationRemoteOwnerWitness.HasApplied(lease),"other life native file qualified");
  WitnessFile.Set(original,true);RebirthGearNativePlayerFile.File.Bag[3].itemValue.Data=Convert.ToBase64String(new byte[]{99});Need(!RebirthHydrationRemoteOwnerWitness.HasApplied(lease),"foreign rich item postimage qualified");
  WitnessFile.Set(original,true);RebirthGearNativePlayerFile.AfterRead=()=>SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();Need(!RebirthHydrationRemoteOwnerWitness.HasApplied(lease)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"peer swap during saved file read qualified or released custody");
