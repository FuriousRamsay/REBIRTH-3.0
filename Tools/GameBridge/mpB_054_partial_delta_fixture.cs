using System;
using System.IO;
using System.Linq;
// AUTHORED_NOT_RUN. Actual full-delta predicate and actual work arithmetic.
// The detached codec below is an explicit fixture grammar, not native schema.
internal sealed class Recipe {}
internal sealed class RecipeQueueItem
{internal Recipe Recipe=new Recipe();internal int Multiplier=1,AmountToRepair;internal object RepairItem;internal float CraftingTimeLeft,OneItemCraftTime=5;}
internal sealed class Image
{internal RecipeQueueItem[] Queue;internal int[] Tools=new[]{0};internal int Output,Completions;}
internal static class RebirthProductiveEarnedCoordinator
{
 // ACTUAL_PARTIAL_WORK_SOURCE
}
internal static class RebirthProductiveEarnedNativeBody
{
 // ACTUAL_PARTIAL_DELTA_SOURCE
 internal static bool TryRead(byte[] bytes,int offset,out Image image,out ulong elapsed)
 {
  image=null;elapsed=0;if(offset!=0)return false;
  using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
  {
   elapsed=reader.ReadUInt64();var value=new Image{Tools=new[]{reader.ReadInt32()},Output=reader.ReadInt32(),Completions=reader.ReadInt32()};
   int count=reader.ReadInt32();if(count<1||count>4)return false;value.Queue=new RecipeQueueItem[count];
   for(int i=0;i<count;i++)value.Queue[i]=new RecipeQueueItem{Recipe=reader.ReadBoolean()?new Recipe():null,Multiplier=reader.ReadInt32(),
    CraftingTimeLeft=reader.ReadSingle(),OneItemCraftTime=reader.ReadSingle(),RepairItem=reader.ReadBoolean()?new object():null,AmountToRepair=reader.ReadInt32()};
   if(stream.Position!=stream.Length)return false;image=value;return true;
  }
 }
 internal static byte[] Encode(Image image,ulong elapsed,out int offset)
 {
  offset=0;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {
   writer.Write(elapsed);writer.Write(image.Tools[0]);writer.Write(image.Output);writer.Write(image.Completions);writer.Write(image.Queue.Length);
   foreach(var row in image.Queue){writer.Write(row.Recipe!=null);writer.Write(row.Multiplier);writer.Write(row.CraftingTimeLeft);writer.Write(row.OneItemCraftTime);writer.Write(row.RepairItem!=null);writer.Write(row.AmountToRepair);}
   writer.Flush();return stream.ToArray();
  }
 }
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  var before=new Image{Queue=new[]{new RecipeQueueItem{CraftingTimeLeft=-2},new RecipeQueueItem{CraftingTimeLeft=20}},Output=7,Completions=11};
  byte[] prior=RebirthProductiveEarnedNativeBody.Encode(before,100,out _);
  var after=new Image{Queue=new[]{new RecipeQueueItem{CraftingTimeLeft=3},new RecipeQueueItem{CraftingTimeLeft=20}},Tools=new[]{2},Output=7,Completions=11};
  Func<byte[]> encode=()=>RebirthProductiveEarnedNativeBody.Encode(after,100,out _);
  Need(RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"exact tool/timer partial delta refused");
  after.Output++;Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"output mutation accepted");after.Output--;
  after.Completions++;Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"completion receipt mutation accepted");after.Completions--;
  after.Queue[1].CraftingTimeLeft--;Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"unrelated queue work accepted");after.Queue[1].CraftingTimeLeft++;
  after.Queue[0].Multiplier++;Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"queue decrement/increment accepted");after.Queue[0].Multiplier--;
  Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,1),"wrong productive work accepted");
  Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,-7,2),"earned child accepted as partial");
  byte[] wrongElapsed=RebirthProductiveEarnedNativeBody.Encode(after,101,out _);
  Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,wrongElapsed,0,0,-2,3,2),"changed original captured elapsed accepted");
  before.Queue[0].RepairItem=new object();prior=RebirthProductiveEarnedNativeBody.Encode(before,100,out _);
  Need(!RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(prior,0,encode(),0,0,-2,3,2),"repair row acquired ordinary partial work");
  Console.WriteLine("PASS extracted full partial native delta/arithmetic with disclosed detached codec");
 }
}
