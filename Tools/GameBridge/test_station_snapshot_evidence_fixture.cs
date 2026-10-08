// CODEC
class Check
{
 static void A(bool ok,string why){if(!ok)throw new System.Exception(why);}
 static System.IO.MemoryStream Image(){return new System.IO.MemoryStream(new byte[]{116,116,99,0,47,0,0,0,7,9});}
 static void Main(){
 var world=new object();var chunk=new object();var snapshot=new object();byte[] bytes;
 using(var request=RebirthStationSnapshotEvidence.Watch(world,chunk,1,-2)){
 A(request!=null,"watch");A(RebirthStationSnapshotEvidence.Watch(world,chunk,1,-2)==null,"same chunk exclusive");
 A(RebirthStationSnapshotEvidence.Begin(snapshot,chunk,new object(),1,-2,true)==null,"wrong world");
 A(RebirthStationSnapshotEvidence.Begin(snapshot,new object(),world,1,-2,true)==null,"old or replacement chunk at identical coordinates");
 var frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);var stream=Image();
 RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);
 A(RebirthStationSnapshotEvidence.TryCopy(request,out bytes)&&bytes.Length==10,"successful serialization captured");
 bytes[8]=0;stream.Position=8;stream.WriteByte(0);stream.Position=0;A(RebirthStationSnapshotEvidence.TryCopy(request,out bytes)&&bytes[8]==7,"both directions detached");
 A(RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,false)==null,"skipped save");A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"skipped generation cannot retain witness");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);stream=Image();RebirthStationSnapshotEvidence.Finish(frame,stream,null);A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"swallowed serializer failure without successful marker");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);RebirthStationSnapshotEvidence.Serialized(new object(),stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"wrong chunk marker");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,Image(),null);A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"swapped stream");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,new System.Exception());A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"native exception");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);A(RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"retry capture");RebirthStationSnapshotEvidence.Invalidate(snapshot);A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"pool reset invalidates");
 frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,-2,true);RebirthStationSnapshotEvidence.Serialized(chunk,stream);request.Dispose();RebirthStationSnapshotEvidence.Finish(frame,stream,null);A(!RebirthStationSnapshotEvidence.TryCopy(request,out bytes),"disposed request cannot publish");
 }
 for(int mutation=0;mutation<3;mutation++){
 var tile=new object();using(var request=RebirthStationSnapshotEvidence.Watch(world,chunk,4,5,null,tile,(input,queue)=>input.Length==2&&input[1]==7&&queue.Length==2&&queue[1]==9)){
 var frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,4,5,true);var stream=new System.IO.MemoryStream();stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);
 long p=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(1);stream.WriteByte(7);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,p,true);
 if(mutation!=2){p=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(1);stream.WriteByte(9);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,p,false);}
 if(mutation==1){stream.Position=9;stream.WriteByte(8);}stream.Position=0;
 RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);
 A(RebirthStationSnapshotEvidence.TryCopy(request,out bytes)==(mutation==0),"exact serializer spans required, later rewrite/missing span refused");
 }} A(RebirthStationSnapshotEvidence.Watch(world,chunk,8,8,null,new object(),null,(o,c)=>true)==null,"terminal requires station validator");
 for(int mutation=0;mutation<7;mutation++){
 var tile=new object();using(var request=RebirthStationSnapshotEvidence.Watch(world,chunk,6,7,null,tile,(i,q)=>i[0]==1&&q[0]==2,(o,c)=>mutation!=6&&o[0]==3&&c[0]==4)){
 var frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,6,7,true);var stream=new System.IO.MemoryStream();stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);
 long p=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(1);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,p,true);
 p=RebirthStationSnapshotEvidence.StartStationSpan(tile,stream);stream.WriteByte(2);RebirthStationSnapshotEvidence.CaptureStationSpan(tile,stream,p,false);
 if(mutation!=1){p=RebirthStationSnapshotEvidence.StartTerminalSpan(tile,stream);stream.WriteByte(3);RebirthStationSnapshotEvidence.CaptureTerminalSpan(tile,stream,p,true);if(mutation==3)RebirthStationSnapshotEvidence.CaptureTerminalSpan(tile,stream,p,true);}
 if(mutation!=2){p=RebirthStationSnapshotEvidence.StartTerminalSpan(tile,stream);stream.WriteByte(4);RebirthStationSnapshotEvidence.CaptureTerminalSpan(tile,mutation==5?Image():stream,p,false);}
 if(mutation==4){stream.Position=10;stream.WriteByte(99);}stream.Position=0;
 RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);
 A(RebirthStationSnapshotEvidence.TryCopy(request,out bytes)==(mutation==0),"terminal exact output/receipt spans: missing, duplicate, rewritten, stream or validator failures refused");
 if(mutation==0){RebirthStationSnapshotEvidence.MarkPublished(request,snapshot,request.Generation,"path","digest");RebirthStationSnapshotEvidence.Publication proof;A(RebirthStationSnapshotEvidence.TryGetPublished(request,out proof)&&proof.OutputStart==10&&proof.OutputLength==1&&proof.CompletionStart==11&&proof.CompletionLength==1,"terminal publication retains offsets");}
 }} var holds=new RebirthStationSnapshotEvidence.Request[4];for(int i=0;i<4;i++)holds[i]=RebirthStationSnapshotEvidence.Watch(world,chunk,i,0);A(RebirthStationSnapshotEvidence.Watch(world,chunk,9,0)==null,"bounded requests");foreach(var r in holds)r.Dispose();
 System.Console.WriteLine("PASS actual snapshot evidence lifecycle: generation, detached payload, world/chunk/stream identity, skipped/failed serialization, reset/dispose, bounded watches");
 }
}