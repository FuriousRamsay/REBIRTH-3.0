using System;using System.IO;using System.Collections.Generic;
public enum RebirthVehicleOwnerTransferResult {Pending,Applied,Rejected,Indeterminate}
static class EntityBuffs {public const int Version=3;}
class BuffValue {public void Read(BinaryReader r,int v){r.ReadInt32();}}
// SOURCE
class Test {
static byte[] Bytes(Guid id,float value,bool duplicate=false,bool otherNamespace=false){using(var m=new MemoryStream()){var w=new BinaryWriter(m);w.Write((byte)EntityBuffs.Version);w.Write((ushort)1);w.Write(42);w.Write((ushort)(duplicate?2:1));for(int i=0;i<(duplicate?2:1);i++){w.Write((otherNamespace?"rbMusic_":"rbVehicle_")+id.ToString("N"));w.Write(value);}return m.ToArray();}}
static void Main(){var id=Guid.NewGuid();var missing=Guid.NewGuid();Dictionary<Guid,RebirthVehicleOwnerTransferResult> result;
foreach(var pair in new[]{Tuple.Create(1f,RebirthVehicleOwnerTransferResult.Applied),Tuple.Create(-1f,RebirthVehicleOwnerTransferResult.Rejected),Tuple.Create(2f,RebirthVehicleOwnerTransferResult.Indeterminate),Tuple.Create(0f,RebirthVehicleOwnerTransferResult.Pending)})if(!RebirthVehicleReceiptReader.TryRead(Bytes(id,pair.Item1),new[]{id,missing},out result)||result[id]!=pair.Item2||result[missing]!=RebirthVehicleOwnerTransferResult.Pending)throw new Exception("Receipt state lost");
if(!RebirthVehicleReceiptReader.TryRead(Bytes(id,1,false,true),new[]{id},out result)||result[id]!=RebirthVehicleOwnerTransferResult.Pending)throw new Exception("Other namespace accepted");
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,3f})if(RebirthVehicleReceiptReader.TryRead(Bytes(id,invalid),new[]{id},out result)||result!=null)throw new Exception("Invalid receipt accepted");
if(RebirthVehicleReceiptReader.TryRead(Bytes(id,1,true),new[]{id},out result)||result!=null)throw new Exception("Duplicate native receipt accepted");
var truncated=Bytes(id,1);Array.Resize(ref truncated,truncated.Length-1);if(RebirthVehicleReceiptReader.TryRead(truncated,new[]{id},out result))throw new Exception("Truncated accepted");
var trailing=Bytes(id,1);Array.Resize(ref trailing,trailing.Length+1);if(RebirthVehicleReceiptReader.TryRead(trailing,new[]{id},out result))throw new Exception("Trailing accepted");
var wrongVersion=Bytes(id,1);wrongVersion[0]=99;if(RebirthVehicleReceiptReader.TryRead(wrongVersion,new[]{id},out result))throw new Exception("Unknown version accepted");
if(RebirthVehicleReceiptReader.TryRead(Bytes(id,1),new[]{id,id},out result)||RebirthVehicleReceiptReader.TryRead(Bytes(id,1),new[]{Guid.Empty},out result))throw new Exception("Invalid requested identities accepted");
Console.WriteLine("PASS actual vehicle receipt reader: applied/rejected/uncertain/missing, namespace isolation, duplicates, malformed values, truncation/trailing/version. Native BuffValue/version substituted; player-file loading not executed.");}
}
