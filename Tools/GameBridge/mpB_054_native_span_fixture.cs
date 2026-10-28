using System;
using System.IO;
using System.Linq;
// AUTHORED_NOT_RUN. Entire actual returned-span adapter. The station parser
// below is an explicit fixed native-envelope double, not native schema proof.
// ACTUAL_NATIVE_SPAN_SOURCE
internal static class RebirthRemoteResourcePlacedStationNativeReader
{
 internal sealed class Image {internal byte[] Native;}
 internal static int Reads;internal static long Graph=256,Scratch=64;
 internal static bool TryMeasure(byte[] bytes,out long graph,out long scratch){graph=Graph;scratch=Scratch;return bytes.Length==74;}
 internal static bool TryRead(byte[] raw,out Image image)
 {
  Reads++;image=null;if(raw.Length!=74||raw[22]!=50)return false;var native=new byte[64];
  Array.Copy(raw,2,native,0,12);native[12]=raw[22];Array.Copy(raw,31,native,13,19);Array.Copy(raw,50,native,40,24);
  image=new Image{Native=native};return true;
 }
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal const int MaximumBodyBytes=2097088;
 internal static byte[] Encode(RebirthRemoteResourcePlacedStationNativeReader.Image image,ulong elapsed,out int offset){offset=32;return image.Native;}
 internal static bool SameExceptElapsed(byte[] actual,int ao,byte[] expected,int eo)
 {if(actual.Length!=expected.Length||ao!=eo)return false;for(int i=0;i<actual.Length;i++)if((i<ao||i>=ao+8)&&actual[i]!=expected[i])return false;return true;}
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 private static byte[] Persistence(byte[] native)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {
   writer.Write((ushort)19);writer.Write(native,0,12);writer.Write((ulong)123);writer.Write((byte)50);writer.Write((ulong)456);
   writer.Write(native,13,19);writer.Write(native,40,24);writer.Flush();return stream.ToArray();
  }
 }
 public static void Main()
 {
  byte[] native=Enumerable.Range(0,64).Select(value=>(byte)value).ToArray();native[12]=50;byte[] raw=Persistence(native);
  Need(raw.Length==native.Length+10,"persistency/ToServer envelope delta");
  using(var stream=new MemoryStream(raw,false))
  {
   Need(RebirthProductiveEarnedLoadedNativeSpan.MatchesStart(stream,native,raw.Length)&&stream.Position==0,"prefix source changed reader position");
   stream.Position=stream.Length;var destination=new byte[raw.Length];
   Need(RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,destination,native,32,256,64)&&stream.Position==raw.Length&&destination.SequenceEqual(raw),"actual returned complete span not captured");
   stream.Position=stream.Length-1;Need(!RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,destination,native,32,256,64)&&stream.Position==raw.Length-1,"partial native read accepted");
  }
  raw[14]=99;raw[23]=88;
  using(var stream=new MemoryStream(raw,false)){stream.Position=raw.Length;Need(RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,new byte[raw.Length],native,32,256,64),"persisted clocks substituted for native state identity");}
  raw[50]^=1;
  using(var stream=new MemoryStream(raw,false)){stream.Position=raw.Length;Need(!RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,new byte[raw.Length],native,32,256,64),"changed saved native state adopted");}
  raw=Persistence(native);RebirthRemoteResourcePlacedStationNativeReader.Reads=0;RebirthRemoteResourcePlacedStationNativeReader.Graph=257;
  using(var stream=new MemoryStream(raw,false)){stream.Position=raw.Length;Need(!RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,new byte[raw.Length],native,32,256,64)&&RebirthRemoteResourcePlacedStationNativeReader.Reads==0,"unreserved native graph decoded");}
  RebirthRemoteResourcePlacedStationNativeReader.Graph=256;RebirthRemoteResourcePlacedStationNativeReader.Scratch=65;
  using(var stream=new MemoryStream(raw,false)){stream.Position=raw.Length;Need(!RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,0,new byte[raw.Length],native,32,256,64)&&RebirthRemoteResourcePlacedStationNativeReader.Reads==0,"unreserved parser scratch decoded");}
  raw[2]^=1;
  using(var stream=new MemoryStream(raw,false))Need(!RebirthProductiveEarnedLoadedNativeSpan.MatchesStart(stream,native,raw.Length)&&stream.Position==0,"foreign tile position accepted");
  Console.WriteLine("PASS actual native returned-span control with disclosed envelope/parser doubles");
 }
}
