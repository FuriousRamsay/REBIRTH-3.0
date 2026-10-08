using System;using System.IO;using System.Linq;
public static class PacketTests{
public static int Count;static void C(bool b,string n){if(!b)throw new Exception(n);Count++;}
public static void Run(RebirthGearPreparationRefusal refusal){
foreach(bool ack in new[]{false,true}){
var packet=new NetPackageRebirthGearPreparationRefusal().Setup(7,refusal,ack);using var output=new MemoryStream();packet.write(new PooledBinaryWriter(output));var bytes=output.ToArray();C(packet.GetLength()==bytes.Length+2,"packet declared native header allowance");C(bytes[4]==(ack?1:0),"packet phase written");var read=new NetPackageRebirthGearPreparationRefusal();read.read(new PooledBinaryReader(new MemoryStream(bytes)));using var copy=new MemoryStream();read.write(new PooledBinaryWriter(copy));C(bytes.SequenceEqual(copy.ToArray()),"packet exact roundtrip");C(packet.PackageDirection==NetPackageDirection.ToClient,"packet direction");
for(int i=0;i<bytes.Length;i++){bool bad=false;try{new NetPackageRebirthGearPreparationRefusal().read(new PooledBinaryReader(new MemoryStream(bytes.Take(i).ToArray())));}catch{bad=true;}C(bad,"packet truncation "+i);}
foreach(var fault in new[]{"id","phase","lengthzero","lengthmax","body"}){
var invalid=(byte[])bytes.Clone();switch(fault){case "id":Array.Clear(invalid,0,4);break;case "phase":invalid[4]=2;break;case "lengthzero":invalid[5]=invalid[6]=0;break;case "lengthmax":invalid[5]=invalid[6]=255;break;case "body":invalid[7]=2;break;}bool bad=false;try{new NetPackageRebirthGearPreparationRefusal().read(new PooledBinaryReader(new MemoryStream(invalid)));}catch{bad=true;}C(bad,"packet invalid "+fault);}
}
bool setupBad=false;try{new NetPackageRebirthGearPreparationRefusal().Setup(0,refusal,false);}catch(ArgumentException){setupBad=true;}C(setupBad,"packet invalid setup");
}
}
