#requires -Version 7.0
param([string]$Root=(Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),[string]$SourceRoot=$Root)
$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/UI/RebirthStationCompletionReceipt.cs')) -replace '(?m)^using System(?:\.Collections\.Generic)?;\r?\n',''
$scope=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs')).Replace('using System;','')
$capture=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/UI/RebirthStationCompletionCapture.cs')).Replace('using System;','')
$delta=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/UI/RebirthStationOutputDelta.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Linq;','')
$terminal=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Survivor/Persistence/RebirthStationTerminalContents.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.IO;','')
$terminal=$terminal.Replace('MemoryPools.poolBinaryReader.AllocSync(true)','new BinaryReader(stream,System.Text.Encoding.UTF8,true)').Replace('reader.SetBaseStream(stream);','')
$expectation=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Survivor/Persistence/RebirthStationTerminalExpectation.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','')
$terminalSnapshot=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/UI/RebirthStationTerminalSnapshot.cs')).Replace('using System;','').Replace('using System.IO;','').Replace('MemoryPools.poolBinaryWriter.AllocSync(true)','new BinaryWriter(stream,System.Text.Encoding.UTF8,true)').Replace('writer.SetBaseStream(stream);','')
$inputSnapshot=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/UI/RebirthStationNativeInputSnapshot.cs')).Replace('using System;','').Replace('using System.IO;','').Replace('MemoryPools.poolBinaryWriter.AllocSync(true)','new BinaryWriter(stream,System.Text.Encoding.UTF8,true)').Replace('writer.SetBaseStream(stream);','')
$integration=[IO.File]::ReadAllText((Join-Path $SourceRoot 'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.cs'))
function Slice([string]$start,[string]$end){
    $i=$integration.IndexOf($start,[StringComparison]::Ordinal)
    $j=if($i -ge 0){$integration.IndexOf($end,$i,[StringComparison]::Ordinal)}else{-1}
    if($i -lt 0 -or $j -le $i){throw "Native receipt method boundary missing: $start"}
    $integration.Substring($i,$j-$i)
}
$receiptMethods='public static class RebirthRecipeOutputIntegration { internal const string ReceiptField="outputs";'+
    (Slice '    internal static bool Predict(' '    // Native cached Update')+
    (Slice '    internal static bool StampReceipt(' '    internal static bool BeginUi(')+'}'
