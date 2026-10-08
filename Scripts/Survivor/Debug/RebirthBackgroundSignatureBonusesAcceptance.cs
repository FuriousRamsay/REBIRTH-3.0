using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// PC029 read-only release acceptance surface for Background Signature Bonuses and their
/// supporting systems. It intentionally does not create entitlement or mutate gameplay state.
/// </summary>
public static class RebirthBackgroundSignatureBonusesAcceptance
{
    public static string BuildSummary(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        int backgrounds=bundle!=null?bundle.Backgrounds.Count:0;
        int skills=bundle!=null&&bundle.Progression!=null?bundle.Progression.Skills.Count:0;
        int knowledge=bundle!=null&&bundle.Progression!=null?bundle.Progression.Knowledge.Count:0;
        int expectedSkills=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds().Length;
        if(!RebirthProgressionGraphRegistry.IsReady)
        {
            try { RebirthProgressionGraphRegistry.BuildFromCurrentAuthority(); }
            catch { }
        }
        b.AppendLine("[REBIRTH Background Signature Bonuses Release Acceptance]");
        b.AppendLine("parent=REBIRTH-3.0-BACKGROUND-SIGNATURE-BONUSES-001 chunk=P pc=029 status=SOURCE_STATIC_COMPLETE_LIVE_ACCEPTANCE_REQUIRED");
        b.AppendLine("definitionsReady="+RebirthSurvivorDefinitionRegistry.IsReady+" backgrounds="+backgrounds+" expectedBackgrounds=28 bonuses="+RebirthBackgroundBonusRegistry.Count+" expectedBonuses=27 bonusSchema="+RebirthBackgroundBonusRegistry.SchemaVersion);
        b.AppendLine("skills="+skills+" expectedSkills="+expectedSkills+" knowledge="+knowledge+" worldSchema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" networkProtocol="+RebirthSurvivorNetworkProtocol.Version);
        b.AppendLine("itemProvenanceVersion="+RebirthItemProvenanceAdapter.CurrentVersion+" metabolismVersion="+RebirthMetabolismState.CurrentVersion+" bonusOwnership=derived_from_background noSecondEntitlement=True");
        b.AppendLine("progressionGraphReady="+RebirthProgressionGraphRegistry.IsReady+" nodes="+RebirthProgressionGraphRegistry.NodeCount+" edges="+RebirthProgressionGraphRegistry.EdgeCount);
        b.AppendLine("authority repositoryServer="+RebirthWorldCharacterRepository.IsServerAuthority+" debugMutationGate="+RebirthSurvivorDebug.Enabled+" rebirthMode="+RebirthSurvivorMode.IsEnabledForCurrentWorld());
        b.AppendLine("runtimeEvidenceRequired=compile,sp,migration,p2p,dedicated,save_reload,handoff,performance");
        if(player!=null)
        {
            RebirthBackgroundBonusDefinition bonus=RebirthBackgroundBonusService.GetSignatureBonus(player);
            b.AppendLine("player="+player.entityId+" signatureBonus="+(bonus!=null?bonus.Id:"none")+" background="+(bonus!=null?bonus.BackgroundId:"clean_slate_or_unavailable"));
            b.AppendLine(RebirthSkillWaveAService.BuildDebugReport(player));
            b.AppendLine(RebirthRepairSignatureService.BuildDebugReport(player));
            b.AppendLine(RebirthWorkmanshipSignatureService.BuildDebugReport(player,null));
            b.AppendLine(RebirthResourceSignatureService.BuildDebugReport(player));
            b.AppendLine(RebirthCombatPatrolTrackingSignatureService.BuildDebugReport(player));
            b.AppendLine(RebirthFoodFarmingButcherySignatureService.BuildDebugReport(player,null));
            b.AppendLine(RebirthDrinkPreparationSignatureService.BuildDebugReport(player));
            b.AppendLine(RebirthScavengerSalvageProfileService.BuildDebugReport(player,null));
        }
        return b.ToString().TrimEnd();
    }

    public static string BuildBalanceDefaults()
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Background Signature Bonus Balance Defaults]");
        b.AppendLine("bonusId,backgroundId,category,tuningKey,value,locked");
        IList<RebirthBackgroundBonusDefinition> all=RebirthBackgroundBonusRegistry.All;
        for(int i=0;i<all.Count;i++)
        {
            RebirthBackgroundBonusDefinition d=all[i];
            if(d==null)continue;
            if(d.Tuning==null||d.Tuning.Count==0)
            {
                b.AppendLine(d.Id+","+d.BackgroundId+","+d.Category+",<none>,<handler-authored>,false");
                continue;
            }
            for(int j=0;j<d.Tuning.Count;j++)
            {
                RebirthBackgroundBonusTuningValue t=d.Tuning[j];if(t==null)continue;
                b.AppendLine(d.Id+","+d.BackgroundId+","+d.Category+","+t.Key+","+t.Value+","+t.Locked);
            }
        }
        b.AppendLine("NOTE: locked values are design-locked; unlocked values remain balance tuning data. Runtime authority always comes from the registry/owning service.");
        return b.ToString().TrimEnd();
    }

    public static string BuildAuthorityReport()
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Background Signature Bonus Authority]");
        b.AppendLine("ownership=selected Background -> RebirthBackgroundBonusRegistry; duplicate entitlement field=False");
        b.AppendLine("loot/harvest/salvage/trading/teaching/combat awards=server-authoritative or server-validated");
        b.AppendLine("crafted item state=RebirthItemProvenanceAdapter v"+RebirthItemProvenanceAdapter.CurrentVersion+" stackCompatibility=provenance-aware");
        b.AppendLine("world character persistence=schema "+RebirthWorldCharacterRecord.CurrentSchemaVersion+" stable-player identity scoped; deleted/recreated world uses save-scoped repositories");
        b.AppendLine("metabolism ingestion persistence=v"+RebirthMetabolismState.CurrentVersion+" prepared meal/drink snapshots survive normal save/load");
        b.AppendLine("placed construction/trap provenance=server world sidecar; electrical provenance=server infrastructure state; Farmer crop provenance=versioned crop tile payload");
        b.AppendLine("UI=read-only projection of authoritative registry/services; Base Game progression paths remain native");
        b.AppendLine("debug mutations=require RebirthSurvivorDebug gate; this acceptance surface is read-only");
        return b.ToString().TrimEnd();
    }

    public static string BuildMigrationReport(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();
        int expected=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds().Length;
        b.AppendLine("[REBIRTH Background Signature Bonus Migration]");
        b.AppendLine("targetWorldSchema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" currentSkillCatalogue="+expected+" bonusRegistrySchema="+RebirthBackgroundBonusRegistry.SchemaVersion+" itemProvenanceVersion="+RebirthItemProvenanceAdapter.CurrentVersion+" metabolismVersion="+RebirthMetabolismState.CurrentVersion);
        b.AppendLine("schema15To16=missing-only Skill/SkillKnowledge reconciliation; existing signed Skill values/progress preserved; Teacher runtime state retained");
        b.AppendLine("signatureBonusEntitlementMigration=none ownership remains derived from Background");
        b.AppendLine("newSkillHistory=AnimalHandling/BlackMagic/Rage via prior discipline schemas; DrinkPreparation+Trading schema14; Teaching schema15; final 48-Skill reconcile schema16");
        if(player!=null)
        {
            RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
            if(RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)&&identity!=null&&RebirthWorldCharacterRepository.TryGet(identity,out record)&&record!=null)
            {
                b.AppendLine("player="+player.entityId+" storedSchema="+record.SchemaVersion+" migrationSource="+record.MigrationSourceSchema+" migrationTarget="+record.MigrationTargetSchema+" migrationApplied="+record.MigrationApplied);
                b.AppendLine("migrationPolicy="+(record.MigrationPolicyId??string.Empty));
                b.AppendLine("migrationProgressionAudit="+(record.MigrationProgressionAudit??string.Empty));
            }
            else b.AppendLine("player="+player.entityId+" committedServerRecord=<unavailable on this authority>");
        }
        return b.ToString().TrimEnd();
    }

    public static string RunVectors()
    {
        StringBuilder b=new StringBuilder();
        b.AppendLine("[REBIRTH Background Signature Bonuses Consolidated Vectors]");
        b.AppendLine(RebirthRepairSignatureVectorHarness.Run().TrimEnd());
        b.AppendLine(RebirthSkillWaveAVectorHarness.RunAll().TrimEnd());
        IList<string> teaching=RebirthTeachingVectorHarness.Run();
        b.AppendLine("[REBIRTH Teaching Vectors]");for(int i=0;i<teaching.Count;i++)b.AppendLine(teaching[i]);
        b.AppendLine(RebirthResourceFieldSkillVectorHarness.RunAll().TrimEnd());
        b.AppendLine(RebirthCombatPatrolTrackingSignatureService.RunVectors().TrimEnd());
        b.AppendLine(RebirthFoodFarmingButcherySignatureService.RunVectors().TrimEnd());
        b.AppendLine(RebirthDrinkPreparationSignatureService.RunVectors().TrimEnd());
        b.AppendLine(RebirthScavengerSalvageProfileService.RunVectors().TrimEnd());
        b.AppendLine(RebirthSurvivorMigrationVectorHarness.RunAll().TrimEnd());
        b.AppendLine(RebirthAdvancedDisciplinesAcceptance.RunVectors().TrimEnd());
        return b.ToString().TrimEnd();
    }
}
