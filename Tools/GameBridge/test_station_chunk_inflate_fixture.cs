using System;
using System.IO;
using Noemax.GZip;
class StationInflateChecks
{
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static byte[] Encode(byte[] content){using(var stream=new MemoryStream()){stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);var zip=new DeflateOutputStream(stream,3,true);zip.Write(content,0,content.Length);zip.Restart();return stream.ToArray();}}
 static void Main(){
 var content=new byte[10000];for(int i=0;i<content.Length;i++)content[i]=(byte)(i%251);
 byte[] decoded;var payload=Encode(content);
 Check(RebirthStationChunkInflate.TryDecode(payload,content.Length,out decoded)&&decoded.Length==content.Length,"native compression exact bound");for(int i=0;i<content.Length;i++)Check(decoded[i]==content[i],"native payload equality");
 Check(!RebirthStationChunkInflate.TryDecode(payload,content.Length-1,out decoded)&&decoded==null,"expanded limit no partial result");
 Check(!RebirthStationChunkInflate.TryDecode(payload,0,out decoded)&&!RebirthStationChunkInflate.TryDecode(payload,RebirthStationChunkInflate.MaximumDecodedBytes+1,out decoded),"invalid caller limit");
 int acceptedTruncations=0;
 for(int length=9;length<payload.Length;length++){
 var prefix=new byte[length];Buffer.BlockCopy(payload,0,prefix,0,length);
 if(RebirthStationChunkInflate.TryDecode(prefix,content.Length,out decoded))acceptedTruncations++;
 else Check(decoded==null,"failed truncated decode exposes no output");
 }
 Check(acceptedTruncations==0,"all truncated prefixes refused");
 var trailing=new byte[payload.Length+1];Buffer.BlockCopy(payload,0,trailing,0,payload.Length);Check(!RebirthStationChunkInflate.TryDecode(trailing,content.Length,out decoded)&&decoded==null,"trailing compressed bytes refused");
 foreach(int size in new[]{1,4096,70000}){
 var raw=new byte[size];new Random(123+size).NextBytes(raw);var packed=Encode(raw);int sectors=(packed.Length+16+4095)/4096;
 foreach(int index in new[]{0,1023}){
 var region=new byte[(3+sectors)*4096];region[0]=(byte)'7';region[1]=(byte)'r';region[2]=(byte)'g';region[3]=1;
 region[4096+index*4]=3;region[4099+index*4]=(byte)sectors;Buffer.BlockCopy(BitConverter.GetBytes(packed.Length),0,region,12288,4);Buffer.BlockCopy(packed,0,region,12304,packed.Length);
 using(var stream=new MemoryStream(region,false)){
 stream.Position=27;byte[] extracted;
 Check(RebirthStationRegionPayload.MatchesWrittenPayload(stream,index%32,index/32,packed)&&stream.Position==27,"complete native compressed writer payload matches region");
 Check(RebirthStationRegionPayload.TryRead(stream,index%32,index/32,out extracted)&&stream.Position==27&&RebirthStationChunkInflate.TryDecode(extracted,size,out decoded)&&decoded.Length==size,"region extraction to native inflate");
 for(int i=0;i<size;i++)Check(decoded[i]==raw[i],"region pipeline exact bytes");
 }
 Buffer.BlockCopy(BitConverter.GetBytes(packed.Length-1),0,region,12288,4);
 using(var stream=new MemoryStream(region,false)){Check(!RebirthStationRegionPayload.MatchesWrittenPayload(stream,index%32,index/32,packed),"truncated published payload cannot match writer");byte[] extracted;Check(RebirthStationRegionPayload.TryRead(stream,index%32,index/32,out extracted)&&!RebirthStationChunkInflate.TryDecode(extracted,size,out decoded)&&decoded==null,"region bounded payload still needs strict compressed completion");}
 }
 }
 payload[4]=46;Check(!RebirthStationChunkInflate.TryDecode(payload,10000,out decoded),"unsupported chunk version");
 Check(!RebirthStationChunkInflate.TryDecode(new byte[]{116,116,99,0,47,0,0,0},100,out decoded),"missing compressed body");
 Console.WriteLine("PASS actual bounded decoder with native Noemax writer/reader; no chunk parsing or world files");
 }
}