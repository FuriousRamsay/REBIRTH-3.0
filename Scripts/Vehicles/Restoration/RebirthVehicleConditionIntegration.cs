using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;

// Required-for-conversion remains the authored slot.Required flag. Operation
// follows the legacy rule: every required component except headlights must work.
internal static class RebirthVehicleConditionIntegration
{
    private sealed class Wear { internal string Slot,Identity;internal float Amount; }
    internal const string DeferredWearKey="RebirthVehicleDeferredWearV1";
    private sealed class Pending { internal Guid Assembly;internal bool Loaded,Invalid,Logged,Dirty;internal readonly List<Wear> Rows=new List<Wear>(); }
    private static readonly ConditionalWeakTable<EntityVehicle,Pending> deferred=new ConditionalWeakTable<EntityVehicle,Pending>();

    internal static bool RequiredToOperate(RebirthVehicleSlotDefinition slot)
        =>slot!=null&&slot.Required&&!slot.SlotId.StartsWith("Headlight",StringComparison.OrdinalIgnoreCase);
    internal static bool IsPartUsable(RebirthInstalledVehiclePart part)
    {
        if(part==null)return false;
        try
        {
            var stack=RebirthVehicleInventoryTransactions.CreateItemStack(part);
            if(stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
            float max=stack.itemValue.MaxUseTimes,use=stack.itemValue.UseTimes;
            return !float.IsNaN(max)&&!float.IsInfinity(max)&&!float.IsNaN(use)&&!float.IsInfinity(use)&&max-use>0;
        }
        catch{return false;}
    }
    internal static bool IsOperational(RebirthVehicleAssembly assembly)
    {
        if(assembly==null)return false;
        var family=RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId);
        if(family==null)return false;
        foreach(var slot in family.Slots)
            if(RequiredToOperate(slot)&&(!assembly.Parts.TryGetValue(slot.SlotId,out var part)
                ||part==null||!string.Equals(slot.ItemName,part.ItemName,StringComparison.Ordinal)||!IsPartUsable(part)))return false;
        return true;
    }
    internal static bool Blocked(EntityVehicle entity)
    {
        if(entity==null||!RebirthVehicleEntityResolver.Resolve(entity,out var assembly))return false;
        if(entity.Health<=0)return true;
        try{Flush(entity);}catch(Exception error){Report(entity,error);return true;}
        if(deferred.TryGetValue(entity,out var pending)&&pending.Invalid)return true;
        if(RebirthVehicleAssemblyIndex.TryGetEntity(entity,out var current))assembly=current;
        return assembly==null||assembly.Performance==null||!assembly.Performance.CanOperate;
    }
    internal static bool DamageEligible(int damage,bool horde)=>damage>0&&(horde?damage:damage/10)>=1;
    internal static float Worn(float use,float max,float amount)=>Mathf.Min(max,use+amount);

