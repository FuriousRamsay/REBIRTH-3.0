using System;
using System.Text;

#nullable disable

public static class RebirthServiceCraftSkillVectorHarness
{
    private static int pass,fail;
    public static string RunAll()
    {
        pass=0;fail=0;StringBuilder b=new StringBuilder(); b.AppendLine("[REBIRTH Survivor Chunk 8 Service/Crafting vectors]");
        Check(b,"zero craft neutral",Near(RebirthServiceCraftSkillService.SignedEndpoint(0f,.2f,-.2f),0f));
        Check(b,"weak craft slower",RebirthServiceCraftSkillService.SignedEndpoint(-50f,.2f,-.2f)>0f);
        Check(b,"strong craft faster",RebirthServiceCraftSkillService.SignedEndpoint(100f,.2f,-.2f)<0f);
        Check(b,"weak repair amount lower",RebirthServiceCraftSkillService.SignedEndpoint(-50f,-.2f,.25f)<0f);
        Check(b,"strong repair amount higher",RebirthServiceCraftSkillService.SignedEndpoint(100f,-.2f,.25f)>0f);
        Check(b,"weak mechanics repair lower",RebirthServiceCraftSkillService.SignedEndpoint(-50f,-.2f,.3f)<0f);
        Check(b,"strong mechanics repair higher",RebirthServiceCraftSkillService.SignedEndpoint(100f,-.2f,.3f)>0f);
        Check(b,"weak medicine lower",RebirthServiceCraftSkillService.SignedEndpoint(-50f,-.15f,.2f)<0f);
        Check(b,"strong medicine higher",RebirthServiceCraftSkillService.SignedEndpoint(100f,-.15f,.2f)>0f);
        Check(b,"medicine reserve neutral at zero",Near(RebirthServiceCraftSkillService.SignedEndpoint(0f,RebirthProgressionRuntimeConfig.MedicineReserveNegative,RebirthProgressionRuntimeConfig.MedicineReservePositive),0f));
        Check(b,"medicine fracture weak slower",RebirthServiceCraftSkillService.SignedEndpoint(-50f,RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative,RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive)<0f);
        Check(b,"medicine fracture strong faster",RebirthServiceCraftSkillService.SignedEndpoint(100f,RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative,RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive)>0f);
        float infectionTransferZero=RebirthServiceCraftSkillService.ComputeMedicineInfectionTransfer(0f,25f,40f);
        float infectionTransferMax=RebirthServiceCraftSkillService.ComputeMedicineInfectionTransfer(100f,25f,40f);
        float expectedTransfer=25f*Math.Min(1f,Math.Max(0f,RebirthProgressionRuntimeConfig.MedicineInfectionAccelerationPositive));
        Check(b,"medicine infection neutral at zero",Near(infectionTransferZero,0f));
        Check(b,"medicine infection max transfer bounded",Near(infectionTransferMax,expectedTransfer)&&infectionTransferMax<=25f&&infectionTransferMax<=40f);
        Check(b,"medicine infection cure budget conserved",Near(infectionTransferMax+(25f-infectionTransferMax),25f));
        Check(b,"medicine infection cannot transfer more infection than remains",RebirthServiceCraftSkillService.ComputeMedicineInfectionTransfer(100f,25f,2f)<=2f+.0001f);
        string[] ids={"skill.maintenance","skill.gunsmithing","skill.cooking","skill.medicine","skill.chemistry","skill.mechanics","skill.metalworking"};
        for(int i=0;i<ids.Length;i++)Check(b,"chunk8 id "+ids[i],RebirthServiceCraftSkillService.IsChunk8Skill(ids[i]));
        Check(b,"electrical craft route included",RebirthServiceCraftSkillService.IsChunk8Skill("skill.electrical"));
        Check(b,"unrelated weapon skill excluded",!RebirthServiceCraftSkillService.IsChunk8Skill("skill.pistols"));
        Check(b,"cooking fallback",RebirthServiceCraftSkillService.ClassifyRecipe("foodGrilledMeat")=="skill.cooking");
        Check(b,"drink preparation",RebirthServiceCraftSkillService.ClassifyRecipe("drinkJarRedTea")=="skill.drink_preparation");
        Check(b,"chemistry fallback",RebirthServiceCraftSkillService.ClassifyRecipe("resourceGunPowder")=="skill.chemistry");
        Check(b,"metal fallback",RebirthServiceCraftSkillService.ClassifyRecipe("resourceForgedSteel")=="skill.metalworking");
        Check(b,"gunsmith part fallback",RebirthServiceCraftSkillService.ClassifyRecipe("gunPartReceiver")=="skill.gunsmithing");
        Check(b,"unknown craft excluded",RebirthServiceCraftSkillService.ClassifyRecipe("resourceWood").Length==0);
        Check(b,"cooking award positive",RebirthServiceCraftSkillService.GetCraftAward("skill.cooking",1)>0f);
        Check(b,"chemistry award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.chemistry",99)<=RebirthProgressionRuntimeConfig.ChemistryMax+.0001f);
        Check(b,"metal award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.metalworking",99)<=RebirthProgressionRuntimeConfig.MetalworkingMax+.0001f);
        Check(b,"gunsmith craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.gunsmithing",99)<=RebirthProgressionRuntimeConfig.GunsmithingCraftMax+.0001f);
        Check(b,"maintenance craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.maintenance",99)<=RebirthProgressionRuntimeConfig.MaintenanceCraftMax+.0001f);
        Check(b,"mechanics craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.mechanics",99)<=RebirthProgressionRuntimeConfig.MechanicsCraftMax+.0001f);
        Check(b,"farming craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.farming",99)<=RebirthProgressionRuntimeConfig.FarmingCraftMax+.0001f);
        Check(b,"explosives craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.explosives",99)<=RebirthProgressionRuntimeConfig.ExplosivesCraftMax+.0001f);
        Check(b,"turret craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.deployable_turrets",99)<=RebirthProgressionRuntimeConfig.TurretsCraftMax+.0001f);
        Check(b,"medicine craft award capped",RebirthServiceCraftSkillService.GetCraftAward("skill.medicine",99)<=RebirthProgressionRuntimeConfig.MedicineCraftMax+.0001f);
        Check(b,"metal heat-treatment persistent grade enabled by Chunk G",RebirthServiceCraftSkillService.MetalworkingPersistentHeatTreatmentGradeEnabled);
        b.AppendLine("result="+(fail==0?"PASS":"FAIL")+" pass="+pass+" fail="+fail+" total="+(pass+fail)); return b.ToString();
    }
    private static bool Near(float a,float b){return Math.Abs(a-b)<.0001f;}
    private static void Check(StringBuilder b,string n,bool ok){if(ok){pass++;b.AppendLine("PASS "+n);}else{fail++;b.AppendLine("FAIL "+n);}}
}
