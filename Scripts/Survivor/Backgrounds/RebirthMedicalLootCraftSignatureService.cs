using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// PC019 / Chunk F runtime consumers for the Paramedic, Pharmacist, Lab Technician and Librarian
/// Signature Bonuses. All mutation paths are Rebirth-only and server-authoritative where they
/// affect persistent/player-owned results.
/// </summary>
public static class RebirthMedicalLootCraftSignatureService
{
    public const string TraumaSpecialistBonusId = "background_bonus.trauma_specialist";
    public const string PharmacyEyeBonusId = "background_bonus.pharmacy_eye";
    public const string ReagentRecoveryBonusId = "background_bonus.reagent_recovery";
    public const string BookwormBonusId = "background_bonus.bookworm";
    public const string PatrolCarFamiliarityBonusId = "background_bonus.patrol_car_familiarity";

    public const string ParamedicAbrasionMarker = "rbParamedicAbrasionTreatment";
    public const string ParamedicLegMarker = "rbParamedicLegTreatment";
    public const string ParamedicArmMarker = "rbParamedicArmTreatment";

    // Implementation tuning, intentionally unlocked by the design. The locked values remain in
    // background_bonuses.xml: Trauma Specialist=2x treated healing, Bookworm=2x literature content.
    public const float PharmacyUsefulMedicationCopyChance = 0.75f;
    public const float ReagentRecoveryChance = 0.25f;

