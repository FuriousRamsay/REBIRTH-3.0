// Opt-in actual-native ABI fixture, separate assembly/source only. Parent must
// explicitly compile/load/call after all coding; no production route registration.
// Requires initialized real game ItemClass/Block definitions and game main thread.
// Operates exclusively on caller-provided detached item clones, no real save writes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

public static class RebirthEnergyNativeRoundtripFixture
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Field(object target,string name)=>target.GetType().GetField(name,Fields).GetValue(target);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Fields).SetValue(target,value);
    private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty);}
    private static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
    public static Dictionary<string,object> Run(ItemStack installed,ItemStack[] components,ushort fuel)
    {
        Require(ThreadManager.IsMainThread(),"Actual initialized game main thread required.");
        Require(installed!=null&&installed.count==1&&components!=null&&components.Length==6,"Actual detached native bank plus six source slots required.");
        string originalInstalled=RebirthNativeItemCodec.Encode(installed.itemValue);
        string[] originalComponents=components.Select(item=>RebirthNativeItemCodec.Encode(item.IsEmpty()?ItemStack.Empty.itemValue:item.itemValue)).ToArray();
        Require(RebirthCamperEnergyRuntime.TryCreateBank(installed.Clone(),out var bank,out var reason),reason??"Native bank factory refused.");
        bank.SetSlots(ItemStack.Clone(components));
        if(bank is PowerGenerator generator){Require(fuel<=generator.MaxFuel,"Native generator fuel exceeds capacity.");generator.CurrentFuel=fuel;}
        else Require(fuel==0,"Non-generator cannot carry fuel.");
        var assembly=typeof(RebirthCamperEnergyRuntime).Assembly;
        Type stateType=assembly.GetType("RebirthCamperEnergyState",true),sourceType=stateType.GetNestedType("Source",BindingFlags.NonPublic);
        object state=Activator.CreateInstance(stateType,true),source=Activator.CreateInstance(sourceType,true);Guid vehicle=Guid.NewGuid(),id=Guid.NewGuid();
        Set(state,"Identity",vehicle);Set(source,"Id",id);Set(source,"Installed",installed.Clone());Set(source,"Bank",bank);
        ((IList)Field(state,"Sources")).Add(source);
        MethodInfo encode=stateType.GetMethod("Encode",BindingFlags.Static|BindingFlags.NonPublic),decode=stateType.GetMethod("Decode",BindingFlags.Static|BindingFlags.NonPublic);
        byte[] image=(byte[])encode.Invoke(null,new[]{state});object restored=decode.Invoke(null,new object[]{image,vehicle});
        var sources=(IList)Field(restored,"Sources");Require(sources.Count==1,"Native source count changed.");
        var returned=(ItemStack)Field(sources[0],"Installed");var returnedBank=(PowerSource)Field(sources[0],"Bank");
        Require(returned.count==installed.count&&RebirthNativeItemCodec.Encode(returned.itemValue)==originalInstalled,"Actual native installed image changed.");
        Require((Guid)Field(sources[0],"Id")==id,"Actual native source GUID changed.");
        for(int i=0;i<6;i++)
        {
            int expectedCount=components[i].IsEmpty()?0:components[i].count;
            Require(returnedBank.Stacks[i].count==expectedCount&&RebirthNativeItemCodec.Encode(returnedBank.Stacks[i].itemValue)==originalComponents[i],"Actual native component image/count changed at slot"+i);
        }
        Require(!(bank is PowerGenerator)||(returnedBank as PowerGenerator)?.CurrentFuel==fuel,"Actual native fuel changed.");
        Require(image.SequenceEqual((byte[])encode.Invoke(null,new[]{restored})),"Actual native Energy frame reencoding changed.");
        bool rejected=false;try{decode.Invoke(null,new object[]{image.Concat(new byte[]{1}).ToArray(),vehicle});}catch(TargetInvocationException){rejected=true;}
        Require(rejected,"Actual native Energy decoder accepted trailing data.");
        Require(RebirthNativeItemCodec.Encode(installed.itemValue)==originalInstalled,"Caller original bank mutated.");
        for(int i=0;i<6;i++)Require(RebirthNativeItemCodec.Encode(components[i].IsEmpty()?ItemStack.Empty.itemValue:components[i].itemValue)==originalComponents[i],"Caller original component mutated.");
        return new Dictionary<string,object> {
            {"scope","actual native detached Energy codec roundtrip only"},{"nativeItemCodec","real RebirthNativeItemCodec"},
            {"allNativeImagesRetained",true},{"canonicalFrameRetained",true},{"noOriginalItemMutation",true},{"frameSha256",Hash(image)},
            {"loadedRebirthDllSha256",Hash(File.ReadAllBytes(assembly.Location))},{"nativeAssembly",typeof(ItemValue).Assembly.Location},
            {"notQualified","world save custody; actual actor debit; multiplayer; driver mounting; HUD rendering; wireless devices"}
        };
    }
}
