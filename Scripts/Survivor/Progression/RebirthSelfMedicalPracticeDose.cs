using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using HarmonyLib;

// Only exact native consumption callsites can create a dose witness. No inventory-wide observer.
internal static class RebirthSelfMedicalPracticeDose
{
    internal sealed class Frame
    {
        internal Frame Previous;
        internal ItemActionEat Action;
        internal ItemActionData Data;
        internal EntityPlayer Player;
        internal ItemStack Stack,Source;
        internal ItemValue Value,SourceValue;
        internal XUiC_ItemStack Controller;
        internal Inventory Inventory;
        internal Hand Hand;
        internal ItemStack[] Cells;
        internal int Slot,Count,Calls;
        internal float Uses;
        internal bool Held,Revoked,Consumed,Returned,Entered;
        internal RebirthMedicalPractice.Evidence Evidence;
    }
    [ThreadStatic] static Frame active;
    internal static Frame Begin(ItemActionEat action,EntityAlive entity,ItemStack stack,ItemActionData data,bool held,XUiC_ItemStack controller,bool ranOriginal)
    {
        var frame=new Frame{Previous=active,Action=action,Data=data,Player=entity as EntityPlayer,Stack=stack,Held=held,Controller=controller};
        if(active!=null)active.Revoked=true;
        active=frame;
        try{
            if(frame.Previous!=null || !ranOriginal || action==null || !action.Consume || stack?.itemValue?.ItemClass==null
                || !RebirthSelfMedicalSimulationOwner.Current(frame.Player))return frame;
            bool exact=false;foreach(var itemAction in stack.itemValue.ItemClass.Actions)if(ReferenceEquals(itemAction,action))exact=true;
            if(!exact || !RebirthDifficultyPractice.HasTreatment(stack.itemValue.ItemClass.GetItemName()))return frame;
            frame.Inventory=frame.Player.inventory;frame.Hand=frame.Player.Hand;
            frame.Source=held?stack:controller?.ItemStack;
            frame.Value=stack.itemValue;frame.SourceValue=frame.Source?.itemValue;frame.Count=frame.Source?.count??0;frame.Uses=frame.Value.UseTimes;
            if(held){frame.Cells=frame.Inventory?.ItemGrid?.items;frame.Slot=frame.Inventory?.SelectedSlot??-1;}
            if(frame.Count<1 || !Bound(frame) || float.IsNaN(frame.Uses)||float.IsInfinity(frame.Uses))return frame;
            frame.Entered=true;
            frame.Evidence=RebirthMedicalPractice.Begin(frame.Player,frame.Player,frame.Value.ItemClass.GetItemName());
        }catch(Exception){frame.Revoked=true;}
        return frame;
    }
    static bool Bound(Frame f){
        if(f==null || f.Revoked || !ReferenceEquals(active,f) || !RebirthSelfMedicalSimulationOwner.Current(f.Player)
            || !ReferenceEquals(f.Player.inventory,f.Inventory) || !ReferenceEquals(f.Stack.itemValue,f.Value)
            || !ReferenceEquals(f.Source?.itemValue,f.SourceValue))return false;
        if(!f.Held)return f.Controller!=null && ReferenceEquals(f.Controller.xui?.playerUI?.entityPlayer,f.Player)
            && ReferenceEquals(f.Controller.ItemStack,f.Source);
        return f.Hand!=null && ReferenceEquals(f.Player.Hand,f.Hand) && f.Hand.mode==Hand.HoldingMode.Current
            && ReferenceEquals(f.Hand.toolbelt,f.Inventory) && f.Inventory.SelectedSlot==f.Slot
            && f.Cells!=null && f.Slot>=0 && f.Slot<f.Cells.Length && ReferenceEquals(f.Inventory.ItemGrid.items,f.Cells)
            && ReferenceEquals(f.Cells[f.Slot],f.Source)
            && (f.Data==null || ReferenceEquals(f.Data.invData.itemStack,f.Source)
                && ReferenceEquals(f.Data.invData.holdingEntity,f.Player)
                && ReferenceEquals(f.Hand.slotDatas[f.Slot],f.Data.invData));
    }
    static Frame Witness(ItemActionEat action){var f=active;return f!=null && f.Evidence!=null && ReferenceEquals(f.Action,action) && Bound(f)?f:null;}
    internal static bool Decrement(Inventory inventory,int count,ItemActionEat action){
        var f=Witness(action);bool eligible=f!=null && f.Held && ReferenceEquals(inventory,f.Inventory) && count==1 && f.Source.count==f.Count;
        bool result=inventory.DecHoldingItem(count); // Native return/exception preserved; never retry native mutation.
        if(f!=null){f.Calls++;f.Consumed=eligible && result && f.Calls==1 && f.Source.count==f.Count-1 && Bound(f);if(!f.Consumed)f.Revoked=true;}
        return result;
    }
    internal static void SetUses(ItemValue value,float uses,ItemActionEat action){
        var f=Witness(action);bool eligible=f!=null && ReferenceEquals(value,f.Value) && ReferenceEquals(value,f.SourceValue) && value.UseTimes==f.Uses;
        value.UseTimes=uses;
        if(eligible){f.Calls++;f.Consumed=f.Calls==1 && uses>f.Uses && !float.IsInfinity(uses) && !float.IsNaN(uses) && Bound(f);if(!f.Consumed)f.Revoked=true;}
    }
    internal static void SetCount(ItemStack stack,int count,ItemActionEat action){
        var f=Witness(action);bool eligible=f!=null && !f.Held && ReferenceEquals(stack,f.Source) && stack.count==f.Count;
        stack.count=count;
        if(eligible){f.Calls++;f.Consumed=f.Calls==1 && count==f.Count-1 && Bound(f);if(!f.Consumed)f.Revoked=true;}
    }
    internal static void Returned(Frame f,bool ranOriginal){if(f!=null)f.Returned=ranOriginal;}
    internal static Exception Finish(Frame f,Exception error){
        if(f==null)return error;
        bool observed=false;
        try{
            if(error==null && f.Returned && f.Consumed && f.Calls==1 && !f.Revoked && ReferenceEquals(active,f)
                && f.Evidence!=null && f.Evidence.SelfScope.Current()){
                f.Evidence.DoseConfirmed=true;RebirthMedicalPractice.Complete(f.Evidence);observed=true;
            }
        }finally{if(f.Evidence!=null && !observed)RebirthSelfMedicalPracticeAwards.Release(f.Evidence.SelfReservation);if(f.Entered)RebirthMedicalPractice.Exit();active=ReferenceEquals(active,f)?f.Previous:null;}
        return error;
    }
    private static Dictionary<Label, int> Labels(List<CodeInstruction> instructions)
    {
        var targets = new Dictionary<Label, int>();
        for (int i = 0; i < instructions.Count; i++) foreach (Label label in instructions[i].labels) targets.Add(label, i);
        return targets;
    }
    private static string Operand(object operand, Dictionary<Label, int> labels)
    {
        if (operand == null) return "null";
        if (operand is Label) return "target:" + labels[(Label)operand].ToString(CultureInfo.InvariantCulture);
        if (operand is Label[])
        {
            var values = new List<string>();
            foreach (Label label in (Label[])operand) values.Add(labels[label].ToString(CultureInfo.InvariantCulture));
            return "targets:" + string.Join(",", values);
        }
        var member = operand as MemberInfo;
        if (member != null) return "member:" + member.Module.ModuleVersionId + ":" + member.MetadataToken.ToString(CultureInfo.InvariantCulture);
        var local = operand as LocalBuilder;
        if (local != null) return "local:" + local.LocalIndex + ":" + local.LocalType.AssemblyQualifiedName;
        if (operand is string) return "string:" + (string)operand;
        if (operand is float) return "floatbits:" + BitConverter.ToString(BitConverter.GetBytes((float)operand));
        if (operand is double) return "doublebits:" + BitConverter.ToString(BitConverter.GetBytes((double)operand));
        if (operand is byte || operand is sbyte || operand is short || operand is ushort || operand is int || operand is long)
            return operand.GetType().FullName + ":" + Convert.ToString(operand, CultureInfo.InvariantCulture);
        throw new InvalidOperationException("Unknown incoming native instruction operand.");
    }
    internal static bool SameIncoming(List<CodeInstruction> incoming, List<CodeInstruction> original)
    {
        try
        {
            if (incoming == null || original == null || incoming.Count != original.Count) return false;
            var left = Labels(incoming); var right = Labels(original);
            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].opcode != original[i].opcode || incoming[i].labels.Count != original[i].labels.Count ||
                    incoming[i].blocks.Count != 0 || original[i].blocks.Count != 0 ||
                    Operand(incoming[i].operand, left) != Operand(original[i].operand, right)) return false;
            }
            return true;
        }
        catch (Exception) { return false; }
    }

    static bool Pinned(MethodBase method){
        try{
            if(method?.DeclaringType!=typeof(ItemActionEat) || (method.Name!="consume" && method.Name!="ExecuteInstantAction")
                || method.Module.ModuleVersionId.ToString()!="1a9a4203-3d95-4c90-b094-8926dec1ee9c")return false;
            using(var stream=File.OpenRead(method.Module.Assembly.Location))using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")=="FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705";
        }catch(Exception){return false;}
    }
    static readonly Dictionary<string,int> diagnosticCounts=new Dictionary<string,int>(StringComparer.Ordinal);
    internal static void StartupDiagnostic(string method,bool applied,string reason){
        if(method!="consume" && method!="ExecuteInstantAction")return;
        diagnosticCounts.TryGetValue(method,out int count);if(count>=4)return;diagnosticCounts[method]=count+1;
        string message="[REBIRTH MedicinePractice][Startup] target=ItemActionEat."+method+" doseTranspiler="+(applied?"APPLIED":"REFUSED")+" reason="+reason;
        if(applied)Log.Out(message);else Log.Warning(message);
    }
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions,MethodBase method){
        var incoming=new List<CodeInstruction>(instructions);
        string name=method?.Name??"";
        try{
            if(!Pinned(method)){StartupDiagnostic(name,false,"native-pin-mismatch");return incoming;}
            var original=PatchProcessor.GetOriginalInstructions(method,out var generator);
            var rewritten=RewriteUnchangedOrExact(incoming,original,name);
            bool applied=!ReferenceEquals(rewritten,incoming);
            StartupDiagnostic(name,applied,applied?"exact-native-dose-sites":"incoming-body-or-dose-sites-mismatch");
            return rewritten;
        }catch(Exception error){StartupDiagnostic(name,false,"native-instruction-read-"+error.GetType().Name);return incoming;}
    }
    internal static IEnumerable<CodeInstruction> RewriteUnchangedOrExact(List<CodeInstruction> incoming,List<CodeInstruction> original,string methodName){
        try{
            if(!SameIncoming(incoming,original))return incoming;
            var decrement=AccessTools.Method(typeof(Inventory),nameof(Inventory.DecHoldingItem),new[]{typeof(int)});
            var uses=AccessTools.PropertySetter(typeof(ItemValue),nameof(ItemValue.UseTimes));
            var count=AccessTools.PropertySetter(typeof(ItemStack),nameof(ItemStack.count));
            int d=0,u=0,c=0;foreach(var instruction in incoming){if(Equals(instruction.operand,decrement))d++;if(Equals(instruction.operand,uses))u++;if(Equals(instruction.operand,count))c++;}
            if(d!=1 || u!=1 || c!=(methodName=="consume"?0:2))return incoming;
            var output=new List<CodeInstruction>();
            foreach(var instruction in incoming){
                string shim=Equals(instruction.operand,decrement)?nameof(Decrement):Equals(instruction.operand,uses)?nameof(SetUses):Equals(instruction.operand,count)?nameof(SetCount):null;
                if(shim==null){output.Add(new CodeInstruction(instruction));continue;}
                if(instruction.opcode!=OpCodes.Callvirt || instruction.blocks.Count!=0)return incoming;
                var instance=new CodeInstruction(OpCodes.Ldarg_0);instance.labels.AddRange(instruction.labels);output.Add(instance);
                output.Add(new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthSelfMedicalPracticeDose),shim)));
            }
            return output;
        }catch(Exception){return incoming;}
    }
}