    private static readonly Dictionary<string,string> ReagentByRecipe = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        // Source-audited consumed reagents from the retained chemistry recipes. Do not broaden this
        // into generic ingredient refunding without a recipe-by-recipe source audit.
        { "drugHerbalAntibiotics", "resourcePotassiumNitratePowder" },
        { "drugSteroids", "resourceTestosteroneExtract" }
    };

    private static bool IsServerAuthoritativeWorld()
    {
        return RebirthSurvivorMode.IsEnabledForCurrentWorld()
            && GameManager.Instance != null
            && GameManager.Instance.World != null
            && !GameManager.Instance.World.IsRemote();
    }

    public static void OnSuccessfulMedicalTreatment(EntityPlayer healer, EntityAlive patient)
    {
        if (!IsServerAuthoritativeWorld() || healer == null || patient == null) return;
        if (!RebirthBackgroundBonusService.HasBonus(healer, TraumaSpecialistBonusId)) return;
        if (patient.Buffs == null) return;

        RebirthBackgroundBonusDefinition definition;
        float multiplier = 2f;
        if (RebirthBackgroundBonusService.TryGetSignatureBonus(healer, out definition) && definition != null)
        {
            RebirthBackgroundBonusTuningValue tuning;
            float parsed;
            if (definition.TryGetTuning("treated_healing_multiplier", out tuning)
                && tuning != null
                && float.TryParse(tuning.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed >= 1f)
                multiplier = parsed;
        }

        // Abrasion treatment quality is carried by healAbrasionMult for the lifetime of the treated
        // abrasion. Mark once so repeated treatment cannot compound the Signature Bonus.
        if (patient.Buffs.HasBuff("buffInjuryAbrasionTreated") && patient.Buffs.GetCustomVar(ParamedicAbrasionMarker) <= 0f)
        {
            float current = patient.Buffs.GetCustomVar("healAbrasionMult");
            if (current > 0f && !float.IsNaN(current) && !float.IsInfinity(current))
            {
                patient.Buffs.SetCustomVar("healAbrasionMult", current * multiplier);
                patient.Buffs.SetCustomVar(ParamedicAbrasionMarker, 1f);
            }
        }

        bool legTreated = patient.Buffs.HasBuff("buffLegSplinted") || patient.Buffs.HasBuff("buffLegCast");
        if (legTreated && patient.Buffs.GetCustomVar(ParamedicLegMarker) <= 0f)
        {
            float current = patient.Buffs.GetCustomVar("$legTreatedCritHealingBase");
            if (current > 0f && !float.IsNaN(current) && !float.IsInfinity(current))
            {
                patient.Buffs.SetCustomVar("$legTreatedCritHealingBase", current * multiplier);
                patient.Buffs.SetCustomVar(ParamedicLegMarker, 1f);
            }
        }

        bool armTreated = patient.Buffs.HasBuff("buffArmSplinted") || patient.Buffs.HasBuff("buffArmCast");
        if (armTreated && patient.Buffs.GetCustomVar(ParamedicArmMarker) <= 0f)
        {
            float current = patient.Buffs.GetCustomVar("$armTreatedCritHealingBase");
            if (current > 0f && !float.IsNaN(current) && !float.IsInfinity(current))
            {
                patient.Buffs.SetCustomVar("$armTreatedCritHealingBase", current * multiplier);
                patient.Buffs.SetCustomVar(ParamedicArmMarker, 1f);
            }
        }
    }

    public static void ApplySpecializedLoot(LootManager manager, TEFeatureStorage tile, EntityPlayer opener, bool wasAlreadyTouched, string originalLootList)
    {
        if (!IsServerAuthoritativeWorld() || manager == null || tile == null || opener == null || wasAlreadyTouched) return;
        if (!tile.ItemGrid.Touched || tile.ItemGrid.items == null || tile.ItemGrid.items.Length == 0) return;

        if (RebirthBackgroundBonusService.HasBonus(opener, BookwormBonusId) && IsLiteratureContainer(originalLootList))
            MultiplyExistingEligibleStacks(tile.ItemGrid.items, IsKnowledgeLiteratureItem, 2);

        if (RebirthBackgroundBonusService.HasBonus(opener, PharmacyEyeBonusId) && IsPharmaceuticalContainer(originalLootList))
            ApplyPharmacyCopies(manager, tile.ItemGrid.items);

        if (RebirthBackgroundBonusService.HasBonus(opener, PatrolCarFamiliarityBonusId) && IsPoliceVehicleContainer(originalLootList))
            MultiplyPoliceVehicleAmmunition(tile.ItemGrid, GetLockedPoliceAmmoMultiplier(opener));
    }

    public static void OnSuccessfulWorkstationCraft(TileEntityWorkstation workstation, int crafterEntityId, string recipeName, int craftedCount)
    {
        if (!IsServerAuthoritativeWorld() || workstation == null || craftedCount <= 0 || string.IsNullOrWhiteSpace(recipeName)) return;
        EntityPlayer crafter = GameManager.Instance.World.GetEntity(crafterEntityId) as EntityPlayer;
        if (crafter == null || !RebirthBackgroundBonusService.HasBonus(crafter, ReagentRecoveryBonusId)) return;

        string reagent;
        if (!ReagentByRecipe.TryGetValue(recipeName.Trim(), out reagent)) return;
        if (GameManager.Instance.World.GetGameRandom().RandomFloat >= ReagentRecoveryChance) return;

        AddRecoveredReagentToWorkstation(workstation, reagent, 1);
    }

    private static void ApplyPharmacyCopies(LootManager manager, ItemStack[] items)
    {
        for (int i=0;i<items.Length;i++)
        {
            ItemStack stack=items[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue == null || stack.itemValue.ItemClass == null) continue;
            string name=stack.itemValue.ItemClass.GetItemName();
            if (!IsUsefulPharmaceuticalItem(name)) continue;
            if (manager.Random.RandomFloat >= PharmacyUsefulMedicationCopyChance) continue;
            int max=Math.Max(1, stack.itemValue.ItemClass.Stacknumber.Value);
            int add=Math.Min(stack.count, Math.Max(0,max-stack.count));
            if(add>0){stack.count+=add;items[i]=stack;}
        }
    }

    private static void MultiplyExistingEligibleStacks(ItemStack[] items, Func<string,bool> eligible, int multiplier)
    {
        if(multiplier<=1)return;
        for(int i=0;i<items.Length;i++)
        {
            ItemStack stack=items[i];
            if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.ItemClass==null)continue;
            string name=stack.itemValue.ItemClass.GetItemName();
            if(!eligible(name))continue;
            int max=Math.Max(1,stack.itemValue.ItemClass.Stacknumber.Value);
            long wanted=(long)stack.count*multiplier;
            stack.count=(int)Math.Min(max,wanted);items[i]=stack;
        }
    }

    private static bool AddRecoveredReagentToWorkstation(TileEntityWorkstation workstation,string itemName,int count)
    {
        ItemValue value=ItemClass.GetItem(itemName,false);
        if(value==null||value.IsEmpty()||count<=0)return false;
        ItemStack[] output=workstation.Output;
        if(output==null||output.Length==0)return false;
        int max=Math.Max(1,value.ItemClass.Stacknumber.Value);
        int capacity=0;
        for(int i=0;i<output.Length;i++)
        {
            ItemStack stack=output[i];
            if(stack==null||stack.IsEmpty())capacity+=max;
            else if(stack.itemValue.type==value.type)capacity+=Math.Max(0,max-stack.count);
        }
        if(capacity<count)return false;
        int remaining=count;
        for(int i=0;i<output.Length&&remaining>0;i++)
        {
            ItemStack stack=output[i];
            if(stack==null||stack.IsEmpty()||stack.itemValue.type!=value.type)continue;
            int room=Math.Max(0,max-stack.count);int moved=Math.Min(room,remaining);stack.count+=moved;output[i]=stack;remaining-=moved;
        }
        for(int i=0;i<output.Length&&remaining>0;i++)
        {
            ItemStack stack=output[i];
            if(stack!=null&&!stack.IsEmpty())continue;
            int moved=Math.Min(max,remaining);output[i]=new ItemStack(value.Clone(),moved);remaining-=moved;
        }
        workstation.Output=output;
        return true;
    }


    private static int GetLockedPoliceAmmoMultiplier(EntityPlayer opener)
    {
        RebirthBackgroundBonusDefinition d;RebirthBackgroundBonusTuningValue t;float v;
        if(RebirthBackgroundBonusService.TryGetSignatureBonus(opener,out d)&&d!=null&&d.TryGetTuning("ammunition_multiplier",out t)&&t!=null&&float.TryParse(t.Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v))return Math.Max(1,(int)Math.Round(v));
        return 3;
    }

    private sealed class AmmoExtra{public ItemValue Prototype;public int Remaining;}
    private static void MultiplyPoliceVehicleAmmunition(ItemStackGrid grid,int multiplier)
    {
        ItemStack[] items=grid?.items;if(items==null||multiplier<=1)return;Dictionary<int,AmmoExtra> extras=new Dictionary<int,AmmoExtra>();
        for(int i=0;i<items.Length;i++){ItemStack s=items[i];if(s==null||s.IsEmpty()||s.itemValue==null||s.itemValue.ItemClass==null)continue;string n=s.itemValue.ItemClass.GetItemName();if(!IsAmmunitionItem(n))continue;AmmoExtra e;if(!extras.TryGetValue(s.itemValue.type,out e)){e=new AmmoExtra{Prototype=s.itemValue.Clone(),Remaining=0};extras[s.itemValue.type]=e;}long add=(long)s.count*(multiplier-1);e.Remaining+=(int)Math.Min(int.MaxValue,add);}
        foreach(KeyValuePair<int,AmmoExtra> kv in extras){AmmoExtra e=kv.Value;int max=Math.Max(1,e.Prototype.ItemClass.Stacknumber.Value);for(int i=0;i<items.Length&&e.Remaining>0;i++){ItemStack s=items[i];if(s==null||s.IsEmpty()||s.itemValue.type!=kv.Key)continue;int room=Math.Max(0,max-s.count);int moved=Math.Min(room,e.Remaining);s.count+=moved;items[i]=s;e.Remaining-=moved;}for(int i=0;i<items.Length&&e.Remaining>0;i++){ItemStack s=items[i];if(s!=null&&!s.IsEmpty())continue;int moved=Math.Min(max,e.Remaining);grid.SetItem(i,new ItemStack(e.Prototype.Clone(),moved));e.Remaining-=moved;}}
    }

    private static bool IsPoliceVehicleContainer(string lootList)
    {
        return string.Equals(lootList,"policeCars",StringComparison.OrdinalIgnoreCase)||string.Equals(lootList,"policeCarsBonus",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAmmunitionItem(string name)
    {
        return !string.IsNullOrEmpty(name)&&name.StartsWith("ammo",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLiteratureContainer(string lootList)
    {
        string s=(lootList??string.Empty).ToLowerInvariant();
        return s.Contains("bookcase")||s.Contains("bookpile")||s.Contains("crackabook")||s.Contains("bookshelf");
    }

    private static bool IsPharmaceuticalContainer(string lootList)
    {
        string s=(lootList??string.Empty).ToLowerInvariant();
        return s.Contains("medicine")||s.Contains("medical")||s.Contains("pharma")||s.Contains("pill")||s.Contains("drug");
    }

    private static bool IsKnowledgeLiteratureItem(string name)
    {
        if(string.IsNullOrEmpty(name))return false;
        return name.StartsWith("rebirthFieldNotes",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthTheory",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthRecipe",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthManual",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthSchematic",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthGuide",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthCookbook",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthFormula",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthPattern",StringComparison.OrdinalIgnoreCase)
            ||name.StartsWith("rebirthAudio",StringComparison.OrdinalIgnoreCase) && name.EndsWith("Cassette",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsefulPharmaceuticalItem(string name)
    {
        if(string.IsNullOrEmpty(name))return false;
        return name.StartsWith("drug",StringComparison.OrdinalIgnoreCase)
            ||name.Equals("medicalBloodBag",StringComparison.OrdinalIgnoreCase)
            ||name.Equals("foodHoney",StringComparison.OrdinalIgnoreCase)
            ||name.Equals("rebirthSupportNicotinePatch",StringComparison.OrdinalIgnoreCase)
            ||name.Equals("rebirthSupportProbioticCapsules",StringComparison.OrdinalIgnoreCase)
            ||name.Equals("rebirthSupportRespiratoryInhaler",StringComparison.OrdinalIgnoreCase);
    }
}
