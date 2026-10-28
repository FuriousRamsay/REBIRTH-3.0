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
 var bands=new List<RebirthPurgeObjectiveFrame.DiscoveryBand>{new RebirthPurgeObjectiveFrame.DiscoveryBand(0,10),new RebirthPurgeObjectiveFrame.DiscoveryBand(5,90)};
 var withBands=new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,true,new[]{biome},80,bands);bands.Clear();
 Check(withBands.DiscoveryBands.Count==2,"frame owns detached discovery band list");
 Check(RebirthPurgeObjectiveFrame.TryDecode(RebirthPurgeObjectiveFrame.Encode(withBands),out decoded)&&decoded.DiscoveryBands.Count==2&&decoded.DiscoveryBands[1].Tier==5&&decoded.DiscoveryBands[1].Percent==90&&decoded.TargetPercentage==80,"authoritative discovery bands and target roundtrip");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame.DiscoveryBand(0,0))&&Throws(()=>new RebirthPurgeObjectiveFrame.DiscoveryBand(256,50)),"invalid discovery band bounds refused");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame(request,world,session,2,5,3,true,new[]{biome},75,Enumerable.Range(0,16).Select(n=>new RebirthPurgeObjectiveFrame.DiscoveryBand(n,50)))),"oversized band list refused");
 var legacy=bytes.Where((v,i)=>i!=78).Take(bytes.Length-2).ToArray();Array.Copy(BitConverter.GetBytes(0x504f4232),legacy,4);
 Check(RebirthPurgeObjectiveFrame.TryDecode(legacy,out decoded)&&decoded.DiscoveryBands.Count==0&&decoded.Biomes["forest"].Cleared==4,"legacy POB2 reader retains counts without invented bands");
 var exactBand=new RebirthPurgeObjectiveFrame.DiscoveryBand(1,17,1250);
 var exactFrame=new RebirthPurgeObjectiveFrame(request,world,session,3,5,3,true,new[]{biome},50,new[]{exactBand});
 Check(RebirthPurgeObjectiveFrame.TryDecode(RebirthPurgeObjectiveFrame.Encode(exactFrame),out decoded)&&decoded.DiscoveryBands[0].Numerator==1250,"POB4 preserves exact fractional threshold rather than only its rounded label");
 var bandBytes=RebirthPurgeObjectiveFrame.Encode(withBands).Where((v,i)=>i!=78).ToArray();
 var oldBandBytes=bandBytes.Take(bandBytes.Length-1-4*withBands.DiscoveryBands.Count).Concat(new[]{(byte)withBands.DiscoveryBands.Count}).Concat(withBands.DiscoveryBands.SelectMany(b=>new[]{(byte)b.Tier,(byte)b.Percent})).ToArray();
 Array.Copy(BitConverter.GetBytes(0x504f4233),oldBandBytes,4);
 Check(RebirthPurgeObjectiveFrame.TryDecode(oldBandBytes,out decoded)&&decoded.DiscoveryBands[1].Numerator==6750,"POB3 retains historical integer bands without inventing fractional precision");
 Check(Throws(()=>new RebirthPurgeObjectiveFrame.DiscoveryBand(1,17,7501))&&Throws(()=>new RebirthPurgeObjectiveFrame.DiscoveryBand(1,16,1250)),"inconsistent or oversized threshold numerator is refused");
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
 var tierFrame=new RebirthPurgeObjectiveFrame(request,world,session,4,5,3,true,new[]{biome},75,null,1);
 Check(RebirthPurgeObjectiveFrame.TryDecode(RebirthPurgeObjectiveFrame.Encode(tierFrame),out decoded)&&decoded.ProgressionMode==1,"POB5 transports tier mode with raw tier counts");
 var invalidMode=RebirthPurgeObjectiveFrame.Encode(tierFrame);invalidMode[78]=2;Check(!RebirthPurgeObjectiveFrame.TryDecode(invalidMode,out decoded),"unknown progression mode rejected");
 Check(RebirthPurgeObjectiveFrame.TryDecode(legacy,out decoded)&&decoded.ProgressionMode==0,"old POB2 frames retain count mode");
 var pob4=RebirthPurgeObjectiveFrame.Encode(withBands).Where((v,i)=>i!=78).ToArray();Array.Copy(BitConverter.GetBytes(0x504f4234),pob4,4);
 Check(RebirthPurgeObjectiveFrame.TryDecode(pob4,out decoded)&&decoded.ProgressionMode==0&&decoded.DiscoveryBands.Count==2,"POB4 retains count mode and exact bands");
 Console.WriteLine("RESULT "+checks+" PASS; production summary codec/client; native transport not exercised.");
 }
}
