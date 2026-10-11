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
 Console.WriteLine("RESULT "+checks+" PASS; production map codec/receiver, native transport not executed.");
 }}

