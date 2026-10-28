$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$journal=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs'))
$policy=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteCursorPolicy.cs'))
$transport=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteTransport.cs'))
$codec=$transport.Substring($transport.IndexOf('internal static class CamperFrame'))
$saved=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperSavedCursor.cs'))
$receipt=$saved.Substring($saved.IndexOf('    private static bool HasPublishedReceipt'))
$receipt='public static class ReceiptCheck {'+$receipt.Substring(0,$receipt.LastIndexOf('}')).Replace('private static bool','public static bool')+'}'
$doubles=@'
using System;
using System.IO;
public static class RebirthCamperRemoteTransport {public const int MaximumFrame=8*1024*1024;public static void AssertFrame(byte[] bytes){if(bytes==null||bytes.Length>MaximumFrame)throw new InvalidDataException();}}
public static class EntityBuffs {public const int Version=2;}
public class BuffValue {public void Read(Reader r,int version){throw new Exception("Fixture contains no active buffs");}}
public class Reader:IDisposable {
 BinaryReader r;public void SetBaseStream(Stream s){r=new BinaryReader(s);}
 public byte ReadByte()=>r.ReadByte();public ushort ReadUInt16()=>r.ReadUInt16();public string ReadString()=>r.ReadString();public float ReadSingle()=>r.ReadSingle();public void Dispose(){r?.Dispose();}
}
public class Pool {public Reader AllocSync(bool ignored)=>new Reader();}
public static class MemoryPools {public static Pool poolBinaryReader=new Pool();}
public static class RemoteProtocolChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Reject(Action call){try{call();}catch(InvalidDataException){return;}catch(EndOfStreamException){return;}throw new Exception("Malformed frame admitted");}
 static byte[] Buffs(Guid op,float marker,bool duplicate=false){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write((byte)2);w.Write((ushort)0);w.Write((ushort)(duplicate?2:1));w.Write("rbCamper_"+op.ToString("N"));w.Write(marker);if(duplicate){w.Write("rbCamper_"+op.ToString("N"));w.Write(marker);}return m.ToArray();}}
 public static void Run(string directory){
  Guid vehicle=Guid.NewGuid(),op=Guid.NewGuid();var journal=new RebirthCamperTransferJournal(directory);
  var record=journal.Prepare(op,vehicle,"remote:native-owner:creation",0,new byte[]{1},new byte[]{2});
  Check(journal.FindPending(vehicle).Operation==op,"Reconnect failed to discover exact receipt");
  Check(journal.Read(op).OwnerCreation=="remote:native-owner:creation","Owner scope was not durable");
  Check(!ReceiptCheck.HasPublishedReceipt(Buffs(op,0),op),"Unapplied receipt0 admitted");
  Check(!ReceiptCheck.HasPublishedReceipt(Buffs(op,2),op),"Partial receipt2 admitted");
  Check(ReceiptCheck.HasPublishedReceipt(Buffs(op,1),op),"Published receipt1 refused");
  Check(!ReceiptCheck.HasPublishedReceipt(Buffs(Guid.NewGuid(),1),op),"Another operation acknowledged debit");
  Check(!ReceiptCheck.HasPublishedReceipt(Buffs(op,1,true),op),"Conflicting duplicate nonce admitted");
  var policy=RebirthCamperRemoteCursorPolicy.Decide(false,2,false,false);
  Check(policy==RebirthCamperRemoteCursorPolicy.Action.Retain,"Partial unknown cursor cancelled or acknowledged");
  Check(RebirthCamperRemoteCursorPolicy.Decide(false,2,false,true)==RebirthCamperRemoteCursorPolicy.Action.PublishReceipt,"Exact callback-after recovery failed");
  Check(RebirthCamperRemoteCursorPolicy.Decide(false,1,false,false)==RebirthCamperRemoteCursorPolicy.Action.Resume,"Legitimately moved receipt1 cursor re-debited");
  journal.Advance(op,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
  Check(journal.FindPending(vehicle).State==RebirthCamperTransferJournal.Phase.Uncertain,"Acknowledged unfinished vehicle no longer recoverable");
  journal.Advance(op,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
  Check(journal.FindPending(vehicle)==null&&journal.Read(op).After[0]==2,"Applied durable replay or availability wrong");
  using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){CamperFrame.Write(w,new byte[]{2,3});m.Position=0;Check(CamperFrame.Read(new BinaryReader(m))[1]==3,"Network frame roundtrip failed");}
  Reject(()=>{using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(-1);m.Position=0;CamperFrame.Read(new BinaryReader(m));}});
  Reject(()=>{using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(RebirthCamperRemoteTransport.MaximumFrame+1);m.Position=0;CamperFrame.Read(new BinaryReader(m));}});
  Reject(()=>{using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(5);w.Write((byte)1);m.Position=0;CamperFrame.Read(new BinaryReader(m));}});
 }
}
'@
$journal=$journal.Replace('using System;','').Replace('using System.IO;','').Replace('using System.Security.Cryptography;','').Replace('SHA256.Create()','System.Security.Cryptography.SHA256.Create()')
Add-Type -TypeDefinition ($doubles+"`n"+$journal+"`n"+$policy+"`n"+$codec+"`n"+$receipt)
$directory=Join-Path $env:TEMP ('remote_protocol_'+[guid]::NewGuid().ToString('N'))
[RemoteProtocolChecks]::Run($directory)
Write-Output ('PASS: durable scoped receipt discovery/replay, exact published1 versus unapplied0/partial2/wrong/duplicate receipts, moved/partial cursor policy, bounded and truncated packet codec. Native buff reader is an explicit adapter; evidence '+$directory)