    internal static void Damage(EntityVehicle entity,int damage)
    {
        if(entity?.world==null||entity.world.IsRemote())return;
        bool horde=entity.world.aiDirector?.BloodMoonComponent?.BloodMoonActive==true;
        if(!DamageEligible(damage,horde)||!RebirthVehicleEntityResolver.Resolve(entity,out var assembly)||assembly==null)return;
        var pending=Load(entity);if(pending.Invalid)return;
        if(pending.Rows.Count>0&&pending.Assembly!=assembly.AssemblyId){Reject(pending,"Deferred wear assembly identity mismatch; native metadata retained.");return;}
        pending.Assembly=assembly.AssemblyId;
        foreach(var pair in assembly.Parts)
        {
            var part=pair.Value;
            if(part==null||!IsPartUsable(part)||entity.world.GetGameRandom().RandomRange(1,3)!=2)continue;
            var native=RebirthVehicleInventoryTransactions.CreateItemStack(part).itemValue;
            string identity=Identity(RebirthNativeItemCodec.Encode(native));
            var row=pending.Rows.Find(w=>w.Slot==pair.Key&&w.Identity==identity);
            if(row==null)
            {
                if(pending.Rows.Count>=64){Reject(pending,"Deferred wear row limit reached; retained native metadata requires review.");return;}
                row=new Wear{Slot=pair.Key,Identity=identity};pending.Rows.Add(row);
            }
            row.Amount=Mathf.Min(native.MaxUseTimes,row.Amount+3);
            pending.Dirty=true;
        }
        if(pending.Rows.Count==0)return;
        Save(entity,pending,false);
        Flush(entity);
    }
    internal static void Flush(EntityVehicle entity)
    {
        if(entity?.world==null||entity.world.IsRemote()||!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out var assembly)||assembly==null)return;
        var pending=Load(entity);if(pending.Invalid||pending.Rows.Count==0)return;
        if(assembly.AssemblyId!=pending.Assembly){Reject(pending,"Deferred wear assembly identity mismatch; native metadata retained.");return;}
        Save(entity,pending,false);
        if(assembly.PendingInventoryTransferId!=Guid.Empty)return;
        var candidate=assembly.DeepClone();bool changed=false;
        foreach(var row in pending.Rows)
        {
            if(!candidate.Parts.TryGetValue(row.Slot,out var part)||part==null)continue;
            var stack=RebirthVehicleInventoryTransactions.CreateItemStack(part);
            // Reservation resolution may move the original component. Never
            // damage its replacement or mutate an item now in another custody.
            if(stack==null||stack.IsEmpty()||Identity(RebirthNativeItemCodec.Encode(stack.itemValue))!=row.Identity)continue;
            var native=stack.itemValue;
            native.UseTimes=Worn(native.UseTimes,native.MaxUseTimes,row.Amount);
            part.UseTimes=native.UseTimes;part.SerializedItemValue=RebirthNativeItemCodec.Encode(native);changed=true;
        }
        if(changed)RebirthVehicleAssemblyIndex.CommitEntity(entity,candidate);
        Save(entity,pending,true);
        pending.Rows.Clear();
    }
    private static string Identity(string native)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(native))).Replace("-",string.Empty);}
    private static void Reject(Pending pending,string reason)
    {
        pending.Invalid=true;
        if(pending.Logged)return;pending.Logged=true;Log.Error("[REBIRTH Vehicle] "+reason);
    }
    private static Pending Load(EntityVehicle entity)
    {
        var pending=deferred.GetValue(entity,_=>new Pending());if(pending.Loaded)return pending;pending.Loaded=true;
        var item=entity.vehicle?.GetUpdatedItemValue();
        if(item==null){Reject(pending,"Deferred wear native item unavailable.");return pending;}
        if(!item.TryGetMetadata(DeferredWearKey,out string text)||string.IsNullOrEmpty(text))return pending;
        try
        {
            if(text.Length>16384)throw new InvalidDataException("payload limit");
            var bytes=Convert.FromBase64String(text);
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(reader.ReadByte()!=1)throw new InvalidDataException("version");
                var guid=reader.ReadBytes(16);if(guid.Length!=16)throw new InvalidDataException("assembly id");
                pending.Assembly=new Guid(guid);if(pending.Assembly==Guid.Empty)throw new InvalidDataException("empty assembly id");
                int count=reader.ReadInt32();if(count<1||count>64)throw new InvalidDataException("row count");
                var seen=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    string slot=ReadText(reader,256),hash=ReadText(reader,64);float amount=reader.ReadSingle();
                    if(!Valid(slot,hash,amount)||!seen.Add(slot+"|"+hash))throw new InvalidDataException("invalid row");
                    pending.Rows.Add(new Wear{Slot=slot,Identity=hash,Amount=amount});
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("trailing data");
            }
        }
        catch(Exception error){pending.Rows.Clear();Reject(pending,"Malformed deferred wear metadata retained: "+error.GetType().Name);}
        return pending;
    }
    private static string ReadText(BinaryReader reader,int maximumBytes)
    {
        int length=0,shift=0;
        while(true)
        {
            byte b=reader.ReadByte();if(shift>=28&&(b&0xF0)!=0)throw new InvalidDataException("string length");
            length|=(b&127)<<shift;if((b&128)==0)break;shift+=7;if(shift>28)throw new InvalidDataException("string length");
        }
        if(length<0||length>maximumBytes)throw new InvalidDataException("string bound");
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
        return new UTF8Encoding(false,true).GetString(bytes);
    }
    internal static void Report(EntityVehicle entity,Exception error)
    {
        if(entity==null)return;
        var pending=deferred.GetValue(entity,_=>new Pending());if(pending.Logged)return;pending.Logged=true;
        Log.Error("[REBIRTH Vehicle] installed-part wear retained: "+error.GetType().Name+": "+error.Message);
    }
    private static bool Valid(string slot,string hash,float amount)
    {
        if(string.IsNullOrEmpty(slot)||slot.Length>64||hash==null||hash.Length!=64
            ||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return false;
        foreach(char c in slot)if(char.IsControl(c))return false;
        foreach(char c in hash)if(!(c>='0'&&c<='9')&&!(c>='A'&&c<='F'))return false;
        return true;
    }
    private static void Save(EntityVehicle entity,Pending pending,bool clear)
    {
        if(!clear&&!pending.Dirty)return;
        string text=string.Empty;
        if(!clear&&pending.Rows.Count>0)
        {
            if(pending.Rows.Count>64||pending.Assembly==Guid.Empty)throw new InvalidDataException("Deferred wear limits");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8))
            {
                writer.Write((byte)1);writer.Write(pending.Assembly.ToByteArray());writer.Write(pending.Rows.Count);
                foreach(var row in pending.Rows){if(!Valid(row.Slot,row.Identity,row.Amount))throw new InvalidDataException("Deferred wear row");writer.Write(row.Slot);writer.Write(row.Identity);writer.Write(row.Amount);}
                writer.Flush();text=Convert.ToBase64String(stream.ToArray());
            }
            if(text.Length>16384)throw new InvalidDataException("Deferred wear payload limit");
        }
        var item=entity.vehicle?.GetUpdatedItemValue();if(item==null)throw new InvalidOperationException("Deferred wear native owner missing");
        if(item.TryGetMetadata(DeferredWearKey,out string before)&&before==text){pending.Dirty=false;return;}
        item.SetMetadata(DeferredWearKey,text);
        var actual=entity.vehicle.GetUpdatedItemValue();
        if(actual==null||!actual.TryGetMetadata(DeferredWearKey,out string stored)||stored!=text)
            throw new InvalidOperationException("Deferred wear native owner write not verified");
        pending.Dirty=false;
        // Native persistence always serializes the owned vehicle ItemValue.
        // Pending evidence is server-only; replicate after committed cleanup.
        if(clear)entity.SendSyncData(4);
    }
    internal static void ClearDrive(EntityVehicle entity)
    {
        if(entity?.vehicle==null)return;
        var input=entity.movementInput;
        // Native jump is the vehicle handbrake. Keep it available while refusing
        // propulsion, steering commands, turbo and hop input.
        if(input!=null){input.moveForward=0;input.moveStrafe=0;input.running=false;input.down=false;}
        entity.wheelMotor=0;entity.wheelDir=0;entity.motorTorque=0;
        entity.vehicle.CurrentIsAccel=false;entity.vehicle.CurrentMotorTorquePercent=0;entity.vehicle.CurrentSteeringPercent=0;
    }
}

