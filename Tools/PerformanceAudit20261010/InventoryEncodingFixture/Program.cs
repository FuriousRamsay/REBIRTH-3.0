using System;
using System.IO;
class Program {
 static string Old(MemoryStream stream)=>Convert.ToBase64String(stream.ToArray());
 static string New(MemoryStream stream)=>Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length);
 static void Main(){
 var random=new Random(1841);
 for(int test=0;test<1000;test++){
  int length=test<5?new[]{1,2,3,255,196608}[test]:random.Next(1,196609);
  using var stream=new MemoryStream();
  byte[] data=new byte[length+31];random.NextBytes(data);
  stream.Write(data);stream.SetLength(length);stream.Position=random.Next(length+1);
  string a=Old(stream),b=New(stream);
  if(a!=b)throw new Exception("encoded bytes changed");
  byte[] decoded=Convert.FromBase64String(b);
  if(decoded.Length!=length)throw new Exception("capacity bytes leaked");
  for(int i=0;i<length;i++)if(decoded[i]!=data[i])throw new Exception("data changed");
 }
 using var sample=new MemoryStream();sample.Write(new byte[4096]);
 for(int i=0;i<100;i++){Old(sample);New(sample);}
 long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)Old(sample);
 long oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
 start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)New(sample);
 long newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
 if(oldBytes-newBytes<4096000)throw new Exception("copy allocation remained");
 Console.WriteLine($"PASS:1000 seeded byte-equivalence cases including spare capacity and non-end position; old={oldBytes} new={newBytes} bytes for1000 4KiB encodes. .NET9 isolated encoding only, not native ItemValue or FPS.");
 }
}