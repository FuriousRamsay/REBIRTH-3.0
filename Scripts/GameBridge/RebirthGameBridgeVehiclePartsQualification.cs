using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class ConsoleCmdRebirthVehiclePartsQualification:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbvehiclepartsqual"};
    public override string getDescription()=>"CodexTest-only native vehicle component qualification.";
    public override string getHelp()=>"rbvehiclepartsqual selftest | observe ENTITY | setup ENTITY QUALITY ROLL confirmCodexTest | wear ENTITY DAMAGE confirmCodexTest | remove/exhaust ENTITY SLOT confirmCodexTest";
    private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||sender.RemoteClientInfo!=null||world==null||world.IsRemote()
            ||world.GetPrimaryPlayer()==null||!RebirthGameBridge.IsRunning||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
        {SdtdConsole.Instance.Output("*** ERROR local CodexTest Bridge required");return;}
        try
        {
            Check(args.Count>0,getHelp());JObject result;
            if(args[0]=="selftest")
            {Check(args.Count==1,getHelp());result=SelfTest();}
            else
            {
                Check(args.Count>=2&&int.TryParse(args[1],out _),getHelp());
                var entity=world.GetEntity(int.Parse(args[1])) as EntityVehicle;
                var type=entity==null?null:EntityClass.GetEntityClass(entity.entityClass)?.entityClassName;
                Check(entity!=null&&(type=="rebirthCamper80s"||type=="vehicleTruck4x4"),"only exact camper or base4x4 fixture allowed");
                RebirthVehicleAssembly assembly=null;
                if(args[0]=="observe")
                {
                    if(!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out assembly))
                        RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(entity,out assembly);
                    Check(assembly!=null,"assembly unavailable; observation does not create one");
                }
                else Check(RebirthVehicleEntityResolver.Resolve(entity,out assembly)&&assembly!=null,"authoritative assembly unavailable");
                if(args[0]!="observe")
                {
                    Check(args[args.Count-1]=="confirmCodexTest","explicit fixture confirmation required");
                    Check(assembly.PendingInventoryTransferId==Guid.Empty,"pending native inventory transfer");
                    if(args[0]=="wear")
                    {
                        Check(args.Count==4&&int.TryParse(args[2],out int damage)&&damage>=1&&damage<=1000,"damage1..1000 required");
                        entity.ApplyDamage(int.Parse(args[2]));
                    }
                    else
                    {
                        Check(!entity.HasDriver,"park and exit before fixture item mutation");
                        var candidate=assembly.DeepClone();
                        if(args[0]=="setup")
                        {
                            Check(args.Count==5&&int.TryParse(args[2],out int quality)&&quality>=1&&quality<=6,"quality1..6 required");
                            Check(float.TryParse(args[3],NumberStyles.Float,CultureInfo.InvariantCulture,out float roll)&&!float.IsNaN(roll)&&roll>=0&&roll<=1,"roll0..1 required");
                            candidate.Parts.Clear();Fill(candidate,int.Parse(args[2]),float.Parse(args[3],CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            Check(args.Count==4&&(args[0]=="remove"||args[0]=="exhaust"),getHelp());
                            Check(candidate.Parts.TryGetValue(args[2],out var part)&&part!=null,"actual installed slot required");
                            if(args[0]=="remove")candidate.Parts.Remove(args[2]);
                            else Exhaust(part);
                        }
                        RebirthVehicleAssemblyIndex.CommitEntity(entity,candidate);
                    }
                }
                else Check(args.Count==2,getHelp());
                result=Observe(entity);
            }
            result["ok"]=true;string json=result.ToString(Formatting.None);
            SdtdConsole.Instance.Output(json);Log.Out("[VehiclePartsQualification] "+json);
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR "+error.Message);Log.Warning("[VehiclePartsQualification] failed: "+error.Message);}
    }
    private static void Fill(RebirthVehicleAssembly assembly,int quality,float roll)
    {
        var family=RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId);Check(family!=null,"family missing");
        foreach(var slot in family.Slots)
        {
            var item=ItemClass.GetItem(slot.ItemName);Check(item!=null&&item.type!=ItemValue.None.type,"native item missing: "+slot.ItemName);
            item.Quality=(byte)quality;item.UseTimes=0;RebirthVehiclePartMetadata.WriteRoll(item,roll);
            var part=RebirthVehicleInventoryTransactions.FromItemStack(new ItemStack(item,1),null);
            part.SlotId=slot.SlotId;assembly.Parts[slot.SlotId]=part;
        }
    }
    private static void Exhaust(RebirthInstalledVehiclePart part)
    {
        var native=RebirthVehicleInventoryTransactions.CreateItemStack(part).itemValue;
        native.UseTimes=native.MaxUseTimes;part.UseTimes=native.UseTimes;part.SerializedItemValue=RebirthNativeItemCodec.Encode(native);
    }
    private static bool Complete(RebirthVehicleAssembly a)
    {
        foreach(var slot in RebirthVehicleDefinitionRegistry.GetFamily(a.FamilyId).Slots)
            if(slot.Required&&(!a.Parts.TryGetValue(slot.SlotId,out var p)||!RebirthVehicleConditionIntegration.IsPartUsable(p)))return false;
        return true;
    }
    private static JObject SelfTest()
    {
        var a=new RebirthVehicleAssembly{AssemblyId=Guid.NewGuid(),ContentId="Camper80s",FamilyId="V8TruckRepair"};
        Fill(a,3,1);var family=RebirthVehicleDefinitionRegistry.GetFamily(a.FamilyId);Check(family.Slots.Length==15,"expected15slots");
        Check(RebirthVehicleConditionIntegration.IsOperational(a)&&Complete(a),"complete native parts failed");
        int cases=1;
        foreach(var slot in family.Slots)
        {
            var missing=a.DeepClone();missing.Parts.Remove(slot.SlotId);
            bool optional=!RebirthVehicleConditionIntegration.RequiredToOperate(slot);
            Check(RebirthVehicleConditionIntegration.IsOperational(missing)==optional,"missing gate: "+slot.SlotId);
            Check(!Complete(missing),"repair still complete with missing required slot");
            var exhausted=a.DeepClone();Exhaust(exhausted.Parts[slot.SlotId]);
            Check(RebirthVehicleConditionIntegration.IsOperational(exhausted)==optional,"exhausted gate: "+slot.SlotId);cases+=3;
        }
        var engine=a.DeepClone();Exhaust(engine.Parts["Engine"]);
        Check(Mathf.Abs(RebirthVehiclePerformanceCompiler.Compile(engine).AccelerationMultiplier-1)<.0001f,"exhausted engine bonus persisted");cases++;
        var wheels=a.DeepClone();wheels.Parts.Remove("Wheel1");
        Check(Mathf.Abs(RebirthVehiclePerformanceCompiler.Compile(wheels).HandlingMultiplier-1.75f)<.0001f,"wheel denominator excludes missing wheel");cases++;
        var legacy=ItemClass.GetItem("partV8Engine_FR");legacy.Quality=3;
        legacy.SetMetadata("rb_vperf_ver",1);legacy.SetMetadata("rb_vperf_q",3);legacy.SetMetadata("rb_vperf_score",.42f);
        Check(Mathf.Abs(RebirthVehiclePartMetadata.ReadOrCreateRoll(legacy,null)-.42f)<.0001f,"legacy roll not preserved");cases++;
        var q1=ItemClass.GetItem("partV8Engine_FR");q1.Quality=1;
        q1.SetMetadata("rb_vperf_ver",1);q1.SetMetadata("rb_vperf_q",1);q1.SetMetadata("rb_vperf_score",0f);
        Check(RebirthVehiclePartMetadata.ReadOrCreateRoll(q1,null)==0,"legacy Q1 zero roll rerolled");cases++;
        foreach(var content in RebirthVehicleDefinitionRegistry.GetContentsSnapshot())
        {
            if(content.ContentId!="BaseBicycle"&&content.ContentId!="BaseMinibike"&&content.ContentId!="BaseMotorcycle"
                &&content.ContentId!="Base4x4"&&content.ContentId!="BaseGyrocopter")continue;
            var sample=new RebirthVehicleAssembly{AssemblyId=Guid.NewGuid(),ContentId=content.ContentId,FamilyId=content.FamilyId};
            Fill(sample,3,.4f);Check(RebirthVehicleConditionIntegration.IsOperational(sample),"complete base family failed: "+content.ContentId);cases++;
            foreach(var slot in RebirthVehicleDefinitionRegistry.GetFamily(sample.FamilyId).Slots)
            {
                var broken=sample.DeepClone();broken.Parts.Remove(slot.SlotId);
                bool expected=!RebirthVehicleConditionIntegration.RequiredToOperate(slot);
                Check(RebirthVehiclePerformanceCompiler.Compile(broken).CanOperate==expected,"base missing gate: "+content.ContentId+"/"+slot.SlotId);cases++;
                broken=sample.DeepClone();Exhaust(broken.Parts[slot.SlotId]);
                Check(RebirthVehiclePerformanceCompiler.Compile(broken).CanOperate==expected,"base spent gate: "+content.ContentId+"/"+slot.SlotId);cases++;
            }
        }
        return new JObject{["command"]="selftest",["passed"]=true,["cases"]=cases,["persistentMutations"]=false};
    }
    private static JObject Observe(EntityVehicle entity)
    {
        if(!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out var a))RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(entity,out a);
        Check(a!=null,"assembly missing");
        var rows=new JArray();
        foreach(var slot in RebirthVehicleDefinitionRegistry.GetFamily(a.FamilyId).Slots)
        {
            a.Parts.TryGetValue(slot.SlotId,out var part);var stack=part==null?null:RebirthVehicleInventoryTransactions.CreateItemStack(part);
            rows.Add(new JObject{["slot"]=slot.SlotId,["item"]=part?.ItemName,["repairRequired"]=slot.Required,
                ["operateRequired"]=RebirthVehicleConditionIntegration.RequiredToOperate(slot),["usable"]=RebirthVehicleConditionIntegration.IsPartUsable(part),
                ["quality"]=part?.Quality??0,["roll"]=part?.Roll??0,["useTimes"]=stack?.itemValue?.UseTimes??0,["maxUseTimes"]=stack?.itemValue?.MaxUseTimes??0});
        }
        var engine=entity.vehicle.FindPart("engine") as VPEngine;
        RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(entity,out var persisted);
        var persistedWear=new JObject();if(persisted!=null)foreach(var pair in persisted.Parts)persistedWear[pair.Key]=pair.Value?.UseTimes??0;
        return new JObject{["entity"]=entity.entityId,["content"]=a.ContentId,["assembly"]=a.AssemblyId.ToString("N"),["revision"]=a.Revision,["slots"]=rows,
            ["metadataAssembly"]=persisted?.AssemblyId.ToString("N"),["metadataRevision"]=persisted?.Revision??-1,
            ["metadataPartUseTimes"]=persistedWear,
            ["position"]=new JArray(entity.position.x,entity.position.y,entity.position.z),
            ["operational"]=RebirthVehicleConditionIntegration.IsOperational(a),["repairComplete"]=Complete(a),["performance"]=JObject.FromObject(a.Performance),
            ["health"]=entity.vehicle.GetHealth(),["driver"]=entity.GetAttached(0)?.entityId??-1,["engineRunning"]=engine?.isRunning??false,
            ["localPlayerEntityId"]=entity.world.GetPrimaryPlayer()?.entityId??-1,
            ["localPlayerAttachedEntityId"]=entity.world.GetPrimaryPlayer()?.AttachedToEntity?.entityId??-1,
            ["fuel"]=entity.vehicle.GetFuelLevel(),
            ["nativePhysics"]=new JObject{["motorTorqueForward"]=entity.vehicle.MotorTorqueForward,
                ["motorTorqueBackward"]=entity.vehicle.MotorTorqueBackward,["motorTorqueTurboForward"]=entity.vehicle.MotorTorqueTurboForward,
                ["velocityMaxForward"]=entity.vehicle.VelocityMaxForward,["velocityMaxTurboForward"]=entity.vehicle.VelocityMaxTurboForward,
                ["brakeTorque"]=entity.vehicle.brakeTorque,["steerRate"]=entity.vehicle.steerRate},
            ["forwardVelocity"]=entity.vehicle.CurrentForwardVelocity,["wheelMotor"]=entity.wheelMotor,["motorTorque"]=entity.motorTorque,
            ["nativeCurrentMotorTorquePercent"]=entity.vehicle.CurrentMotorTorquePercent,["nativeCurrentIsAccel"]=entity.vehicle.CurrentIsAccel};
    }
}
