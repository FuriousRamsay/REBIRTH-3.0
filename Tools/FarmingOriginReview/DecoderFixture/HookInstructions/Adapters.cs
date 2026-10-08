using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
public class ItemValue {public TextureFullArray TextureFullArray;}
public struct TextureFullArray {public bool IsDefault;}
public struct BlockValue {}
public class Block {}
public class ItemInventoryData {public ItemValue itemValue;}
public class BlockPlacement {public struct Result {public BlockValue blockValue;}}
public class PlayerActionsLocal {}
public class PlatformUserIdentifierAbs {}
public class BlockChangeInfo {}
public class NetPackage {}
public class NetPackageSetBlock:NetPackage {}
public class NetPackageRebirthSeedPlacementReview:NetPackage {public static object Owner;}
public class ConnectionManager {public int Returns,Queued;public bool NoConnection=true;public void SendToServer(NetPackage package,bool flush){Returns++;if(NoConnection)return;Queued++;}}
public class BlockToolSelection {public bool PlaceBlock(ItemInventoryData a,BlockPlacement.Result b,ItemInventoryData c,Block d,BlockValue e){return true;}public bool ExecuteUseAction(ItemInventoryData a,bool b,PlayerActionsLocal c){return true;}}
public class GameManager {public void SetBlocksRPC(List<BlockChangeInfo> c,PlatformUserIdentifierAbs id){}}
public class Scope:IDisposable {public bool BeforeNativeInvocation(){return true;}public void Complete(){}public void Dispose(){}}
public static class AdvancedFarmingLocalPlantingAdmission {public static Scope Begin(ItemInventoryData d,BlockPlacement.Result r,BlockValue v){return null;}}
public static class SeedPlacementClientScopeReview {public static object ClientOwner;public static bool Match;public static int NominalReturns;public static NetPackageRebirthSeedPlacementReview Last;public static bool ObserveNativeSendReturned(NetPackageRebirthSeedPlacementReview e){if(!object.ReferenceEquals(e,Last))throw new Exception("wrong envelope");NominalReturns++;return true;}public static Scope Begin(ItemInventoryData d,BlockPlacement.Result r,BlockValue v){return null;}public static bool TryConsumeMatchingSend(NetPackageSetBlock p,out NetPackageRebirthSeedPlacementReview e){e=Match?new NetPackageRebirthSeedPlacementReview():null;Last=e;return Match;}}
public static class Program {
 static int checks;static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static void Test(string source,string replacement,Func<IEnumerable<CodeInstruction>,IEnumerable<CodeInstruction>> transpiler,Type declaring){
  var native=declaring.GetMethod(source);var wrapper=typeof(SeedPlacementNativeHooksReview).GetMethod(replacement,BindingFlags.NonPublic|BindingFlags.Static);
  Check(native!=null&&wrapper!=null,"exact MethodInfo");
  var dm=new DynamicMethod("labels",typeof(void),Type.EmptyTypes);var label=dm.GetILGenerator().DefineLabel();
  var call=new CodeInstruction(OpCodes.Callvirt,native);call.labels.Add(label);call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
  var before=new CodeInstruction(OpCodes.Nop);var after=new CodeInstruction(OpCodes.Ret);var result=transpiler(new[]{before,call,after}).ToList();
  Check(result.Count==3&&object.ReferenceEquals(result[0],before)&&object.ReferenceEquals(result[1],call)&&object.ReferenceEquals(result[2],after),"same instructions");
  Check(call.opcode==OpCodes.Call&&Equals(call.operand,wrapper),"one static replacement");Check(call.labels.Count==1&&call.labels[0].Equals(label)&&call.blocks.Count==1&&call.blocks[0].blockType==ExceptionBlockType.BeginExceptionBlock,"labels EH preserved");
  foreach(int count in new[]{0,2}){var input=Enumerable.Range(0,count).Select(i=>new CodeInstruction(OpCodes.Call,native)).ToList();bool refused=false;try{transpiler(input).ToList();}catch(InvalidOperationException){refused=true;}Check(refused,"count rejected");Check(input.All(i=>Equals(i.operand,native)),"refusal no mutation");}
  var unrelated=new CodeInstruction(OpCodes.Call,typeof(string).GetMethod(nameof(string.IsNullOrEmpty)));var single=new CodeInstruction(OpCodes.Call,native);var other=transpiler(new[]{unrelated,single}).ToList();Check(Equals(other[0].operand,unrelated.operand),"unrelated call preserved");
 }
 public static void Main(){Test(nameof(BlockToolSelection.PlaceBlock),"PlaceWithOrigin",SeedPlacementNativeHooksReview.VoxelTranspiler,typeof(BlockToolSelection));Test(nameof(ConnectionManager.SendToServer),"SendWithSeedIntent",SeedPlacementNativeHooksReview.SendTranspiler,typeof(ConnectionManager));var connection=new ConnectionManager();SeedPlacementClientScopeReview.Match=true;SeedPlacementNativeHooksReview.SendWithSeedIntent(connection,new NetPackageSetBlock(),false);Check(connection.Returns==1&&connection.Queued==0&&SeedPlacementClientScopeReview.NominalReturns==1,"null connection nominal return NOT queue");SeedPlacementClientScopeReview.Match=false;SeedPlacementNativeHooksReview.SendWithSeedIntent(connection,new NetPackageSetBlock(),false);Check(connection.Returns==2&&connection.Queued==0&&SeedPlacementClientScopeReview.NominalReturns==1,"generic send no specialized observation");Console.WriteLine("PASS"+checks+" actual hook transpilers + real Harmony CodeInstruction, explicit native type adapters; NO Harmony installation.");}
}

