using System;
using System.Text;

#nullable disable

/// <summary>Read-only in-game smoke vectors for the Chunk G crafting-policy / grouped-Knowledge runtime contract.</summary>
public static class RebirthCraftingProgressionVectorHarness
{
    public static string RunAll()
    {
        int pass=0,fail=0;
        StringBuilder b=new StringBuilder();
        Check(b,ref pass,ref fail,"registry ready",RebirthCraftingProgressionRegistry.IsReady);
        Check(b,ref pass,ref fail,"manifest count",RebirthCraftingProgressionRegistry.RecipeCount==656);
        Check(b,ref pass,ref fail,"gated count",RebirthCraftingProgressionRegistry.GatedCount==504);
        Check(b,ref pass,ref fail,"universal count",RebirthCraftingProgressionRegistry.UniversalCount==134);
        Check(b,ref pass,ref fail,"disabled count",RebirthCraftingProgressionRegistry.DisabledCount==18);
        Check(b,ref pass,ref fail,"live Capability count",RebirthCraftingProgressionRegistry.LiveCapabilityCount==504);
        Check(b,ref pass,ref fail,"planned Capability count",RebirthCraftingProgressionRegistry.PlannedCapabilityCount==0);

        RebirthCraftingProgressionDefinition d;
        bool frame=RebirthCraftingProgressionRegistry.TryGetRecipe("frameShapes:VariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"frame policy universal",frame&&d.IsUniversal);
        Check(b,ref pass,ref fail,"frame primary Construction",frame&&d.PrimarySkillId=="skill.construction");
        bool steel=RebirthCraftingProgressionRegistry.TryGetRecipe("steelShapes:VariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"steel policy gated",steel&&d.IsGated);
        Check(b,ref pass,ref fail,"steel structural gate live",steel&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool woodShapes=RebirthCraftingProgressionRegistry.TryGetRecipe("woodShapes:VariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"wood shapes remain universal",woodShapes&&d.IsUniversal&&d.PrimarySkillId=="skill.construction");
        bool concrete=RebirthCraftingProgressionRegistry.TryGetRecipe("concreteShapes:VariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"concrete structural gate live",concrete&&d.IsGated&&d.HasLiveCapability);
        bool vaultPowered=RebirthCraftingProgressionRegistry.TryGetRecipe("vaultDoor01_Powered",out d)&&d!=null;
        Check(b,ref pass,ref fail,"powered vault gate live",vaultPowered&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool spikes=RebirthCraftingProgressionRegistry.TryGetRecipe("trapSpikesIronDmg0",out d)&&d!=null;
        Check(b,ref pass,ref fail,"iron defensive gate live",spikes&&d.IsGated&&d.HasLiveCapability);
        bool primitiveMelee=RebirthCraftingProgressionRegistry.TryGetRecipe("meleeWpnClubT0WoodenClub",out d)&&d!=null;
        Check(b,ref pass,ref fail,"primitive melee universal",primitiveMelee&&d.IsUniversal);
        bool melee=RebirthCraftingProgressionRegistry.TryGetRecipe("meleeWpnClubT3SteelClub",out d)&&d!=null;
        Check(b,ref pass,ref fail,"steel club live gated",melee&&d.IsGated&&d.HasLiveCapability);
        Check(b,ref pass,ref fail,"melee fabrication Metalworking",melee&&d.PrimarySkillId=="skill.metalworking");
        bool stun=RebirthCraftingProgressionRegistry.TryGetRecipe("meleeWpnBatonT2StunBaton",out d)&&d!=null;
        Check(b,ref pass,ref fail,"stun baton live gated",stun&&d.IsGated&&d.HasLiveCapability);
        Check(b,ref pass,ref fail,"stun baton primary Metalworking",stun&&d.PrimarySkillId=="skill.metalworking");
        bool explosiveArrow=RebirthCraftingProgressionRegistry.TryGetRecipe("ammoArrowExploding",out d)&&d!=null;
        Check(b,ref pass,ref fail,"exploding arrow primary Explosives",explosiveArrow&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.explosives");
        bool gas=RebirthCraftingProgressionRegistry.TryGetRecipe("ammoGasCan",out d)&&d!=null;
        Check(b,ref pass,ref fail,"gas can primary Chemistry",gas&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.chemistry");
        bool disabled=RebirthCraftingProgressionRegistry.TryGetRecipe("gunHandgunT1Pistol",out d)&&d!=null;
        Check(b,ref pass,ref fail,"modern firearm disabled",disabled&&d.IsDisabled);
        string primary;
        bool owned=RebirthCraftingProgressionRegistry.TryGetPrimarySkill("cntWoodFurnitureBlockVariantHelper",out primary);
        Check(b,ref pass,ref fail,"furniture Construction ownership",owned&&primary=="skill.construction");
        bool woodFurniture=RebirthCraftingProgressionRegistry.TryGetRecipe("cntWoodFurnitureBlockVariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"wood furniture gate live",woodFurniture&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool upholstery=RebirthCraftingProgressionRegistry.TryGetRecipe("couchModernBlockVariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"upholstered furniture gate live",upholstery&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool secureStorage=RebirthCraftingProgressionRegistry.TryGetRecipe("cntGunSafeVariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"secure storage gate live",secureStorage&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool poweredDecor=RebirthCraftingProgressionRegistry.TryGetRecipe("tvBlockVariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"powered decor gate live",poweredDecor&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        bool appliance=RebirthCraftingProgressionRegistry.TryGetRecipe("appliancesVariantHelper",out d)&&d!=null;
        Check(b,ref pass,ref fail,"appliance gate live",appliance&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.construction");
        Check(b,ref pass,ref fail,"all owned gated live",RebirthCraftingProgressionRegistry.PlannedCapabilityCount==0&&RebirthCraftingProgressionRegistry.LiveCapabilityCount==RebirthCraftingProgressionRegistry.GatedCount);
        bool basicMedicine=RebirthCraftingProgressionRegistry.TryGetRecipe("medicalBandage",out d)&&d!=null;
        Check(b,ref pass,ref fail,"basic medicine universal",basicMedicine&&d.IsUniversal&&d.PrimarySkillId=="skill.medicine");
        bool advancedArmor=RebirthCraftingProgressionRegistry.TryGetRecipe("armorAssassinOutfit",out d)&&d!=null;
        Check(b,ref pass,ref fail,"advanced armor live",advancedArmor&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.tailoring");
        bool salvageTool=RebirthCraftingProgressionRegistry.TryGetRecipe("meleeToolSalvageT3ImpactDriver",out d)&&d!=null;
        Check(b,ref pass,ref fail,"salvage tool fabrication Metalworking",salvageTool&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.metalworking");
        bool rocketCasing=RebirthCraftingProgressionRegistry.TryGetRecipe("resourceRocketCasing",out d)&&d!=null;
        Check(b,ref pass,ref fail,"rocket casing fabrication Metalworking",rocketCasing&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.metalworking");
        bool vehiclePart=RebirthCraftingProgressionRegistry.TryGetRecipe("vehicleMotorcycleChassis",out d)&&d!=null;
        Check(b,ref pass,ref fail,"vehicle component live",vehiclePart&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.mechanics");
        bool maintenanceMod=RebirthCraftingProgressionRegistry.TryGetRecipe("modMeleeStunBatonRepulsor",out d)&&d!=null;
        Check(b,ref pass,ref fail,"maintenance mod live",maintenanceMod&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.maintenance");
        bool advancedExplosive=RebirthCraftingProgressionRegistry.TryGetRecipe("thrownTimedCharge",out d)&&d!=null;
        Check(b,ref pass,ref fail,"advanced explosive live",advancedExplosive&&d.IsGated&&d.HasLiveCapability&&d.PrimarySkillId=="skill.explosives");
        Check(b,ref pass,ref fail,"unknown external not intercepted",!RebirthCraftingProgressionRegistry.RequiresServerAuthorization("thirdPartyExampleRecipe"));

        RebirthCraftingProgressionDefinition stunKnowledge;
        bool stunK=RebirthCraftingProgressionRegistry.TryGetRecipe("meleeWpnBatonT2StunBaton",out stunKnowledge)&&stunKnowledge!=null;
        Check(b,ref pass,ref fail,"stun baton grouped Knowledge",stunK&&HasKnowledge(stunKnowledge,"pattern.metalworking.weapon_forging"));
        RebirthCraftingProgressionDefinition upholsteryKnowledge;
        bool upholsteryK=RebirthCraftingProgressionRegistry.TryGetRecipe("couchModernBlockVariantHelper",out upholsteryKnowledge)&&upholsteryKnowledge!=null;
        Check(b,ref pass,ref fail,"upholstery grouped Knowledge",upholsteryK&&HasKnowledge(upholsteryKnowledge,"procedure.construction.upholstery"));
        RebirthCraftingProgressionDefinition secureKnowledge;
        bool secureK=RebirthCraftingProgressionRegistry.TryGetRecipe("cntGunSafeVariantHelper",out secureKnowledge)&&secureKnowledge!=null;
        Check(b,ref pass,ref fail,"secure storage grouped Knowledge",secureK&&HasKnowledge(secureKnowledge,"procedure.construction.secure_storage"));
        RebirthCraftingProgressionDefinition armorKnowledge;
        bool armorK=RebirthCraftingProgressionRegistry.TryGetRecipe("armorAssassinOutfit",out armorKnowledge)&&armorKnowledge!=null;
        Check(b,ref pass,ref fail,"armor grouped Knowledge",armorK&&HasKnowledge(armorKnowledge,"pattern.tailoring.assassin_armor"));
        RebirthCraftingProgressionDefinition explosiveKnowledge;
        bool explosiveK=RebirthCraftingProgressionRegistry.TryGetRecipe("ammoArrowExploding",out explosiveKnowledge)&&explosiveKnowledge!=null;
        Check(b,ref pass,ref fail,"explosive projectile grouped Knowledge",explosiveK&&HasKnowledge(explosiveKnowledge,"formula.explosives.projectile_payloads"));

        b.Insert(0,"[REBIRTH CraftingProgression Chunk G vectors] "+(fail==0?"PASS":"FAIL")+" pass="+pass+" fail="+fail+" total="+(pass+fail)+"\n");
        return b.ToString().TrimEnd();
    }

    private static bool HasKnowledge(RebirthCraftingProgressionDefinition definition,string knowledgeId)
    {
        if(definition==null||string.IsNullOrEmpty(knowledgeId)||definition.KnowledgeIds==null)return false;
        for(int i=0;i<definition.KnowledgeIds.Count;i++)
            if(string.Equals(definition.KnowledgeIds[i],knowledgeId,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private static void Check(StringBuilder b,ref int pass,ref int fail,string name,bool ok)
    {
        if(ok)pass++;else fail++;
        b.Append(ok?"PASS ":"FAIL ").Append(name).AppendLine();
    }
}
