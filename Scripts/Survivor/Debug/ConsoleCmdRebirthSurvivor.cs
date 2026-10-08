using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthSurvivor : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }
    public override string[] getCommands() { return new[] { "rbsurvivor", "rebirthsurvivor" }; }
    public override string getDescription() { return "Inspects REBIRTH Survivor definitions, local profiles and persistence state."; }
    public override string getHelp()
    {
        return "rbsurvivor audit\n"
             + "rbsurvivor runtime\n"
             + "rbsurvivor defs [summary|backgrounds|traits|diets|support]\n"
             + "rbsurvivor profile [list|show <profileId>|issues|store]\n"
             + "rbsurvivor persistence [summary|issues]\n"
             + "rbsurvivor network [summary|owner|last]\n"
             + "rbsurvivor character [show|progression|knowledge|skillknowledge|condition] [entityId]\n"
             + "rbsurvivor condition meal <profile> <dietTagsCsv> [varietyFamily] [entityId]   (server + debug gate)\n"
             + "rbsurvivor support status [entityId]\n"
             + "rbsurvivor support apply <itemId> [entityId]   (server + debug gate; simulates successful use)\n"
             + "rbsurvivor support set <profileId> <grace|managed|positive|cooldown> <seconds> [entityId]   (server + debug gate)\n"
             + "rbsurvivor gear status [entityId]\n"
             + "rbsurvivor gear unequip <backpack|belt|support> [entityId]   (server + debug gate)\n"
             + "rbsurvivor backpackscroll [status|log on|log off]\n"
             + "rbsurvivor knowledge recipe <recipeName> [entityId]   (legacy binary recipe gate diagnostics)\n"
             + "rbsurvivor skillknowledge [show [entityId]|get <skillId> [entityId]|study <skillId> <amount> [entityId]]\n"
             + "rbsurvivor literature [summary|item <itemId>|state <itemId> [entityId]]\n"
             + "rbsurvivor study [status [entityId]|timing <literatureItemId> [entityId]|audio <audiobookItemId> [entityId]|music [status|stop|volume <0..1>]|verify [entityId]|acceptance [entityId]]\n"
             + "rbsurvivor recipecatalogue [summary|export|unmapped [maxRows]|show <recipeName>]\n"
             + "rbsurvivor capability [summary|recipe <recipeName> [entityId]|skill <skillId>|knowledge <knowledgeId>]\n"
             + "rbsurvivor craftpolicy [summary|recipe <recipeName> [entityId]|vectors]\n"
             + "rbsurvivor disciplines [summary|vectors|show <disciplineId>|acceptance <summary|balance|authority|vectors> [entityId]]\n"
             + "rbsurvivor animalhandling [vectors|status <dogStableId>]\n"
             + "rbsurvivor wildaffinity [summary|vectors|animal <entityId>]\n"
             + "rbsurvivor beastmaster [summary|vectors|animal <entityId>]\n"
             + "rbsurvivor blackmagic [summary|vectors|zombieanimals|summoning|bound <entityId>|explain <entityId>|classify <entityId>]\n"
             + "rbsurvivor rage [status|vectors|activate <basic|controlled|offensive|blood>] [entityId]\n"
             + "rbsurvivor panther [status|vectors|deploy <witch_doctor|berserker>] [entityId]   (deploy: server + debug gate)\n"
             + "rbsurvivor mindcontrol explain <zombieEntityId> [playerEntityId]\n"
             + "rbsurvivor progressiongraph [summary|validate|focus <id>|skill [id]|knowledge [id]|recipe [name]|discipline [id]|search <text>|neighborhood <id> [inDepth] [outDepth] [maxNodes]|path <from> <to> [incoming|outgoing|both]|overlay <neutral|creator|live> <id>|open <neutral|creator|live> [id]]\n"
             + "rbsurvivor provenance [summary|item|block <x> <y> <z>|crop <x> <y> <z>|electrical <x> <y> <z>]   (read-only)\n"
             + "rbsurvivor repairbonus [status|vectors] [entityId]   (read-only)\n"
             + "rbsurvivor resourcebonus [status|ore] [entityId]   (read-only)\n"
             + "rbsurvivor chunkibonus [status|vectors] [entityId]   (read-only)\n"
             + "rbsurvivor chunkjbonus [status|vectors|crop <x> <y> <z>] [entityId]   (read-only)\n"
             + "rbsurvivor chunkkbonus [status|vectors] [entityId]   (read-only)\n"
             + "rbsurvivor chunknbonus [status|vectors|block <x> <y> <z>] [entityId]   (read-only)\n"
             + "rbsurvivor bonuses [summary|balance|authority|migration|vectors] [entityId]   (read-only consolidated release acceptance)\n"
             + "rbsurvivor create validate <backgroundId> <dietId> [traitId,traitId,...]\n"
             + "rbsurvivor create force <entityId> <backgroundId> <dietId> [traitId,traitId,...]   (server + debug gate)\n"
             + "rbsurvivor test all [entityId]   (read-only final regression harness)\n"
             + "rebirthsurvivor debug [creator|profile|migration|points|skill <id-or-name>|training [vectors|<skillId> <continuous|discrete> <creditedWork> <workRate|rawAward> [entityId]]|traits [id-or-name]|trait <id-or-name>|traitui [status|on|off|dump]|diets|diet <id-or-name>|food <itemId-or-name>|wavea [vectors]|resource [vectors]|services [vectors]|complex [vectors]|acceptance [summary|balance|authority|vectors|matrix]]\n"
             + "rbsurvivor debug [status|on|off]   (legacy mutation-gate controls retained)\n"
             + "\nRead-only diagnostics are always available. Mutating creation diagnostics require the explicit Survivor debug gate.";
    }

    public override void Execute(List<string> p, CommandSenderInfo senderInfo)
    {
        if(!RebirthSurvivorDefinitionRegistry.IsReady) RebirthSurvivorInstaller.Install();
        string root=p!=null&&p.Count>0?(p[0]??string.Empty).ToLowerInvariant():"defs";
        if(root=="audit") { Output(RebirthSurvivorAuthoringValidator.BuildReport()); return; }
        if(root=="runtime") { ExecuteRuntime(); return; }
        if(root=="profile") { ExecuteProfile(p); return; }
        if(root=="persistence") { ExecutePersistence(p); return; }
        if(root=="network") { ExecuteNetwork(p); return; }
        if(root=="character") { ExecuteCharacter(p); return; }
        if(root=="condition") { ExecuteCondition(p); return; }
        if(root=="support") { ExecuteSupport(p); return; }
        if(root=="gear") { ExecuteGear(p); return; }
        if(root=="backpackscroll"||root=="bagscroll") { ExecuteBackpackScroll(p); return; }
        if(root=="knowledge") { ExecuteKnowledge(p); return; }
        if(root=="skillknowledge"||root=="theory") { ExecuteSkillKnowledge(p); return; }
        if(root=="literature") { ExecuteLiterature(p); return; }
        if(root=="study") { ExecuteStudyDiagnostics(p); return; }
        if(root=="recipecatalogue"||root=="recipecensus"||root=="recipes") { ExecuteRecipeCatalogue(p); return; }
        if(root=="capability") { ExecuteCapability(p); return; }
        if(root=="craftpolicy"||root=="craftingpolicy") { ExecuteCraftPolicy(p); return; }
        if(root=="disciplines"||root=="discipline") { ExecuteAdvancedDisciplines(p); return; }
        if(root=="animalhandling"||root=="animal") { ExecuteAnimalHandling(p); return; }
        if(root=="wildaffinity"||root=="affinity") { ExecuteWildAffinity(p); return; }
        if(root=="beastmaster"||root=="tame") { ExecuteBeastmaster(p); return; }
        if(root=="blackmagic"||root=="undead"||root=="mindcontrol") { ExecuteBlackMagicClassification(p); return; }
        if(root=="rage"||root=="berserker") { ExecuteRage(p); return; }
        if(root=="panther"||root=="specialpanther") { ExecuteSpecialPanther(p); return; }
        if(root=="progressiongraph"||root=="graph") { ExecuteProgressionGraph(p); return; }
        if(root=="provenance"||root=="workmanship") { ExecuteProvenance(p); return; }
        if(root=="repairbonus"||root=="repaircondition") { ExecuteRepairBonus(p); return; }
        if(root=="resourcebonus"||root=="resourcesignature") { ExecuteResourceBonus(p); return; }
        if(root=="chunkibonus"||root=="combatmomentum"||root=="huntertracking") { ExecuteChunkIBonus(p); return; }
        if(root=="chunkjbonus"||root=="professionalcooking"||root=="rapidcultivation"||root=="wholeanimal") { ExecuteChunkJBonus(p); return; }
        if(root=="chunkkbonus"||root=="drinkpreparation"||root=="mastermixologist") { ExecuteChunkKBonus(p); return; }
        if(root=="chunknbonus"||root=="nothingisjunk"||root=="scavengersalvage") { ExecuteChunkNBonus(p); return; }
        if(root=="bonuses"||root=="signaturebonuses"||root=="backgroundbonuses") { ExecuteBackgroundBonusAcceptance(p); return; }
        if(root=="create") { ExecuteCreate(p); return; }
        if(root=="test") { ExecuteTest(p); return; }
        if(root=="debug") { ExecuteDebug(p); return; }
        if(root!="defs") { Output(getHelp()); return; }
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle;
        if(b==null) { Output(RebirthSurvivorInstaller.LastReport); return; }
        if(mode=="summary") { Output(BuildSummary(b)); return; }
        if(mode=="backgrounds") { foreach(RebirthBackgroundDefinition d in b.Backgrounds) Output(d.Id+" restricted="+d.RestrictedTraitIds.Count+" favored="+d.FavoredTraitIds.Count+" blocked="+d.BlockedTraitIds.Count+" skills="+d.StartingSkills.Count+" skillKnowledge="+d.StartingSkillKnowledge.Count+" legacyKnowledge="+d.StartingKnowledgeIds.Count); return; }
        if(mode=="traits") { foreach(RebirthTraitDefinition d in b.Traits) Output(d.Id+" polarity="+d.Polarity+" points="+d.Points+" budgetDelta="+d.BudgetDelta+" availability="+d.Availability+" allowedBackgrounds="+d.AllowedBackgroundIds.Count+" modifier="+d.ModifierId); return; }
        if(mode=="diets") { foreach(RebirthDietDefinition d in b.Diets) Output(d.Id+" points=+"+d.Points+" compositionRule="+d.CompositionRule+" rule="+d.RuleSummary); return; }
        if(mode=="support") { foreach(RebirthTraitSupportProfileDefinition d in b.SupportProfiles) Output(d.Id+" kind="+d.Kind+" traits="+Join(d.SupportedTraitIds)+" items="+Join(d.ItemBindings)+" habit="+d.HabitTraitId+" grace="+d.GraceSeconds+" managed="+d.ManagedSeconds+" positive="+d.PositiveSeconds+" cooldown="+d.CooldownSeconds+" effects="+d.Effects.Count); return; }
        Output(getHelp());
    }



    private static void ExecuteRuntime()
    {
        Output("[REBIRTH Survivor] runtime installed=" + RebirthSurvivorInstaller.IsInstalled
            + " defsReady=" + RebirthSurvivorDefinitionRegistry.IsReady
            + " mode=" + RebirthSurvivorMode.ConfiguredMode);
        Output("  installer=" + RebirthSurvivorInstaller.LastReport);
        Output("  harmony=" + RebirthHarmonyBootstrap.BuildStatus());
        Output("  firstEntry=" + RebirthSurvivorFirstEntryUiService.BuildDebugSummary());
        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        Output("  owner=" + (owner == null ? "none" : ("rebirth=" + owner.RebirthModeEnabled + " state=" + owner.CreationState + " hasCharacter=" + owner.HasCharacter + " defsCompatible=" + owner.DefinitionsCompatible)));
    }

    private static void ExecuteProvenance(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        EntityPlayer player=world!=null?world.GetPrimaryPlayer() as EntityPlayer:null;
        if(mode=="summary")
        {
            Output(RebirthPlacedWorkmanshipService.BuildDebugSummary(world,null));
            Output(RebirthInfrastructureWorkService.BuildDebugReport(player));
            Output(RebirthWorkmanshipSignatureService.BuildDebugReport(player,null));
            Output("itemMetadata=ItemValue typed metadata; cropPersistence=TileEntityPlantGrowingRebirth; blockPersistence=PlacedWorkmanship.dat; electricalPersistence=InfrastructureElectrical.dat(v2)");
            return;
        }
        if(mode=="item")
        {
            ItemValue value=player!=null&&player.inventory!=null?player.inventory.holdingItemItemValue:null;
            Output(RebirthItemProvenanceAdapter.BuildDebugSummary(value));
            Output(RebirthElectricalItemProvenance.BuildDebugSummary(value));return;
        }
        Vector3i pos=Vector3i.zero;
        if((mode=="block"||mode=="crop"||mode=="electrical")&&!TryParseProvenancePos(p,2,out pos))
        {Output("Usage: rbsurvivor provenance "+mode+" <x> <y> <z>");return;}
        if(mode=="block"){Output(RebirthPlacedWorkmanshipService.BuildDebugSummary(world,pos));Output(RebirthWorkmanshipSignatureService.BuildDebugReport(player,pos));return;}
        if(mode=="crop"){Output(RebirthCropProvenanceAdapter.BuildDebugSummary(world,pos));return;}
        if(mode=="electrical")
        {
            Output(RebirthInfrastructureWorkService.BuildDebugAt(pos));
            RebirthElectricalServiceRecord last=RebirthInfrastructureWorkService.GetLastClientSnapshot();
            if(last!=null)Output("lastReplicatedElectrical pos="+last.Position+" configuredBy="+last.ConfiguredByStableId+" background="+last.ConfiguredByBackgroundId+" bonus="+last.ConfiguredByBonusId+" skill="+last.ConfiguredSkillValue.ToString("0.###"));
            return;
        }
        Output("Usage: rbsurvivor provenance [summary|item|block <x> <y> <z>|crop <x> <y> <z>|electrical <x> <y> <z>]");
    }

    private static void ExecuteRepairBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="vectors"){Output(RebirthRepairSignatureVectorHarness.Run());return;}
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthRepairSignatureService.BuildDebugReport(player));
    }

    private static void ExecuteResourceBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="ore"){Output("[REBIRTH Ore Sense] "+RebirthOreSenseService.BuildDebugReport());return;}
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthResourceSignatureService.BuildDebugReport(player));
        Output("[REBIRTH Ore Sense] "+RebirthOreSenseService.BuildDebugReport());
    }

    private static void ExecuteChunkIBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="vectors"){Output(RebirthCombatPatrolTrackingSignatureService.RunVectors());return;}
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthCombatPatrolTrackingSignatureService.BuildDebugReport(player));
    }

    private static void ExecuteChunkJBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="vectors"){Output(RebirthFoodFarmingButcherySignatureService.RunVectors());return;}
        if(mode=="crop")
        {
            Vector3i pos;
            if(!TryParseProvenancePos(p,2,out pos)){Output("Usage: rbsurvivor chunkjbonus crop <x> <y> <z> [entityId]");return;}
            EntityPlayer cropPlayer=ResolvePlayer(p!=null&&p.Count>5?p[5]:string.Empty);
            Output(RebirthFoodFarmingButcherySignatureService.BuildDebugReport(cropPlayer,pos));return;
        }
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthFoodFarmingButcherySignatureService.BuildDebugReport(player,null));
    }

    private static void ExecuteChunkKBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="vectors"){Output(RebirthDrinkPreparationSignatureService.RunVectors());return;}
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthDrinkPreparationSignatureService.BuildDebugReport(player));
    }

    private static void ExecuteChunkNBonus(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="vectors"){Output(RebirthScavengerSalvageProfileService.RunVectors());return;}
        if(mode=="block")
        {
            Vector3i pos;
            if(!TryParseProvenancePos(p,2,out pos)){Output("Usage: rbsurvivor chunknbonus block <x> <y> <z> [entityId]");return;}
            EntityPlayer blockPlayer=ResolvePlayer(p!=null&&p.Count>5?p[5]:string.Empty);
            Output(RebirthScavengerSalvageProfileService.BuildDebugReport(blockPlayer,pos));return;
        }
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        Output(RebirthScavengerSalvageProfileService.BuildDebugReport(player,null));
    }

    private static bool TryParseProvenancePos(List<string> p,int offset,out Vector3i pos)
    {
        pos=Vector3i.zero;int x,y,z;if(p==null||p.Count<offset+3||!int.TryParse(p[offset],out x)||!int.TryParse(p[offset+1],out y)||!int.TryParse(p[offset+2],out z))return false;pos=new Vector3i(x,y,z);return true;
    }

    private static void ExecuteBackgroundBonusAcceptance(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        if(mode=="summary"||mode=="status"){Output(RebirthBackgroundSignatureBonusesAcceptance.BuildSummary(player));return;}
        if(mode=="balance"){Output(RebirthBackgroundSignatureBonusesAcceptance.BuildBalanceDefaults());return;}
        if(mode=="authority"){Output(RebirthBackgroundSignatureBonusesAcceptance.BuildAuthorityReport());return;}
        if(mode=="migration"){Output(RebirthBackgroundSignatureBonusesAcceptance.BuildMigrationReport(player));return;}
        if(mode=="vectors"||mode=="test"){Output(RebirthBackgroundSignatureBonusesAcceptance.RunVectors());return;}
        Output("Usage: rbsurvivor bonuses [summary|balance|authority|migration|vectors] [entityId]");
    }

    private static void ExecuteAdvancedDisciplines(List<string> p)
    {
        if(!RebirthAdvancedDisciplineRegistry.IsReady)
        {
            try { Output(RebirthAdvancedDisciplineRegistry.Load()); }
            catch(Exception ex) { Output("[REBIRTH AdvancedDisciplines] load failed: "+ex.GetType().Name+": "+ex.Message); return; }
        }
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary"){Output(RebirthAdvancedDisciplineRegistry.BuildDebugSummary(string.Empty));return;}
        if(mode=="vectors"){Output(RebirthAdvancedDisciplineRegistry.RunVectors());return;}
        if(mode=="show"){Output(RebirthAdvancedDisciplineRegistry.BuildDebugSummary(p!=null&&p.Count>2?p[2]:string.Empty));return;}
        if(mode=="acceptance")
        {
            string sub=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():"summary";string id=p!=null&&p.Count>3?p[3]:string.Empty;EntityPlayer player=ResolvePlayer(id);
            if(sub=="summary"){Output(RebirthAdvancedDisciplinesAcceptance.BuildSummary(player));return;}if(sub=="balance"){Output(RebirthAdvancedDisciplinesAcceptance.BuildBalance());return;}if(sub=="authority"){Output(RebirthAdvancedDisciplinesAcceptance.BuildAuthority());return;}if(sub=="vectors"){Output(RebirthAdvancedDisciplinesAcceptance.RunVectors());return;}
            Output("Usage: rbsurvivor disciplines acceptance [summary|balance|authority|vectors] [entityId]");return;
        }
        Output("Usage: rbsurvivor disciplines [summary|vectors|show <disciplineId>|acceptance <summary|balance|authority|vectors> [entityId]]");
    }

    private static void ExecuteAnimalHandling(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"vectors";
        if(mode=="vectors"){Output(RebirthAnimalHandlingService.RunVectors());return;}
        if(mode=="status")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor animalhandling status <dogStableId>");return;}
            RebirthNpcStableId id;if(!RebirthNpcStableId.TryParse(p[2],out id)){Output("Invalid dog StableId: "+p[2]);return;}
            EntityPlayer player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
            Output(RebirthAnimalHandlingService.BuildDebugSummary(player,id));return;
        }
        Output("Usage: rbsurvivor animalhandling [vectors|status <dogStableId>]");
    }

    private static void ExecuteWildAffinity(List<string> p)
    {
        if(!RebirthWildAffinityService.IsReady)
        {
            try { Output(RebirthWildAffinityService.LoadDefinitions()); }
            catch(Exception ex) { Output("[REBIRTH WildAffinity] load failed: "+ex.GetType().Name+": "+ex.Message); return; }
        }
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        EntityPlayer player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
        if(mode=="summary"){Output(RebirthWildAffinityService.BuildDebugSummary(player,0));return;}
        if(mode=="vectors"){Output(RebirthWildAffinityService.RunVectors());return;}
        if(mode=="animal")
        {
            int entityId;
            if(p==null||p.Count<3||!int.TryParse(p[2],out entityId)){Output("Usage: rbsurvivor wildaffinity animal <entityId>");return;}
            Output(RebirthWildAffinityService.BuildDebugSummary(player,entityId));return;
        }
        Output("Usage: rbsurvivor wildaffinity [summary|vectors|animal <entityId>]");
    }

    private static void ExecuteBeastmaster(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";EntityPlayer player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
        if(mode=="vectors"){Output(RebirthBeastmasterService.RunVectors());return;}
        if(mode=="summary"){Output(RebirthBeastmasterService.BuildDebugSummary(player,0));return;}
        if(mode=="animal"){int id=0;if(p==null||p.Count<3||!int.TryParse(p[2],out id)){Output("Usage: rbsurvivor beastmaster animal <entityId>");return;}Output(RebirthBeastmasterService.BuildDebugSummary(player,id));return;}
        Output("Usage: rbsurvivor beastmaster [summary|vectors|animal <entityId>]");
    }

    private static void ExecuteRage(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";string id=p!=null&&p.Count>2?p[p.Count-1]:string.Empty;EntityPlayer player=ResolvePlayer(id);
        if(mode=="vectors"){Output(RebirthRageService.RunVectors());return;}if(player==null){Output("Player not found.");return;}if(mode=="status"){Output(RebirthRageService.BuildStatus(player));return;}
        if(mode=="activate"){if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Rage] activate rejected: enable 'rbsurvivor debug on' first.");return;}string tier=p!=null&&p.Count>2?p[2]:"basic";string reason;bool ok=RebirthRageService.TryActivate(player,tier,out reason);Output((ok?"PASS ":"FAIL ")+reason);return;}Output("Usage: rbsurvivor rage [status|vectors|activate <basic|controlled|offensive|blood>] [entityId]");
    }

    private static void ExecuteSpecialPanther(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";string id=p!=null&&p.Count>3?p[3]:string.Empty;EntityPlayer player=ResolvePlayer(id);if(mode=="vectors"){Output(RebirthSpecialPantherService.RunVectors());return;}if(player==null){Output("Player not found.");return;}if(mode=="status"){Output(RebirthSpecialPantherService.BuildStatus(player));return;}if(mode=="deploy"){if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Special Panthers] deploy rejected: enable 'rbsurvivor debug on' first.");return;}string route=p!=null&&p.Count>2?p[2]:string.Empty;string reason;bool ok=RebirthSpecialPantherService.TryDeploy(player.world,player,route,out reason);Output((ok?"PASS ":"FAIL ")+reason);return;}Output("Usage: rbsurvivor panther [status|vectors|deploy <witch_doctor|berserker>] [entityId]");
    }

    private static void ExecuteBlackMagicClassification(List<string> p)
    {
        if(!RebirthBlackMagicTargetClassifier.IsReady)
        {
            try { Output(RebirthBlackMagicTargetClassifier.LoadDefinitions()); }
            catch(Exception ex) { Output("[REBIRTH BlackMagic] classification load failed: "+ex.GetType().Name+": "+ex.Message); return; }
        }
        string root=p!=null&&p.Count>0?(p[0]??string.Empty).ToLowerInvariant():"blackmagic";
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        EntityPlayer player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
        if(root=="mindcontrol"&&mode!="explain"){Output("Usage: rbsurvivor mindcontrol explain <zombieEntityId> [playerEntityId]");return;}
        if(mode=="zombieanimals"){Output(RebirthBlackMagicTargetClassifier.BuildZombieAnimalAuditSummary());return;}
        if(mode=="summary"){Output(RebirthBlackMagicService.BuildStatus(player,0));Output(RebirthBoundUndeadService.BuildStatus(player));return;}
        if(mode=="vectors"){Output(RebirthBlackMagicTargetClassifier.RunVectors());Output(RebirthBlackMagicService.RunVectors());Output(RebirthBoundUndeadService.RunVectors());return;}
        if(mode=="summoning"){Output(RebirthBoundUndeadService.BuildStatus(player));return;}
        if(mode=="bound")
        {
            int id=0;if(p==null||p.Count<3||!int.TryParse(p[2],out id)){Output("Usage: rbsurvivor blackmagic bound <entityId>");return;}
            EntityAlive target=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetEntity(id) as EntityAlive:null;
            Output(RebirthBoundUndeadService.BuildEntityStatus(target,player));return;
        }
        if(mode=="classify"||mode=="explain")
        {
            int id=0;if(p==null||p.Count<3||!int.TryParse(p[2],out id)){Output(mode=="explain"?"Usage: rbsurvivor mindcontrol explain <zombieEntityId> [playerEntityId]":"Usage: rbsurvivor blackmagic classify <entityId>");return;}
            if(mode=="explain"&&p.Count>3){int pid;if(int.TryParse(p[3],out pid)&&GameManager.Instance!=null&&GameManager.Instance.World!=null)player=GameManager.Instance.World.GetEntity(pid) as EntityPlayer;}
            EntityAlive target=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetEntity(id) as EntityAlive:null;
            if(mode=="classify")Output(RebirthBlackMagicTargetClassifier.BuildEntityDebug(target));else Output(RebirthBlackMagicService.BuildExplain(player,target));return;
        }
        Output("Usage: rbsurvivor blackmagic [summary|vectors|zombieanimals|summoning|bound <entityId>|explain <entityId>|classify <entityId>]");
    }

    private static void ExecuteProgressionGraph(List<string> p)
    {
        if(!RebirthProgressionGraphRegistry.IsReady)
        {
            try { Output(RebirthProgressionGraphRegistry.BuildFromCurrentAuthority()); }
            catch(Exception ex) { Output("[REBIRTH ProgressionGraph] build failed: "+ex.GetType().Name+": "+ex.Message); return; }
        }
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary") { Output(RebirthProgressionGraphDebug.BuildSummary()); return; }
        if(mode=="validate") { Output(RebirthProgressionGraphValidator.ValidateCurrent().BuildText()); return; }
        if(mode=="release") { Output(RebirthProgressionExplorerReleaseGate.Evaluate().BuildText()); return; }
        if(mode=="focus") { Output(RebirthProgressionGraphDebug.BuildFocus(p!=null&&p.Count>2?p[2]:string.Empty)); return; }
        if(mode=="skill") { Output(RebirthProgressionGraphDebug.BuildTypeReport(RebirthProgressionGraphNodeType.Skill,p!=null&&p.Count>2?p[2]:string.Empty)); return; }
        if(mode=="knowledge") { Output(RebirthProgressionGraphDebug.BuildTypeReport(RebirthProgressionGraphNodeType.Knowledge,p!=null&&p.Count>2?p[2]:string.Empty)); return; }
        if(mode=="recipe") { Output(RebirthProgressionGraphDebug.BuildTypeReport(RebirthProgressionGraphNodeType.Recipe,p!=null&&p.Count>2?p[2]:string.Empty)); return; }
        if(mode=="discipline") { Output(RebirthProgressionGraphDebug.BuildTypeReport(RebirthProgressionGraphNodeType.Discipline,p!=null&&p.Count>2?p[2]:string.Empty)); return; }
        if(mode=="search")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor progressiongraph search <text>");return;}
            string query=string.Join(" ",p.GetRange(2,p.Count-2).ToArray());RebirthProgressionExplorerSearchResult[] results=RebirthProgressionExplorerSearchService.Search(query,12);
            Output("[REBIRTH ProgressionExplorer] search query=\"" + query + "\" results=" + results.Length);
            for(int i=0;i<results.Length;i++)Output((i+1)+". "+results[i].NodeType+" "+results[i].DisplayName+" id="+results[i].NodeId+" category="+results[i].Category+" score="+results[i].Score);
            return;
        }
        if(mode=="neighborhood")
        {
            string id=p!=null&&p.Count>2?p[2]:string.Empty; int inDepth=2,outDepth=2,maxNodes=32;
            if(p!=null&&p.Count>3)int.TryParse(p[3],out inDepth); if(p!=null&&p.Count>4)int.TryParse(p[4],out outDepth); if(p!=null&&p.Count>5)int.TryParse(p[5],out maxNodes);
            Output(RebirthProgressionExplorerDebug.BuildNeighborhood(id,inDepth,outDepth,maxNodes)); return;
        }
        if(mode=="path")
        {
            if(p==null||p.Count<4){Output("Usage: rbsurvivor progressiongraph path <from> <to> [incoming|outgoing|both]");return;}
            Output(RebirthProgressionExplorerDebug.BuildPath(p[2],p[3],p.Count>4?p[4]:"both")); return;
        }
        if(mode=="overlay")
        {
            if(p==null||p.Count<4){Output("Usage: rbsurvivor progressiongraph overlay <neutral|creator|live> <id>");return;}
            Output(RebirthProgressionExplorerDebug.BuildOverlay(p[3],p[2])); return;
        }
        if(mode=="open")
        {
            if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH ProgressionExplorer] debug UI open requires: rbsurvivor debug on");return;}
            string requestedMode=p!=null&&p.Count>2?p[2]:"neutral";
            RebirthProgressionExplorerMode explorerMode=RebirthProgressionExplorerMode.Neutral;
            if(string.Equals(requestedMode,"creator",StringComparison.OrdinalIgnoreCase))explorerMode=RebirthProgressionExplorerMode.CreatorPreview;
            else if(string.Equals(requestedMode,"live",StringComparison.OrdinalIgnoreCase))explorerMode=RebirthProgressionExplorerMode.LiveCharacter;
            string focus=p!=null&&p.Count>3?p[3]:"skill.mechanics";
            LocalPlayerUI ui=LocalPlayerUI.GetUIForPrimaryPlayer();string error;
            if(ui==null||ui.xui==null){Output("[REBIRTH ProgressionExplorer] local player UI unavailable");return;}
            bool opened=RebirthProgressionExplorerUiService.Open(ui.xui,focus,explorerMode,out error);
            Output(opened?"[REBIRTH ProgressionExplorer] opened mode="+explorerMode+" focus="+focus:"[REBIRTH ProgressionExplorer] open failed: "+error);
            return;
        }
        Output("Usage: rbsurvivor progressiongraph [summary|validate|release|focus <id>|skill [id]|knowledge [id]|recipe [name]|discipline [id]|search <text>|neighborhood <id> [inDepth] [outDepth] [maxNodes]|path <from> <to> [incoming|outgoing|both]|overlay <neutral|creator|live> <id>|open <neutral|creator|live> [id]]");
    }

    private static void ExecuteProfile(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"list";
        if(mode=="store")
        {
            Output("[REBIRTH Survivor] profileStore="+RebirthSurvivorProfileStore.RootDirectory+" profiles="+RebirthSurvivorProfileStore.GetProfilesSnapshot().Length+" issues="+RebirthSurvivorProfileStore.GetIssuesSnapshot().Length);
            return;
        }
        if(mode=="issues")
        {
            RebirthSurvivorProfileLoadIssue[] issues=RebirthSurvivorProfileStore.GetIssuesSnapshot();
            if(issues.Length==0){Output("[REBIRTH Survivor] profile issues=0");return;}
            for(int i=0;i<issues.Length;i++) Output("profileIssue id="+issues[i].ProfileId+" recovered="+issues[i].RecoveredFromBackup+" reason="+issues[i].Reason+" path="+issues[i].Path);
            return;
        }
        if(mode=="show")
        {
            string id=p!=null&&p.Count>2?p[2]:string.Empty; RebirthSurvivorProfile profile;
            if(!RebirthSurvivorProfileStore.TryGet(id,out profile)){Output("[REBIRTH Survivor] profile not found: "+id);return;}
            RebirthSurvivorProfileCompatibility compatibility=RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
            Output("profile id="+profile.ProfileId+" name='"+profile.ProfileName+"' background="+profile.BackgroundId+" diet="+profile.DietId+" traits="+profile.TraitIds.Count+" authoredVersion="+profile.AuthoredDefinitionVersion+" authoredHash="+profile.AuthoredDefinitionHash+" compatibility="+compatibility.Kind);
            return;
        }
        if(mode=="list")
        {
            RebirthSurvivorProfile[] profiles=RebirthSurvivorProfileStore.GetProfilesSnapshot();
            Output("[REBIRTH Survivor] local profiles="+profiles.Length);
            for(int i=0;i<profiles.Length;i++){RebirthSurvivorProfileCompatibility c=RebirthSurvivorProfileStore.EvaluateCompatibility(profiles[i]);Output(profiles[i].ProfileId+" name='"+profiles[i].ProfileName+"' background="+profiles[i].BackgroundId+" diet="+profiles[i].DietId+" traits="+profiles[i].TraitIds.Count+" state="+c.Kind);}
            return;
        }
        Output("Unknown profile diagnostic mode: "+mode);
    }

    private static void ExecutePersistence(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary")
        {
            Output("[REBIRTH Survivor] persistence server="+RebirthWorldCharacterRepository.IsServerAuthority+" worldRoot="+RebirthWorldCharacterRepository.RootDirectory+" worldIssues="+RebirthWorldCharacterRepository.GetIssuesSnapshot().Length+" profileRoot="+RebirthSurvivorProfileStore.RootDirectory+" profileIssues="+RebirthSurvivorProfileStore.GetIssuesSnapshot().Length);
            return;
        }
        if(mode=="issues")
        {
            RebirthWorldCharacterPersistenceIssue[] issues=RebirthWorldCharacterRepository.GetIssuesSnapshot();
            if(issues.Length==0){Output("[REBIRTH Survivor] world persistence issues=0");return;}
            for(int i=0;i<issues.Length;i++)Output("worldIssue key="+issues[i].StablePlayerKey+" recovered="+issues[i].RecoveredFromBackup+" reason="+issues[i].Reason+" path="+issues[i].Path);
            return;
        }
        Output("Unknown persistence diagnostic mode: "+mode);
    }

    private static void ExecuteNetwork(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary")
        {
            Output("[REBIRTH Survivor] network protocol="+RebirthSurvivorNetworkProtocol.Version+" tx={"+RebirthSurvivorCreationTransactions.GetDebugSummary()+"} clientOwner="+(RebirthSurvivorClientState.GetOwnerStateSnapshot()!=null)+" clientLastResult="+(RebirthSurvivorClientState.GetLastCreationResult()!=null));
            return;
        }
        if(mode=="owner")
        {
            RebirthSurvivorOwnerStateSnapshot s=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(s==null){Output("[REBIRTH Survivor] owner snapshot unavailable");return;}
            Output("owner mode="+s.RebirthModeEnabled+" hasCharacter="+s.HasCharacter+" revision="+s.CharacterRevision+" defsCompatible="+s.DefinitionsCompatible+" background="+s.BackgroundId+" diet="+s.DietId+" traits="+s.TraitIds.Count+" attributes="+s.Attributes.Count+" skills="+s.Skills.Count+" skillKnowledge="+s.SkillKnowledge.Count+" legacyKnowledge="+s.KnowledgeIds.Count+" support="+s.SupportEntries.Count);
            return;
        }
        if(mode=="last")
        {
            RebirthSurvivorCreationNetworkResponse r=RebirthSurvivorClientState.GetLastCreationResult();
            if(r==null){Output("[REBIRTH Survivor] no creation network result received");return;}
            Output("creationResult request="+r.RequestId+" operation="+r.Operation+" status="+r.Status+" replay="+r.WasReplay+" revision="+r.CharacterRevision+" message="+r.MessageCode+" validation="+(r.ValidationResult!=null?(r.ValidationResult.IsValid?"valid":"invalid"):"none"));
            return;
        }
        Output("Unknown network diagnostic mode: "+mode);
    }



    private static void ExecuteTest(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"all";
        if(mode!="all") { Output("Usage: rbsurvivor test all [entityId]"); return; }
        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        string report=RebirthSurvivorTestHarness.RunAll(player);
        Output(report);
        try
        {
            EntityPlayerLocal local=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
            if(local!=null) GameManager.ShowTooltip(local,"REBIRTH Survivor regression complete. See console/log for the 17-phase report.",true,false,4f);
        }
        catch { }
    }

    private static void ExecuteDebug(List<string> p)
    {
        // Chunk 1: no subcommand means the consolidated read-only dump. The old mutation-gate
        // controls remain accepted so existing test instructions and gated mutation commands do
        // not break.
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():string.Empty;
        if(mode.Length==0){Output(RebirthSurvivorDiagnostics.BuildDefaultReport());return;}
        if(mode=="creator"){Output(RebirthSurvivorDiagnostics.BuildCreatorReport());return;}
        if(mode=="profile"){Output(RebirthSurvivorDiagnostics.BuildProfileReport());return;}
        if(mode=="migration")
        {
            string migrationMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(migrationMode=="vectors"||migrationMode=="test"){Output(RebirthSurvivorMigrationVectorHarness.RunAll());return;}
            Output(RebirthSurvivorDiagnostics.BuildMigrationReport());return;
        }
        if(mode=="points"){Output(RebirthSurvivorDiagnostics.BuildPointsReport());return;}
        if(mode=="skill"){Output(RebirthSurvivorDiagnostics.BuildSkillReport(p!=null&&p.Count>2?p[2]:string.Empty));return;}
        if(mode=="training")
        {
            string trainingMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(trainingMode=="vectors"||trainingMode=="test"){Output(RebirthSkillTrainingVectorHarness.RunAll());return;}
            if(p==null||p.Count<6){Output("Usage: rbsurvivor debug training <skillId> <continuous|discrete> <creditedWork> <workRate|rawAward> [entityId] OR ... training vectors");return;}
            string skillId=p[2]??string.Empty,kind=(p[3]??string.Empty).ToLowerInvariant();float credited,normalizer;
            if(!float.TryParse(p[4],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out credited)||credited<=0f||
               !float.TryParse(p[5],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out normalizer)||normalizer<=0f){Output("[REBIRTH Training] creditedWork and workRate/rawAward must be positive numbers.");return;}
            EntityPlayer player=ResolvePlayer(p.Count>6?p[6]:string.Empty);if(player==null){Output("[REBIRTH Training] Player not found.");return;}
            RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence{SkillId=skillId,SourceKey="debug-projection",AuthoritativeSuccess=true,CreditedWork=credited,ReferenceDescription="manual read-only projection"};
            if(kind=="continuous"){evidence.Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork;evidence.LiveWorkRate=normalizer;}
            else if(kind=="discrete"){evidence.Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward;evidence.DiscreteRawAward=normalizer;}
            else{Output("[REBIRTH Training] mode must be continuous or discrete.");return;}
            Output(RebirthSkillTrainingProjectionService.BuildDebugProjection(player,evidence));return;
        }
        if(mode=="traitui"||mode=="traitsui")
        {
            string traitUiMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():"status";
            if(traitUiMode=="on"||traitUiMode=="true"||traitUiMode=="1")
            {
                RebirthSurvivorDebug.SetTraitUiLoggingEnabled(true);
                Output("[REBIRTH Survivor][TraitUI] logging=True. Every Trait add/remove attempt will now be written to output_log with before/after counts and point accounting.");
                return;
            }
            if(traitUiMode=="off"||traitUiMode=="false"||traitUiMode=="0")
            {
                RebirthSurvivorDebug.SetTraitUiLoggingEnabled(false);
                Output("[REBIRTH Survivor][TraitUI] logging=False");
                return;
            }
            if(traitUiMode=="status")
            {
                int cap = RebirthSurvivorDefinitionRegistry.Bundle!=null&&RebirthSurvivorDefinitionRegistry.Bundle.Progression!=null ? RebirthSurvivorDefinitionRegistry.Bundle.Progression.MaxNegativeTraitRefund : int.MinValue;
                Output("[REBIRTH Survivor][TraitUI] logging="+RebirthSurvivorDebug.TraitUiLoggingEnabled+" negativeRefundCap="+(cap==int.MinValue?"<unavailable>":(RebirthSurvivorTraitPointEconomy.HasRefundCap(cap)?cap.ToString():"unlimited")));
                return;
            }
            if(traitUiMode=="dump")
            {
                RebirthSurvivorCreatorViewModel activeModel; bool nextVisible,nextEnabled; string status;
                if(!XUiC_RebirthSurvivorCreator.TryGetActiveDebugState(out activeModel,out nextVisible,out nextEnabled,out status))
                {
                    Output("[REBIRTH Survivor][TraitUI] no active Survivor Creator window/model");
                    return;
                }
                Output(RebirthSurvivorTraitUiDebug.BuildReport(activeModel));
                return;
            }
            Output("Usage: rbsurvivor debug traitui [status|on|off|dump]");
            return;
        }
        if(mode=="traits")
        {
            string filter=p!=null&&p.Count>2?p[2]:string.Empty;
            Output(string.IsNullOrEmpty(filter)?RebirthTraitRuntimeReconciliation.BuildSummary():RebirthTraitRuntimeReconciliation.BuildTraitReport(filter));
            return;
        }
        if(mode=="trait"){Output(RebirthTraitRuntimeReconciliation.BuildTraitReport(p!=null&&p.Count>2?p[2]:string.Empty));return;}
        if(mode=="diets"){Output(RebirthDietRuntimeReconciliation.BuildSummary());return;}
        if(mode=="diet")
        {
            string dietMode=p!=null&&p.Count>2?(p[2]??string.Empty):string.Empty;
            if(string.Equals(dietMode,"vectors",StringComparison.OrdinalIgnoreCase)||string.Equals(dietMode,"test",StringComparison.OrdinalIgnoreCase)){Output(RebirthSurvivorDietVectorHarness.RunAll());return;}
            Output(RebirthDietRuntimeReconciliation.BuildDietReport(dietMode));return;
        }
        if(mode=="food"){Output(RebirthDietRuntimeReconciliation.BuildFoodReport(p!=null&&p.Count>2?p[2]:string.Empty));return;}
        if(mode=="wavea")
        {
            string waveMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(waveMode=="vectors"||waveMode=="test"){Output(RebirthSkillWaveAVectorHarness.RunAll());return;}
            EntityPlayer player=ResolvePlayer(waveMode);
            Output(RebirthSkillWaveAService.BuildDebugReport(player));
            return;
        }
        if(mode=="resource"||mode=="field"||mode=="chunk6")
        {
            string resourceMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(resourceMode=="vectors"||resourceMode=="test"){Output(RebirthResourceFieldSkillVectorHarness.RunAll());return;}
            EntityPlayer player=ResolvePlayer(resourceMode);
            Output(RebirthResourceFieldSkillService.BuildDebugReport(player));
            return;
        }
        if(mode=="weapons"||mode=="weapon"||mode=="chunk7")
        {
            string weaponMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(weaponMode=="vectors"||weaponMode=="test"){Output(RebirthWeaponFamilySkillVectorHarness.RunAll());return;}
            EntityPlayer player=ResolvePlayer(weaponMode);
            Output(RebirthWeaponFamilySkillService.BuildDebugReport(player));
            return;
        }
        if(mode=="services"||mode=="service"||mode=="crafting"||mode=="chunk8")
        {
            string serviceMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(serviceMode=="vectors"||serviceMode=="test"){Output(RebirthServiceCraftSkillVectorHarness.RunAll());return;}
            EntityPlayer player=ResolvePlayer(serviceMode);
            Output(RebirthServiceCraftSkillService.BuildDebugReport(player));
            return;
        }
        if(mode=="complex"||mode=="complexskills"||mode=="chunk10")
        {
            string complexMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():string.Empty;
            if(complexMode=="vectors"||complexMode=="test"){Output(RebirthComplexSkillSystemVectorHarness.RunAll());return;}
            EntityPlayer player=ResolvePlayer(complexMode);
            Output(RebirthComplexSkillSystemService.BuildDebugReport(player));
            return;
        }
        if(mode=="acceptance"||mode=="release"||mode=="chunk11")
        {
            string acceptanceMode=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():"summary";
            if(acceptanceMode=="vectors"||acceptanceMode=="test"){Output(RebirthSurvivorReleaseAcceptanceVectorHarness.RunAll());return;}
            if(acceptanceMode=="balance"){Output(RebirthSurvivorReleaseAcceptance.BuildBalanceBands());return;}
            if(acceptanceMode=="authority"){Output(RebirthSurvivorReleaseAcceptance.BuildAuthorityReport());return;}
            if(acceptanceMode=="matrix"){Output(RebirthSurvivorReleaseAcceptance.BuildRuntimeMatrix());return;}
            EntityPlayer player=ResolvePlayer(p!=null&&p.Count>3?p[3]:string.Empty);
            Output(RebirthSurvivorReleaseAcceptance.BuildSummary(player));
            return;
        }
        if(mode=="status"){Output("[REBIRTH Survivor] debug mutation gate="+RebirthSurvivorDebug.Enabled);return;}
        if(mode=="on"||mode=="true"||mode=="1"){RebirthSurvivorDebug.SetEnabled(true);Output("[REBIRTH Survivor] debug mutation gate=True");return;}
        if(mode=="off"||mode=="false"||mode=="0"){RebirthSurvivorDebug.SetEnabled(false);Output("[REBIRTH Survivor] debug mutation gate=False");return;}
        Output("Usage: rebirthsurvivor debug [creator|profile|migration [vectors]|points|skill <id-or-name>|training [vectors|<skillId> <continuous|discrete> <creditedWork> <workRate|rawAward> [entityId]]|traits [id-or-name]|trait <id-or-name>|traitui [status|on|off|dump]|diets|diet <id-or-name>|diet vectors|food <itemId-or-name>|wavea [vectors|entityId]|resource [vectors|entityId]|weapons [vectors|entityId]|services [vectors|entityId]|complex [vectors|entityId]|acceptance [summary|balance|authority|vectors]] OR rbsurvivor debug [status|on|off]");
    }

    private static void ExecuteBackpackScroll(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        if(mode=="status")
        {
            Output(XUiC_RebirthExpandableBackpackScroll.BuildActiveDebugReport());
            return;
        }
        if(mode=="log")
        {
            string value=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():"";
            if(value=="on"||value=="true"||value=="1")
            {
                XUiC_RebirthExpandableBackpackScroll.SetDiagnosticLogging(true);
                Output("[REBIRTH BackpackScroll] logging=True");
                return;
            }
            if(value=="off"||value=="false"||value=="0")
            {
                XUiC_RebirthExpandableBackpackScroll.SetDiagnosticLogging(false);
                Output("[REBIRTH BackpackScroll] logging=False");
                return;
            }
        }
        Output("Usage: rbsurvivor backpackscroll [status|log on|log off]");
    }

    private static void ExecuteCharacter(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"show";
        if(mode!="show"&&mode!="progression"&&mode!="knowledge"&&mode!="skillknowledge"&&mode!="condition"){Output("Usage: rbsurvivor character [show|progression|knowledge|skillknowledge|condition] [entityId]");return;}

        EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
        if(player==null){Output("[REBIRTH Survivor] character: player not found");return;}

        RebirthStablePlayerIdentity identity;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null)
        {
            RebirthSurvivorOwnerStateSnapshot client=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(client!=null&&client.HasCharacter&&(mode=="progression"||mode=="knowledge"||mode=="skillknowledge"||mode=="condition")){if(mode=="condition")OutputClientCondition(client);else OutputClientProgression(client,mode);return;}
            Output("[REBIRTH Survivor] character: stable identity unavailable for entity="+player.entityId);return;
        }

        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterRepository.TryGet(identity,out record)||record==null)
        {Output("[REBIRTH Survivor] character: no committed world character key="+identity.StorageKey);return;}

        if(mode=="progression") { OutputServerProgression(record); return; }
        if(mode=="knowledge") { OutputServerKnowledge(record); return; }
        if(mode=="skillknowledge") { OutputServerSkillKnowledge(record); return; }
        if(mode=="condition") { OutputServerCondition(record); return; }

        string creationId=record.Origin!=null?record.Origin.CreationId:string.Empty;
        RebirthMetabolismState metabolism;
        bool hasMetabolism=RebirthMetabolismStateRepository.TryGet(player,out metabolism)&&metabolism!=null;
        bool metabolismLinked=hasMetabolism&&RebirthSurvivorMetabolismCreationGate.IsLinkedToCommittedOrigin(player,metabolism);
        Output("character entity="+player.entityId+" key="+identity.StorageKey+" schema="+record.SchemaVersion+" revision="+record.Revision
            +" creationId="+creationId+" background="+record.Origin.BackgroundId+" diet="+record.Origin.DietId+" traits="+record.Origin.TraitIds.Count
            +" attributes="+record.Progression.Attributes.Count+" skills="+record.Progression.Skills.Count+" skillKnowledge="+record.Progression.SkillKnowledge.Count+" legacyKnowledge="+record.Progression.KnowledgeIds.Count
            +" mood="+record.Condition.MoodCurrent.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)
            +" dietSatisfaction="+record.Condition.DietSatisfaction.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)
            +" healthCapacity="+record.Condition.HealthCapacity.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)
            +" metabolism="+hasMetabolism+" metabolismCreationId="+(hasMetabolism?metabolism.SurvivorCreationId:string.Empty)+" linked="+metabolismLinked);
    }

    private static void ExecuteCondition(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():string.Empty;
        if(mode!="meal"||p==null||p.Count<4){Output("Usage: rbsurvivor condition meal <profile> <dietTagsCsv> [varietyFamily] [entityId]");return;}
        if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Survivor] condition meal rejected: enable 'rbsurvivor debug on' first.");return;}
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null||!connection.IsServer){Output("[REBIRTH Survivor] condition meal rejected: server authority required.");return;}
        string profile=p[2]??string.Empty;float baseMood;
        if(!RebirthConditionRuntimeConfig.TryGetFoodMoodInfluence(profile,out baseMood)){Output("[REBIRTH Survivor] unknown Mood food profile: "+profile);return;}
        string family=p.Count>4&&!string.IsNullOrEmpty(p[4])?p[4]:"debug."+profile;
        EntityPlayer player=ResolvePlayer(p.Count>5?p[5]:string.Empty);
        if(player==null){Output("[REBIRTH Survivor] condition meal: player not found");return;}
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null){Output("[REBIRTH Survivor] condition meal: committed character not found");return;}
        RebirthFoodMoodDefinition food=new RebirthFoodMoodDefinition{SourceItemId="debug."+profile,MoodProfileId=profile,VarietyFamilyId=family,BaseMoodInfluence=baseMood};
        string[] tags=(p[3]??string.Empty).Split(',');for(int i=0;i<tags.Length;i++){string tag=(tags[i]??string.Empty).Trim();if(tag.Length>0)food.DietTags.Add(tag);}
        RebirthMealEvaluationResult r=RebirthDietSatisfactionService.RecordMeaningfulMeal(record,food);
        if(r==null||!r.Valid){Output("[REBIRTH Survivor] condition meal evaluation failed: "+(r!=null?r.Reason:"unavailable"));return;}
        RebirthWorldCharacterService.MarkDirty(record,"debug-condition-meal");
        RebirthSurvivorNetworkService.SendOwnerState(player,Math.Max(0L,record.Revision-1L),false,"debug-condition-meal");
        Output("conditionMeal profile="+r.MoodProfileId+" diet="+r.DietId+" compatible="+r.CompatibleWithDiet+" compatibilityState="+r.CompatibilityState+" compatibilityReason="+r.CompatibilityReason+" base="+r.BaseMoodInfluence.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" traitAdjusted="+r.TraitAdjustedMoodInfluence.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" repetition="+r.RepetitionMultiplier.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" dietModifier="+r.DietModifier.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" effective="+r.EffectiveMoodInfluence.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" satisfaction="+r.DietSatisfactionBefore.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"->"+r.DietSatisfactionAfter.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" moodTarget="+r.MoodTargetAfter.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" varieties="+r.RecentVarietyCount+"/"+r.RecentMealCount);
    }

    private static void ExecuteSupport(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        string entityArg=string.Empty;
        if(mode=="status") entityArg=p!=null&&p.Count>2?p[2]:string.Empty;
        else if(mode=="apply") entityArg=p!=null&&p.Count>3?p[3]:string.Empty;
        else if(mode=="set") entityArg=p!=null&&p.Count>5?p[5]:string.Empty;
        EntityPlayer player=ResolvePlayer(entityArg);
        if(player==null){Output("[REBIRTH Survivor] support: player not found");return;}
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null){Output("[REBIRTH Survivor] support: committed character not found");return;}
        if(mode=="status") { Output("[REBIRTH Survivor] "+RebirthTraitSupportService.BuildDebugSummary(record)); return; }
        if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Survivor] support mutation rejected: enable 'rbsurvivor debug on' first.");return;}
        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null||!connection.IsServer){Output("[REBIRTH Survivor] support mutation rejected: server authority required.");return;}
        if(mode=="apply")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor support apply <itemId> [entityId]");return;}
            RebirthTraitSupportProfileDefinition profile;
            if(!RebirthSurvivorDefinitionRegistry.TryGetSupportByItem(p[2],out profile)||profile==null){Output("[REBIRTH Survivor] support apply: no support profile binds item="+p[2]);return;}
            bool applied=RebirthTraitSupportService.TryApplyFromItem(player,p[2],"debug-trait-support-apply");
            Output("supportApply item="+p[2]+" profile="+profile.Id+" applied="+applied+" NOTE=debug simulation does not consume inventory");
            return;
        }
        if(mode=="set")
        {
            if(p==null||p.Count<5){Output("Usage: rbsurvivor support set <profileId> <grace|managed|positive|cooldown> <seconds> [entityId]");return;}
            RebirthTraitSupportProfileDefinition profile;
            if(!RebirthSurvivorDefinitionRegistry.TryGetSupport(p[2],out profile)||profile==null){Output("[REBIRTH Survivor] support set: unknown profile="+p[2]);return;}
            string timer=(p[3]??string.Empty).ToLowerInvariant();
            float seconds;
            if(!float.TryParse(p[4],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out seconds)){Output("[REBIRTH Survivor] support set: invalid seconds="+p[4]);return;}
            seconds=Math.Max(0f,seconds);
            RebirthTraitSupportRuntimeState state;
            if(!record.Support.Entries.TryGetValue(profile.Id,out state)||state==null){state=new RebirthTraitSupportRuntimeState{SupportProfileId=profile.Id};record.Support.Entries[profile.Id]=state;}
            if(timer=="grace")state.GraceRemainingActiveSeconds=seconds;
            else if(timer=="managed")state.ManagedRemainingActiveSeconds=seconds;
            else if(timer=="positive")state.PositiveRemainingActiveSeconds=seconds;
            else if(timer=="cooldown")state.CooldownRemainingActiveSeconds=seconds;
            else {Output("Usage: rbsurvivor support set <profileId> <grace|managed|positive|cooldown> <seconds> [entityId]");return;}
            RebirthWorldCharacterService.MarkDirty(record,"debug-trait-support-set");
            RebirthSurvivorNetworkService.SendOwnerState(player,Math.Max(0L,record.Revision-1L),false,"debug-trait-support-set");
            Output("supportSet profile="+profile.Id+" timer="+timer+" seconds="+seconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        Output("Usage: rbsurvivor support [status|apply|set] ...");
    }

    private static void ExecuteGear(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        string entityArg=mode=="unequip"?(p!=null&&p.Count>3?p[3]:string.Empty):(p!=null&&p.Count>2?p[2]:string.Empty);
        EntityPlayer player=ResolvePlayer(entityArg);
        if(player==null){Output("[REBIRTH Survivor] gear: player not found");return;}

        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null||!connection.IsServer)
        {
            RebirthSurvivorOwnerStateSnapshot snapshot=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(mode!="status"||snapshot==null){Output("[REBIRTH Survivor] gear mutation requires server authority.");return;}
            List<string> rows=new List<string>();
            for(int i=0;i<snapshot.GearSlots.Count;i++)if(snapshot.GearSlots[i]!=null)rows.Add(snapshot.GearSlots[i].SlotId+"="+snapshot.GearSlots[i].ItemId);
            rows.Sort(StringComparer.OrdinalIgnoreCase);
            Output("[REBIRTH Survivor] gear="+(rows.Count==0?"<none>":string.Join(";",rows.ToArray()))+" physicalBagSlots="+snapshot.PhysicalBagSlots);
            return;
        }

        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null){Output("[REBIRTH Survivor] gear: committed character not found");return;}
        if(mode=="status"){Output("[REBIRTH Survivor] "+RebirthSurvivorGearService.BuildDebugSummary(record));return;}
        if(mode=="unequip")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor gear unequip <backpack|belt|support> [entityId]");return;}
            if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Survivor] gear mutation rejected: enable 'rbsurvivor debug on' first.");return;}
            string slot=(p[2]??string.Empty).ToLowerInvariant();
            if(slot!=RebirthSurvivorGearService.BackpackSlotId&&slot!=RebirthSurvivorGearService.BeltSlotId&&slot!=RebirthSurvivorGearService.SupportSlotId){Output("Usage: rbsurvivor gear unequip <backpack|belt|support> [entityId]");return;}
            string message;bool ok=RebirthSurvivorGearService.TryUnequip(player,slot,out message);
            Output("gearUnequip slot="+slot+" success="+ok+" message="+message);return;
        }
        Output("Usage: rbsurvivor gear [status|unequip] ...");
    }

    private static void OutputServerCondition(RebirthWorldCharacterRecord record)
    {
        RebirthConditionStatusSnapshot s=RebirthSurvivorConditionService.BuildStatus(record);
        Output("[REBIRTH Survivor] condition revision="+record.Revision+" mood="+s.MoodCurrent.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" target="+s.MoodTarget.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" dietSatisfaction="+s.DietSatisfaction.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" meals="+s.RecentMealCount+" varieties="+s.RecentVarietyCount+" healthCapacity="+s.HealthCapacity.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"/"+s.HealthPotential.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" positive="+s.DominantPositiveCauseId+":"+s.DominantPositiveCauseDelta.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" negative="+s.DominantNegativeCauseId+":"+s.DominantNegativeCauseDelta.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
        Output("  multipliers energyUse="+s.EnergyUseMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" energyRecovery="+s.EnergyRecoveryMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" hydrationDemand="+s.HydrationDemandMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" nutritionDemand="+s.NutritionUseMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" capacityLoss="+s.HealthCapacityLossMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" capacityRecovery="+s.HealthCapacityRecoveryMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
        Output("  deprivation dehydrationActiveSeconds="+s.SevereDehydrationActiveSeconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" malnutritionActiveSeconds="+s.SevereMalnutritionActiveSeconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
        Output("  "+RebirthTraitSupportService.BuildDebugSummary(record));
        for(int i=0;i<record.Condition.RecentMeals.Count;i++){RebirthRecentMealState m=record.Condition.RecentMeals[i];if(m!=null)Output("  meal["+i+"] item="+m.SourceItemId+" family="+m.VarietyFamilyId+" quality="+m.MoodQuality.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" compatible="+m.CompatibleWithDiet+" ageActive="+m.AgeActiveSeconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));}
        if(record.Origin!=null)for(int i=0;i<record.Origin.TraitIds.Count;i++)
        {
            RebirthTraitDefinition trait;RebirthConditionModifierProfileDefinition profile;
            if(!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i],out trait)||trait==null||!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId,out profile)||profile==null)continue;
            for(int c=0;c<profile.Components.Count;c++){RebirthConditionModifierComponent component=profile.Components[c];if(component!=null&&string.Equals(component.Phase,"runtime",StringComparison.OrdinalIgnoreCase))Output("  traitComponent trait="+trait.Id+" target="+component.Target+" op="+component.Operation+" value="+component.Value);}
        }
    }

    private static void OutputClientCondition(RebirthSurvivorOwnerStateSnapshot s)
    {
        Output("[REBIRTH Survivor] owner condition revision="+s.CharacterRevision+" mood="+s.MoodCurrent.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" target="+s.MoodTarget.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" dietSatisfaction="+s.DietSatisfaction.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" meals="+s.RecentMealCount+" varieties="+s.RecentVarietyCount+" healthCapacity="+s.HealthCapacity.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"/"+s.HealthPotential.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" positive="+s.MoodPositiveCauseId+":"+s.MoodPositiveCauseDelta.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" negative="+s.MoodNegativeCauseId+":"+s.MoodNegativeCauseDelta.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void ExecuteKnowledge(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():string.Empty;
        if(mode!="recipe"||p==null||p.Count<3){Output("Usage: rbsurvivor knowledge recipe <recipeName> [entityId]");return;}
        EntityPlayer player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
        if(player==null){Output("[REBIRTH Survivor] knowledge recipe: player not found");return;}
        string required; bool allowed=RebirthKnowledgeService.CanCraft(player,p[2],out required);
        Output("knowledgeRecipe recipe="+p[2]+" allowed="+allowed+" required="+(string.IsNullOrEmpty(required)?"<none>":required));
    }


    private static void ExecuteSkillKnowledge(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"show";
        if(mode=="show")
        {
            EntityPlayer player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] Skill Knowledge: player not found");return;}
            RebirthWorldCharacterRecord record;
            if(player.world!=null&&!player.world.IsRemote()&&RebirthWorldCharacterService.TryGet(player,out record)&&record!=null){OutputServerSkillKnowledge(record);return;}
            RebirthSurvivorOwnerStateSnapshot snapshot=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(snapshot==null||!snapshot.HasCharacter){Output("[REBIRTH Survivor] Skill Knowledge: owner snapshot unavailable");return;}
            OutputClientProgression(snapshot,"skillknowledge");
            return;
        }
        if(mode=="get")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor skillknowledge get <skillId> [entityId]");return;}
            EntityPlayer player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] Skill Knowledge get: player not found");return;}
            RebirthSkillDefinition definition;
            if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(p[2],out definition)||definition==null){Output("[REBIRTH Survivor] Skill Knowledge get: unknown Skill="+p[2]);return;}
            float value;
            if(!RebirthSkillKnowledgeService.TryGetValue(player,definition.Id,out value)){Output("[REBIRTH Survivor] Skill Knowledge get: value unavailable for "+definition.Id);return;}
            Output("skillKnowledge skill="+definition.Id+" value="+value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" bounds="+RebirthSkillKnowledgeService.GetMinimum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+".."+RebirthSkillKnowledgeService.GetMaximum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if(mode=="study")
        {
            if(p==null||p.Count<4){Output("Usage: rbsurvivor skillknowledge study <skillId> <amount> [entityId]");return;}
            if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Survivor] Skill Knowledge study rejected: enable 'rbsurvivor debug on' first.");return;}
            ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection==null||!connection.IsServer){Output("[REBIRTH Survivor] Skill Knowledge study rejected: server authority required.");return;}
            float amount;
            if(!float.TryParse(p[3],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out amount)||amount<=0f){Output("[REBIRTH Survivor] Skill Knowledge study: amount must be > 0");return;}
            EntityPlayer player=ResolvePlayer(p.Count>4?p[4]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] Skill Knowledge study: player not found");return;}
            RebirthSkillDefinition definition;
            if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(p[2],out definition)||definition==null){Output("[REBIRTH Survivor] Skill Knowledge study: unknown Skill="+p[2]);return;}
            float applied; bool ok=RebirthSkillKnowledgeService.TryStudy(player,definition.Id,amount,"debug-console",out applied);
            float value=RebirthSkillKnowledgeService.GetValue(player,definition.Id);
            Output("skillKnowledgeStudy skill="+definition.Id+" requested="+amount.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" applied="+applied.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" value="+value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" success="+ok);
            return;
        }
        Output("Usage: rbsurvivor skillknowledge [show [entityId]|get <skillId> [entityId]|study <skillId> <amount> [entityId]]");
    }


    private static void ExecuteLiterature(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        RebirthLiteratureDefinition[] values=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
        if(mode=="summary")
        {
            int theory=0,discovery=0;
            for(int i=0;i<values.Length;i++)
            {
                RebirthLiteratureDefinition d=values[i]; if(d==null)continue;
                if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))theory++;
                else if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))discovery++;
            }
            Output("[REBIRTH Survivor] literature total="+values.Length+" theory="+theory+" discovery="+discovery+" reusable=true consumedOnRead=false");
            return;
        }
        if(mode=="item")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor literature item <itemId>");return;}
            RebirthLiteratureDefinition d;
            if(!RebirthProgressionRuntimeConfig.TryGetLiterature(p[2],out d)||d==null){Output("[REBIRTH Survivor] literature item not found: "+p[2]);return;}
            Output("literature item="+d.ItemId+" kind="+d.Kind+" skill="+(string.IsNullOrEmpty(d.SkillId)?"<none>":d.SkillId)+" amount="+d.Amount.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" knowledge="+(string.IsNullOrEmpty(d.KnowledgeId)?"<none>":d.KnowledgeId)+" marker="+(string.IsNullOrEmpty(d.MarkerId)?"<none>":d.MarkerId)+" reusable=true");
            return;
        }
        if(mode=="state")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor literature state <itemId> [entityId]");return;}
            RebirthLiteratureDefinition d;
            if(!RebirthProgressionRuntimeConfig.TryGetLiterature(p[2],out d)||d==null){Output("[REBIRTH Survivor] literature item not found: "+p[2]);return;}
            EntityPlayer player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] literature state: player unavailable");return;}
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete){Output("[REBIRTH Survivor] literature state: no committed Survivor character");return;}

            if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))
            {
                bool read=!string.IsNullOrEmpty(d.MarkerId)&&record.Progression.KnowledgeIds.Contains(d.MarkerId);
                float value=RebirthSkillKnowledgeService.GetValue(player,d.SkillId);
                Output("literature state item="+d.ItemId+" kind=theory read="+read+" reusable=true skill="+d.SkillId+" skillKnowledge="+value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" marker="+d.MarkerId);
                return;
            }
            if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            {
                bool learned=!string.IsNullOrEmpty(d.KnowledgeId)&&record.Progression.KnowledgeIds.Contains(d.KnowledgeId);
                Output("literature state item="+d.ItemId+" kind=discovery learned="+learned+" reusable=true knowledge="+d.KnowledgeId);
                return;
            }
            Output("literature state item="+d.ItemId+" kind="+d.Kind+" unsupported=true");
            return;
        }
        Output("Usage: rbsurvivor literature [summary|item <itemId>|state <itemId> [entityId]]");
    }


    private static void ExecuteStudyDiagnostics(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"status";
        EntityPlayer player;

        if(mode=="status")
        {
            player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
            if(player==null){Output("[REBIRTH Study] player unavailable");return;}
            Output(RebirthLiteratureStudySessionService.BuildDebugSummary(player));
            Output(RebirthAudiobookListeningSessionService.BuildDebugSummary(player));
            RebirthWorldCharacterRecord record;
            int partial=0;
            if(RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.Progression!=null)
                foreach(KeyValuePair<string,float> pair in record.Progression.LiteratureStudyProgress)
                    if(pair.Value>0f&&pair.Value<1f)partial++;
            Output("[REBIRTH Study] persistedPartialTitles="+partial
                +" global="+RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" physicalTrait="+RebirthTraitGameplayModifierService.GetPhysicalLiteratureStudyTimeMultiplier(player).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" audioTrait="+RebirthTraitGameplayModifierService.GetAudioLiteratureStudyTimeMultiplier(player).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        if(mode=="timing")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor study timing <literatureItemId> [entityId]");return;}
            RebirthLiteratureDefinition d;
            if(!RebirthProgressionRuntimeConfig.TryGetLiterature(p[2],out d)||d==null){Output("[REBIRTH Study] literature item not found: "+p[2]);return;}
            player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Study] player unavailable");return;}
            float global=RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier;
            float trait=RebirthTraitGameplayModifierService.GetPhysicalLiteratureStudyTimeMultiplier(player);
            float effective=Math.Max(2f,d.StudySeconds*global*trait);
            Output("[REBIRTH Study] physical item="+d.ItemId
                +" authored="+d.StudySeconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" global="+global.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" trait="+trait.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" effective="+effective.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" halfAttentionEquivalent="+(effective*2f).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        if(mode=="audio"||mode=="audiobook")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor study audio <audiobookItemId> [entityId]");return;}
            RebirthAudiobookDefinition a;
            if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(p[2],out a)||a==null){Output("[REBIRTH Study] audiobook item not found: "+p[2]);return;}
            player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Study] player unavailable");return;}
            float global=RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier;
            float trait=RebirthTraitGameplayModifierService.GetAudioLiteratureStudyTimeMultiplier(player);
            float effective=Math.Max(2f,a.AudioSeconds*global*trait);
            Output("[REBIRTH Study] audio item="+a.ItemId
                +" source="+a.SourceLiteratureId
                +" skill="+a.SkillId
                +" authored="+a.AudioSeconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" global="+global.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" trait="+trait.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" effective="+effective.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        if(mode=="music")
        {
            EntityPlayerLocal local=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
            if(local==null){Output("[REBIRTH Music] local player unavailable");return;}
            string sub=p!=null&&p.Count>2?(p[2]??string.Empty).ToLowerInvariant():"status";
            if(sub=="status"){Output(RebirthLegacyMusicPlaybackService.BuildDebugSummary(local));return;}
            if(sub=="stop")
            {
                string message;RebirthLegacyMusicPlaybackService.Stop(out message);Output("[REBIRTH Music] "+message);return;
            }
            if(sub=="volume")
            {
                if(p==null||p.Count<4){Output("Usage: rbsurvivor study music volume <0..1>");return;}
                float value;
                if(!float.TryParse(p[3],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))
                {Output("[REBIRTH Music] invalid volume: "+p[3]);return;}
                string message;RebirthLegacyMusicPlaybackService.SetVolume(value,out message);Output("[REBIRTH Music] "+message);return;
            }
            Output("Usage: rbsurvivor study music [status|stop|volume <0..1>]");
            return;
        }

        if(mode=="acceptance")
        {
            player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
            if(player==null){Output("[REBIRTH Study Acceptance] player unavailable result=CHECK");return;}

            bool serverAuthority=RebirthWorldCharacterRepository.IsServerAuthority;
            bool rebirthMode=RebirthSurvivorMode.IsEnabledForCurrentWorld();

            RebirthWorldCharacterRecord record;
            bool characterAvailable=RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete&&record.Progression!=null;

            RebirthLiteratureDefinition[] literature=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
            RebirthAudiobookDefinition[] audio=RebirthProgressionRuntimeConfig.GetAudiobookSnapshot();

            int theory=0,discovery=0,badLiteratureTime=0;
            for(int i=0;i<literature.Length;i++)
            {
                RebirthLiteratureDefinition d=literature[i];
                if(d==null)continue;
                if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))theory++;
                else if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))discovery++;
                if(d.StudySeconds<=0f)badLiteratureTime++;
            }

            int mappedAudio=0,missingAudioSource=0,badAudioTime=0,theoryAudio=0,discoveryAudio=0;
            for(int i=0;i<audio.Length;i++)
            {
                RebirthAudiobookDefinition a=audio[i];
                if(a==null)continue;
                RebirthLiteratureDefinition source;
                if(!RebirthProgressionRuntimeConfig.TryGetLiterature(a.SourceLiteratureId,out source)||source==null)
                {
                    missingAudioSource++;
                    continue;
                }
                mappedAudio++;
                if(string.Equals(source.Kind,"theory",StringComparison.OrdinalIgnoreCase))theoryAudio++;
                else if(string.Equals(source.Kind,"discovery",StringComparison.OrdinalIgnoreCase))discoveryAudio++;
                if(a.AudioSeconds<=0f)badAudioTime++;
            }

            bool readerTraitsExclusive=true;
            int partialTitles=0,badPartial=0;
            if(characterAvailable)
            {
                if(record.Origin!=null)
                    readerTraitsExclusive=!(record.Origin.TraitIds.Contains("trait.fast_reader")&&record.Origin.TraitIds.Contains("trait.slow_reader"));

                foreach(KeyValuePair<string,float> pair in record.Progression.LiteratureStudyProgress)
                {
                    if(pair.Value>0f&&pair.Value<1f)partialTitles++;
                    if(pair.Value<=0f||pair.Value>=1f)badPartial++;
                }
            }

            string readItem;float readProgress,readRemaining;bool readSlow;
            bool readingActive=RebirthLiteratureStudySessionService.TryGetUiState(player,out readItem,out readProgress,out readRemaining,out readSlow);

            string audioItem,sourceItem;float audioProgress,audioRemaining;
            bool audioActive=RebirthAudiobookListeningSessionService.TryGetUiState(player,out audioItem,out sourceItem,out audioProgress,out audioRemaining);

            bool oneMedium=!(readingActive&&audioActive);

            RebirthStudyHudMode clientHudMode;
            string clientHudItem;
            float clientHudProgress,clientHudRemaining;
            bool clientHudSlow;
            bool clientHudActive=RebirthStudyHudClientState.TryGet(out clientHudMode,out clientHudItem,out clientHudProgress,out clientHudRemaining,out clientHudSlow);

            float global=RebirthSandboxOptionManager.Current.LiteratureStudyTimeMultiplier;
            float physicalTrait=RebirthTraitGameplayModifierService.GetPhysicalLiteratureStudyTimeMultiplier(player);
            float audioTrait=RebirthTraitGameplayModifierService.GetAudioLiteratureStudyTimeMultiplier(player);
            bool timingMultipliersValid=global>0f&&physicalTrait>0f&&audioTrait>0f;

            bool catalogPass=literature.Length==187
                &&theory==84
                &&discovery==103
                &&audio.Length==155
                &&mappedAudio==155
                &&missingAudioSource==0
                &&theoryAudio==84
                &&discoveryAudio==71
                &&badLiteratureTime==0
                &&badAudioTime==0;

            bool statePass=characterAvailable
                &&readerTraitsExclusive
                &&badPartial==0
                &&oneMedium
                &&timingMultipliersValid;

            bool result=serverAuthority&&rebirthMode&&catalogPass&&statePass;

            Output("[REBIRTH Study Acceptance] environment"
                +" serverAuthority="+serverAuthority
                +" rebirthMode="+rebirthMode
                +" characterAvailable="+characterAvailable);

            Output("[REBIRTH Study Acceptance] catalogue"
                +" literature="+literature.Length
                +" theory="+theory
                +" discovery="+discovery
                +" badLiteratureTime="+badLiteratureTime
                +" audiobooks="+audio.Length
                +" mappedAudio="+mappedAudio
                +" missingAudioSource="+missingAudioSource
                +" theoryAudio="+theoryAudio
                +" discoveryAudio="+discoveryAudio
                +" badAudioTime="+badAudioTime
                +" result="+(catalogPass?"PASS":"CHECK"));

            Output("[REBIRTH Study Acceptance] persistence"
                +" partialTitles="+partialTitles
                +" invalidPartialEntries="+badPartial
                +" readerTraitsExclusive="+readerTraitsExclusive);

            Output("[REBIRTH Study Acceptance] active"
                +" reading="+readingActive
                +" audiobook="+audioActive
                +" oneMediumOnly="+oneMedium
                +" readingItem="+(readingActive?readItem:"<none>")
                +" audiobookItem="+(audioActive?audioItem:"<none>"));

            Output("[REBIRTH Study Acceptance] clientHud"
                +" active="+clientHudActive
                +" mode="+clientHudMode
                +" item="+(clientHudActive?clientHudItem:"<none>")
                +" progress="+clientHudProgress.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" slow="+clientHudSlow);

            Output("[REBIRTH Study Acceptance] timing"
                +" global="+global.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" physicalTrait="+physicalTrait.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" audioTrait="+audioTrait.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" valid="+timingMultipliersValid);

            Output("[REBIRTH Study Acceptance] RESULT="+(result?"PASS":"CHECK")
                +" NOTE=This verifies live runtime invariants only; perform the documented action/save/multiplayer scenarios before declaring full acceptance.");
            return;
        }

        if(mode=="verify")
        {
            player=ResolvePlayer(p!=null&&p.Count>2?p[2]:string.Empty);
            if(player==null){Output("[REBIRTH Study Verify] player unavailable");return;}
            RebirthAudiobookDefinition[] audio=RebirthProgressionRuntimeConfig.GetAudiobookSnapshot();
            int mapped=0,missing=0,discoverySources=0,badTime=0;
            for(int i=0;i<audio.Length;i++)
            {
                RebirthAudiobookDefinition a=audio[i]; if(a==null)continue;
                RebirthLiteratureDefinition d;
                if(!RebirthProgressionRuntimeConfig.TryGetLiterature(a.SourceLiteratureId,out d)||d==null){missing++;continue;}
                mapped++;
                if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))discoverySources++;
                if(a.AudioSeconds<=0f||d.StudySeconds<=0f)badTime++;
            }
            RebirthWorldCharacterRecord record; bool exclusive=true;
            if(RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.Origin!=null)
                exclusive=!(record.Origin.TraitIds.Contains("trait.fast_reader")&&record.Origin.TraitIds.Contains("trait.slow_reader"));
            Output("[REBIRTH Study Verify] audiobooks="+audio.Length
                +" mapped="+mapped
                +" missing="+missing
                +" discoverySources="+discoverySources
                +" badTime="+badTime
                +" readerTraitsExclusive="+exclusive
                +" result="+((audio.Length==155&&mapped==155&&missing==0&&badTime==0&&exclusive)?"PASS":"CHECK"));
            return;
        }

        Output("Usage: rbsurvivor study [status [entityId]|timing <literatureItemId> [entityId]|audio <audiobookItemId> [entityId]|music [status|stop|volume <0..1>]|verify [entityId]|acceptance [entityId]]");
    }


    private static void ExecuteRecipeCatalogue(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary")
        {
            Output(RebirthRecipeCatalogueAuditService.BuildSummary(false));
            return;
        }
        if(mode=="export")
        {
            string path,detail;
            bool ok=RebirthRecipeCatalogueAuditService.TryExport(out path,out detail);
            Output("[REBIRTH Survivor][RecipeCatalogue] export ok="+ok+" "+detail);
            return;
        }
        if(mode=="unmapped")
        {
            int max=80;
            if(p!=null&&p.Count>2)int.TryParse(p[2],out max);
            Output(RebirthRecipeCatalogueAuditService.BuildUnmappedLearnableReport(max));
            return;
        }
        if(mode=="show")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor recipecatalogue show <recipeName>");return;}
            Output(RebirthRecipeCatalogueAuditService.BuildRecipeDiagnostic(p[2]));
            return;
        }
        Output("Usage: rbsurvivor recipecatalogue [summary|export|unmapped [maxRows]|show <recipeName>]");
    }

    private static void ExecuteCraftPolicy(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary") { Output(RebirthCraftingProgressionRegistry.BuildSummary()); return; }
        if(mode=="vectors"||mode=="test") { Output(RebirthCraftingProgressionVectorHarness.RunAll()); return; }
        if(mode=="recipe")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor craftpolicy recipe <recipeName> [entityId]");return;}
            EntityPlayer player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] craftpolicy recipe: player not found");return;}
            Output(RebirthCraftingProgressionRegistry.BuildRecipeDiagnostic(player,p[2]));
            return;
        }
        Output("Usage: rbsurvivor craftpolicy [summary|recipe <recipeName> [entityId]|vectors]");
    }

    private static void ExecuteCapability(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"summary";
        if(mode=="summary")
        {
            Output("[REBIRTH Survivor] Capability registry ready="+RebirthCapabilityRegistry.IsReady+" capabilities="+RebirthCapabilityRegistry.CapabilityCount+" recipeCapabilities="+RebirthCapabilityRegistry.RecipeCapabilityCount+" projects="+RebirthCapabilityRegistry.ProjectCount+" blueprints="+RebirthCapabilityRegistry.BlueprintCount);
            RebirthCapabilityDefinition[] all=RebirthCapabilityRegistry.GetCapabilitiesSnapshot();
            for(int i=0;i<all.Length;i++) Output("  "+all[i].Id+" target="+all[i].TargetType+":"+all[i].TargetId+" category="+all[i].Category+" legacy="+all[i].LegacyAdapted);
            return;
        }
        if(mode=="recipe")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor capability recipe <recipeName> [entityId]");return;}
            EntityPlayer player=ResolvePlayer(p.Count>3?p[3]:string.Empty);
            if(player==null){Output("[REBIRTH Survivor] capability recipe: player not found");return;}
            Output(RebirthCapabilityService.BuildRecipeDiagnostic(player,p[2]));
            return;
        }
        if(mode=="skill"||mode=="knowledge")
        {
            if(p==null||p.Count<3){Output("Usage: rbsurvivor capability "+mode+" <id>");return;}
            RebirthCapabilityDefinition[] values=mode=="skill"?RebirthCapabilityRegistry.GetBySkill(p[2]):RebirthCapabilityRegistry.GetByKnowledge(p[2]);
            Output("[REBIRTH Survivor] capability "+mode+"="+p[2]+" references="+values.Length);
            for(int i=0;i<values.Length;i++)Output("  "+values[i].Id+" -> "+values[i].TargetType+":"+values[i].TargetId+" category="+values[i].Category);
            return;
        }
        Output("Usage: rbsurvivor capability [summary|recipe <recipeName> [entityId]|skill <skillId>|knowledge <knowledgeId>]");
    }

    private static void OutputServerProgression(RebirthWorldCharacterRecord record)
    {
        Output("[REBIRTH Survivor] progression revision="+record.Revision+" healthPotential="+record.Progression.HealthPotential.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" skillKnowledge="+record.Progression.SkillKnowledge.Count+" legacyKnowledge="+record.Progression.KnowledgeIds.Count);
        List<string> attrKeys=new List<string>(record.Progression.Attributes.Keys); attrKeys.Sort(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<attrKeys.Count;i++){RebirthAttributeRuntimeState a=record.Progression.Attributes[attrKeys[i]];Output("  attribute "+a.AttributeId+" current="+a.Current.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" potential="+a.Potential.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));}
        List<string> skillKeys=new List<string>(record.Progression.Skills.Keys); skillKeys.Sort(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<skillKeys.Count;i++){RebirthSkillRuntimeState s=record.Progression.Skills[skillKeys[i]];Output("  skill "+s.SkillId+" value="+s.Value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" progress="+s.Progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));}
    }

    private static void OutputServerKnowledge(RebirthWorldCharacterRecord record)
    {
        List<string> ids=new List<string>(record.Progression.KnowledgeIds);ids.Sort(StringComparer.OrdinalIgnoreCase);
        Output("[REBIRTH Survivor] legacy binary knowledge owned="+ids.Count);
        for(int i=0;i<ids.Count;i++)Output("  "+ids[i]+" ("+RebirthKnowledgeService.GetDisplayName(ids[i])+")");
    }

    private static void OutputServerSkillKnowledge(RebirthWorldCharacterRecord record)
    {
        List<string> ids=new List<string>(record.Progression.SkillKnowledge.Keys);ids.Sort(StringComparer.OrdinalIgnoreCase);
        Output("[REBIRTH Survivor] Skill Knowledge areas="+ids.Count+" revision="+record.Revision+" bounds="+RebirthSkillKnowledgeService.GetMinimum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+".."+RebirthSkillKnowledgeService.GetMaximum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
        for(int i=0;i<ids.Count;i++)
        {
            RebirthSkillKnowledgeRuntimeState state=record.Progression.SkillKnowledge[ids[i]];
            if(state!=null)Output("  theory "+state.SkillId+" value="+state.Value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static void OutputClientProgression(RebirthSurvivorOwnerStateSnapshot snapshot,string mode)
    {
        if(mode=="knowledge")
        {
            List<string> ids=new List<string>(snapshot.KnowledgeIds);ids.Sort(StringComparer.OrdinalIgnoreCase);
            Output("[REBIRTH Survivor] owner legacy binary knowledge owned="+ids.Count+" revision="+snapshot.CharacterRevision);
            for(int i=0;i<ids.Count;i++)Output("  "+ids[i]+" ("+RebirthKnowledgeService.GetDisplayName(ids[i])+")");
            return;
        }
        if(mode=="skillknowledge")
        {
            List<RebirthSurvivorOwnerSkillKnowledgeSnapshot> rows=new List<RebirthSurvivorOwnerSkillKnowledgeSnapshot>(snapshot.SkillKnowledge);
            rows.Sort(delegate(RebirthSurvivorOwnerSkillKnowledgeSnapshot a,RebirthSurvivorOwnerSkillKnowledgeSnapshot z){return StringComparer.OrdinalIgnoreCase.Compare(a!=null?a.Id:string.Empty,z!=null?z.Id:string.Empty);});
            Output("[REBIRTH Survivor] owner Skill Knowledge areas="+rows.Count+" revision="+snapshot.CharacterRevision+" bounds="+RebirthSkillKnowledgeService.GetMinimum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+".."+RebirthSkillKnowledgeService.GetMaximum().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            for(int i=0;i<rows.Count;i++)if(rows[i]!=null)Output("  theory "+rows[i].Id+" value="+rows[i].Value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        Output("[REBIRTH Survivor] owner progression revision="+snapshot.CharacterRevision+" attributes="+snapshot.Attributes.Count+" skills="+snapshot.Skills.Count+" skillKnowledge="+snapshot.SkillKnowledge.Count+" legacyKnowledge="+snapshot.KnowledgeIds.Count);
        for(int i=0;i<snapshot.Attributes.Count;i++)if(snapshot.Attributes[i]!=null)Output("  attribute "+snapshot.Attributes[i].Id+" current="+snapshot.Attributes[i].Current.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" potential="+snapshot.Attributes[i].Potential.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
        for(int i=0;i<snapshot.Skills.Count;i++)if(snapshot.Skills[i]!=null)Output("  skill "+snapshot.Skills[i].Id+" value="+snapshot.Skills[i].Value.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" progress="+snapshot.Skills[i].Progress.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void ExecuteCreate(List<string> p)
    {
        string mode=p!=null&&p.Count>1?(p[1]??string.Empty).ToLowerInvariant():"";
        if(mode=="validate")
        {
            if(p==null||p.Count<4){Output("Usage: rbsurvivor create validate <backgroundId> <dietId> [traitId,traitId,...]");return;}
            RebirthSurvivorCreationSelection selection=BuildSelection(p[2],p[3],p.Count>4?p[4]:string.Empty);
            RebirthSurvivorCreationResult result=RebirthSurvivorCreationService.Validate(selection);
            Output(FormatValidation(result));
            return;
        }

        if(mode=="force")
        {
            if(!RebirthSurvivorDebug.Enabled){Output("[REBIRTH Survivor] force-create rejected: enable 'rbsurvivor debug on' first.");return;}
            ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection==null||!connection.IsServer){Output("[REBIRTH Survivor] force-create rejected: server authority required.");return;}
            if(p==null||p.Count<5){Output("Usage: rbsurvivor create force <entityId> <backgroundId> <dietId> [traitId,traitId,...]");return;}
            EntityPlayer player=ResolvePlayer(p[2]);
            if(player==null){Output("[REBIRTH Survivor] force-create: player not found");return;}
            RebirthStablePlayerIdentity identity;
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null){Output("[REBIRTH Survivor] force-create: stable identity unavailable");return;}
            RebirthSurvivorCreationSelection selection=BuildSelection(p[3],p[4],p.Count>5?p[5]:string.Empty);
            RebirthSurvivorCreationNetworkRequest request=new RebirthSurvivorCreationNetworkRequest
            {
                PlayerEntityId=player.entityId,
                RequestId=RebirthSurvivorClientState.NextRequestId(),
                Operation=RebirthSurvivorCreationNetworkOperation.Commit,
                ClientDefinitionHash=RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty,
                ClientDefinitionVersion=RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty,
                BackgroundId=selection.BackgroundId,
                DietId=selection.DietId,
                SourceProfileId="debug-force",
                SourceProfileName="DEBUG FORCE CREATE"
            };
            for(int i=0;i<selection.TraitIds.Count;i++)request.TraitIds.Add(selection.TraitIds[i]);
            RebirthSurvivorCreationNetworkResponse response=RebirthSurvivorCreationTransactions.Execute(player,identity,request);
            Output("forceCreate entity="+player.entityId+" status="+response.Status+" revision="+response.CharacterRevision+" message="+response.MessageCode+" replay="+response.WasReplay);
            return;
        }

        Output("Usage: rbsurvivor create [validate|force] ...");
    }

    private static RebirthSurvivorCreationSelection BuildSelection(string backgroundId,string dietId,string traitCsv)
    {
        List<string> traits=new List<string>();
        string[] parts=(traitCsv??string.Empty).Split(',');
        for(int i=0;i<parts.Length;i++){string t=(parts[i]??string.Empty).Trim();if(t.Length>0)traits.Add(t);}
        return new RebirthSurvivorCreationSelection(backgroundId,dietId,traits,RebirthSurvivorDefinitionRegistry.SemanticHash);
    }

    private static string FormatValidation(RebirthSurvivorCreationResult r)
    {
        if(r==null)return "[REBIRTH Survivor] validation result unavailable";
        StringBuilder b=new StringBuilder();
        b.Append("[REBIRTH Survivor] validation valid=").Append(r.IsValid)
            .Append(" background=").Append(r.BackgroundId).Append(" diet=").Append(r.DietId)
            .Append(" traits=").Append(r.TraitIds.Count).Append(" points=").Append(r.RemainingCreationPoints)
            .Append(" healthPotential=").Append(r.HealthPotential.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture))
            .Append(" attributes=").Append(r.Attributes.Count).Append(" skills=").Append(r.StartingSkills.Count)
            .Append(" skillKnowledge=").Append(r.StartingSkillKnowledge.Count).Append(" legacyKnowledge=").Append(r.StartingKnowledgeIds.Count);
        for(int i=0;i<r.Errors.Count;i++)b.Append("\n  ERROR ").Append(r.Errors[i].ToString());
        return b.ToString();
    }

    private static EntityPlayer ResolvePlayer(string entityIdText)
    {
        if(GameManager.Instance==null||GameManager.Instance.World==null)return null;
        int entityId;
        if(!string.IsNullOrEmpty(entityIdText)&&int.TryParse(entityIdText,out entityId))
            return GameManager.Instance.World.GetEntity(entityId) as EntityPlayer;
        EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();
        return local;
    }

    private static string BuildSummary(RebirthSurvivorDefinitionBundle b)
    {
        int universal=0,restricted=0; foreach(RebirthTraitDefinition t in b.Traits) { if(t.Availability==RebirthDefinitionAvailability.Universal)universal++; else if(t.Availability==RebirthDefinitionAvailability.Restricted)restricted++; }
        return "[REBIRTH Survivor] defs ready="+RebirthSurvivorDefinitionRegistry.IsReady
            +" mode="+RebirthSurvivorMode.ConfiguredMode
            +" backgrounds="+b.Backgrounds.Count
            +" traits="+b.Traits.Count+" (universal="+universal+", restricted="+restricted+")"
            +" diets="+b.Diets.Count+" skills="+b.Progression.Skills.Count+" skillKnowledgeAreas="+b.Progression.Skills.Count+" legacyKnowledge="+b.Progression.Knowledge.Count
            +" modifiers="+b.ModifierProfiles.Count+" support="+b.SupportProfiles.Count
            +" version="+RebirthSurvivorDefinitionRegistry.DefinitionVersion
            +" hash="+RebirthSurvivorDefinitionRegistry.SemanticHash;
    }
    private static string Join(IEnumerable<string> values) { StringBuilder b=new StringBuilder(); foreach(string v in values) { if(b.Length>0)b.Append(','); b.Append(v); } return b.ToString(); }
    private static void Output(string text) { if(string.IsNullOrEmpty(text))return; string[] lines=text.Replace("\r",string.Empty).Split('\n'); for(int i=0;i<lines.Length;i++) if(lines[i].Length>0) { SdtdConsole.Instance.Output(lines[i]); Log.Out(lines[i]); } }
}
