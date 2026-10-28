$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Join-Path $PSScriptRoot '../..'
$portable=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperPortableStation.cs')).Replace('using System;','').Replace('using System.IO;','')
$address=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs')).Replace('using System;','')
$doubles=@'
using System;
using System.IO;
using System.Collections.Generic;
public static class ThreadManager {public static bool IsMainThread(){return true;}}
public sealed class ItemValue {
 public Dictionary<string,string> Metadata;
 public ItemValue Clone(){return new ItemValue{Metadata=Metadata==null?null:new Dictionary<string,string>(Metadata)};}
 public bool TryGetMetadata(string key,out string value){value=null;return Metadata!=null&&Metadata.TryGetValue(key,out value);}
 public void SetMetadata(string key,string value){if(Metadata==null)Metadata=new Dictionary<string,string>();Metadata[key]=value;}
}
public sealed class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue item,int n){itemValue=item;count=n;}}
public sealed class RebirthCamperStationState {public Guid InstallationId;public int Stock;public float Timer;public ulong lastTickTime;}
public static class RebirthCamperStationSnapshot {
 public static byte[] Capture(Guid id,RebirthCamperStationState state){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(id.ToByteArray());w.Write(state.Stock);w.Write(state.Timer);w.Write(state.lastTickTime);return s.ToArray();}}
 public static RebirthCamperStationState Restore(Guid id,ItemStack item,byte[] data){using(var s=new MemoryStream(data))using(var r=new BinaryReader(s)){if(new Guid(r.ReadBytes(16))!=id)throw new InvalidDataException();return new RebirthCamperStationState{InstallationId=id,Stock=r.ReadInt32(),Timer=r.ReadSingle(),lastTickTime=r.ReadUInt64()};}}
}
public static class PortableCorrelationChecks {
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidDataException){return;}catch(EndOfStreamException){return;}throw new Exception("Malformed portable frame admitted.");}
 static ItemStack Modified(ItemStack source,Action<byte[]> change){var item=new ItemStack(source.itemValue.Clone(),1);byte[] bytes=Convert.FromBase64String(item.itemValue.Metadata[RebirthCamperPortableStation.MetadataKey]);change(bytes);item.itemValue.SetMetadata(RebirthCamperPortableStation.MetadataKey,Convert.ToBase64String(bytes));return item;}
 public static void Run(){
  var id=Guid.NewGuid();var vehicle=Guid.NewGuid();var state=new RebirthCamperStationState{InstallationId=id,Stock=123456,Timer=9.75f,lastTickTime=777};
  var installed=new ItemStack(new ItemValue(),1);var source=new RebirthCamperStationAddress(vehicle,id);
  var portable=RebirthCamperPortableStation.Capture(installed,state,source);
  Check(installed.itemValue.Metadata==null&&state.lastTickTime==777,"Capture mutated live source.");
  Check(RebirthCamperPortableStation.TryGetSourceAddress(portable,out var actual)&&actual.Equals(source),"Source address correlation lost.");
  Check(RebirthCamperPortableStation.TryRestore(portable,out var restored)&&restored.InstallationId==id&&restored.Stock==123456&&restored.Timer==9.75f&&restored.lastTickTime==0,"RSP2 lost native stock/timer/identity or processed item time.");
  var legacy=RebirthCamperPortableStation.Capture(installed,state);
  Check(!RebirthCamperPortableStation.TryGetSourceAddress(legacy,out actual)&&!actual.IsValid,"RSP1 invented source address.");
  Check(RebirthCamperPortableStation.TryRestore(legacy,out restored)&&restored.Stock==123456&&restored.Timer==9.75f,"RSP1 compatibility broken.");
  Check(!RebirthCamperPortableStation.TryGetSourceAddress(installed,out actual)&&!RebirthCamperPortableStation.TryRestore(installed,out restored),"Ordinary station acquired portable state.");
  Reject(()=>RebirthCamperPortableStation.Capture(installed,state,new RebirthCamperStationAddress(vehicle,Guid.NewGuid())));
  Reject(()=>RebirthCamperPortableStation.Capture(installed,state,default(RebirthCamperStationAddress)));
  Reject(()=>RebirthCamperPortableStation.TryGetSourceAddress(Modified(portable,b=>Array.Clear(b,20,16)),out actual));
  Reject(()=>RebirthCamperPortableStation.TryRestore(Modified(portable,b=>b[0]=0),out restored));
  Reject(()=>RebirthCamperPortableStation.TryGetSourceAddress(Modified(portable,b=>Array.Copy(BitConverter.GetBytes(int.MaxValue),0,b,36,4)),out actual));
  Reject(()=>RebirthCamperPortableStation.TryRestore(Modified(portable,b=>b[40]^=1),out restored));
  byte[] frame=Convert.FromBase64String(portable.itemValue.Metadata[RebirthCamperPortableStation.MetadataKey]);Array.Resize(ref frame,frame.Length+1);
  var trailing=new ItemStack(portable.itemValue.Clone(),1);trailing.itemValue.SetMetadata(RebirthCamperPortableStation.MetadataKey,Convert.ToBase64String(frame));Reject(()=>RebirthCamperPortableStation.TryGetSourceAddress(trailing,out actual));
 }
}
'@
Add-Type -TypeDefinition ($doubles+"`n"+$address+"`n"+$portable)
[PortableCorrelationChecks]::Run()
Write-Output 'PASS: actual RSP2 source address/frame/native snapshot roundtrip; RSP1 restoration without invented correlation; exact install identity; clock pause; source isolation and malformed address/frame/identity refusal. Native snapshot is an explicit adapter; correlation grants no authority.'
