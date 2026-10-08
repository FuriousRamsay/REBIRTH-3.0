using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using System.Security.Cryptography;using HarmonyLib;
// TOOLS-only pinned transform candidate. Never installed.
internal static class OrdinaryNativeTransformCandidate {
 internal static IEnumerable<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions,MethodBase original){
  const string pin="9D6DD060B6343C7D0F0A11E724C33C8CFAD4E5E802E7D878D99A99A834BBC1DD";
  if(original==null||original.DeclaringType!=typeof(TileEntityWorkstation)||original.Name!="HandleRecipeQueue"||original.Module.ModuleVersionId!=new Guid("1a9a4203-3d95-4c90-b094-8926dec1ee9c"))throw new InvalidOperationException("Unsupported original native method");
  using(var sha=SHA256.Create()){if(BitConverter.ToString(sha.ComputeHash(original.GetMethodBody().GetILAsByteArray())).Replace("-","")!=pin)throw new InvalidOperationException("Unsupported original native IL");}
  var body=instructions.Select(x=>new CodeInstruction(x)).ToList();var insert=typeof(ItemStack).GetMethod("AddToItemStackArray",new[]{typeof(ItemStack[]),typeof(ItemStack),typeof(int)});var receipt=typeof(TileEntityWorkstation).GetMethod("AddCraftComplete",new[]{typeof(int),typeof(ItemValue),typeof(string),typeof(string),typeof(int),typeof(int)});
  var ii=body.Select((x,i)=>new{x,i}).Where(x=>x.x.Calls(insert)).Select(x=>x.i).ToArray();var ri=body.Select((x,i)=>new{x,i}).Where(x=>x.x.Calls(receipt)).Select(x=>x.i).ToArray();if(ii.Length!=1||ri.Length!=1||ii[0]>=ri[0]||body.Any(x=>x.blocks.Count!=0))throw new InvalidOperationException("Unsupported original call boundaries");
  // Insertion already has array,input,max on stack; appended actual receiver increases stack by one temporarily.
  var receiver=new CodeInstruction(OpCodes.Ldarg_0);receiver.labels.AddRange(body[ii[0]].labels);body[ii[0]].labels.Clear();body[ii[0]].opcode=OpCodes.Call;body[ii[0]].operand=typeof(OrdinaryNativeCallsiteCandidate).GetMethod("Insert",BindingFlags.Static|BindingFlags.NonPublic);body[ri[0]].opcode=OpCodes.Call;body[ri[0]].operand=typeof(OrdinaryNativeCallsiteCandidate).GetMethod("Receipt",BindingFlags.Static|BindingFlags.NonPublic);body.Insert(ii[0],receiver);return body;
 }
}
