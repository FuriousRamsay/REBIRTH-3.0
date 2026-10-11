using System;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk 8 / Pass 3E owner for Maintenance, Gunsmithing, Cooking, Medicine, Chemistry, Mechanics, Metalworking and Tailoring.
/// Reuses native craft/repair/treatment/service completion flows. Failure/workmanship persistence is deferred.
/// </summary>
public static class RebirthServiceCraftSkillService
{
    // Chunk G: PC017 verified typed ItemValue metadata/stack compatibility and PC018 verified the
    // V3 DegradationMax stat adapter. Heat Treatment now writes bounded crafted-item provenance.
    public const bool MetalworkingPersistentHeatTreatmentGradeEnabled = true;

    private static readonly string[] SkillIds = new string[] { "skill.maintenance", "skill.gunsmithing", "skill.cooking", "skill.drink_preparation", "skill.medicine", "skill.chemistry", "skill.mechanics", "skill.metalworking", "skill.tailoring", "skill.construction", "skill.electrical" };
    private static bool installed;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Service/Crafting] already installed";
        installed = true;
        return "[REBIRTH Survivor Service/Crafting] installed";
    }

    public static float SignedEndpoint(float skillValue, float negativeAtMinus50, float positiveAt100)
    {
        float v=Mathf.Clamp(skillValue,-50f,100f);
        if(v<0f)return negativeAtMinus50*(-v/50f);
        if(v>0f)return positiveAt100*(v/100f);
        return 0f;
    }

    public static bool IsChunk8Skill(string skillId)
    {
        for(int i=0;i<SkillIds.Length;i++) if(string.Equals(SkillIds[i],skillId,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string ClassifyRecipe(Recipe recipe)
    {
        if(recipe==null)return string.Empty;
        string recipeName=SafeRecipeName(recipe);
        string explicitSkill;
        if(RebirthCraftingProgressionRegistry.IsReady && RebirthCraftingProgressionRegistry.TryGetPrimarySkill(recipeName,out explicitSkill))
            return explicitSkill;

        // Only external/compatibility recipes reach heuristic classification. Owned recipes are
        // never reclassified by output names, tags or crafting-area tokens.
        ItemClass output=ItemClass.GetForId(recipe.itemValueType);
        string outputName=output!=null?(output.GetItemName()??string.Empty):string.Empty;
        string outputTags=output!=null?(output.ItemTags.ToString()??string.Empty):string.Empty;
        string area=recipe.craftingArea??string.Empty;

        if (Contains(outputName,"drink") || Contains(outputTags,"drink")) return "skill.drink_preparation";
        if(RebirthProgressionRuntimeConfig.IsCookingRecipe(recipeName) || Contains(outputName,"food") || Contains(outputTags,"food")) return "skill.cooking";
        if(IsTailoringWearableName(recipeName) || IsTailoringWearableName(outputName) || MatchesAny(recipeName,RebirthProgressionRuntimeConfig.TailoringRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.TailoringRecipeTokens)) return "skill.tailoring";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ElectricalRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.ElectricalRecipeTokens)) return "skill.electrical";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ConstructionRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.ConstructionRecipeTokens)) return "skill.construction";
        if(MatchesAny(area,RebirthProgressionRuntimeConfig.ChemistryAreaTokens) || MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ChemistryRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.ChemistryRecipeTokens)) return "skill.chemistry";
        string firearm=RebirthProgressionRuntimeConfig.ClassifyCombatItemName(outputName);
        if(RebirthProgressionRuntimeConfig.IsFirearmSkill(firearm) || MatchesAny(recipeName,RebirthProgressionRuntimeConfig.GunsmithingRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.GunsmithingRecipeTokens)) return "skill.gunsmithing";
        if(MatchesAny(area,RebirthProgressionRuntimeConfig.MetalworkingAreaTokens) || MatchesAny(recipeName,RebirthProgressionRuntimeConfig.MetalworkingRecipeTokens) || MatchesAny(outputName,RebirthProgressionRuntimeConfig.MetalworkingRecipeTokens)) return "skill.metalworking";
        return string.Empty;
    }

    public static string ClassifyRecipe(string recipeName)
    {
        if((recipeName??string.Empty).StartsWith("rebirthImprovised",StringComparison.Ordinal))return "skill.cooking";
        string explicitSkill;
        if(RebirthCraftingProgressionRegistry.IsReady && RebirthCraftingProgressionRegistry.TryGetPrimarySkill(recipeName,out explicitSkill))
            return explicitSkill;
        Recipe recipe=null;
        try { recipe=CraftingManager.GetRecipe(recipeName??string.Empty); } catch { }
        if(recipe!=null)return ClassifyRecipe(recipe);
        if((recipeName??string.Empty).StartsWith("drink",StringComparison.OrdinalIgnoreCase))return "skill.drink_preparation";
        if(RebirthProgressionRuntimeConfig.IsCookingRecipe(recipeName))return "skill.cooking";
        if(IsTailoringWearableName(recipeName) || MatchesAny(recipeName,RebirthProgressionRuntimeConfig.TailoringRecipeTokens))return "skill.tailoring";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ElectricalRecipeTokens))return "skill.electrical";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ConstructionRecipeTokens))return "skill.construction";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.ChemistryRecipeTokens))return "skill.chemistry";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.GunsmithingRecipeTokens))return "skill.gunsmithing";
        if(MatchesAny(recipeName,RebirthProgressionRuntimeConfig.MetalworkingRecipeTokens))return "skill.metalworking";
        return string.Empty;
    }

    public static float ApplyCraftTime(EntityPlayer player,Recipe recipe,float nativeTime)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld() || player==null || recipe==null || nativeTime<=0f)return nativeTime;
        string skillId=ClassifyRecipe(recipe);
        float value;
        if(!TryGetSkillValue(player,skillId,out value))return nativeTime;
        float delta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.ServiceCraftTimeNegative,RebirthProgressionRuntimeConfig.ServiceCraftTimePositive);
        float traitMultiplier=RebirthTraitGameplayModifierService.GetCraftTimeMultiplier(player,skillId);
        return Mathf.Max(0.10f,nativeTime*(1f+delta)*traitMultiplier);
    }

    public static void AdjustRepairQueue(EntityPlayer player,ItemValue item,ref float repairTime,ref int repairAmount)
    {
        if(player==null || item==null || item.ItemClass==null || repairTime<=0f || repairAmount<=0)return;
        string itemName=item.ItemClass.GetItemName()??string.Empty;
        string skillId=ClassifyRepairSkill(player,itemName);
        float value;
        if(!TryGetSkillValue(player,skillId,out value))return;
        float timeDelta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.ServiceRepairTimeNegative,RebirthProgressionRuntimeConfig.ServiceRepairTimePositive);
        float amountDelta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.ServiceRepairAmountNegative,RebirthProgressionRuntimeConfig.ServiceRepairAmountPositive);

        // Core repair proficiency is earned by and scales directly from the owning Skill.
        // Drone field service remains a distinct authored technique because Drone Operations already
        // has independent combat/stun/recovery Skill value; it is not needed to make the Skill useful.
        string procedure=GetRepairProcedureForItem(skillId,itemName);
        if((timeDelta<0f || amountDelta>0f) && !string.IsNullOrEmpty(procedure) && !RebirthKnowledgeService.HasKnowledge(player,procedure))
        {
            timeDelta=0f;
            amountDelta=0f;
        }

        repairTime=Mathf.Max(0.10f,repairTime*(1f+timeDelta));
        repairAmount=Math.Max(1,Mathf.RoundToInt(repairAmount*(1f+amountDelta)));
    }



    public static string GetRepairProcedureForItem(string skillId,string itemName)
    {
        // Do not put a second Knowledge gate in front of the core benefit of Maintenance,
        // Gunsmithing or Tailoring. Drone field service is intentionally a separate technique.
        if(string.Equals(skillId,"skill.drone_operations",StringComparison.OrdinalIgnoreCase))
            return "procedure.drone.field_service";
        return string.Empty;
    }

    public static int AdjustVehicleRepairAmount(EntityPlayer player,int baseHealth)
    {
        if(player==null || baseHealth<=0)return Math.Max(0,baseHealth);
        float value;
        if(!TryGetSkillValue(player,"skill.mechanics",out value))return baseHealth;
        // Mechanics Skill directly governs vehicle repair effectiveness. Roadside Diagnostics may
        // remain learnable knowledge, but it does not nullify earned core Skill scaling.
        float delta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.MechanicsRepairNegative,RebirthProgressionRuntimeConfig.MechanicsRepairPositive);
        float traitMultiplier=RebirthTraitGameplayModifierService.GetVehicleRepairHealthMultiplier(player);
        return Math.Max(1,Mathf.RoundToInt(baseHealth*(1f+delta)*traitMultiplier));
    }

    /// <summary>
    /// Applies Medicine Skill only to treatment outcomes the consumed item already creates.
    /// HP treatments scale their newly-added regeneration reserve. Fracture treatment scales the
    /// native treated-critical-healing base. Infection treatment front-loads a bounded fraction
    /// of the newly scheduled native cure without increasing the total cure budget.
    /// </summary>
    public static void AdjustMedicalTreatmentOutcome(EntityPlayer healer,EntityAlive patient,string treatmentKey,
        float reserveBefore,float infectionBefore,float cureBefore,bool legTreatedBefore,bool armTreatedBefore,
        float legHealingBaseBefore,float armHealingBaseBefore)
    {
        if(healer==null || patient==null || patient.Buffs==null)return;
        float value;
        if(!TryGetSkillValue(healer,"skill.medicine",out value))return;

        AdjustMedicalReserveDeltaInternal(healer,patient,reserveBefore,treatmentKey,value);

        if(IsFractureTreatment(treatmentKey))
        {
            float delta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative,RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive);
            float factor=Mathf.Max(0.10f,1f+delta);
            bool legAfter=patient.Buffs.HasBuff("buffLegSplinted")||patient.Buffs.HasBuff("buffLegCast");
            bool armAfter=patient.Buffs.HasBuff("buffArmSplinted")||patient.Buffs.HasBuff("buffArmCast");
            float legAfterBase=patient.Buffs.GetCustomVar("$legTreatedCritHealingBase");
            float armAfterBase=patient.Buffs.GetCustomVar("$armTreatedCritHealingBase");

            // Apply once to the newly established/refreshed native treated-fracture base. Repeated
            // use that did not change the treatment state/base cannot compound the Skill multiplier.
            if(legAfter && legAfterBase>0.001f && (!legTreatedBefore || Mathf.Abs(legAfterBase-legHealingBaseBefore)>0.001f))
                patient.Buffs.SetCustomVar("$legTreatedCritHealingBase",Mathf.Max(0.001f,legAfterBase*factor));
            if(armAfter && armAfterBase>0.001f && (!armTreatedBefore || Mathf.Abs(armAfterBase-armHealingBaseBefore)>0.001f))
                patient.Buffs.SetCustomVar("$armTreatedCritHealingBase",Mathf.Max(0.001f,armAfterBase*factor));
        }

        if(IsInfectionTreatment(treatmentKey))
        {
            // The native infection model exposes infectionCounter plus $infectionCureCounter. A
            // positive Medicine Skill transfers part of only the cure budget added by this dose
            // into immediate progress, then removes the same amount from the pending cure counter.
            // Total cure from the item is unchanged; only treatment completion is accelerated.
            float infectionAfter=patient.Buffs.GetCustomVar("infectionCounter");
            float cureAfter=patient.Buffs.GetCustomVar("$infectionCureCounter");
            float newlyScheduled=Mathf.Max(0f,cureAfter-cureBefore);
            float transfer=ComputeMedicineInfectionTransfer(value,newlyScheduled,infectionAfter);
            if(transfer>0.0001f)
            {
                patient.Buffs.SetCustomVar("infectionCounter",Mathf.Max(0f,infectionAfter-transfer));
                patient.Buffs.SetCustomVar("$infectionCureCounter",Mathf.Max(cureBefore,cureAfter-transfer));
            }
        }
    }

    public static void AdjustNewAbrasionTreatment(EntityPlayer healer, EntityAlive patient,
        bool untreatedBefore, bool treatedBefore)
    {
        if (healer == null || patient?.Buffs == null || !untreatedBefore || treatedBefore) return;
        float skill;
        if (!TryGetSkillValue(healer, "skill.medicine", out skill)) return;
        float nativeMultiplier = patient.Buffs.GetCustomVar("healAbrasionMult");
        float delta = SignedEndpoint(skill, RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative,
            RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive);
        float adjusted = ComputeNewAbrasionMultiplier(untreatedBefore, treatedBefore,
            patient.Buffs.HasBuff("buffInjuryAbrasionTreated"), nativeMultiplier, delta);
        if (adjusted != nativeMultiplier) patient.Buffs.SetCustomVar("healAbrasionMult", adjusted);
    }

    public static float ComputeNewAbrasionMultiplier(bool untreatedBefore, bool treatedBefore,
        bool treatedAfter, float nativeMultiplier, float skillDelta)
    {
        if (!untreatedBefore || treatedBefore || !treatedAfter || nativeMultiplier <= 0f ||
            float.IsNaN(nativeMultiplier) || float.IsInfinity(nativeMultiplier) ||
            float.IsNaN(skillDelta) || float.IsInfinity(skillDelta)) return nativeMultiplier;
        return nativeMultiplier * Math.Max(0.10f, 1f + skillDelta);
    }
    /// <summary>
    /// Pure arithmetic used by runtime and debug vectors. Transfers only part of the newly scheduled
    /// cure into immediate progress. Returning a transfer rather than mutating state makes the
    /// conservation invariant directly testable.
    /// </summary>
    public static float ComputeMedicineInfectionTransfer(float skillValue,float newlyScheduled,float infectionRemaining)
    {
        if(newlyScheduled<=0f || infectionRemaining<=0f)return 0f;
        float acceleration=SignedEndpoint(skillValue,0f,RebirthProgressionRuntimeConfig.MedicineInfectionAccelerationPositive);
        if(acceleration<=0.0001f)return 0f;
        return Mathf.Min(newlyScheduled,Mathf.Max(0f,infectionRemaining))*Mathf.Clamp01(acceleration);
    }

    /// <summary>Compatibility entry point for callers that only need HP-regeneration reserve scaling.</summary>
    public static void AdjustMedicalReserveDelta(EntityPlayer healer,EntityAlive patient,float reserveBefore,string treatmentKey)
    {
        if(healer==null || patient==null || patient.Buffs==null)return;
        float value;
        if(!TryGetSkillValue(healer,"skill.medicine",out value))return;
        AdjustMedicalReserveDeltaInternal(healer,patient,reserveBefore,treatmentKey,value);
    }

    private static void AdjustMedicalReserveDeltaInternal(EntityPlayer healer,EntityAlive patient,float reserveBefore,string treatmentKey,float value)
    {
        float after=patient.Buffs.GetCustomVar("medicalRegHealthAmount");
        float added=after-reserveBefore;
        if(added<=0.001f)return;
        float delta=SignedEndpoint(value,RebirthProgressionRuntimeConfig.MedicineReserveNegative,RebirthProgressionRuntimeConfig.MedicineReservePositive);

        // Medicine Skill directly improves the treatment reserve the item already creates. Learned
        // procedures may unlock distinct treatments/techniques, but do not suppress this core scaling.
        float adjusted=Mathf.Max(0f,added*(1f+delta));

        // Field Triage remains narrow: strengthen only the regeneration reserve created by
        // first-aid/bandage treatments. Native instant healing and unrelated medicine buffs remain untouched.
        if(IsFirstAidReserveTreatment(treatmentKey))
            adjusted*=RebirthTraitGameplayModifierService.GetMedicineFirstAidReserveMultiplier(healer);

        patient.Buffs.SetCustomVar("medicalRegHealthAmount",reserveBefore+adjusted);
    }

    public static bool IsFractureTreatment(string treatmentKey)
    {
        string n=(treatmentKey??string.Empty).ToLowerInvariant();
        return n.Contains("plaster") || n.Contains("splint");
    }

    public static bool IsInfectionTreatment(string treatmentKey)
    {
        string n=(treatmentKey??string.Empty).ToLowerInvariant();
        return n.Contains("antibiotic") || n=="foodhoney";
    }

    public static bool IsFirstAidReserveTreatment(string treatmentKey)
    {
        string n=(treatmentKey??string.Empty).ToLowerInvariant();
        return n.Contains("bandage") || n.Contains("firstaid");
    }

    public static string GetMedicineProcedureForTreatment(string treatmentKey)
    {
        string n=(treatmentKey??string.Empty).ToLowerInvariant();
        if(n.Contains("plaster")||n.Contains("splint"))return "procedure.medicine.fracture_care";
        if(n.Contains("antibiotic"))return "procedure.medicine.infection_care";
        if(n.Contains("bandage")||n.Contains("firstaid"))return "procedure.medicine.sterile_dressing";
        return "procedure.medicine.field_triage";
    }

    public static float GetCraftAward(string skillId,int outputCount)
    {
        int count=Math.Max(1,outputCount);
        if(string.Equals(skillId,"skill.cooking",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.drink_preparation",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.CookingMax,count*RebirthProgressionRuntimeConfig.CookingPerOutput);
        if(string.Equals(skillId,"skill.chemistry",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.ChemistryMax,count*RebirthProgressionRuntimeConfig.ChemistryPerOutput);
        if(string.Equals(skillId,"skill.metalworking",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.MetalworkingMax,count*RebirthProgressionRuntimeConfig.MetalworkingPerOutput);
        if(string.Equals(skillId,"skill.gunsmithing",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.GunsmithingCraftMax,count*RebirthProgressionRuntimeConfig.GunsmithingCraftPerOutput);
        if(string.Equals(skillId,"skill.tailoring",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.TailoringMax,count*RebirthProgressionRuntimeConfig.TailoringPerOutput);
        if(string.Equals(skillId,"skill.construction",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.ConstructionCraftMax,count*RebirthProgressionRuntimeConfig.ConstructionCraftPerOutput);
        if(string.Equals(skillId,"skill.electrical",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.ElectricalCraftMax,count*RebirthProgressionRuntimeConfig.ElectricalCraftPerOutput);
        if(string.Equals(skillId,"skill.maintenance",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.MaintenanceCraftMax,count*RebirthProgressionRuntimeConfig.MaintenanceCraftPerOutput);
        if(string.Equals(skillId,"skill.mechanics",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.MechanicsCraftMax,count*RebirthProgressionRuntimeConfig.MechanicsCraftPerOutput);
        if(string.Equals(skillId,"skill.farming",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.FarmingCraftMax,count*RebirthProgressionRuntimeConfig.FarmingCraftPerOutput);
        if(string.Equals(skillId,"skill.explosives",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.ExplosivesCraftMax,count*RebirthProgressionRuntimeConfig.ExplosivesCraftPerOutput);
        if(string.Equals(skillId,"skill.deployable_turrets",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.TurretsCraftMax,count*RebirthProgressionRuntimeConfig.TurretsCraftPerOutput);
        if(string.Equals(skillId,"skill.medicine",StringComparison.OrdinalIgnoreCase)) return Math.Min(RebirthProgressionRuntimeConfig.MedicineCraftMax,count*RebirthProgressionRuntimeConfig.MedicineCraftPerOutput);
        return 0f;
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Service/Crafting]");
        b.AppendLine("status=IMPLEMENTED_PREBOOT compileClaim=False primaryAuthority=native completion flows; server LBD where completion is authoritative");
        b.AppendLine("skills=Maintenance,Gunsmithing,Cooking,Medicine,Chemistry,Mechanics,Metalworking,Tailoring,Construction,Electrical");
        b.AppendLine("implemented=explicit crafting_progression.xml primary-Skill ownership first; external-only token fallback; native craft-time adapter; specialist repair queue routing/time/amount adapter; Tailoring wearable craft/repair routing; Drone Operations platform field-service routing; medicine regeneration-reserve adapter; mechanics repair-efficiency adapter; completed craft/repair/treatment/service LBD routing");
        b.AppendLine(RebirthCraftingProgressionRegistry.BuildSummary());
        b.AppendLine("enabled=persistent metal heat-treatment grade=" + MetalworkingPersistentHeatTreatmentGradeEnabled + " carrier=ItemValue typed provenance + V3 DegradationMax adapter; no catastrophic item loss path");
        if(player==null){b.AppendLine("player=<null>");return b.ToString();}
        b.AppendLine("player="+player.entityId);
        for(int i=0;i<SkillIds.Length;i++)
        {
            float v; bool ok=TryGetSkillValue(player,SkillIds[i],out v);
            b.AppendLine(SkillIds[i]+"="+(ok?v.ToString("0.###"):"<unavailable>"));
        }
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;
        if(held!=null&&held.ItemClass!=null)
        {
            string name=held.ItemClass.GetItemName()??string.Empty;
            b.AppendLine("held="+name+" repairOwner="+ClassifyRepairSkill(player,name));
        }
        return b.ToString();
    }


    public static string ClassifyRepairSkill(EntityPlayer player,string itemName)
    {
        string name=itemName??string.Empty;
        string useSkill=RebirthProgressionRuntimeConfig.ClassifyCombatItemName(name);
        if(RebirthProgressionRuntimeConfig.IsFirearmSkill(useSkill))return "skill.gunsmithing";

        // Drone platform repair has its own authored field-service technique. The repair action
        // remains available; only the separate drone repair-efficiency technique is knowledge-gated.
        if(IsDroneServiceItemName(name))return "skill.drone_operations";

        // Repair ownership follows the object being repaired, not a second knowledge gate. Recognized
        // wearables are Tailoring work from the first valid repair onward; Skill level controls efficiency.
        if(IsTailoringWearableName(name))return "skill.tailoring";
        return "skill.maintenance";
    }

    public static bool IsDroneServiceItemName(string value)
    {
        if(string.IsNullOrEmpty(value))return false;
        // Verified stock platform name in the current source map. Keep this narrow: schematics,
        // drone mods and unrelated "drone" strings are not repair ownership targets here.
        return value.Equals("gunBotT3JunkDrone",StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasBasicTailoringRepairKnowledge(EntityPlayer player)
    {
        return RebirthKnowledgeService.HasKnowledge(player,"procedure.tailoring.basic_repair")
            || RebirthKnowledgeService.HasKnowledge(player,"knowledge.tailoring.basic");
    }

    public static bool HasLeatherRepairKnowledge(EntityPlayer player)
    {
        return RebirthKnowledgeService.HasKnowledge(player,"procedure.tailoring.leather_repair")
            || RebirthKnowledgeService.HasKnowledge(player,"knowledge.tailoring.basic");
    }

    public static bool IsLeatherTailoringWearableName(string value)
    {
        if(string.IsNullOrEmpty(value))return false;
        string n=value.Trim();

        // Current b259 localization explicitly describes the Biker boots as leather and the
        // Ranger outfit as a leather duster. Keep the classifier evidence-based rather than
        // guessing material composition from armor weight tier.
        if(n.StartsWith("armorBiker",StringComparison.OrdinalIgnoreCase))return true;
        if(n.Equals("armorRangerOutfit",StringComparison.OrdinalIgnoreCase))return true;

        // Preserve compatibility with custom/legacy wearable IDs that explicitly declare leather
        // in their stable item name.
        return n.IndexOf("leather",StringComparison.OrdinalIgnoreCase)>=0
            && IsTailoringWearableName(n);
    }

    public static bool IsTailoringWearableName(string value)
    {
        if(string.IsNullOrEmpty(value))return false;
        if(value.StartsWith("armor",StringComparison.OrdinalIgnoreCase)
            && !value.Equals("armorParts",StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("modArmor",StringComparison.OrdinalIgnoreCase))return true;

        return value.StartsWith("rebirthGear",StringComparison.OrdinalIgnoreCase)
            || value.Equals("rebirthSupportKneeBrace",StringComparison.OrdinalIgnoreCase)
            || value.Equals("rebirthSupportBackSupportBelt",StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gameplay-outcome value: stored Practical Skill plus the small bounded primary-Attribute contribution.
    /// Use TryGetPracticalSkillValue when the persisted Skill itself is required for UI/debug/state logic.
    /// </summary>
    public static bool TryGetSkillValue(EntityPlayer player,string id,out float value)
    {
        return RebirthSkillOutcomeValueService.TryGetEffective(player,id,out value);
    }

    public static bool TryGetPracticalSkillValue(EntityPlayer player,string id,out float value)
    {
        float effective,attribute;
        return RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(player,id,out value,out effective,out attribute);
    }

    public static bool TryGetPracticalSkillProgress(EntityPlayer player,string id,out float value,out float progress)
    {
        value=progress=0f;
        if(player==null||string.IsNullOrEmpty(id))return false;
        if(player.world!=null&&player.world.IsRemote())
        {
            RebirthSurvivorOwnerStateSnapshot owner=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(owner==null)return false;
            for(int i=0;i<owner.Skills.Count;i++)
            {
                RebirthSurvivorOwnerSkillSnapshot skill=owner.Skills[i];
                if(skill!=null&&string.Equals(skill.Id,id,StringComparison.OrdinalIgnoreCase))
                {value=skill.Value;progress=Mathf.Clamp01(skill.Progress);return true;}
            }
            return false;
        }
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null)return false;
        RebirthSkillRuntimeState state;
        if(!record.Progression.Skills.TryGetValue(id,out state)||state==null)return false;
        value=state.Value;progress=Mathf.Clamp01(state.Progress);return true;
    }

    private static string SafeRecipeName(Recipe recipe){try{return recipe.GetName()??string.Empty;}catch{return string.Empty;}}
    private static bool Contains(string value,string token){return !string.IsNullOrEmpty(value)&&value.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0;}
    private static bool MatchesAny(string value,string[] tokens)
    {
        if(string.IsNullOrEmpty(value)||tokens==null)return false;
        for(int i=0;i<tokens.Length;i++) if(!string.IsNullOrEmpty(tokens[i])&&value.IndexOf(tokens[i],StringComparison.OrdinalIgnoreCase)>=0)return true;
        return false;
    }
}

/// <summary>
/// One recipe cycle, not one output item, is the unit of crafting practice. This is the common
/// raw-gain calculator used by completion and previews; trait/teaching bonuses are applied later
/// by RebirthSkillAwardService. No inventory, queue or skill state is mutated by this helper.
/// </summary>
public static class RebirthCraftTrainingRules
{
    public struct Model
    {
        public float Raw, Difficulty;
        public string RecipeId, SkillId;
        public int Outputs, IngredientTypes;
        public float Seconds;
        public bool Valid;
    }
    private sealed class RecipeRule
    {
        public float Multiplier = 1f;
        public float Difficulty = -1f;
    }
    private static readonly System.Collections.Generic.Dictionary<string, float> Materials =
        new System.Collections.Generic.Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Generic.Dictionary<string, float> SkillRates =
        new System.Collections.Generic.Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Generic.Dictionary<string, RecipeRule> Recipes =
        new System.Collections.Generic.Dictionary<string, RecipeRule>(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Generic.Dictionary<string, float> Recommendations =
        new System.Collections.Generic.Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static float referenceEffort = 10f, maximumRaw = 0.6f, practiceFade = 20f;
    private static float economicDivisor = 20f, unknownWeight = 0.25f;

    public static void Load(string configRoot)
    {
        Materials.Clear(); SkillRates.Clear(); Recipes.Clear(); Recommendations.Clear();
        string path = System.IO.Path.Combine(configRoot, "craft_training.xml");
        var root = System.Xml.Linq.XDocument.Load(path).Root;
        if (root == null || root.Name.LocalName != "craft_training" || (string)root.Attribute("schema_version") != "1")
            throw new System.IO.InvalidDataException("Invalid craft_training.xml schema.");
        referenceEffort = Number(root, "reference_effort", 10f, 0.01f, 10000f);
        maximumRaw = Number(root, "maximum_raw_per_cycle", 0.6f, 0.001f, 10f);
        practiceFade = Number(root, "practice_fade_levels", 20f, 1f, 100f);
        economicDivisor = Number(root, "economic_value_divisor", 20f, 0.01f, 10000f);
        unknownWeight = Number(root, "unknown_material_weight", 0.25f, 0f, 100f);
        foreach (var n in root.Elements("material"))
        {
            string id = (string)n.Attribute("item");
            if (string.IsNullOrWhiteSpace(id) || Materials.ContainsKey(id))
                throw new System.IO.InvalidDataException("Duplicate/empty craft-training material.");
            Materials.Add(id, Number(n, "weight", unknownWeight, 0f, 10000f));
        }
        foreach (var n in root.Elements("skill"))
        {
            string id = (string)n.Attribute("id");
            if (string.IsNullOrWhiteSpace(id) || SkillRates.ContainsKey(id))
                throw new System.IO.InvalidDataException("Duplicate/empty craft-training skill.");
            SkillRates.Add(id, Number(n, "base_rate", 0f, 0f, 10f));
        }
        foreach (var n in root.Elements("recipe"))
        {
            string id = (string)n.Attribute("id");
            if (string.IsNullOrWhiteSpace(id) || Recipes.ContainsKey(id))
                throw new System.IO.InvalidDataException("Duplicate/empty craft-training recipe.");
            Recipes.Add(id, new RecipeRule { Multiplier = Number(n, "multiplier", 1f, 0f, 10f),
                Difficulty = Number(n, "difficulty", -1f, -1f, 100f) });
        }
        // A recipe that is gated at a specialist level must not become trivial before it unlocks.
        // Use its existing recommended level for the actual associated skill, not another skill.
        string capabilityPath = System.IO.Path.Combine(configRoot, "capabilities.xml");
        if (System.IO.File.Exists(capabilityPath))
        {
            var capabilities = System.Xml.Linq.XDocument.Load(capabilityPath).Root;
            if (capabilities != null) foreach (var n in capabilities.Elements("capability"))
            {
                if ((string)n.Attribute("target_type") != "recipe") continue;
                string recipe = (string)n.Attribute("target_id");
                if (string.IsNullOrEmpty(recipe)) continue;
                foreach (var skill in n.Descendants("skill"))
                {
                    string id = (string)skill.Attribute("id");
                    if (string.IsNullOrEmpty(id)) continue;
                    float min = Number(skill, "minimum", 0f, -50f, 100f);
                    float level = Number(skill, "recommended", min, -50f, 100f);
                    string key = recipe + "|" + id;
                    float old;
                    if (!Recommendations.TryGetValue(key, out old) || level > old) Recommendations[key] = level;
                }
            }
        }
    }

    public static Model BuildModel(EntityPlayer player, Recipe recipe, string skillId)
    {
        if (recipe == null || string.IsNullOrEmpty(skillId)) return new Model();
        if(recipe.ingredients==null||recipe.ingredients.Count==0)return new Model{Valid=true};
        string name = recipe.GetName();
        if(string.IsNullOrEmpty(name))return new Model();
        RecipeRule rule; Recipes.TryGetValue(name, out rule);
        if (rule != null && rule.Multiplier <= 0f) return new Model{Valid=true};
        float units = 0f;
        var types = new System.Collections.Generic.HashSet<int>();
        foreach (ItemStack ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.IsEmpty() || ingredient.itemValue.ItemClass == null) continue;
            // Packing/unpacking and self-conversion are not new material work.
            if (ingredient.itemValue.type == recipe.itemValueType) return new Model{Valid=true};
            int amount;
            if (!RebirthCraftingIngredientQuantity.TryResolvePerBatch(player, recipe, ingredient,
                    Math.Max(1, recipe.craftingTier), out amount)) return new Model();
            if (amount <= 0) continue;
            float weight = MaterialWeight(ingredient.itemValue.ItemClass);
            units += Math.Min(100000f, amount * weight);
            types.Add(ingredient.itemValue.type);
        }
        if (!Finite(units)) return new Model();
        if(units<=0f)return new Model{Valid=true};
        Recipe canonical=CraftingManager.GetRecipe(name);
        float seconds=canonical!=null?canonical.craftingTime:
            name.StartsWith("rebirthImprovised",StringComparison.Ordinal)
                ? RebirthCookingHeatRules.Duration(RebirthCookingHeatRules.Method(recipe),types.Count) : recipe.craftingTime;
        float authoredSeconds = Finite(seconds) ? Mathf.Clamp(seconds, 0f, 120f) : 0f;
        float complexity = 1f + authoredSeconds / 120f + Math.Min(0.5f, Math.Max(0, types.Count - 1) * 0.1f);
        float baseRate;
        if (!SkillRates.TryGetValue(skillId, out baseRate)) baseRate = RebirthServiceCraftSkillService.GetCraftAward(skillId, 1);
        float raw = Math.Min(maximumRaw, baseRate * units / referenceEffort * complexity * (rule == null ? 1f : rule.Multiplier));
        float difficulty = Mathf.Clamp(5f + units * 1.5f + Math.Max(0, types.Count - 1) * 8f + authoredSeconds / 10f, 5f, 100f);
        float recommended;
        if (Recommendations.TryGetValue(name + "|" + skillId, out recommended)) difficulty = Math.Max(difficulty, recommended);
        if (rule != null && rule.Difficulty >= 0f) difficulty = rule.Difficulty;
        return new Model { Raw = Finite(raw) ? Math.Max(0f,raw) : 0f, Difficulty=difficulty, Valid=true, RecipeId=name, SkillId=skillId, Outputs=Math.Max(1,recipe.count), IngredientTypes=types.Count, Seconds=authoredSeconds };
    }

    public static float Practice(Model model,float practicalValue)
    {
        if(!model.Valid || !Finite(model.Raw) || model.Raw<=0f)return 0f;
        float tuned;
        if(RebirthDifficultyPractice.TryCraft(model.RecipeId,model.SkillId,practicalValue,model.Outputs,out tuned))return tuned;
        if(model.SkillId=="skill.cooking" || model.SkillId=="skill.drink_preparation")return RebirthDifficultyPractice.Cooking(practicalValue,model.IngredientTypes,model.Seconds);
        return model.Raw*Mathf.Clamp01(1f-Math.Max(0f,practicalValue-model.Difficulty)/practiceFade);
    }

    public static Model ModelForRecipe(EntityPlayer player,string name)
    {
        Recipe recipe=string.IsNullOrEmpty(name)?null:CraftingManager.GetRecipe(name);
        return BuildModel(player,recipe,RebirthServiceCraftSkillService.ClassifyRecipe(name));
    }

    public static string PreviewDescription(Recipe recipe)
    {
        if(recipe==null||recipe.ingredients==null||recipe.ingredients.Count>12)return "";
        var root=new System.Xml.Linq.XElement("inputs",new System.Xml.Linq.XAttribute("seconds",recipe.craftingTime),
            new System.Xml.Linq.XAttribute("tier",recipe.craftingTier),new System.Xml.Linq.XAttribute("area",recipe.craftingArea??string.Empty),new System.Xml.Linq.XAttribute("tool",recipe.craftingToolType),
            new System.Xml.Linq.XAttribute("modified",recipe.UseIngredientModifier));
        foreach(var i in recipe.ingredients)
            if(i!=null&&!i.IsEmpty())root.Add(new System.Xml.Linq.XElement("i",
                new System.Xml.Linq.XAttribute("name",i.itemValue.ItemClass.GetItemName()),new System.Xml.Linq.XAttribute("count",i.count)));
        return root.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }
    public static ItemValue PreviewCarrier(Recipe recipe,string description)
    {
        var carrier=new ItemValue(recipe.itemValueType);
        carrier.SetMetadata("rebirth.craft.preview.inputs35",description);
        return carrier;
    }
    public static Recipe DecodePreview(string recipeName,ItemValue carrier)
    {
        if(carrier==null||carrier.ItemClass?.GetItemName()!=recipeName)return null;
        string text;
        if(!carrier.TryGetMetadata("rebirth.craft.preview.inputs35",out text)||string.IsNullOrEmpty(text)||text.Length>8192)return null;
        try
        {
            var root=System.Xml.Linq.XElement.Parse(text);
            if(root.Name.LocalName!="inputs")return null;
            Recipe canonical=CraftingManager.GetRecipe(recipeName);
            string area=(string)root.Attribute("area");
            if(area!=null&&area.Length>160)return null;
            var recipe=new Recipe { itemValueType=carrier.type, count=Math.Max(1,canonical==null?1:canonical.count),
                craftingArea=area??(canonical==null?"campfire":canonical.craftingArea), Effects=canonical==null?null:canonical.Effects,
                craftingTime=Number(root,"seconds",0f,0f,86400f),
                craftingToolType=(int)Number(root,"tool",0f,0f,65535f),
                UseIngredientModifier=string.Equals((string)root.Attribute("modified"),"true",StringComparison.OrdinalIgnoreCase),
                ingredients=new System.Collections.Generic.List<ItemStack>() };
            if(canonical!=null)recipe.craftingTier=canonical.craftingTier;
            foreach(var i in root.Elements("i"))
            {
                if(recipe.ingredients.Count>=12)return null;
                string name=(string)i.Attribute("name");
                int count=(int)Number(i,"count",0f,1f,1000000f);
                if(string.IsNullOrEmpty(name)||name.Length>160)return null;
                ItemValue value=ItemClass.GetItem(name);if(value.IsEmpty())return null;
                recipe.ingredients.Add(new ItemStack(value,count));
            }
            return recipe;
        }
        catch { return null; } // invalid UI input is not a recipe, and has no award path
    }

    private static float MaterialWeight(ItemClass item)
    {
        float weight;
        if (Materials.TryGetValue(item.GetItemName(), out weight)) return weight;
        float value = RebirthConsumableResolver.GetFloat(item, "EconomicValue", -1f);
        float bundle = RebirthConsumableResolver.GetFloat(item, "EconomicBundleSize", 1f);
        if (!Finite(value) || value < 0f || !Finite(bundle) || bundle <= 0f) return unknownWeight;
        return Mathf.Clamp(value / bundle / economicDivisor, 0.02f, 20f);
    }

    private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private static float Number(System.Xml.Linq.XElement element, string attribute, float fallback, float min, float max)
    {
        var a = element.Attribute(attribute); if (a == null) return fallback;
        float result;
        if (!float.TryParse(a.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result) ||
            !Finite(result) || result < min || result > max)
            throw new System.IO.InvalidDataException("Invalid crafting-training " + attribute + ": " + a.Value);
        return result;
    }
}
