using System;using System.Collections.Generic;using System.Linq;
class Program
{
 static int checks;
 static void Check(bool pass,string label){if(!pass)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
 static bool Throws(Action call){try{call();return false;}catch(ArgumentException){return true;}}
 static void Main()
 {
 var request=Guid.NewGuid();var world=Guid.NewGuid();var session=Guid.NewGuid();
 var tiers=new Dictionary<int,int>{{1,7},{2,3}};var clears=new Dictionary<int,int>{{1,4}};
 var biome=new RebirthPurgeObjectiveFrame.Biome("forest",10,6,4,tiers,clears);tiers[1]=99;clears[1]=99;
 Check(biome.EligibleByTier[1]==7&&biome.ClearedByTier[1]==4,"summary owns immutable tier counts");
 var f=new RebirthPurgeObjectiveFrame(request,world,session,1,4,2,true,new[]{biome});var bytes=RebirthPurgeObjectiveFrame.Encode(f);RebirthPurgeObjectiveFrame decoded;
 Check(RebirthPurgeObjectiveFrame.TryDecode(bytes,out decoded)&&decoded.Biomes["forest"].Cleared==4&&decoded.Revision==4,"bounded summary roundtrip preserves committed counts and scope");
 Check(!RebirthPurgeObjectiveFrame.TryDecode(bytes.Concat(new byte[]{0}).ToArray(),out decoded),"trailing bytes refused");
 for(int n=0;n<bytes.Length;n++)Check(!RebirthPurgeObjectiveFrame.TryDecode(bytes.Take(n).ToArray(),out decoded),"truncation refused at "+n);
 Check(Throws(()=>new RebirthPurgeObjectiveFrame.Biome("forest",10,3,4,new Dictionary<int,int>{{1,10}},new Dictionary<int,int>{{1,4}})),"clear count cannot exceed discovery");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame.Biome("forest",10,10,2,new Dictionary<int,int>{{1,9}},new Dictionary<int,int>{{1,2}})),"inconsistent tier denominator refused");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame(request,world,session,2,4,2,false,new[]{biome})),"unknown state cannot carry invented progress");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame(request,world,session,2,4,2,true,new[]{biome,biome})),"duplicate biome refused");
 var client=new RebirthPurgeObjectiveClient(request);
 Check(client.Accept(f,Guid.Empty,Guid.Empty)&&client.Published==null,"early objective reply staged until map scope publishes");
 client.Publish(world,session);Check(client.Published==f,"matching committed map session publishes staged summary");
 Check(!client.Accept(f,world,session),"duplicate sequence is idempotent");
 var configured=new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,true,new[]{biome},90);Check(RebirthPurgeObjectiveFrame.TryDecode(RebirthPurgeObjectiveFrame.Encode(configured),out decoded)&&decoded.TargetPercentage==90,"configured authoritative target roundtrips to client");Check(Throws(()=>new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,true,new[]{biome},0)),"zero objective target refused");Check(Throws(()=>new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,true,new[]{biome},101)),"objective target above 100 refused");var badTarget=RebirthPurgeObjectiveFrame.Encode(configured);badTarget[77]=0;Check(!RebirthPurgeObjectiveFrame.TryDecode(badTarget,out decoded),"forged zero target byte refused");badTarget[77]=101;Check(!RebirthPurgeObjectiveFrame.TryDecode(badTarget,out decoded),"forged oversized target byte refused");
 var unknown=new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,false,new RebirthPurgeObjectiveFrame.Biome[0]);
 Check(client.Accept(unknown,world,session)&&!client.Published.Known,"source invalidation removes prior visible counts");
 Check(!client.Accept(f,world,session),"late old known summary cannot undo invalidation");
 var old=new RebirthPurgeObjectiveFrame(request,world,session,3,4,2,true,new[]{biome});
 Check(!client.Accept(old,world,session),"higher transport sequence cannot regress durable revision or census generation");
 Check(!client.Accept(new RebirthPurgeObjectiveFrame(Guid.NewGuid(),world,session,4,5,3,true,new[]{biome}),world,session),"foreign request refused");
 Check(!client.Accept(new RebirthPurgeObjectiveFrame(request,Guid.NewGuid(),session,4,5,3,true,new[]{biome}),world,session),"foreign world refused");
 Check(!client.Accept(new RebirthPurgeObjectiveFrame(request,world,Guid.NewGuid(),4,5,3,true,new[]{biome}),world,session),"foreign server session refused");
 var fresh=new RebirthPurgeObjectiveClient(Guid.NewGuid());Check(!fresh.Accept(unknown,world,session)&&fresh.Published==null,"resync discards previous nonce and publication");
 Console.WriteLine("RESULT "+checks+" PASS; production summary codec/client; native transport not exercised.");
 }
}