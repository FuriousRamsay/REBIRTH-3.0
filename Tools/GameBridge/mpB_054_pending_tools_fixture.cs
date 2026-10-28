using System;
using System.IO;
using System.Linq;
// AUTHORED_NOT_RUN. Actual full-delta predicate and actual work arithmetic.
// The detached codec below is an explicit fixture grammar, not native schema.
internal sealed class Recipe {internal int Id=1;}
internal sealed class RecipeQueueItem
{internal Recipe Recipe=new Recipe();internal int Multiplier=1,AmountToRepair;internal object RepairItem;internal float CraftingTimeLeft,OneItemCraftTime=5;}
internal sealed class Image
{internal RecipeQueueItem[] Queue;internal int[] Tools=new[]{0};internal int Output,Completions,Input,Fuel,LastInput;internal bool Burning;}
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
   elapsed=reader.ReadUInt64();int tools=reader.ReadInt32();if(tools<0||tools>4)return false;var value=new Image{Tools=new int[tools]};for(int i=0;i<tools;i++)value.Tools[i]=reader.ReadInt32();value.Output=reader.ReadInt32();value.Completions=reader.ReadInt32();value.Input=reader.ReadInt32();value.Fuel=reader.ReadInt32();value.LastInput=reader.ReadInt32();value.Burning=reader.ReadBoolean();
   int count=reader.ReadInt32();if(count<1||count>4)return false;value.Queue=new RecipeQueueItem[count];
   for(int i=0;i<count;i++)value.Queue[i]=new RecipeQueueItem{Recipe=reader.ReadBoolean()?new Recipe{Id=reader.ReadInt32()}:null,Multiplier=reader.ReadInt32(),
    CraftingTimeLeft=reader.ReadSingle(),OneItemCraftTime=reader.ReadSingle(),RepairItem=reader.ReadBoolean()?new object():null,AmountToRepair=reader.ReadInt32()};
   if(stream.Position!=stream.Length)return false;image=value;return true;
  }
 }
 internal static byte[] Encode(Image image,ulong elapsed,out int offset)
 {
  offset=0;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {
   writer.Write(elapsed);writer.Write(image.Tools.Length);foreach(int tool in image.Tools)writer.Write(tool);writer.Write(image.Output);writer.Write(image.Completions);writer.Write(image.Input);writer.Write(image.Fuel);writer.Write(image.LastInput);writer.Write(image.Burning);writer.Write(image.Queue.Length);
   foreach(var row in image.Queue){writer.Write(row.Recipe!=null);if(row.Recipe!=null)writer.Write(row.Recipe.Id);writer.Write(row.Multiplier);writer.Write(row.CraftingTimeLeft);writer.Write(row.OneItemCraftTime);writer.Write(row.RepairItem!=null);writer.Write(row.AmountToRepair);}
   writer.Flush();return stream.ToArray();
  }
 }
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static Image Image(int tools)
 =>new Image{Queue=new[]{new RecipeQueueItem{CraftingTimeLeft=20},new RecipeQueueItem{CraftingTimeLeft=-12}},Tools=new[]{tools},Output=7,Completions=11,Input=13,Fuel=17,LastInput=19,Burning=true};
 public static void Main()
 {
  var before=Image(2);var after=Image(9);byte[] prior=RebirthProductiveEarnedNativeBody.Encode(before,100,out _);
  Func<bool> valid=()=>RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(prior,0,RebirthProductiveEarnedNativeBody.Encode(after,101,out _),0,1,-12);
  Need(valid(),"exact tool-only replacement across a later clock refused");
  Need(before.Tools[0]==2&&before.Queue[1].CraftingTimeLeft==-12,"comparison mutated caller-owned before image");
  after.Tools[0]=2;Need(!valid(),"clock-only/no-op delta accepted");after.Tools[0]=9;
  after.Output++;Need(!valid(),"output mutation accepted");after.Output--;
  after.Completions++;Need(!valid(),"receipt mutation accepted");after.Completions--;
  after.Input++;Need(!valid(),"input transfer smuggled into tool replacement");after.Input--;
  after.Fuel++;Need(!valid(),"fuel transfer smuggled into tool replacement");after.Fuel--;
  after.LastInput++;Need(!valid(),"last-input mutation accepted");after.LastInput--;
  after.Burning=false;Need(!valid(),"burning state mutation accepted");after.Burning=true;
  after.Queue[0].CraftingTimeLeft--;Need(!valid(),"other queue row work accepted");after.Queue[0].CraftingTimeLeft++;
  after.Queue[1].CraftingTimeLeft--;Need(!valid(),"pending debt changed");after.Queue[1].CraftingTimeLeft++;
  after.Queue[1].Multiplier++;Need(!valid(),"pending multiplier changed");after.Queue[1].Multiplier--;
  after.Queue[1].Recipe.Id++;Need(!valid(),"pending recipe changed");after.Queue[1].Recipe.Id--;
  after.Tools=new[]{9,10};Need(!valid(),"tool slot layout changed");after.Tools=new[]{9};
  Need(!RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(prior,0,RebirthProductiveEarnedNativeBody.Encode(after,101,out _),0,0,20),"non-tail row accepted as pending native source");
  Need(!RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(prior,0,RebirthProductiveEarnedNativeBody.Encode(after,101,out _),0,1,float.NaN),"nonfinite pending timer accepted");
  before.Queue[1].RepairItem=new object();after.Queue[1].RepairItem=new object();prior=RebirthProductiveEarnedNativeBody.Encode(before,100,out _);Need(!valid(),"repair work entered ordinary tool-replacement domain");
  before=Image(2);after=Image(9);before.Queue[1].OneItemCraftTime=float.PositiveInfinity;after.Queue[1].OneItemCraftTime=float.PositiveInfinity;prior=RebirthProductiveEarnedNativeBody.Encode(before,100,out _);Need(!valid(),"unbounded productive unit accepted");
  Console.WriteLine("PASS extracted pending tool-only native delta; disclosed detached codec, no owner transfer/publication authority");
 }
}