$double=@"
public class TileEntityWorkstation{public RecipeQueueItem[] Queue;public ItemStack[] Output;public System.Collections.Generic.List<CraftCompleteData> CraftCompleteList;public bool FailNotify;public int Notifications;public void SetModified(){Notifications++;if(FailNotify)throw new Exception("notify fault");}}public static class RebirthStationObservationDispatcher{public static bool Verified=true;public static RebirthStationGridAdmission Admission;public static bool TryGetVerifiedQueuedAdmission(TileEntityWorkstation s,RecipeQueueItem q,out RebirthStationGridAdmission a){a=Admission;return Verified;}}public class ItemClass{public bool HasQuality=true;}
public class ItemValue{public int type=7,Quality=4;public ItemClass ItemClass=new ItemClass();public System.Collections.Generic.Dictionary<string,object> Metadata=new System.Collections.Generic.Dictionary<string,object>();public bool TryGetMetadata<T>(string k,out T value){object raw;if(Metadata.TryGetValue(k,out raw)&&raw is T){value=(T)raw;return true;}value=default(T);return false;}public bool HasMetadata(string k){return Metadata.ContainsKey(k);}public void SetMetadata(string k,object v){Metadata[k]=v;}public ItemValue Clone(){return new ItemValue{type=type,Quality=Quality,ItemClass=ItemClass,Metadata=new System.Collections.Generic.Dictionary<string,object>(Metadata)};}}
public class ItemStack{public static ItemStack[] Clone(ItemStack[] a){return a.Select(x=>x.Clone()).ToArray();}public bool IsEmpty(){return count<=0||itemValue.type<=0;}public bool CanStackWith(ItemStack other){return !IsEmpty()&&RebirthStationGridIngredients.IsSameStackSnapshot(this,new ItemStack(other.itemValue,count));}public ItemStack Clone(){return new ItemStack(itemValue.Clone(),count);}public ItemStack(){}public void Write(BinaryWriter w){w.Write(itemValue.type);w.Write(itemValue.Quality);w.Write(count);w.Write(itemValue.Metadata.Count);foreach(var pair in itemValue.Metadata){w.Write(pair.Key);w.Write(pair.Value is int);if(pair.Value is int)w.Write((int)pair.Value);else w.Write((string)pair.Value);}}public ItemStack Read(BinaryReader reader){itemValue=new ItemValue{type=reader.ReadInt32(),Quality=reader.ReadInt32()};count=reader.ReadInt32();int entries=reader.ReadInt32();for(int i=0;i<entries;i++){string key=reader.ReadString();bool integer=reader.ReadBoolean();itemValue.Metadata[key]=integer?(object)reader.ReadInt32():reader.ReadString();}return this;}public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){if(a.count!=b.count||a.itemValue.type!=b.itemValue.type||a.itemValue.Quality!=b.itemValue.Quality||a.itemValue.Metadata.Count!=b.itemValue.Metadata.Count)return false;foreach(var p in a.itemValue.Metadata){object v;if(!b.itemValue.Metadata.TryGetValue(p.Key,out v)||!object.Equals(p.Value,v))return false;}return true;}}public class Recipe{public int itemValueType=7,count=12,craftExpGain=8;public string GetName(){return "recipe";}}
public class RecipeQueueItem{public Recipe Recipe=new Recipe();public int StartingEntityId=42;public int Multiplier=1;public byte Quality=4;}
public class CraftCompleteData{public int CrafterEntityID,CraftExpGain;public ushort RecipeUsedCount;public ItemStack CraftedItemStack;public string RecipeName,ItemScrapped;public CraftCompleteData(int id,ItemStack s,string r,string scrap,int xp,ushort used){CrafterEntityID=id;CraftedItemStack=s;RecipeName=r;ItemScrapped=scrap;CraftExpGain=xp;RecipeUsedCount=used;}}
public class RebirthStationGridAdmission{public bool IsPublicationAttempted=true,Match=true;public string JobId=Guid.NewGuid().ToString("N"),CreationId=Guid.NewGuid().ToString("N"),DefinitionId=new string('a',64);public RebirthStationGridAdmission Clone(){return new RebirthStationGridAdmission{IsPublicationAttempted=IsPublicationAttempted,Match=Match,JobId=JobId,CreationId=CreationId,DefinitionId=DefinitionId};}public bool TryMaterialize(IList<Recipe> definitions,out Recipe recipe,out ItemStack[] before,out ItemStack[] after){recipe=new Recipe{count=definitions[0].count,itemValueType=definitions[0].itemValueType,craftExpGain=definitions[0].craftExpGain};before=after=null;return Match;}public bool MatchesQueuedQuality(Recipe r,int m,int q){return Match&&m==1&&q==4;}}
internal sealed class RebirthStationCompletionOriginalScope
{
 internal readonly TileEntityWorkstation Station;
 internal readonly RebirthStationGridAdmission Admission;
 private readonly RecipeQueueItem active;
 private readonly Recipe recipe;
 private readonly int thread;
 private RebirthStationCompletionOriginalScope(TileEntityWorkstation station,RecipeQueueItem row,RebirthStationGridAdmission admission)
 {Station=station;Admission=admission.Clone();active=row;recipe=row.Recipe;thread=Environment.CurrentManagedThreadId;}
 internal static bool TryCapture(TileEntityWorkstation station,RecipeQueueItem row,out RebirthStationCompletionOriginalScope scope)
 {
  scope=null;
  if(!RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,row,out var admission))return false;
  var candidate=new RebirthStationCompletionOriginalScope(station,row,admission);
  if(!candidate.IsCurrent())return false;
  scope=candidate;return true;
 }
 internal bool IsCurrent()
 {
  return thread==Environment.CurrentManagedThreadId&&Station.Queue!=null&&Station.Queue.Length>0&&
   ReferenceEquals(active,Station.Queue[Station.Queue.Length-1])&&ReferenceEquals(recipe,active.Recipe)&&
   RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(Station,active,out var current)&&current.JobId==Admission.JobId;
 }
}
public static class RebirthNativeItemCodec
{
 public static string Encode(ItemValue value)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(value,1)}));
}
public static class RebirthNativeItemConformanceReader
{
 public static bool TryDecodeStationCompletions(byte[] bytes,out CraftCompleteData[] values)
 {
  values=null;
  try{using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){
   int count=r.ReadInt16();if(count<1)return false;
   var rows=new CraftCompleteData[count];for(int i=0;i<count;i++){if(r.ReadUInt16()!=1)return false;s.Position-=2;rows[i]=new CraftCompleteData();rows[i].Read(r);}
   if(s.Position!=s.Length)return false;values=rows;return true;
  }}catch{return false;}
 }
 public static bool TryDecodeStackArrayV1(string text,int expected,int maximumBytes,int maximumText,out ItemStack[] output)
 {
  output=null;
  try{var bytes=Convert.FromBase64String(text);if(bytes.Length>maximumBytes||text.Length>maximumText)return false;
   using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){
    if(r.ReadByte()!=1||r.ReadByte()!=expected)return false;
    var rows=new ItemStack[expected];for(int i=0;i<expected;i++)rows[i]=new ItemStack().Read(r);
    if(s.Position!=s.Length)return false;output=rows;return true;
   }
  }catch{return false;}
 }
}
public static class RebirthRecipeOutputs
{
 public static IReadOnlyList<ItemStack> Extras=Array.Empty<ItemStack>();
 public static bool HasOutputs(Recipe r){return Extras.Count>0;}
 public static bool TryPlan(Recipe r,int count,out IReadOnlyList<ItemStack> outputs,out string reason){outputs=Extras;reason=null;return true;}
}
public static class RebirthCamperInventorySnapshot
{
 public static int Restores;
 public static byte[] Capture(ItemStack[] values)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
   writer.Write(values.Length);foreach(var value in values)value.Write(writer);writer.Flush();return stream.ToArray();
  }
 }
 public static ItemStack[] Restore(byte[] bytes)
 {
  Restores++;
  using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream)){
   int count=reader.ReadInt32();
   if(count<0||count>65)throw new InvalidDataException();
   var values=new ItemStack[count];for(int i=0;i<count;i++)values[i]=new ItemStack().Read(reader);
   return values;
  }
 }
}
public static class ReceiptBuilderFixture{static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("receipt assertion "+checks);}public static string Run(){var a=new RebirthStationGridAdmission();var q=new RecipeQueueItem();var output=new ItemValue();CraftCompleteData r;
Check(RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r)&&r.CrafterEntityID==42&&r.CraftExpGain==8&&r.RecipeUsedCount==1&&r.CraftedItemStack.count==12&&r.CraftedItemStack.itemValue.HasMetadata(RebirthStationCompletionReceipt.Prefix+"job")&&!output.HasMetadata(RebirthStationCompletionReceipt.Prefix+"job")&&!object.ReferenceEquals(r.CraftedItemStack.itemValue,output));
Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,13,out r)&&r==null);output.type=8;Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));output.type=7;
output.Quality=5;Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));output.ItemClass.HasQuality=false;Check(RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));output.ItemClass.HasQuality=true;output.Quality=4;
a.IsPublicationAttempted=false;Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));a.IsPublicationAttempted=true;a.Match=false;Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));a.Match=true;
q.StartingEntityId=0;Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));q.StartingEntityId=42;output.SetMetadata(RebirthStationCompletionReceipt.Prefix+"version",1);Check(!RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r));
output.Metadata.Clear();RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r);RebirthStationCompletionReceipt.Identity identity;
Check(RebirthStationCompletionReceipt.TryReadIdentity(r,out identity)&&identity.Job==a.JobId&&identity.Count==12&&identity.Experience==8);
r.CraftedItemStack.count=13;Check(!RebirthStationCompletionReceipt.TryReadIdentity(r,out identity)&&identity==null);r.CraftedItemStack.count=12;
r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"job","malformed");Check(!RebirthStationCompletionReceipt.TryReadIdentity(r,out identity));r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"job",a.JobId);
r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"unexpected",1);Check(!RebirthStationCompletionReceipt.TryReadIdentity(r,out identity));r.CraftedItemStack.itemValue.Metadata.Remove(RebirthStationCompletionReceipt.Prefix+"unexpected");
r.CraftExpGain=9;Check(!RebirthStationCompletionReceipt.TryReadIdentity(r,out identity));r.CraftExpGain=8;r.RecipeUsedCount=2;Check(!RebirthStationCompletionReceipt.TryReadIdentity(r,out identity));
r.RecipeUsedCount=1;Check(RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));
r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"creation",Guid.NewGuid().ToString("N"));Check(!RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"creation",a.CreationId);
r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"definition",new string('b',64));Check(!RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"definition",a.DefinitionId);
r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"count",13);r.CraftedItemStack.count=13;Check(!RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));r.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"count",12);r.CraftedItemStack.count=12;
r.CraftedItemStack.itemValue.Quality=5;Check(!RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));r.CraftedItemStack.itemValue.Quality=4;r.RecipeName="other";Check(!RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,r));
r.RecipeName="recipe";output.Metadata.Clear();var station=new TileEntityWorkstation{Queue=new[]{q}};RebirthStationObservationDispatcher.Admission=a;
Check(RebirthStationCompletionCapture.TryBuild(station,42,output,"recipe","",8,12,out r));
Check(!RebirthStationCompletionCapture.TryBuild(station,99,output,"recipe","",8,12,out r));
Check(!RebirthStationCompletionCapture.TryBuild(station,42,output,"recipe","",9,12,out r));
RebirthStationObservationDispatcher.Verified=false;Check(!RebirthStationCompletionCapture.TryBuild(station,42,output,"recipe","",8,12,out r));RebirthStationObservationDispatcher.Verified=true;
station.Queue=new[]{q,new RecipeQueueItem{StartingEntityId=99}};Check(!RebirthStationCompletionCapture.TryBuild(station,42,output,"recipe","",8,12,out r));
station.Queue=new[]{q};var outputBefore=new[]{new ItemStack(new ItemValue{type=0},0)};station.Output=outputBefore;RebirthStationCompletionCapture.OutputObservation observation;Check(RebirthStationCompletionCapture.TryObserveBefore(station,42,output,"recipe","",8,12,out observation));station.Output=new[]{new ItemStack(output.Clone(),12)};station.CraftCompleteList=new System.Collections.Generic.List<CraftCompleteData>{new CraftCompleteData(99,new ItemStack(new ItemValue(),2),"vanilla","",1,1)};var previous=station.CraftCompleteList;var vanilla=previous[0];
station.Output[0].count=11;Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&object.ReferenceEquals(station.CraftCompleteList,previous)&&station.Notifications==0);station.Output[0].count=12;
Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,null)&&object.ReferenceEquals(station.CraftCompleteList,previous)&&station.Notifications==0);
RebirthStationCompletionCapture.OutputObservation unchanged;Check(RebirthStationCompletionCapture.TryObserveBefore(station,42,output,"recipe","",8,12,out unchanged));Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,unchanged)&&object.ReferenceEquals(station.CraftCompleteList,previous)&&station.Notifications==0);
var foreign=new TileEntityWorkstation{Queue=station.Queue,Output=station.Output};Check(!RebirthStationCompletionCapture.TryAppend(foreign,42,output,"recipe","",8,12,observation)&&foreign.CraftCompleteList==null&&foreign.Notifications==0);
station.Queue=new[]{new RecipeQueueItem{Recipe=q.Recipe}};Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.Notifications==0);station.Queue=new[]{q};
var originalRecipe=q.Recipe;q.Recipe=new Recipe();Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.Notifications==0);q.Recipe=originalRecipe;
bool workerAccepted=true;var worker=new System.Threading.Thread(()=>workerAccepted=RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation));worker.Start();worker.Join();Check(!workerAccepted&&station.Notifications==0);
outputBefore[0].count=4;outputBefore[0].itemValue.SetMetadata("callerMutation",1);
Check(RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.CraftCompleteList.Count==2&&object.ReferenceEquals(station.CraftCompleteList[0],vanilla)&&previous.Count==1&&station.Notifications==1);
Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.CraftCompleteList.Count==2&&station.Notifications==1);
var appended=station.CraftCompleteList;station.CraftCompleteList=previous;Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&object.ReferenceEquals(station.CraftCompleteList,previous)&&station.Notifications==1);station.CraftCompleteList=appended;
outputBefore[0]=new ItemStack(new ItemValue{type=0},0);
station.CraftCompleteList=null;station.Output=outputBefore;Check(RebirthStationCompletionCapture.TryObserveBefore(station,42,output,"recipe","",8,12,out observation));station.Output=new[]{new ItemStack(output.Clone(),12)};station.FailNotify=true;Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.CraftCompleteList.Count==1);Check(!RebirthStationCompletionCapture.TryAppend(station,42,output,"recipe","",8,12,observation)&&station.CraftCompleteList.Count==1);
RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r);CraftCompleteData loaded;
using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))r.Write(writer);stream.Position=0;loaded=new CraftCompleteData();using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))loaded.Read(reader);Check(stream.Position==stream.Length);}
Check(RebirthStationCompletionReceipt.TryReadIdentity(loaded,out identity)&&identity.Job==a.JobId&&identity.Creation==a.CreationId&&identity.Definition==a.DefinitionId&&identity.Tier==4&&identity.Count==12&&identity.Experience==8);
Check(RebirthStationCompletionReceipt.MatchesAdmission(a,q.Recipe,loaded)&&loaded.CrafterEntityID==42&&loaded.RecipeUsedCount==1&&loaded.CraftedItemStack.itemValue.Quality==4);
var empty=new ItemStack(new ItemValue{type=0},0);var prior=new ItemStack(output.Clone(),5);
Check(RebirthStationOutputDelta.Matches(new[]{empty,prior},new[]{new ItemStack(output.Clone(),7),new ItemStack(output.Clone(),10)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{empty},new[]{new ItemStack(output.Clone(),11)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{new ItemStack(output.Clone(),12)},new[]{new ItemStack(output.Clone(),12)},r));
var changed=output.Clone();changed.SetMetadata("unrelated",1);Check(!RebirthStationOutputDelta.Matches(new[]{empty},new[]{new ItemStack(changed,12)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{empty,prior},new[]{new ItemStack(output.Clone(),13),new ItemStack(output.Clone(),4)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{empty},new[]{new ItemStack(r.CraftedItemStack.itemValue.Clone(),12)},r));
Check(r.CraftedItemStack.itemValue.HasMetadata(RebirthStationCompletionReceipt.Prefix+"job"));
output.SetMetadata("ordinary",7);RebirthStationCompletionReceipt.TryCreate(a,q,output,12,out r);Check(RebirthStationOutputDelta.Matches(new[]{empty},new[]{new ItemStack(output.Clone(),12)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{empty},new[]{new ItemStack(new ItemValue(),12)},r));
Check(!RebirthStationOutputDelta.Matches(new[]{new ItemStack(output.Clone(),0)},new[]{new ItemStack(output.Clone(),12)},r));
var exactOutput=new[]{new ItemStack(output.Clone(),12)};byte[] outputBytes,completionBytes;
using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){w.Write((byte)1);exactOutput[0].Write(w);}outputBytes=stream.ToArray();}
var vanillaReceipt=new CraftCompleteData(99,new ItemStack(new ItemValue(),2),"vanilla","",1,1);
using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){w.Write((short)2);vanillaReceipt.Write(w);r.Write(w);}completionBytes=stream.ToArray();}
Check(RebirthStationTerminalContents.Matches(outputBytes,completionBytes,a,q.Recipe,r,exactOutput));
var changedOutput=(byte[])outputBytes.Clone();changedOutput[9]++;Check(!RebirthStationTerminalContents.Matches(changedOutput,completionBytes,a,q.Recipe,r,exactOutput));
var trailing=new byte[completionBytes.Length+1];Array.Copy(completionBytes,trailing,completionBytes.Length);Check(!RebirthStationTerminalContents.Matches(outputBytes,trailing,a,q.Recipe,r,exactOutput));
var truncated=new byte[completionBytes.Length-1];Array.Copy(completionBytes,truncated,truncated.Length);Check(!RebirthStationTerminalContents.Matches(outputBytes,truncated,a,q.Recipe,r,exactOutput));
var unsupported=(byte[])completionBytes.Clone();unsupported[2]=2;Check(!RebirthStationTerminalContents.Matches(outputBytes,unsupported,a,q.Recipe,r,exactOutput));
byte[] duplicates;using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){w.Write((short)2);r.Write(w);r.Write(w);}duplicates=stream.ToArray();}Check(!RebirthStationTerminalContents.Matches(outputBytes,duplicates,a,q.Recipe,r,exactOutput));
r.CrafterEntityID=43;Check(!RebirthStationTerminalContents.Matches(outputBytes,completionBytes,a,q.Recipe,r,exactOutput));r.CrafterEntityID=42;
Check(!RebirthStationTerminalContents.Matches(outputBytes,new byte[]{255,255},a,q.Recipe,r,exactOutput));
var outTrailing=new byte[outputBytes.Length+1];Array.Copy(outputBytes,outTrailing,outputBytes.Length);Check(!RebirthStationTerminalContents.Matches(outTrailing,completionBytes,a,q.Recipe,r,exactOutput));
Check(!RebirthStationTerminalContents.Matches(outputBytes,completionBytes,a,q.Recipe,r,new ItemStack[0]));
RebirthStationTerminalExpectation expectation;var definitions=new[]{q.Recipe};Check(RebirthStationTerminalExpectation.TryCreate(a,definitions,r,exactOutput,out expectation)&&expectation.Matches(outputBytes,completionBytes));
var originalJob=a.JobId;var originalCount=q.Recipe.count;a.JobId=Guid.NewGuid().ToString("N");q.Recipe.count=99;r.CraftedItemStack.itemValue.SetMetadata("callerMutation",1);exactOutput[0].count=99;
Check(expectation.Matches(outputBytes,completionBytes));Check(!RebirthStationTerminalExpectation.TryCreate(a,definitions,r,exactOutput,out var invalid)&&invalid==null);
a.JobId=originalJob;q.Recipe.count=originalCount;r.CraftedItemStack.itemValue.Metadata.Remove("callerMutation");exactOutput[0].count=12;
Check(!RebirthStationTerminalExpectation.TryCreate(a,definitions,r,new ItemStack[]{null},out invalid)&&invalid==null);
Check(!expectation.Matches(changedOutput,completionBytes));Check(!expectation.Matches(outputBytes,duplicates));
Check(RebirthStationCompletionReceipt.HasNoJobReceipt(null,a.JobId));Check(!RebirthStationCompletionReceipt.HasNoJobReceipt(new[]{r},a.JobId));
Check(RebirthStationCompletionReceipt.HasNoJobReceipt(new[]{r},Guid.NewGuid().ToString("N")));
var malformedReceipt=new CraftCompleteData(99,new ItemStack(new ItemValue(),2),"vanilla","",1,1);malformedReceipt.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"unexpected",1);
Check(!RebirthStationCompletionReceipt.HasNoJobReceipt(new[]{malformedReceipt},a.JobId));Check(!RebirthStationCompletionReceipt.HasNoJobReceipt(new CraftCompleteData[]{null},a.JobId));
station.Output=exactOutput;station.CraftCompleteList=new List<CraftCompleteData>{r,vanillaReceipt};RebirthStationTerminalSnapshot terminalSnapshot;
Check(RebirthStationTerminalSnapshot.TryCapture(station,out terminalSnapshot)&&terminalSnapshot.Matches(station));
exactOutput[0].count++;Check(!terminalSnapshot.Matches(station));exactOutput[0].count--;exactOutput[0].itemValue.Quality++;Check(!terminalSnapshot.Matches(station));exactOutput[0].itemValue.Quality--;
r.CraftExpGain++;Check(!terminalSnapshot.Matches(station));r.CraftExpGain--;r.CrafterEntityID++;Check(!terminalSnapshot.Matches(station));r.CrafterEntityID--;
r.CraftedItemStack.itemValue.SetMetadata("snapshotMutation",1);Check(!terminalSnapshot.Matches(station));r.CraftedItemStack.itemValue.Metadata.Remove("snapshotMutation");Check(terminalSnapshot.Matches(station));
station.CraftCompleteList.Add(null);Check(!RebirthStationTerminalSnapshot.TryCapture(station,out var invalidSnapshot)&&invalidSnapshot==null);station.CraftCompleteList.RemoveAt(2);
exactOutput[0].itemValue.SetMetadata("oversized",new string('x',300000));Check(!RebirthStationTerminalSnapshot.TryCapture(station,out invalidSnapshot)&&invalidSnapshot==null);exactOutput[0].itemValue.Metadata.Remove("oversized");Check(terminalSnapshot.Matches(station));
var terminalDigest=terminalSnapshot.Digest();Check(RebirthStationTerminalSnapshot.TryCapture(station,out invalidSnapshot)&&invalidSnapshot.Digest()==terminalDigest&&terminalDigest.Length==64);
byte[] observedOutput,observedReceipts;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)station.Output.Length);foreach(var stack in station.Output)stack.Write(writer);writer.Flush();observedOutput=stream.ToArray();}using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((short)station.CraftCompleteList.Count);foreach(var data in station.CraftCompleteList)data.Write(writer);writer.Flush();observedReceipts=stream.ToArray();}Check(terminalSnapshot.MatchesSerializedTerminal(observedOutput,observedReceipts));observedOutput[observedOutput.Length-1]++;Check(!terminalSnapshot.MatchesSerializedTerminal(observedOutput,observedReceipts));observedOutput[observedOutput.Length-1]--;observedReceipts[observedReceipts.Length-1]++;Check(!terminalSnapshot.MatchesSerializedTerminal(observedOutput,observedReceipts));Check(!terminalSnapshot.MatchesSerializedTerminal(null,observedReceipts));
r.CraftExpGain++;Check(RebirthStationTerminalSnapshot.TryCapture(station,out invalidSnapshot)&&invalidSnapshot.Digest()!=terminalDigest&&terminalSnapshot.Digest()==terminalDigest);r.CraftExpGain--;
RebirthStationNativeInputSnapshot inputSnapshot;var nativeInputs=new[]{new ItemStack{itemValue=new ItemValue{type=7},count=3},new ItemStack{itemValue=new ItemValue{type=8},count=9}};Check(RebirthStationNativeInputSnapshot.TryCapture(nativeInputs,out inputSnapshot));byte[] inputWire;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)nativeInputs.Length);foreach(var stack in nativeInputs)stack.Write(writer);writer.Flush();inputWire=stream.ToArray();}Check(inputSnapshot.MatchesSerializedInput(inputWire));nativeInputs[0].count++;Check(inputSnapshot.MatchesSerializedInput(inputWire));inputWire[inputWire.Length-1]++;Check(!inputSnapshot.MatchesSerializedInput(inputWire));Check(!inputSnapshot.MatchesSerializedInput(null)&&!inputSnapshot.MatchesSerializedInput(new byte[0]));Check(!RebirthStationNativeInputSnapshot.TryCapture(new ItemStack[256],out var invalidInput));Check(!RebirthStationNativeInputSnapshot.TryCapture(new ItemStack[]{null},out invalidInput));nativeInputs[0].itemValue.SetMetadata("oversizedInput",new string('x',300000));Check(!RebirthStationNativeInputSnapshot.TryCapture(nativeInputs,out invalidInput));
var legacyAdmission=a.Clone();legacyAdmission.CreationId="legacy-"+new string('b',64);CraftCompleteData legacyReceipt;
Check(RebirthStationCompletionReceipt.TryCreate(legacyAdmission,q,new ItemValue(),12,out legacyReceipt));
Check(RebirthStationCompletionReceipt.TryReadIdentity(legacyReceipt,out identity)&&identity.Creation==legacyAdmission.CreationId);
Check(RebirthStationCompletionReceipt.MatchesAdmission(legacyAdmission,q.Recipe,legacyReceipt));
using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))legacyReceipt.Write(writer);stream.Position=0;var legacyLoaded=new CraftCompleteData();using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))legacyLoaded.Read(reader);Check(stream.Position==stream.Length&&RebirthStationCompletionReceipt.TryReadIdentity(legacyLoaded,out identity)&&identity.Creation==legacyAdmission.CreationId);}
legacyReceipt.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"creation","legacy-"+new string('c',64));Check(!RebirthStationCompletionReceipt.MatchesAdmission(legacyAdmission,q.Recipe,legacyReceipt));
legacyReceipt.CraftedItemStack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"creation","legacy-"+new string('B',64));Check(!RebirthStationCompletionReceipt.TryReadIdentity(legacyReceipt,out identity));
legacyAdmission.CreationId="legacy-"+new string('b',63);Check(!RebirthStationCompletionReceipt.TryCreate(legacyAdmission,q,new ItemValue(),12,out legacyReceipt)&&legacyReceipt==null);
legacyAdmission.CreationId=Guid.NewGuid().ToString("D");Check(!RebirthStationCompletionReceipt.TryCreate(legacyAdmission,q,new ItemValue(),12,out legacyReceipt));
legacyAdmission.CreationId=Guid.Empty.ToString("N");Check(!RebirthStationCompletionReceipt.TryCreate(legacyAdmission,q,new ItemValue(),12,out legacyReceipt));
var extra=new ItemStack(new ItemValue{type=8},2);
RebirthRecipeOutputs.Extras=new[]{extra};
Check(RebirthStationCompletionReceipt.TryCreate(a,q,new ItemValue(),12,out var addedReceipt));
var startCells=new[]{new ItemStack(new ItemValue{type=0},0),new ItemStack(new ItemValue{type=0},0)};
var afterCells=new[]{new ItemStack(new ItemValue(),12),extra.Clone()};
RebirthCamperInventorySnapshot.Restores=0;
Check(RebirthStationOutputDelta.Matches(startCells,afterCells,addedReceipt));
Check(RebirthCamperInventorySnapshot.Restores==1);
RebirthCamperInventorySnapshot.Restores=0;
Check(RebirthStationCompletionReceipt.TryReadIdentity(addedReceipt,out identity)&&RebirthCamperInventorySnapshot.Restores==1);
var field=RebirthStationCompletionReceipt.Prefix+RebirthRecipeOutputIntegration.ReceiptField;
string canonical;
Check(addedReceipt.CraftedItemStack.itemValue.TryGetMetadata(field,out canonical));
addedReceipt.CraftedItemStack.itemValue.SetMetadata(field,canonical+" ");
Check(!RebirthStationOutputDelta.Matches(startCells,afterCells,addedReceipt));
addedReceipt.CraftedItemStack.itemValue.SetMetadata(field,"malformed");
Check(!RebirthStationCompletionReceipt.TryReadIdentity(addedReceipt,out identity));
Check(!RebirthStationOutputDelta.Matches(startCells,afterCells,addedReceipt));
addedReceipt.CraftedItemStack.itemValue.SetMetadata(field,3);
Check(!RebirthStationOutputDelta.Matches(startCells,afterCells,addedReceipt));
addedReceipt.CraftedItemStack.itemValue.Metadata.Remove(field);
Check(RebirthRecipeOutputIntegration.ReadReceipt(addedReceipt,out var emptyExtras)&&ReferenceEquals(emptyExtras,Array.Empty<ItemStack>()));
Check(RebirthStationOutputDelta.Matches(startCells,new[]{afterCells[0],startCells[1]},addedReceipt));
Check(!RebirthStationOutputDelta.Matches(startCells,afterCells,addedReceipt));
return "PASS "+checks+" actual receipt/capture checks plus exact native CraftCompleteData.Write/Read algorithms with item/admission doubles; no native ItemValue/disk/output settlement";}}
"@
# Extract exact native completion record Write/Read algorithms; ItemStack/ItemValue remain explicit doubles.
$native=[IO.File]::ReadAllText((Join-Path $root 'Tools/ThirdTeam/Native/CraftCompleteData.cs'))
$begin=$native.IndexOf('public void Write(');$finish=$native.IndexOf('public void ReadLegacy(',$begin)
if($begin-lt0-or$finish-lt$begin){throw 'Native completion codec boundary missing'}
$methods=$native.Substring($begin,$finish-$begin).Replace('PooledBinaryWriter','BinaryWriter').Replace('PooledBinaryReader','BinaryReader')
# Pool signatures are fixture adapters only; actual installed codec method bodies are retained.
$double=$double.Replace('RecipeUsedCount=used;}}','RecipeUsedCount=used;}public CraftCompleteData(){}'+$methods+'}')
Add-Type -TypeDefinition ('using System;using System.IO;using System.Collections.Generic;using System.Linq;'+$source+$scope+$capture+$delta+$terminal+$expectation+$terminalSnapshot+$inputSnapshot+$receiptMethods+$double)
[ReceiptBuilderFixture]::Run()