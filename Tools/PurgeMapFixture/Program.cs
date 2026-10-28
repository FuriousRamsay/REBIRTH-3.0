using System;using System.Linq;using System.IO;
internal enum RebirthPoiClearanceState { Discovered=0,Cleared=1,ResetPending=2 }
class Program {
 static int checks;static Guid request=Guid.NewGuid(),world=Guid.NewGuid(),session=Guid.NewGuid();
 static RebirthPoiMapRecord R(int n,long epoch=0,RebirthPoiClearanceState state=RebirthPoiClearanceState.Discovered)=>new RebirthPoiMapRecord(new RebirthPoiIdentity("duplicate_name",n*100,40,10,0,20,20,20,"forest"),epoch,state);
 static RebirthPoiMapFrame F(long seq,long prev,bool full,int page,int count,Guid transfer,params RebirthPoiMapRecord[] records)=>new RebirthPoiMapFrame(request,world,session,transfer,seq,prev,full,page,Math.Max(1,(count+7)/8),count,records);
 static void C(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main(){
 object scope=new object();var receiver=new RebirthPoiMapReceiver(scope,request);Guid tx=Guid.NewGuid();
 var all=Enumerable.Range(0,9).Select(n=>R(n)).ToArray();var p0=F(1,0,true,0,9,tx,all.Take(8).ToArray());var p1=F(1,0,true,1,9,tx,all.Skip(8).ToArray());
 C(receiver.Accept(scope,p1)==RebirthPoiMapReceive.Accepted&&receiver.Published.Count==0,"out of order page stages without early publication");
 C(receiver.Accept(scope,p1)==RebirthPoiMapReceive.Duplicate,"duplicate page idempotent");
 C(receiver.Accept(scope,p0)==RebirthPoiMapReceive.Published&&receiver.Published.Count==9,"complete snapshot publishes atomically");
 C(receiver.Published.Count==9,"duplicate prefab names at different positions remain separate");
 C(receiver.Accept(scope,p0)==RebirthPoiMapReceive.Duplicate,"old snapshot replay cannot replace map");
 var cleared=F(2,1,false,0,1,Guid.NewGuid(),R(0,0,RebirthPoiClearanceState.Cleared));
 C(receiver.Accept(scope,cleared)==RebirthPoiMapReceive.Published&&receiver.Published[all[0].Identity.Key].State==RebirthPoiClearanceState.Cleared,"ordered clear delta preserves other records");
 var reset=F(3,2,false,0,1,Guid.NewGuid(),R(0,0,RebirthPoiClearanceState.ResetPending));
 C(receiver.Accept(scope,reset)==RebirthPoiMapReceive.Published&&receiver.Published[all[0].Identity.Key].State==RebirthPoiClearanceState.ResetPending,"committed reset removes cleared presentation");
 var fresh=F(4,3,false,0,1,Guid.NewGuid(),R(0,1));
 C(receiver.Accept(scope,fresh)==RebirthPoiMapReceive.Published,"new reset epoch accepted");
 C(receiver.Accept(scope,F(6,5,false,0,1,Guid.NewGuid(),R(1)))==RebirthPoiMapReceive.Resync&&receiver.Sequence==4,"gap requests resync without publishing");
 C(receiver.Accept(new object(),F(5,4,false,0,1,Guid.NewGuid(),R(1)))==RebirthPoiMapReceive.Refused,"replacement scope refused");
 var foreign=new RebirthPoiMapFrame(Guid.NewGuid(),world,session,Guid.NewGuid(),5,4,false,0,1,1,new[]{R(1)});
 C(receiver.Accept(scope,foreign)==RebirthPoiMapReceive.Refused,"previous join request rejected");
 foreign=new RebirthPoiMapFrame(request,Guid.NewGuid(),session,Guid.NewGuid(),5,4,false,0,1,1,new[]{R(1)});
 C(receiver.Accept(scope,foreign)==RebirthPoiMapReceive.Refused,"foreign world rejected");
 foreign=new RebirthPoiMapFrame(request,world,Guid.NewGuid(),Guid.NewGuid(),5,4,false,0,1,1,new[]{R(1)});
 C(receiver.Accept(scope,foreign)==RebirthPoiMapReceive.Refused,"foreign server session rejected");
 C(receiver.Accept(scope,F(5,4,false,0,1,Guid.NewGuid(),R(0)))==RebirthPoiMapReceive.Refused&&receiver.Sequence==4,"epoch regression refused");
 var resync=F(7,4,true,0,1,Guid.NewGuid(),R(0,1,RebirthPoiClearanceState.Cleared));
 C(receiver.Accept(scope,resync)==RebirthPoiMapReceive.Published&&receiver.Published.Count==1,"fresh full snapshot repairs gap");
 var bytes=RebirthPoiMapFrame.Encode(resync);RebirthPoiMapFrame decoded;
 C(RebirthPoiMapFrame.TryDecode(bytes,out decoded)&&decoded.Records[0].Same(resync.Records[0]),"production wire roundtrip");
 C(!RebirthPoiMapFrame.TryDecode(bytes.Concat(new byte[]{0}).ToArray(),out decoded),"trailing wire data rejected");
 C(!RebirthPoiMapFrame.TryDecode(bytes.Take(bytes.Length-1).ToArray(),out decoded),"truncated wire data rejected");
 bytes[0]^=1;C(!RebirthPoiMapFrame.TryDecode(bytes,out decoded),"wrong protocol magic rejected");
 var empty=F(8,7,true,0,0,Guid.NewGuid());
 C(receiver.Accept(scope,empty)==RebirthPoiMapReceive.Published&&receiver.Published.Count==0,"empty world snapshot removes owned map state");
 // No authenticated transport yet: conflicting sessions during first staged snapshot also refuse.
 var staged=new RebirthPoiMapReceiver(scope,request);C(staged.Accept(scope,p0)==RebirthPoiMapReceive.Accepted,"initial partial snapshot retained");
 foreign=new RebirthPoiMapFrame(request,Guid.NewGuid(),session,tx,1,0,true,1,2,9,new[]{R(8)});
 C(staged.Accept(scope,foreign)==RebirthPoiMapReceive.Refused&&staged.Published.Count==0,"mixed world pages cannot establish session");
 var repeated=Enumerable.Range(0,8).Select(n=>R(0)).ToArray();
 C(new RebirthPoiMapReceiver(scope,request).Accept(scope,F(1,0,true,0,8,Guid.NewGuid(),repeated))==RebirthPoiMapReceive.Refused,"duplicate stable keys reject transaction");
 var longName=new string('\u4e00',256);var large=Enumerable.Range(0,8).Select(n=>new RebirthPoiMapRecord(new RebirthPoiIdentity(longName,n,0,0,0,1,1,1,new string('\u4e00',64)),0,RebirthPoiClearanceState.Discovered)).ToArray();
 C(RebirthPoiMapFrame.Encode(F(1,0,true,0,8,Guid.NewGuid(),large)).Length<RebirthPoiMapFrame.MaxBytes,"bounded worst-case multibyte names fit packet");
 for(int tier=0;tier<=7;tier++){
 var tierRecord=new RebirthPoiMapRecord(R(tier).Identity,0,RebirthPoiClearanceState.Discovered,tier);
 var tierFrame=F(1,0,true,0,1,Guid.NewGuid(),tierRecord);
 C(RebirthPoiMapFrame.TryDecode(RebirthPoiMapFrame.Encode(tierFrame),out decoded)&&decoded.Records[0].Tier==tier,"actual discovered tier roundtrips "+tier);
 }
 var historyRecord=new RebirthPoiMapRecord(R(0).Identity,0,RebirthPoiClearanceState.Cleared,3,"FuriousRamsay","FuriousRamsay\nMira",24000,36000,0,0,false);
 var historyFrame=F(1,0,true,0,1,Guid.NewGuid(),historyRecord);
 C(RebirthPoiMapFrame.TryDecode(RebirthPoiMapFrame.Encode(historyFrame),out decoded)&&decoded.Records[0].Same(historyRecord),"PMP4 retains history, counters and approximation");
 var estimate=new RebirthPoiMapRecord(R(1).Identity,0,RebirthPoiClearanceState.Discovered,1,"FuriousRamsay","",24000,0,3,12,true);
 C(RebirthPoiMapFrame.TryDecode(RebirthPoiMapFrame.Encode(F(2,0,true,0,1,Guid.NewGuid(),estimate)),out decoded)&&decoded.Records[0].Same(estimate),"estimated room/entity counters roundtrip without changing evidence");
 C(!historyRecord.Same(new RebirthPoiMapRecord(historyRecord.Identity,0,historyRecord.State,3,"Other","FuriousRamsay\nMira",24000,36000)),"history change invalidates record equality without changing clearance");
 var previousHistory=LegacyMapFixture.Encode(historyFrame,0x504d5033);
 C(RebirthPoiMapFrame.TryDecode(previousHistory,out decoded)&&decoded.Records[0].Discoverer==historyRecord.Discoverer&&decoded.Records[0].Contributors==historyRecord.Contributors&&decoded.Records[0].DiscoveredAt==historyRecord.DiscoveredAt&&decoded.Records[0].PurgedAt==historyRecord.PurgedAt&&decoded.Records[0].AreasLeft==-1&&decoded.Records[0].EstimatedEntities==-1,"PMP3 retains discovery and purge history without inventing counters");
 foreach(int version in new[]{0x504d5031,0x504d5032}){
 var legacy=LegacyMapFixture.Encode(historyFrame,version);
 C(RebirthPoiMapFrame.TryDecode(legacy,out decoded)&&decoded.Records[0].Discoverer==""&&decoded.Records[0].Contributors==""&&decoded.Records[0].DiscoveredAt==0&&decoded.Records[0].PurgedAt==0&&decoded.Records[0].Tier==(version==0x504d5032?3:-1),"old map frame does not invent history "+version);
 }
 Console.WriteLine("RESULT "+checks+" PASS; production map codec/receiver, native transport not executed.");
 }}

// Previous wire layouts, independent of the current encoder.
static class LegacyMapFixture {
 internal static byte[] Encode(RebirthPoiMapFrame f,int version){using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
 w.Write(version);foreach(var id in new[]{f.Request,f.World,f.Session,f.Transfer})w.Write(id.ToByteArray());w.Write(f.Sequence);w.Write(f.Previous);w.Write(f.Full);w.Write(f.Page);w.Write(f.Pages);w.Write(f.Count);w.Write(f.Records.Length);
 foreach(var record in f.Records){var id=record.Identity;w.Write(id.Prefab);w.Write(id.Biome);w.Write(id.X);w.Write(id.Y);w.Write(id.Z);w.Write(id.Rotation);w.Write(id.SizeX);w.Write(id.SizeY);w.Write(id.SizeZ);w.Write(record.Epoch);w.Write((byte)record.State);if(version>=0x504d5032)w.Write((sbyte)record.Tier);if(version>=0x504d5033){w.Write(record.Discoverer);w.Write(record.Contributors);w.Write(record.DiscoveredAt);w.Write(record.PurgedAt);}}w.Flush();return stream.ToArray();}}
}

