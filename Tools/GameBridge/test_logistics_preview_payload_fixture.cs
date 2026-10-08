using System;using System.IO;
class PersistentPlayerData{public PlatformUserIdentifierAbs PrimaryId=new PlatformUserIdentifierAbs();}
class PlatformUserIdentifierAbs{public void ToStream(BinaryWriter b){b.Write(123);}}
enum QuickStackRadialAction:byte{Deposit,DepositOwned,Restock,CollectWorkstationOutputs,PushVehicle,PullVehicle,PushDrone,PullDrone}
class Bag{public Bag Clone(){throw new Exception("unused preview clone");}public void Write(BinaryWriter b){throw new Exception("unused preview payload");}}
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
class NetPackage{public virtual void write(PooledBinaryWriter w){}}
class RebirthSurvivorNetworkCodec{public static void WriteString(BinaryWriter b,string s,int max){b.Write(s);}}
class NetPackageLogisticsPreviewRequest:NetPackage{
private int id;private PlatformUserIdentifierAbs uid;private ulong requestEpoch,requestId;private QuickStackRadialAction action;private string targetId;private Bag clientBag;
// SETUP
// WRITE
}
class Check{static int Main(){foreach(bool hasBag in new[]{false,true})foreach(QuickStackRadialAction a in Enum.GetValues(typeof(QuickStackRadialAction))){var p=new NetPackageLogisticsPreviewRequest().Setup(42,new PersistentPlayerData(),12,34,a,"vehicle-7",hasBag?new Bag():null);using(var m=new MemoryStream()){var w=new PooledBinaryWriter(m);p.write(w);w.Flush();m.Position=0;var r=new BinaryReader(m);if(r.ReadInt32()!=42||r.ReadInt32()!=123||r.ReadUInt64()!=12||r.ReadUInt64()!=34||r.ReadByte()!=(byte)a||r.ReadString()!="vehicle-7"||r.ReadBoolean()||m.Position!=m.Length)throw new Exception("wire header or optional bag flag changed");}}Console.WriteLine("PASS: all preview actions preserve legacy header and false optional-bag flag; no bag clone or payload with populated input");return 0;}}