[HarmonyPatch(typeof(VPEngine),"startEngine")]
internal static class RebirthVehicleConditionStartEngine
{
    private static bool Prefix(VPEngine __instance)=>!RebirthVehicleConditionIntegration.Blocked(__instance?.vehicle?.entity);
}
[HarmonyPatch(typeof(VPEngine),nameof(VPEngine.Update))]
internal static class RebirthVehicleConditionEngineUpdate
{
    private static void Prefix(VPEngine __instance)
    {
        if(RebirthVehicleConditionIntegration.Blocked(__instance?.vehicle?.entity))__instance.stopEngine();
    }
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.MoveByAttachedEntity),typeof(EntityPlayerLocal))]
internal static class RebirthVehicleConditionMoveInput
{
    private static void Postfix(EntityVehicle __instance)
    {if(RebirthVehicleConditionIntegration.Blocked(__instance))RebirthVehicleConditionIntegration.ClearDrive(__instance);}
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthVehicleConditionPhysics
{
    private static void Prefix(EntityVehicle __instance)=>Clear(__instance);
    private static void Postfix(EntityVehicle __instance)=>Clear(__instance);
    private static void Clear(EntityVehicle entity)
    {if(RebirthVehicleConditionIntegration.Blocked(entity))RebirthVehicleConditionIntegration.ClearDrive(entity);}
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ApplyDamage))]
internal static class RebirthVehicleConditionDamage
{
    private static void Prefix(EntityVehicle __instance,out bool __state)=>__state=__instance!=null&&__instance.Health>0;
    private static void Postfix(EntityVehicle __instance,int damage,bool __state)
    {
        if(!__state)return;
        try{RebirthVehicleConditionIntegration.Damage(__instance,damage);}
        catch(Exception error){RebirthVehicleConditionIntegration.Report(__instance,error);}
    }
}
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.Update))]
internal static class RebirthVehicleConditionTick
{
    private static void Postfix(EntityVehicle __instance)
    {
        try{RebirthVehicleConditionIntegration.Flush(__instance);}
        catch(Exception error){RebirthVehicleConditionIntegration.Report(__instance,error);}
    }
}
