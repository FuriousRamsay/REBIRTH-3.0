using System;using System.IO;using System.Linq;
public static class EntityBuffs {public const int Version=2;}
public class BuffValue {public void Read(Reader r,int version){r.ReadInt32();}}
public class Reader:IDisposable {BinaryReader r;public void SetBaseStream(Stream s){r=new BinaryReader(s,System.Text.Encoding.UTF8,true);}public byte ReadByte()=>r.ReadByte();public char ReadChar()=>r.ReadChar();public ushort ReadUInt16()=>r.ReadUInt16();public int ReadInt32()=>r.ReadInt32();public string ReadString()=>r.ReadString();public float ReadSingle()=>r.ReadSingle();public byte[] ReadBytes(int n)=>r.ReadBytes(n);public void Dispose()=>r?.Dispose();}
public class Pool {public Reader AllocSync(bool b)=>new Reader();}
public static class MemoryPools {public static Pool poolBinaryReader=new Pool();}
public class RebirthStablePlayerIdentity {public string CanonicalId="owner";}
public class RebirthGearTransferState {public string PreparationRequestDigest;public string TransactionId="11111111111111111111111111111111";public bool Valid=true;public bool TryGetPlan(out Plan p){p=new Plan();return Valid;}}
public class RebirthGearInventoryPlan {public class Stack {}public enum ApplicationState {Conflict,Before,Partial,After}public static bool Matches=true;public object GearBefore;public bool MatchesBefore(object bag,object belt,object gear,int b,int t)=>Matches&&b==52&&t==20;public int BagSlotsBefore=52,BagSlotsAfter=65;public bool MatchesAppliedInventory(object bag,object belt)=>Matches;public ApplicationState InspectApplication(Stack[] bag,Stack[] belt,bool applying)=>Matches?ApplicationState.Partial:ApplicationState.Conflict;}public class Plan:RebirthGearInventoryPlan {}
public class ItemStack {}
public enum StreamModeRead {Persistency}
public class PlayerDataFile {public const string EXT="ttp";public const byte cFileVersion=62;public MemoryStream buffData;public void Read(Reader r,uint v,StreamModeRead mode){int n=r.ReadInt32();if(n<0||n>1024*1024)throw new Exception();var b=r.ReadBytes(n);if(b.Length!=n)throw new Exception();buffData=new MemoryStream(b);}}
public static class GameIO {public static string Root;public static string GetPlayerDataDir()=>Root;}
public static class SdFile {public static bool Exists(string p)=>File.Exists(p);public static Stream OpenRead(string p)=>File.OpenRead(p);}
public static class RebirthPlayerDataInventory {public static int BagSlots=65;public static bool Valid=true;public static ItemStack[] ReadSlots(PlayerDataFile d,bool bag)=>Valid?new ItemStack[bag?BagSlots:20]:null;}
public class RebirthGearInventorySnapshot {public RebirthGearInventoryPlan.Stack[] Bag,Belt;public static bool TryCapture(ItemStack[] bag,ItemStack[] belt,int n,out RebirthGearInventorySnapshot s){s=new RebirthGearInventorySnapshot{Bag=bag==null?null:new RebirthGearInventoryPlan.Stack[bag.Length],Belt=belt==null?null:new RebirthGearInventoryPlan.Stack[belt.Length]};return bag!=null&&belt!=null;}}
public class RebirthGearPreparationIntent {public Guid TransactionId=Guid.ParseExact("11111111111111111111111111111111","N");public static bool Image=true;public System.Xml.Linq.XElement Write()=>new("intent");public bool MatchesInventory(RebirthGearInventorySnapshot snapshot)=>Image;}
public static class RebirthGearPreparationMarkerReader {public static bool Absent;public static bool HasNoOriginal(byte[] b)=>Absent;public static bool Valid=true;public static Guid World=Guid.NewGuid();public static bool TryRead(byte[] bytes,out string key,out Guid world,out int owned,out RebirthGearPreparationIntent intent){key="original-marker";world=World;owned=4;intent=new();return Valid;}}
class Program {
 static int checks;static RebirthStablePlayerIdentity owner=new();static RebirthGearTransferState offer=new();static string path;
 static byte[] Receipt(float value=1,string tx=null){using var m=new MemoryStream();using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write((byte)2);w.Write((ushort)0);w.Write((ushort)1);w.Write("rbGear_"+(tx??offer.TransactionId));w.Write(value);}return m.ToArray();}
 static byte[] FileBytes(byte version=62,string header="ttp\0",byte[] blob=null){using var m=new MemoryStream();using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){foreach(char c in header)w.Write(c);w.Write(version);var b=blob??Receipt();w.Write(b.Length);w.Write(b);}return m.ToArray();}
 static bool Has()=>RebirthGearPlayerFileWitness.HasApplied(owner,offer);
 static void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
 static void Main(){
 GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-gear-file-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Root);path=Path.Combine(GameIO.Root,"owner.ttp");
 try{
 Check(RebirthGearReceiptReader.TryReadStage(Receipt(2),offer.TransactionId,out var receiptStage)&&receiptStage==2,"strict original applying stage exposed");
 Check(RebirthGearReceiptReader.TryReadStage(Receipt(tx:"22222222222222222222222222222222"),offer.TransactionId,out receiptStage)&&receiptStage==0,"fully parsed absent original is stage zero");
 Check(!RebirthGearReceiptReader.TryReadStage(Receipt(0),offer.TransactionId,out _)&&!RebirthGearReceiptReader.TryReadStage(Receipt(float.NaN),offer.TransactionId,out _),"present zero and nonfinite original stage refused");
 Check(!RebirthGearReceiptReader.TryReadStage(Receipt(),Guid.NewGuid().ToString(),out _),"noncanonical transaction cannot query native receipt");
 Check(!RebirthGearNativePlayerFile.TryRead(owner,out _),"exact loader missing file refused");
 File.WriteAllBytes(path,FileBytes());var originalWorld=RebirthGearPreparationMarkerReader.World;var intent=new RebirthGearPreparationIntent();
 Check(RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,originalWorld,out var phaseMarker,out var phaseIntent,out var phaseInventory,out var phaseReceipt)&&phaseMarker=="original-marker"&&phaseReceipt==1&&phaseInventory.Bag.Length==65,"same original native file binds marker applied receipt and full grids");
 Check(!RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,Guid.NewGuid(),out _,out _,out _,out _),"foreign world cannot read original phase");
 Check(RebirthGearPreparationPlayerFileWitness.HasOriginal(owner,originalWorld,"original-marker",intent),"original file marker plus inventory accepted");
 Check(!RebirthGearPreparationPlayerFileWitness.HasOriginal(owner,Guid.NewGuid(),"original-marker",intent),"foreign savedworld refused");
 Check(!RebirthGearPreparationPlayerFileWitness.HasOriginal(owner,originalWorld,"replacement-marker",intent),"foreign original marker refused");
 RebirthGearPreparationIntent.Image=false;Check(!RebirthGearPreparationPlayerFileWitness.HasOriginal(owner,originalWorld,"original-marker",intent),"changed saved preimage cannot authorize preparation");
 Check(RebirthGearPreparationPlayerFileWitness.TryRead(owner,originalWorld,out _,out _,out _),"postimage still permits original marker phase reconciliation only");RebirthGearPreparationIntent.Image=true;
 RebirthGearPreparationMarkerReader.Valid=false;Check(!RebirthGearPreparationPlayerFileWitness.TryRead(owner,originalWorld,out _,out _,out _),"malformed marker adapter refuses file witness");RebirthGearPreparationMarkerReader.Valid=true;
 File.Delete(path);
 Check(!Has(),"missing final accepted");File.WriteAllBytes(path+".bak",FileBytes());Check(!Has(),"backup substituted");
 File.WriteAllBytes(path,FileBytes());Check(Has(),"valid same-file evidence refused");Check(!RebirthGearPlayerFileWitness.HasApplying(owner,offer),"applied marker treated as intent");
 owner.CanonicalId="../owner";Check(!Has(),"escaping owner accepted");owner.CanonicalId="owner";
 File.WriteAllBytes(path,FileBytes(version:61));Check(!Has(),"old native version accepted");
 File.WriteAllBytes(path,FileBytes(header:"bad\0"));Check(!Has(),"wrong header accepted");
 var valid=FileBytes();File.WriteAllBytes(path,valid.Concat(new byte[]{0}).ToArray());Check(!Has(),"trailing file bytes accepted");
 File.WriteAllBytes(path,valid.Take(valid.Length-1).ToArray());Check(!Has(),"truncated final accepted");
 File.WriteAllBytes(path,FileBytes(blob:Receipt(2)));Check(!Has(),"applying receipt treated as applied");Check(RebirthGearPlayerFileWitness.HasApplying(owner,offer),"applying receipt refused");Plan.Matches=false;Check(!RebirthGearPlayerFileWitness.HasApplying(owner,offer),"conflicting intent inventory accepted");Plan.Matches=true;
 File.WriteAllBytes(path,FileBytes(blob:Receipt(-1)));Check(!Has(),"rejection treated as applied");Check(!RebirthGearPlayerFileWitness.HasRejected(owner,offer),"expanded capacity treated as untouched rejection");RebirthPlayerDataInventory.BagSlots=52;Check(RebirthGearPlayerFileWitness.HasRejected(owner,offer),"original-input rejection witness refused");Plan.Matches=false;Check(!RebirthGearPlayerFileWitness.HasRejected(owner,offer),"changed inputs accepted as rejection");Plan.Matches=true;RebirthPlayerDataInventory.BagSlots=65;
 using(var duplicateBlob=new MemoryStream()){
 using(var writer=new BinaryWriter(duplicateBlob,System.Text.Encoding.UTF8,true)){writer.Write((byte)2);writer.Write((ushort)0);writer.Write((ushort)2);writer.Write("rbGear_"+offer.TransactionId);writer.Write(1f);writer.Write(("rbGear_"+offer.TransactionId).ToUpperInvariant());writer.Write(-1f);}
 File.WriteAllBytes(path,FileBytes(blob:duplicateBlob.ToArray()));Check(!Has(),"case-insensitive conflicting receipt cannot prove saved applied");}
 File.WriteAllBytes(path,FileBytes(blob:Receipt(tx:"22222222222222222222222222222222")));Check(!Has(),"wrong receipt accepted");
 File.WriteAllBytes(path,FileBytes());offer.PreparationRequestDigest=new string('a',64);RebirthGearPreparationMarkerReader.Absent=true;
 Check(RebirthGearPlayerFileWitness.HasRetired(owner,offer,true),"same final file bound terminal receipt and absence accepted");
 RebirthGearPreparationMarkerReader.Absent=false;Check(!RebirthGearPlayerFileWitness.HasRetired(owner,offer,true),"present marker cannot authorize terminal retirement");RebirthGearPreparationMarkerReader.Absent=true;
 offer.PreparationRequestDigest=null;Check(!RebirthGearPlayerFileWitness.HasRetired(owner,offer,true),"legacy offer cannot claim bound retirement");
 Plan.Matches=false;Check(!Has(),"inventory witness skipped");Plan.Matches=true;
 RebirthPlayerDataInventory.Valid=false;Check(!Has(),"invalid inventory accepted");RebirthPlayerDataInventory.Valid=true;
 offer.Valid=false;Check(!Has(),"invalid offer accepted");offer.Valid=true;
 Check(!RebirthGearPlayerFileWitness.HasApplied(null,offer)&&!RebirthGearPlayerFileWitness.HasApplied(owner,null),"null identity accepted");
 Console.WriteLine("PASS "+checks+" actual final-file witness and receipt reader checks with synthetic files. Native PlayerDataFile/BuffValue/decoder/plan evidence are doubles; no native save validation.");
 }finally{foreach(var p in Directory.GetFiles(GameIO.Root))File.Delete(p);Directory.Delete(GameIO.Root);}
 }
}