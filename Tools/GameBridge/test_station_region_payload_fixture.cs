using System;
using System.IO;
public static class StationRegionPayloadChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static byte[] Region(){var data=new byte[16384];data[0]=(byte)'7';data[1]=(byte)'r';data[2]=(byte)'g';data[3]=1;data[4096]=3;data[4099]=1;data[12288]=9;data[12304]=(byte)'t';data[12305]=(byte)'t';data[12306]=(byte)'c';data[12308]=47;data[12312]=99;return data;}
 public static void Run(){
 byte[] output;var data=Region();using(var stream=new MemoryStream(data,false)){stream.Position=17;Check(RebirthStationRegionPayload.TryRead(stream,0,0,out output)&&output.Length==9&&output[8]==99&&stream.Position==17&&stream.CanRead,"bounded payload and caller stream retained");Check(!RebirthStationRegionPayload.TryRead(stream,-1,0,out output)&&output==null,"world coordinates not accepted as local indices");Check(!RebirthStationRegionPayload.TryRead(stream,1,0,out output)&&output==null&&stream.Position==17,"absent chunk refuses and restores position");}
 data=Region();var expected=new byte[9];Array.Copy(data,12304,expected,0,9);
 using(var stream=new MemoryStream(data,false)){
 stream.Position=27;
 Check(RebirthStationRegionPayload.MatchesWrittenPayload(stream,0,0,expected)&&stream.Position==27,"exact current writer payload matches");
 var changed=(byte[])expected.Clone();changed[8]^=1;
 Check(!RebirthStationRegionPayload.MatchesWrittenPayload(stream,0,0,changed)&&stream.Position==27,"same-length changed payload refuses");
 var capacity=new byte[512000];Array.Copy(expected,capacity,9);
 Check(!RebirthStationRegionPayload.MatchesWrittenPayload(stream,0,0,capacity),"pooled writer capacity is not current payload");
 Check(!RebirthStationRegionPayload.MatchesWrittenPayload(stream,1,0,expected),"wrong location refuses");
 Check(!RebirthStationRegionPayload.MatchesWrittenPayload(stream,0,0,null),"missing witness refuses");
 }
 foreach(int offset in new[]{0,3,4096,4099,12304,12308}){data=Region();data[offset]=0;using(var stream=new MemoryStream(data,false))Check(!RebirthStationRegionPayload.TryRead(stream,0,0,out output)&&output==null,"invalid header/location/chunk version refused");}
 data=Region();data[12288]=255;data[12289]=255;using(var stream=new MemoryStream(data,false))Check(!RebirthStationRegionPayload.TryRead(stream,0,0,out output)&&output==null,"payload exceeds allocated sector");
 data=Region();using(var stream=new MemoryStream(data,0,data.Length-1,false))Check(!RebirthStationRegionPayload.TryRead(stream,0,0,out output)&&output==null,"truncated region extent refused");
 }
}