using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

public static class RebirthMedicalPractice
{
    public sealed class Evidence
    {
        public EntityPlayer Healer; public EntityAlive Patient; public World TreatmentWorld; public string Item;
        public float Health, HealthDeficit, Reserve, Infection, Cure, LegHealingBase, ArmHealingBase;
        public bool Laceration, Bleeding, Fracture, Strain, Steroids, Illness, Vitamins, LegTreated, ArmTreated, LegBroken, ArmBroken, Abrasion, AbrasionTreated;
        public RebirthConstitutionTrainingService.ExposureEvidence ConstitutionExposure;
        internal RebirthExternalTreatmentEvidenceScope ExternalScope;
        internal RebirthSelfMedicalPracticeAwards.OwnerScope SelfScope;
        internal RebirthSelfMedicalPracticeAwards.Reservation SelfReservation;
        internal bool DoseConfirmed, Completed, Submitted;
        internal string Receipt=Guid.NewGuid().ToString("N");
        internal float CreditedFraction;
        internal BuffValue FatigueBefore, VitaminAfter, PainkillerBefore, PainkillerAfter, CureBuff;
        internal float ScheduledCureBefore, ScheduledCureNativeBudget, FrozenInfectionAcceleration;
    }
    private sealed class Pending
    {
        public Evidence Before;
        public float Health, Reserve, Infection, Remaining, Unit;
        public bool InfectionTreatment, SawReserve, SawCure;
        internal QualityBucket Quality;internal double QualityBudget,QualityWeighted;internal long QualityVersion;internal bool QualityOnly,QualityBurned,QualityRetired;
        public float Cure;
        public float CreatedAt;
    }
    // One aggregate native-cure quality bucket per live patient. No practice claims or unbounded dose history.
    private sealed class QualityBucket
    {
        internal Evidence Evidence;internal double Budget,Weighted;internal long Version,ConsumedVersion;internal float CreatedAt;
    }
    private static ConditionalWeakTable<EntityAlive,QualityBucket> Quality=new ConditionalWeakTable<EntityAlive,QualityBucket>();
    static bool QualityCurrent(QualityBucket bucket)=>bucket!=null && IsCurrentTreatment(bucket.Evidence)
        && ReferenceEquals(bucket.Evidence.Patient.Buffs.GetBuff("buffInfectionAddCure"),bucket.Evidence.CureBuff)
        && UnityEngine.Time.time-bucket.CreatedAt<=20f;
    static void StageOverflowQuality(Pending use){
        var e=use.Before;if(!use.InfectionTreatment || e.SelfScope==null || !e.DoseConfirmed || !IsCurrentTreatment(e)
            || e.CureBuff==null || e.ScheduledCureNativeBudget<=0 || float.IsNaN(e.ScheduledCureNativeBudget) || float.IsInfinity(e.ScheduledCureNativeBudget))return;
        if(!Quality.TryGetValue(e.Patient,out var bucket) || !QualityCurrent(bucket)
            || !ReferenceEquals(bucket.Evidence.CureBuff,e.CureBuff)){
            Quality.Remove(e.Patient);bucket=new QualityBucket{Evidence=e,CreatedAt=UnityEngine.Time.time};Quality.Add(e.Patient,bucket);
        }
        if(bucket.Version==long.MaxValue)return;bucket.Version++;
        double room=float.MaxValue-bucket.Budget,amount=Math.Min(room,e.ScheduledCureNativeBudget);
        double acceleration=float.IsNaN(e.FrozenInfectionAcceleration)||float.IsInfinity(e.FrozenInfectionAcceleration)?0:Math.Max(0,Math.Min(1,e.FrozenInfectionAcceleration));
        bucket.Budget+=amount;bucket.Weighted+=amount*acceleration;
    }
    static Pending QualitySnapshot(Evidence e){
        if(!Quality.TryGetValue(e.Patient,out var bucket) || !QualityCurrent(bucket) || bucket.Budget<=0
            || !ReferenceEquals(bucket.Evidence.CureBuff,e.CureBuff))return null;
        var original=bucket.Evidence;
        var copy=new Evidence{Healer=original.Healer,Patient=original.Patient,TreatmentWorld=original.TreatmentWorld,
            Item=original.Item,SelfScope=original.SelfScope,CureBuff=original.CureBuff,DoseConfirmed=true,Completed=true,
            ScheduledCureNativeBudget=(float)bucket.Budget,FrozenInfectionAcceleration=(float)(bucket.Weighted/bucket.Budget)};
        return new Pending{Before=copy,InfectionTreatment=true,QualityOnly=true,Quality=bucket,
            CreatedAt=bucket.CreatedAt,QualityBudget=bucket.Budget,QualityWeighted=bucket.Weighted,QualityVersion=bucket.Version,Remaining=0,Unit=1};
    }
    static bool OwnsPending(Pending use){
        if(!use.QualityOnly)return PendingUses.Contains(use);
        return !use.QualityRetired && QualityCurrent(use.Quality)
            && (use.QualityBurned || Quality.TryGetValue(use.Before.Patient,out var current) && ReferenceEquals(current,use.Quality)
                && use.QualityVersion>use.Quality.ConsumedVersion && use.Quality.Budget>=use.QualityBudget);
    }
    static bool BurnQuality(Pending use){
        if(!OwnsPending(use) || use.QualityBurned)return false;
        use.QualityBurned=true;use.Quality.ConsumedVersion=use.QualityVersion;double amount=use.QualityBudget;
        use.Quality.Budget-=amount;use.Quality.Weighted=Math.Max(0,use.Quality.Weighted-use.QualityWeighted);
        return true;
    }
    static void RetirePending(Pending use){if(use.QualityOnly)use.QualityRetired=true;else PendingUses.Remove(use);}
    private static readonly List<Pending> PendingUses=new List<Pending>();
    private static readonly HashSet<EntityAlive> ClinicalTransfers=new HashSet<EntityAlive>();
    [ThreadStatic] private static int actionDepth;
    private static World world;
    private static float lastObserveLog;
    private static bool Has(EntityAlive p,string name)=>p?.Buffs!=null && p.Buffs.HasBuff(name);
    private static float C(EntityAlive p,string name)=>p?.Buffs==null?0:p.Buffs.GetCustomVar(name);
    private static bool Bleeding(EntityAlive p)=>Has(p,"buffInjuryBleeding")||Has(p,"buffInjuryBleedingTwo")||Has(p,"buffInjuryBleedingBarbedWire");
    private static bool Fracture(EntityAlive p)=>Has(p,"buffLegBroken")||Has(p,"buffArmBroken");
    private static bool LegTreated(EntityAlive p)=>Has(p,"buffLegSplinted")||Has(p,"buffLegCast");
    private static bool ArmTreated(EntityAlive p)=>Has(p,"buffArmSplinted")||Has(p,"buffArmCast");
    private static bool Strain(EntityAlive p)=>Fracture(p)||Has(p,"buffLegSprained")||Has(p,"buffArmSprained");
    public static Evidence Begin(EntityPlayer healer,EntityAlive patient,string item)
    {
        actionDepth++;
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] medical Begin item=" + item + " depth=" + actionDepth + " hasTreatment=" + RebirthDifficultyPractice.HasTreatment(item) + " remote=" + (healer != null && healer.world != null && healer.world.IsRemote()));
        if(actionDepth!=1 || healer?.world==null || patient==null || !ReferenceEquals(patient.world,healer.world) || healer.IsDead() || patient.IsDead() || !RebirthDifficultyPractice.HasTreatment(item))return null;
        if(!ReferenceEquals(healer.world.GetEntity(healer.entityId),healer)||
            !ReferenceEquals(healer.world.GetEntity(patient.entityId),patient))return null;
        if(ReferenceEquals(healer,patient) && !RebirthSelfMedicalSimulationOwner.Current(healer))return null;
        if(!ReferenceEquals(healer,patient) && healer.world.IsRemote())return null;
        if(world!=healer.world){world=healer.world;ClearPending();}
        Evidence evidence=new Evidence{Healer=healer,Patient=patient,TreatmentWorld=healer.world,Item=item,Health=patient.Stats.Health.Value,HealthDeficit=Math.Max(0,patient.Stats.Health.Max-patient.Stats.Health.Value),Reserve=C(patient,"medicalRegHealthAmount"),
            Infection=C(patient,"infectionCounter"),Cure=C(patient,"$infectionCureCounter"),Bleeding=Bleeding(patient),Laceration=Has(patient,"buffLaceration"),Fracture=Fracture(patient),
            Strain=Strain(patient),Steroids=Has(patient,"buffDrugSteroids"),Illness=Has(patient,"buffFatigued"),Vitamins=Has(patient,"buffDrugVitamins"),
            Abrasion=Has(patient,"buffInjuryAbrasion"),AbrasionTreated=Has(patient,"buffInjuryAbrasionTreated"),LegBroken=Has(patient,"buffLegBroken"),ArmBroken=Has(patient,"buffArmBroken"),LegTreated=LegTreated(patient),ArmTreated=ArmTreated(patient),LegHealingBase=C(patient,"$legTreatedCritHealingBase"),ArmHealingBase=C(patient,"$armTreatedCritHealingBase")};
        if(!ReferenceEquals(healer,patient) && RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(item))
        {
            evidence.ExternalScope=RebirthExternalTreatmentEvidenceScope.Capture(healer,patient as EntityPlayer);
            if(evidence.ExternalScope==null)return null;
        }
        if(ReferenceEquals(healer,patient)){
            evidence.SelfScope=RebirthSelfMedicalPracticeAwards.Capture(healer);
            if(evidence.SelfScope==null)return null;
            evidence.FatigueBefore=patient.Buffs.GetBuff("buffFatigued");
            evidence.PainkillerBefore=patient.Buffs.GetBuff("buffDrugPainkillers");
            evidence.ScheduledCureBefore=C(patient,"$buffInfectionAddCurePerc");
            float medicine;
            if(InfectionUnit(item)>0 && RebirthServiceCraftSkillService.TryGetSkillValue(healer,"skill.medicine",out medicine))
                evidence.FrozenInfectionAcceleration=RebirthServiceCraftSkillService.ComputeMedicineInfectionTransfer(medicine,1f,1f);
            evidence.ConstitutionExposure=RebirthConstitutionTrainingService.BeginExposure(healer,item);
            evidence.SelfReservation=RebirthSelfMedicalPracticeAwards.Reserve(evidence.SelfScope,item,evidence.Receipt);
            if(evidence.SelfReservation==null)Log.Warning("[REBIRTH Medicine] practice admission refused before dose; native treatment/quality remains active");
        }
        return evidence;
    }
    static void AddPending(Pending use){
        PendingUses.RemoveAll(pending=>{if(IsCurrentTreatment(pending.Before))return false;RebirthSelfMedicalPracticeAwards.Release(pending.Before.SelfReservation);return true;});
        int count=0;foreach(var pending in PendingUses)if(ReferenceEquals(pending.Before.Patient,use.Before.Patient))count++;
        if(count>=64 || PendingUses.Count>=4096){StageOverflowQuality(use);FinishSelf(use.Before);Log.Warning("[REBIRTH Medicine] pending clinical practice capacity reached");return;}
        PendingUses.Add(use);
    }
    public static void Exit()=>actionDepth=Math.Max(0,actionDepth-1);
    private static void ClearPending(){Quality=new ConditionalWeakTable<EntityAlive,QualityBucket>();foreach(var pending in PendingUses)RebirthSelfMedicalPracticeAwards.Release(pending.Before.SelfReservation);PendingUses.Clear();}
    internal static void ClearRuntime(){ClearPending();world=null;lastObserveLog=0;}
    // Practice normalization only; native item healing and Medicine modifiers determine actual HP.
    public static float HealthUnit(string id)
    {
        switch(id){case "medicalFirstAidBandage":return 30;case "medicalFirstAidKit":return 100;case "medicalAloeCream":return 10;case "drugPainkillers":return 40;default:return 0;}
    }
    public static float InfectionUnit(string id)=>id=="drugAntibiotics"?25:id=="drugHerbalAntibiotics"?10:id=="foodHoney"?5:0;
    public static string Purpose(string id)
    {
        float hp=HealthUnit(id),infection=InfectionUnit(id);
        if(hp>0)return "Restore health";
        if(infection>0)return "Remove "+infection+"% infection";
        if(id=="medicalBandage")return "Stop a new bleed";
        if(id=="resourceSewingKit")return "Close an untreated laceration or stop a bleed";
        if(id=="medicalSplint"||id=="medicalPlasterCast")return "Treat an untreated fracture";
        if(id=="drugSteroids")return "Newly relieve an existing strain";
        if(id=="drugVitamins")return "Newly relieve existing fatigue";
        return "No verified treatment practice";
    }
    public static bool IsNewFractureTreatment(string item, bool legBroken, bool armBroken,
        bool legTreatedBefore, bool armTreatedBefore, bool legTreatedAfter, bool armTreatedAfter)
    {
        return (item == "medicalSplint" || item == "medicalPlasterCast") &&
            ((legBroken && !legTreatedBefore && legTreatedAfter) ||
             (armBroken && !armTreatedBefore && armTreatedAfter));
    }
    private static bool IsCurrentTreatment(Evidence e)
    {
        return e?.TreatmentWorld!=null &&
            (ReferenceEquals(e.Healer,e.Patient) ? e.SelfScope!=null && e.SelfScope.Current() : !e.TreatmentWorld.IsRemote()) &&
            (e.ExternalScope!=null ? e.ExternalScope.IsCurrent(e.Healer,e.Patient) :
                (ReferenceEquals(e.Healer,e.Patient) || !RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(e.Item))) &&
            e.Healer!=null && e.Patient!=null && !e.Healer.IsDead() && !e.Patient.IsDead() &&
            ReferenceEquals(e.Healer.world,e.TreatmentWorld) && ReferenceEquals(e.Patient.world,e.TreatmentWorld)&&
            ReferenceEquals(e.TreatmentWorld.GetEntity(e.Healer.entityId),e.Healer)&&
            ReferenceEquals(e.TreatmentWorld.GetEntity(e.Patient.entityId),e.Patient);
    }
    public static void Complete(Evidence e)
    {
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] medical Complete evidence=" + (e != null));
        if(!IsCurrentTreatment(e) || e.Completed || (ReferenceEquals(e.Healer,e.Patient) && !e.DoseConfirmed))return;
        e.Completed=true;
        var p=e.Patient;
        // Both self and external fracture quality initialize after native buff-start; their respective adapters own it.
        if (!(e.Item == "medicalSplint" || e.Item == "medicalPlasterCast"))
            RebirthServiceCraftSkillService.AdjustMedicalTreatmentOutcome(e.Healer,p,e.Item,e.Reserve,e.Infection,e.Cure,e.LegTreated,e.ArmTreated,e.LegHealingBase,e.ArmHealingBase);
        // Self quality is applied once by the new native item-effect/buff-start adapter.
        if (!ReferenceEquals(e.Healer, p)) RebirthServiceCraftSkillService.AdjustNewAbrasionTreatment(e.Healer, p, e.Abrasion, e.AbrasionTreated);
        bool newFractureTreatment = IsNewFractureTreatment(e.Item, e.LegBroken, e.ArmBroken, e.LegTreated, e.ArmTreated, LegTreated(p), ArmTreated(p));
        if (newFractureTreatment && e.SelfScope==null)
        {
            float insightApplied; bool insightAlreadyEarned;
            RebirthTheoryProgressionService.TryAwardInsight(e.Healer,
                "insight.medicine.fracture_successfully_treated", "medical-treatment:" + e.Item, out insightApplied, out insightAlreadyEarned);
        }
        float unit=HealthUnit(e.Item);
        if(unit>0)
        {
            float direct=Math.Min(unit,Math.Max(0,p.Stats.Health.Value-e.Health));
            if(direct>0f)RebirthConstitutionTrainingService.OnHealthRestored(p,direct);
            Award(e,direct/unit);
            float reserve=Math.Max(0,C(p,"medicalRegHealthAmount")-e.Reserve);
            if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] medical Complete unit=" + unit + " direct=" + direct + " reserveDelta=" + reserve + " pendingBefore=" + PendingUses.Count);
            if(e.Item=="drugPainkillers" && e.SelfScope!=null){
                e.PainkillerAfter=p.Buffs.GetBuff("buffDrugPainkillers");
                if(e.PainkillerBefore==null && e.PainkillerAfter!=null)AddPending(new Pending{Before=e,Health=p.Stats.Health.Value,Unit=unit,Remaining=unit-direct,CreatedAt=UnityEngine.Time.time});
                else FinishSelf(e);
            }
            else if(unit>direct && (e.SelfScope==null || reserve>0 && e.HealthDeficit>0))AddPending(new Pending{Before=e,Health=p.Stats.Health.Value,Reserve=C(p,"medicalRegHealthAmount"),Remaining=unit-direct,Unit=unit,SawReserve=reserve>0,CreatedAt=UnityEngine.Time.time});   // Supported native self first-aid/aloe items add this reserve during action-end; no unrelated later reserve admission.
            RebirthConstitutionTrainingService.CompleteExposure(e.ConstitutionExposure);
            if(unit<=direct || (e.Item!="drugPainkillers" && e.SelfScope!=null && (reserve<=0 || e.HealthDeficit<=0)))FinishSelf(e);
            return;
        }
        unit=InfectionUnit(e.Item);
        if(e.SelfScope!=null && unit>0)unit*=Math.Max(0,C(p,"$infectionMaxDuration"))/100f;
        if(unit>0)
        {
            float direct=Math.Min(unit,Math.Max(0,e.Infection-C(p,"infectionCounter")));
            Award(e,direct/unit);
            e.CureBuff=p.Buffs.GetBuff("buffInfectionAddCure");
            bool immediateCure=C(p,"$infectionCureCounter")>e.Cure;
            bool scheduledCure=e.SelfScope!=null && e.CureBuff!=null && C(p,"$buffInfectionAddCurePerc")>e.ScheduledCureBefore;
            e.ScheduledCureNativeBudget=scheduledCure ? Math.Max(0,C(p,"$buffInfectionAddCurePerc")-e.ScheduledCureBefore)*Math.Max(0,C(p,"$infectionMaxDuration"))/100f : 0;
            bool pendingInfection=(immediateCure || scheduledCure) && e.Infection>direct;
            if(pendingInfection)
                AddPending(new Pending{Before=e,Infection=C(p,"infectionCounter"),Remaining=Math.Min(unit-direct,Math.Max(0,e.Infection-direct)),Unit=unit,InfectionTreatment=true,SawCure=immediateCure,Cure=C(p,"$infectionCureCounter"),CreatedAt=UnityEngine.Time.time});
            RebirthConstitutionTrainingService.CompleteExposure(e.ConstitutionExposure);
            if(!pendingInfection)FinishSelf(e);
            return;
        }
        bool useful=e.Item=="resourceSewingKit"?(e.Laceration&&!Has(p,"buffLaceration"))||(e.Bleeding&&!Bleeding(p)):
            e.Item=="medicalBandage"?e.Bleeding&&!Bleeding(p):
            (e.Item=="medicalSplint"||e.Item=="medicalPlasterCast")?newFractureTreatment:
            e.Item=="drugSteroids"?e.Strain&&!e.Steroids&&Has(p,"buffDrugSteroids"):
            false;
        if(e.Item=="drugVitamins" && e.Illness && !e.Vitamins && e.FatigueBefore!=null){
            e.VitaminAfter=p.Buffs.GetBuff("buffDrugVitamins");
            if(e.VitaminAfter!=null && !e.VitaminAfter.Remove && !e.VitaminAfter.Invalid)
                AddPending(new Pending{Before=e,CreatedAt=UnityEngine.Time.time,Unit=1,Remaining=1});
            else FinishSelf(e);
            return;
        }
        if(useful)Award(e,1);
        FinishSelf(e);
        RebirthConstitutionTrainingService.CompleteExposure(e.ConstitutionExposure);
    }
    private static void Award(Evidence e,float fraction)
    {
        if(fraction<=0 || !IsCurrentTreatment(e))return;
        if(ReferenceEquals(e.Healer,e.Patient)){
            e.CreditedFraction=Math.Min(1f,e.CreditedFraction+fraction);
            return;
        }
        float level,progress;
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] medical Award fraction=" + fraction + " item=" + e.Item);
        if(!RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(e.Healer,"skill.medicine",out level,out progress))return;
        float credited=Math.Min(1f,Math.Max(0f,fraction));
        float raw=RebirthDifficultyPractice.Treatment(e.Item,level+progress)*credited;
        if(raw<=0f)return;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId="skill.medicine",SourceKey="phase9:medicine:"+(e.Item??string.Empty),ReferenceDescription="actual realized treatment outcome",
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=credited,DiscreteRawAward=raw
        };
        RebirthSkillTrainingComputation computation;float gained,attribute;
        bool awarded=RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(e.Healer,evidence,out computation,out gained,out attribute);
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] medical award result=" + awarded + " raw=" + raw + " gained=" + gained + " attribute=" + attribute);
    }
    public static void Observe(EntityAlive patient)
    {
        if (RebirthSkillEvalDiagnostics.On && PendingUses.Count > 0 && UnityEngine.Time.time - lastObserveLog > 1f) { lastObserveLog = UnityEngine.Time.time; Log.Out("[REBIRTH SkillEval] medical Observe enter pending=" + PendingUses.Count + " patient=" + (patient != null ? patient.entityId : -1) + " sameWorld=" + (patient != null && ReferenceEquals(world, patient.world))); }
        if(PendingUses.Count==0 || patient?.world==null || ClinicalTransfers.Contains(patient))return;
        if(world!=patient.world){ClearPending();world=patient.world;return;}
        // Allocate a tick's useful change once, oldest treatment first, never once per stacked dose.
        float healthBudget=-1,infectionBudget=-1;
        for(int i=0;i<PendingUses.Count;)
        {
            var use=PendingUses[i];var e=use.Before;
            if(!IsCurrentTreatment(e) || !ReferenceEquals(e.TreatmentWorld,world)){RebirthSelfMedicalPracticeAwards.Release(e.SelfReservation);PendingUses.RemoveAt(i);continue;}
            if(!ReferenceEquals(e.Patient,patient)){i++;continue;}
            if(e.Item=="drugVitamins" || e.Item=="drugPainkillers"){
                if(UnityEngine.Time.time-use.CreatedAt>20f || (e.Item=="drugVitamins"?!ReferenceEquals(patient.Buffs.GetBuff("buffDrugVitamins"),e.VitaminAfter):!ReferenceEquals(patient.Buffs.GetBuff("buffDrugPainkillers"),e.PainkillerAfter))){FinishSelf(e);PendingUses.RemoveAt(i);}else i++;
                continue;
            }
            float amount;
            if(use.InfectionTreatment)
            {
                if(e.SelfScope!=null && !use.SawCure){if(UnityEngine.Time.time-use.CreatedAt>20f){FinishSelf(e);PendingUses.RemoveAt(i);}else i++;continue;}
                float infection=C(patient,"infectionCounter"),cure=C(patient,"$infectionCureCounter");
                if(infectionBudget<0)infectionBudget=e.SelfScope==null?Math.Max(0,use.Infection-infection):Math.Min(Math.Max(0,use.Infection-infection),Math.Max(0,use.Cure-cure));
                amount=Math.Min(use.Remaining,infectionBudget);infectionBudget-=amount;use.Infection=infection;use.Cure=cure;
            }
            else
            {
                float health=patient.Stats.Health.Value,reserve=C(patient,"medicalRegHealthAmount");
                if(reserve>use.Reserve){use.Reserve=reserve;use.SawReserve=true;use.Health=health;}   // the reserve is still filling: nothing healed from it yet
                if(healthBudget<0)healthBudget=reserve<use.Reserve?Math.Min(Math.Max(0,health-use.Health),(use.Reserve-reserve)*Math.Max(0,C(patient,"medRegHealthIncSpeed"))/2f):0;
                amount=Math.Min(use.Remaining,healthBudget);healthBudget-=amount;use.Health=health;use.Reserve=reserve;
            }
            if (RebirthSkillEvalDiagnostics.On && true) Log.Out("[REBIRTH SkillEval] medical Observe pending=" + PendingUses.Count + " amount=" + amount + " remaining=" + use.Remaining + " useReserve=" + use.Reserve + " saw=" + use.SawReserve + " reserveNow=" + C(patient,"medicalRegHealthAmount"));
            if(amount>0){if(!use.InfectionTreatment)RebirthConstitutionTrainingService.OnHealthRestored(patient,amount);Award(e,amount/use.Unit);use.Remaining-=amount;}
            bool exhausted=use.InfectionTreatment?C(patient,"$infectionCureCounter")<=0 || C(patient,"infectionCounter")<=0:(C(patient,"medicalRegHealthAmount")<=0 && use.SawReserve) || (!use.SawReserve && UnityEngine.Time.time-use.CreatedAt>20f);
            if(use.Remaining<=.0001f || exhausted){FinishSelf(e);PendingUses.RemoveAt(i);}else i++;
        }
    }
        private static void FinishSelf(Evidence e){
        if(e.SelfScope==null || e.Submitted)return;
        // Refused practice admission does not suppress native treatment or Medicine quality/transfer.
        if(e.SelfReservation==null){e.Submitted=true;return;}
        if(!IsCurrentTreatment(e)){RebirthSelfMedicalPracticeAwards.Release(e.SelfReservation);return;}
        if(e.CreditedFraction<=0){RebirthSelfMedicalPracticeAwards.Release(e.SelfReservation);e.Submitted=true;return;}
        e.Submitted=RebirthSelfMedicalPracticeAwards.Submit(e.SelfReservation,e.CreditedFraction);
        if(!e.Submitted){RebirthSelfMedicalPracticeAwards.Release(e.SelfReservation);Log.Warning("[REBIRTH Medicine] invalid or revoked completed practice reservation refused");}
    }
    internal sealed class ClinicalEvent
    {
        internal Evidence Evidence;internal object PendingUse;internal object[] Admissions;internal float Health,Cure;internal string Name;
    }
    static object[] CureAdmissions(Evidence e,Pending qualityRoot=null){
        var admissions=new List<object>();
        foreach(var pending in PendingUses)if(pending.InfectionTreatment && !pending.SawCure
            && ReferenceEquals(pending.Before.Patient,e.Patient) && ReferenceEquals(pending.Before.CureBuff,e.CureBuff)
            && pending.Before.DoseConfirmed && IsCurrentTreatment(pending.Before) && UnityEngine.Time.time-pending.CreatedAt<=20f)
            admissions.Add(pending);
        var quality=qualityRoot??QualitySnapshot(e);if(quality!=null)admissions.Add(quality);
        return admissions.ToArray();
    }
    internal static object ClinicalBefore(EntityBuffs buffs,MinEventTypes type,BuffClass definition,MinEventParams context){
        string name=definition?.Name;
        if(!ReferenceEquals(context?.Self,buffs?.parent))return null;
        foreach(var use in PendingUses){var e=use.Before;
            if(e.SelfScope==null || !IsCurrentTreatment(e) || !ReferenceEquals(e.Patient.Buffs,buffs))continue;
            if(name=="buffInfectionAddCure" && type==MinEventTypes.onSelfBuffStart && use.InfectionTreatment && !use.SawCure
                && ReferenceEquals(context.Buff,e.CureBuff) && ReferenceEquals(buffs.GetBuff(name),e.CureBuff)
                && !e.CureBuff.Started && !e.CureBuff.Remove && !e.CureBuff.Invalid && UnityEngine.Time.time-use.CreatedAt<=20f)
                return new ClinicalEvent{Evidence=e,PendingUse=use,Admissions=CureAdmissions(e),Cure=C(e.Patient,"$infectionCureCounter"),Name=name};
            if(name=="buffDrugPainkillers" && type==MinEventTypes.onSelfBuffUpdate && e.Item=="drugPainkillers"
                && ReferenceEquals(context.Buff,e.PainkillerAfter) && ReferenceEquals(buffs.GetBuff(name),e.PainkillerAfter)
                && !e.PainkillerAfter.Remove && !e.PainkillerAfter.Invalid && C(e.Patient,"$buffDrugPainkillersHealed")==0)
                return new ClinicalEvent{Evidence=e,PendingUse=use,Health=e.Patient.Stats.Health.Value,Name=name};
        }
        if(name=="buffInfectionAddCure" && type==MinEventTypes.onSelfBuffStart && buffs.parent is EntityPlayer patient
            && Quality.TryGetValue(patient,out var bucket) && QualityCurrent(bucket)
            && ReferenceEquals(context.Buff,bucket.Evidence.CureBuff) && !bucket.Evidence.CureBuff.Started
            && !bucket.Evidence.CureBuff.Remove && !bucket.Evidence.CureBuff.Invalid){
            var use=QualitySnapshot(bucket.Evidence);
            if(use!=null)return new ClinicalEvent{Evidence=use.Before,PendingUse=use,Admissions=CureAdmissions(use.Before,use),
                Cure=C(patient,"$infectionCureCounter"),Name=name};
        }
        return null;
    }
    internal static void ClinicalAfter(object observation,bool ranOriginal){
        var clinical=observation as ClinicalEvent;var e=clinical?.Evidence;
        if(!ranOriginal || e==null || ClinicalTransfers.Contains(e.Patient) || !IsCurrentTreatment(e) || !(clinical.PendingUse is Pending use) || !OwnsPending(use))return;
        if(clinical.Name=="buffInfectionAddCure"){
            float after=C(e.Patient,"$infectionCureCounter");if(after<=clinical.Cure)return;
            float nativeBudget=after-clinical.Cure;
            var activated=new List<Pending>();
            foreach(var admission in clinical.Admissions ?? new object[0])if(admission is Pending pending && OwnsPending(pending) && pending.InfectionTreatment && !pending.SawCure
                && ReferenceEquals(pending.Before.Patient,e.Patient) && ReferenceEquals(pending.Before.CureBuff,e.CureBuff)
                && pending.Before.DoseConfirmed && IsCurrentTreatment(pending.Before)){
                if(pending.QualityOnly && !BurnQuality(pending))continue;
                pending.SawCure=true;activated.Add(pending); // Burn this admission before any scalar write/event reentry.
            }
            float infection=C(e.Patient,"infectionCounter"),cure=after,ownedTotal=0,weightedAcceleration=0;
            foreach(var pending in activated){
                var before=pending.Before;
                float ownedBudget=Math.Min(nativeBudget,Math.Max(0,before.ScheduledCureNativeBudget));
                nativeBudget-=ownedBudget;ownedTotal+=ownedBudget;
                float acceleration=before.FrozenInfectionAcceleration;
                if(acceleration>0 && !float.IsInfinity(acceleration) && !float.IsNaN(acceleration))
                    weightedAcceleration+=ownedBudget*Math.Min(1f,acceleration);
            }
            // One transfer for the native aggregate, not another min(infection,budget)*factor for every matching dose.
            float transfer=ownedTotal>0 ? Math.Min(ownedTotal,Math.Max(0,infection))*(weightedAcceleration/ownedTotal) : 0;
            if(transfer>0.0001f){
                float expectedInfection=Math.Max(0,infection-transfer),expectedCure=Math.Max(clinical.Cure,cure-transfer);
                bool committed=false;
                ClinicalTransfers.Add(e.Patient);
                try {
                    e.Patient.Buffs.SetCustomVar("infectionCounter",expectedInfection);
                    if(IsCurrentTreatment(e))e.Patient.Buffs.SetCustomVar("$infectionCureCounter",expectedCure);
                    committed=IsCurrentTreatment(e) && OwnsPending(use)
                        && C(e.Patient,"infectionCounter")==expectedInfection && C(e.Patient,"$infectionCureCounter")==expectedCure;
                } catch(Exception) { committed=false; }
                finally {
                    // Prevent a partial write from becoming recovery practice on the next observation.
                    foreach(var pending in PendingUses)if(pending.InfectionTreatment && ReferenceEquals(pending.Before.Patient,e.Patient)){
                        pending.Infection=C(e.Patient,"infectionCounter");pending.Cure=C(e.Patient,"$infectionCureCounter");
                    }
                    ClinicalTransfers.Remove(e.Patient);
                }
                if(!committed){foreach(var pending in activated){RebirthSelfMedicalPracticeAwards.Release(pending.Before.SelfReservation);RetirePending(pending);}return;}
                infection=expectedInfection;cure=expectedCure;
                float usefulBudget=Math.Min(transfer,Math.Max(0,after-cure));
                foreach(var pending in activated)if(OwnsPending(pending) && IsCurrentTreatment(pending.Before)){
                    float useful=Math.Min(pending.Remaining,usefulBudget);usefulBudget-=useful;
                    Award(pending.Before,useful/pending.Unit);pending.Remaining-=useful;
                }
            }
            // Rebase every same-patient infection observation: the immediate transfer has already been credited once.
            foreach(var pending in PendingUses)if(pending.InfectionTreatment && ReferenceEquals(pending.Before.Patient,e.Patient)
                && IsCurrentTreatment(pending.Before)){
                pending.Infection=C(e.Patient,"infectionCounter");pending.Cure=C(e.Patient,"$infectionCureCounter");
            }
            foreach(var pending in activated)if(pending.Remaining<=0.0001f || infection<=0){FinishSelf(pending.Before);RetirePending(pending);}
            foreach(var pending in activated)if(pending.QualityOnly)RetirePending(pending);
        }else if(C(e.Patient,"$buffDrugPainkillersHealed")>0){
            float restored=Math.Min(use.Remaining,Math.Max(0,e.Patient.Stats.Health.Value-clinical.Health));
            Award(e,restored/use.Unit);if(restored>0)RebirthConstitutionTrainingService.OnHealthRestored(e.Patient,restored);
            FinishSelf(e);PendingUses.Remove(use);
        }
    }
    internal static Evidence FatigueUpdateBefore(EntityBuffs buffs,MinEventTypes type,BuffClass definition,MinEventParams context){
        if(type!=MinEventTypes.onSelfBuffUpdate || definition?.Name!="buffFatigued" || !ReferenceEquals(context?.Self,buffs?.parent))return null;
        foreach(var use in PendingUses){var e=use.Before;
            if(e.Item=="drugVitamins" && IsCurrentTreatment(e) && ReferenceEquals(e.Patient.Buffs,buffs)
                && ReferenceEquals(context.Buff,e.FatigueBefore) && ReferenceEquals(buffs.GetBuff("buffFatigued"),e.FatigueBefore)
                && !e.FatigueBefore.Remove && !e.FatigueBefore.Invalid
                && ReferenceEquals(buffs.GetBuff("buffDrugVitamins"),e.VitaminAfter)
                && !e.VitaminAfter.Remove && !e.VitaminAfter.Invalid
                && C(e.Patient,"$fatiguedCounter")>Math.Max(0,C(e.Patient,"$critHitNaturalHealingRate")))return e;
        }
        return null;
    }
    internal static void FatigueUpdateAfter(Evidence e,bool ranOriginal){
        if(e==null || !ranOriginal || !IsCurrentTreatment(e) || !ReferenceEquals(e.Patient.Buffs.GetBuff("buffDrugVitamins"),e.VitaminAfter))return;
        if(e.FatigueBefore.Remove || !ReferenceEquals(e.Patient.Buffs.GetBuff("buffFatigued"),e.FatigueBefore)){
            Award(e,1);FinishSelf(e);PendingUses.RemoveAll(use=>ReferenceEquals(use.Before,e));
        }
    }
    public static string Preview(EntityPlayer player,string item)
    {
        float level,progress;
        if(!RebirthDifficultyPractice.HasTreatment(item) || !RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player,"skill.medicine",out level,out progress))return "";
        float raw=RebirthDifficultyPractice.Treatment(item,level+progress);if(raw<=0f)return "";
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId="skill.medicine",SourceKey="phase9:medicine-preview:"+(item??string.Empty),ReferenceDescription="full realized treatment reference",
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw
        };
        RebirthSkillTrainingComputation computation;
        return RebirthSkillAwardService.TryPreviewTrainingEvidence(player,evidence,out computation)&&computation!=null
            ?computation.ExpectedFinalGain.ToString("R",System.Globalization.CultureInfo.InvariantCulture):"";
    }
}
[HarmonyPatch(typeof(ItemActionEat),nameof(ItemActionEat.ExecuteInstantAction))]
internal static class RebirthMedicalInstantPracticePatch
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,System.Reflection.MethodBase __originalMethod)
        =>RebirthSelfMedicalPracticeDose.Transpile(instructions,__originalMethod);
    static void Prefix(ItemActionEat __instance,EntityAlive ent,ItemStack stack,bool isHeldItem,XUiC_ItemStack stackController,bool __runOriginal,out RebirthSelfMedicalPracticeDose.Frame __state)
        =>__state=RebirthSelfMedicalPracticeDose.Begin(__instance,ent,stack,null,isHeldItem,stackController,__runOriginal);
    static void Postfix(bool __runOriginal,bool __result,RebirthSelfMedicalPracticeDose.Frame __state)
        =>RebirthSelfMedicalPracticeDose.Returned(__state,__runOriginal&&__result);
    static Exception Finalizer(Exception __exception,RebirthSelfMedicalPracticeDose.Frame __state)
        =>RebirthSelfMedicalPracticeDose.Finish(__state,__exception);
}
[HarmonyPatch(typeof(ItemActionEat),"consume")]
internal static class RebirthMedicalHeldPracticePatch
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,System.Reflection.MethodBase __originalMethod)
        =>RebirthSelfMedicalPracticeDose.Transpile(instructions,__originalMethod);
    static void Prefix(ItemActionEat __instance,ItemActionData _actionData,bool __runOriginal,out RebirthSelfMedicalPracticeDose.Frame __state){
        var data=_actionData as ItemActionEat.MyInventoryData;
        __state=RebirthSelfMedicalPracticeDose.Begin(__instance,data?.invData?.holdingEntity,data?.invData?.itemStack,data,true,null,__runOriginal&&data!=null&&data.bEatingStarted);
    }
    static void Postfix(bool __runOriginal,RebirthSelfMedicalPracticeDose.Frame __state)=>RebirthSelfMedicalPracticeDose.Returned(__state,__runOriginal);
    static Exception Finalizer(Exception __exception,RebirthSelfMedicalPracticeDose.Frame __state)=>RebirthSelfMedicalPracticeDose.Finish(__state,__exception);
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.OnUpdateLive))]
internal static class RebirthMedicalRecoveryPracticePatch
{
    static void Postfix(EntityAlive __instance)=>RebirthMedicalPractice.Observe(__instance);
}
[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.FireEvent),new Type[]{typeof(MinEventTypes),typeof(BuffClass),typeof(MinEventParams)})]
internal static class RebirthMedicalFatigueReliefPracticePatch
{
    static void Prefix(EntityBuffs __instance,MinEventTypes _eventType,BuffClass _buffClass,MinEventParams _params,out RebirthMedicalPractice.Evidence __state)
        =>__state=RebirthMedicalPractice.FatigueUpdateBefore(__instance,_eventType,_buffClass,_params);
    static void Postfix(bool __runOriginal,RebirthMedicalPractice.Evidence __state)=>RebirthMedicalPractice.FatigueUpdateAfter(__state,__runOriginal);
}

[HarmonyPatch(typeof(EntityBuffs),nameof(EntityBuffs.FireEvent),new Type[]{typeof(MinEventTypes),typeof(BuffClass),typeof(MinEventParams)})]
internal static class RebirthMedicalDelayedClinicalPracticePatch
{
    static void Prefix(EntityBuffs __instance,MinEventTypes _eventType,BuffClass _buffClass,MinEventParams _params,out object __state)
        =>__state=RebirthMedicalPractice.ClinicalBefore(__instance,_eventType,_buffClass,_params);
    static void Postfix(bool __runOriginal,object __state)=>RebirthMedicalPractice.ClinicalAfter(__state,__runOriginal);
}





