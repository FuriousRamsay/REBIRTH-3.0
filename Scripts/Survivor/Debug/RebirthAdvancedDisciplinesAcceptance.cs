using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

#nullable disable

/// <summary>Read-only final acceptance contract for Advanced Disciplines chunks A-K.</summary>
public static class RebirthAdvancedDisciplinesAcceptance
{
    public static string BuildSummary(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        b.AppendLine("[REBIRTH Advanced Disciplines Final Acceptance]");
        b.AppendLine("stage=11/11 sourceStaticComplete=True runtimeAcceptanceRequired=True releaseApproved=False");
        int skillCount=RebirthSurvivorDefinitionRegistry.Bundle?.Progression?.Skills?.Count??0;int expectedSkillCount=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds().Length;b.AppendLine("definitions="+RebirthAdvancedDisciplineRegistry.DefinitionCount+" expected=3 skills="+skillCount+" expectedSkills="+expectedSkillCount+" worldSchema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" networkProtocol="+RebirthSurvivorNetworkProtocol.Version);
        b.AppendLine("modeRebirth="+RebirthSurvivorMode.IsEnabledForCurrentWorld()+" isServer="+(c!=null&&c.IsServer)+" repositoryServer="+RebirthWorldCharacterRepository.IsServerAuthority+" debugMutationGate="+RebirthSurvivorDebug.Enabled);
        b.AppendLine("pc002ZombieAnimalBoundary=BeastmasterDenied;BlackMagicAuthoredOnly");
        b.AppendLine("rage dataDriven=True bloodMoonOnly="+RebirthRageService.BloodRequiresBloodMoon+" bloodMoonVisible="+SkyManager.IsBloodMoonVisible()+" energy="+F(RebirthRageService.ActivationEnergy)+" duration="+F(RebirthRageService.DurationSeconds));
        b.AppendLine("specialPanthers horror="+RebirthSpecialPantherService.HorrorPantherClass+" rage="+RebirthSpecialPantherService.RagePantherClass+" serverCommitAuthority=True");
        b.AppendLine("summoningAssetsAvailable=False failClosed=True");
        b.AppendLine("runtimeRolesRequired=single_player_host,p2p_host,p2p_client,dedicated_client");
        b.AppendLine("runtimeScenariosRequired=save_migration,reconnect,death_respawn,companion_capacity,animal_taming,zombie_binding,temporary_domination,bound_undead,panther_deployment,rage_energy,rage_capsule,blood_moon,base_game_isolation");
        b.AppendLine("releaseBlockers=live_compile,live_single_player,live_p2p,live_dedicated,live_migration,live_performance; static diagnostics never self-approve these gates");
        if(player!=null){b.AppendLine("player="+player.entityId);b.Append(RebirthRageService.BuildStatus(player).TrimEnd());b.AppendLine();b.Append(RebirthSpecialPantherService.BuildStatus(player).TrimEnd());}
        return b.ToString().TrimEnd();
    }

    public static string BuildBalance()
    {
        XDocument d=XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath);XElement r=d.Root?.Element("tunables")?.Element("rage"),b=d.Root?.Element("tunables")?.Element("beastmaster_animal_capacity"),z=d.Root?.Element("tunables")?.Element("zombie_control_capacity");
        StringBuilder s=new StringBuilder();s.AppendLine("[REBIRTH Advanced Disciplines Balance Contract]");
        if(r!=null)s.AppendLine("rage breadth="+A(r,"breadth_families")+"@"+A(r,"breadth_level")+" depth="+A(r,"depth_families")+"@"+A(r,"depth_level")+" trialHits="+A(r,"trial_melee_hits")+" energy="+A(r,"activation_energy")+" offensiveExtra="+A(r,"offensive_energy_extra")+" bloodExtra="+A(r,"blood_energy_extra")+" duration="+A(r,"duration_seconds")+" repeat="+A(r,"target_repeat_seconds")+" award="+A(r,"award_min")+".."+A(r,"award_max")+" bloodMoonOnly="+A(r,"blood_requires_blood_moon"));
        if(b!=null)s.AppendLine("animalCapacity base="+A(b,"base_at_skill_30")+" step="+A(b,"skill_step")+" max="+A(b,"max_capacity"));
        if(z!=null){StringBuilder x=new StringBuilder();foreach(XElement e in z.Elements("tier")){if(x.Length>0)x.Append(',');x.Append(A(e,"id")).Append('=').Append(A(e,"weight"));}s.AppendLine("zombieControlWeights "+x);}
        s.AppendLine("NOTE: these are authored balance values, not live telemetry. Adjust only after recorded SP/P2P/dedicated acceptance evidence.");return s.ToString().TrimEnd();
    }

    public static string BuildAuthority()
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Advanced Disciplines Authority Contract]");
        b.AppendLine("connectionPresent="+(c!=null)+" isServer="+(c!=null&&c.IsServer)+" repositoryServer="+RebirthWorldCharacterRepository.IsServerAuthority);
        b.AppendLine("AnimalHandlingLBD=server BeastmasterTameCommit=server BlackMagicControl=server BoundUndeadCommit=server PantherDeployCommit=server RageActivation=server RageCapsuleConsume=server");
        b.AppendLine("ownerStateProtocol="+RebirthSurvivorNetworkProtocol.Version+" persistenceSchema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" stableIdentityRequired=True");
        b.AppendLine("clientMutationAuthority=False baseGameCustomDisciplineAuthority=False debugMutationGate="+RebirthSurvivorDebug.Enabled);return b.ToString().TrimEnd();
    }

    public static string RunVectors()
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Advanced Disciplines Final Vectors]");b.AppendLine(RebirthAdvancedDisciplineRegistry.RunVectors());b.AppendLine(RebirthAnimalHandlingService.RunVectors());b.AppendLine(RebirthWildAffinityService.RunVectors());b.AppendLine(RebirthBeastmasterService.RunVectors());b.AppendLine(RebirthBlackMagicTargetClassifier.RunVectors());b.AppendLine(RebirthBlackMagicService.RunVectors());b.AppendLine(RebirthBoundUndeadService.RunVectors());b.AppendLine(RebirthRageService.RunVectors());b.AppendLine(RebirthSpecialPantherService.RunVectors());b.AppendLine(RebirthSurvivorMigrationVectorHarness.RunAll());return b.ToString().TrimEnd();
    }
    private static string A(XElement e,string n){return ((string)e?.Attribute(n)??string.Empty).Trim();}
    private static string F(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
}
