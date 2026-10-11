using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Linq;using HarmonyLib;using System.Reflection.Emit;
class Program
{
 static string root;static Assembly native,mod;
 static void Main(string[] args)
 {
  root=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
   string name=new AssemblyName(e.Name).Name+".dll";
   foreach(string folder in new[]{root,Path.Combine(root,"../../7DaysToDie_Data/Managed"),Path.Combine(root,"../0_TFP_Harmony")}){
    string path=Path.GetFullPath(Path.Combine(folder,name));if(File.Exists(path))return Assembly.LoadFrom(path);
   }return null;};
  Run();
 }
 static void Run()
 {
  native=Assembly.LoadFrom(Path.GetFullPath(Path.Combine(root,"../../7DaysToDie_Data/Managed/Assembly-CSharp.dll")));
  mod=Assembly.LoadFrom(Path.Combine(root,"RebirthUtils.dll"));
  var explosion=native.GetType("Explosion").GetMethod("AttackBlocks");
  Check("RebirthWorkstationExplosionProtection","Transpile",explosion,"Filter");
  var reader=native.GetType("TileEntityComposite").GetMethods().Single(m=>m.Name=="read"&&m.GetParameters().Length==3);
  Check("RebirthWorldStationMigration","LegacyReader",reader,"ResolveLegacySchema");
  Console.WriteLine("PASS 2 production transpilers against installed native method instructions; no game launch.");
 }
 static List<CodeInstruction> Read(MethodInfo method)
 {
  var ops=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>(ushort)o.Value);
  var bytes=method.GetMethodBody().GetILAsByteArray();var result=new List<CodeInstruction>();
  for(int p=0;p<bytes.Length;){ushort key=bytes[p++];if(key==0xfe)key=(ushort)(0xfe00|bytes[p++]);var op=ops[key];object operand=null;int n=0;
   switch(op.OperandType){
    case OperandType.InlineNone:break;
    case OperandType.InlineMethod:operand=method.Module.ResolveMethod(BitConverter.ToInt32(bytes,p));n=4;break;
    case OperandType.InlineField:operand=method.Module.ResolveField(BitConverter.ToInt32(bytes,p));n=4;break;
    case OperandType.InlineType:operand=method.Module.ResolveType(BitConverter.ToInt32(bytes,p));n=4;break;
    case OperandType.InlineString:operand=method.Module.ResolveString(BitConverter.ToInt32(bytes,p));n=4;break;
    case OperandType.ShortInlineVar:case OperandType.ShortInlineI:case OperandType.ShortInlineBrTarget:operand=(int)bytes[p];n=1;break;
    case OperandType.InlineVar:operand=(int)BitConverter.ToUInt16(bytes,p);n=2;break;
    case OperandType.InlineI:case OperandType.InlineBrTarget:case OperandType.InlineTok:case OperandType.InlineSig:operand=BitConverter.ToInt32(bytes,p);n=4;break;
    case OperandType.ShortInlineR:operand=BitConverter.ToSingle(bytes,p);n=4;break;
    case OperandType.InlineR:operand=BitConverter.ToDouble(bytes,p);n=8;break;
    case OperandType.InlineI8:operand=BitConverter.ToInt64(bytes,p);n=8;break;
    case OperandType.InlineSwitch:n=4+4*BitConverter.ToInt32(bytes,p);break;
    default:throw new Exception("Unexpected operand");}
   result.Add(new CodeInstruction(op,operand));p+=n;
  }return result;
 }
 static void Check(string type,string method,MethodInfo original,string target)
 {
  var instructions=Read(original);
  var t=mod.GetType(type);var m=t.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static);
  var output=((IEnumerable<CodeInstruction>)m.Invoke(null,m.GetParameters().Length==1?new object[]{instructions}:new object[]{instructions,original})).ToList();
  if(output.Count(c=>c.operand is MethodInfo mi&&mi.DeclaringType==t&&mi.Name==target)!=1)throw new Exception("Hook count mismatch "+method);
 }
}