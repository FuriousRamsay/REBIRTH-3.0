#!/usr/bin/env python3
import xml.etree.ElementTree as ET
"""Static validator for the approved REBIRTH 3.0 UI redesign.

Run from the mod root:
  python BuildScripts/UI/validate_rebirth_ui_redesign.py
Optional native source comparison:
  python BuildScripts/UI/validate_rebirth_ui_redesign.py --base-xui-dir <7DTD Config/XUi_InGame>

The validator intentionally checks source ownership and banned hallucinated controls;
it does not infer new gameplay functionality from concept art.
"""
from pathlib import Path
import argparse, csv, re, sys, xml.etree.ElementTree as ET

TARGETS = {
    "crafting": ["crafting", "windowCraftingList", "craftingInfoPanel", "windowCraftingQueue"],
    "station_crafting": ["workstation_campfire", "windowToolsCampfire", "windowFuel", "windowOutput"],
    "creative": ["creative", "windowCreative2"],
    "looting": ["looting", "windowLooting", "windowBackpack"],
    "map": ["map", "mapArea", "mapTracking", "mapInvites"],
    "quests": ["quests", "windowQuestList", "windowQuestSharedList", "windowQuestDescription", "windowQuestObjectives", "windowQuestRewards"],
    "challenges": ["challenges", "windowChallengeList", "windowChallengeEntryDescription"],
    "players": ["players"],
    "character": ["character", "CharacterFrameWindow", "rebirthSurvivorCharacter", "rebirthSurvivorCharacterWindow", "rebirthProgressionExplorerWindow"],
}
FOUNDATION = [
    "rebirth_ui_panel", "rebirth_ui_inner_panel", "rebirth_ui_section_band",
    "rebirth_ui_table_row", "rebirth_ui_divider", "rebirth_ui_progress_track"
]
# Explicitly rejected concepts from the approved-design correction history.
BANNED_LITERAL_PATTERNS = [
    r"Remote Storage", r"Add Friend", r">\s*Message\s*<", r">\s*Mute\s*<",
    r"Tracking Options", r"Radio Outpost", r"Sort By"
]


def parse(path, errors):
    try:
        return ET.parse(path)
    except Exception as e:
        errors.append(f"XML parse failed: {path}: {e}")
        return None


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--project-root', default='.')
    ap.add_argument('--base-xui-dir')
    args=ap.parse_args()
    root=Path(args.project_root).resolve()
    cfg=root/'Config/XUi_InGame'
    errors=[]; warnings=[]
    for fn in ('xui.xml','windows.xml','controls.xml','templates.xml'):
        p=cfg/fn
        if not p.exists(): errors.append(f"Missing project XUi file: {p}")
        else: parse(p,errors)

    templates=(cfg/'templates.xml').read_text(encoding='utf-8',errors='replace') if (cfg/'templates.xml').exists() else ''
    for name in FOUNDATION:
        c=len(re.findall(rf'<{re.escape(name)}(?:\s|>)', templates))
        if c != 1: errors.append(f"Expected exactly one shared template {name}; found {c}")

    scope='\n'.join((cfg/f).read_text(encoding='utf-8',errors='replace') for f in ('xui.xml','windows.xml','controls.xml','templates.xml') if (cfg/f).exists())
    for pat in BANNED_LITERAL_PATTERNS:
        if re.search(pat,scope,re.I): warnings.append(f"Rejected-concept literal found in active in-game XUi source: {pat}")

    # Chunk B implementation invariants. These are source-level checks against the
    # patch authored in Config/XUi_InGame/windows.xml; runtime controller behavior is
    # still verified in-game.
    windows_text=(cfg/'windows.xml').read_text(encoding='utf-8',errors='replace') if (cfg/'windows.xml').exists() else ''
    chunk_b_required = {
        'chunk_b_marker': 'REBIRTH UI REDESIGN - CHUNK B: CREATIVE + LOOTING',
        'creative_width': "window[@name='windowCreative2']\" name=\"width\">870",
        'creative_10_columns': "window[@name='windowCreative2']/rect[@name='content']/grid[@name='queue']\" name=\"cols\">10",
        'looting_height': "window[@name='windowLooting']\" name=\"height\">741",
        'looting_compact_cells': "window[@name='windowLooting']/rect[@name='content']/grid[@name='queue']\" name=\"cell_width\">65",
        'looting_category_shortcut_removed': "button[@name='btnRebirthQuickStackCategories']\"/",
        'creative_frame': 'rebirthCreativeApprovedFrame',
        'looting_frame': 'rebirthLootingApprovedFrame',
        'backpack_frame': 'rebirthBackpackApprovedFrame',
    }
    for key, literal in chunk_b_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk B invariant missing: {key}")

    if 'name="btnRebirthRemoteResources"' not in windows_text:
        errors.append('Chunk B invariant missing: verified Remote Resources/Wi-Fi control')


    # Chunk C implementation invariants. These verify the source-authored layout
    # contract and the explicit Survivor Profile Creator visual-language correction.
    chunk_c_required = {
        'chunk_c_marker': 'REBIRTH UI REDESIGN - CHUNK C: MAP + QUESTS + CHALLENGES',
        'map_width': "window[@name='mapArea']\" name=\"width\">1000",
        'map_height': "window[@name='mapArea']\" name=\"height\">805",
        'map_frame': 'rebirthMapApprovedFrame',
        'map_tracking_frame': 'rebirthMapTrackingApprovedFrame',
        'map_invites_frame': 'rebirthMapInvitesApprovedFrame',
        'quest_list_width': "window[@name='windowQuestList']\" name=\"width\">560",
        'quest_detail_width': "window[@name='windowQuestDescription']\" name=\"width\">780",
        'quest_list_frame': 'rebirthQuestListApprovedFrame',
        'quest_objectives_frame': 'rebirthQuestObjectivesApprovedFrame',
        'quest_rewards_frame': 'rebirthQuestRewardsApprovedFrame',
        'challenge_list_width': "window[@name='windowChallengeList']\" name=\"width\">800",
        'challenge_detail_width': "window[@name='windowChallengeEntryDescription']\" name=\"width\">650",
        'challenge_list_frame': 'rebirthChallengeListApprovedFrame',
        'challenge_detail_frame': 'rebirthChallengeDetailsApprovedFrame',
        'survivor_red_accent': 'color=\"228,18,21,255\"',
    }
    for key, literal in chunk_c_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk C invariant missing: {key}")

    # Chunk D Character Overview invariants. The overview is a read-only aggregate over
    # existing native/REBIRTH state and may only expose verified equipment/gear slots.
    chunk_d_required = {
        'character_overview_tab': 'btnSurvivorTabOverview',
        'character_overview_panel': 'survivorOverviewPanel',
        'character_overview_model': 'survivorOverviewModel',
        'character_overview_background': 'survivorOverviewBackgroundArt',
        'character_survivor_style_shadow': '<boxshadow_new color="228,18,21,170" on_press="true"/>',
        'character_survivor_style_frame': '<boxbg backcolor="12,12,12,254" bordersprite="menu_empty2px" bordercolor="64,64,64,255"/>',
        'character_real_head_slot': '<equipment_stack_sdcs slot="Head"',
        'character_real_chest_slot': '<equipment_stack_sdcs slot="Chest"',
        'character_real_hands_slot': '<equipment_stack_sdcs slot="Hands"',
        'character_real_feet_slot': '<equipment_stack_sdcs slot="Feet"',
        'character_real_biome_slot': '<equipment_stack_sdcs slot="BiomeBadge"',
        'character_rebirth_backpack': 'survivorOverviewGearBackpackIcon',
        'character_rebirth_belt': 'survivorOverviewGearBeltIcon',
        'character_rebirth_support': 'survivorOverviewGearSupportIcon',
    }
    for key, literal in chunk_d_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk D invariant missing: {key}")
    xui_text=(cfg/'xui.xml').read_text(encoding='utf-8',errors='replace') if (cfg/'xui.xml').exists() else ''
    if 'name="rebirthSurvivorCharacter" close_compass_on_open="true" defaultselected="btnSurvivorTabOverview"' not in xui_text:
        errors.append('Chunk D invariant missing: Character group default Overview selection')
    for rejected in ('FUTURE SLOT', 'Future Slot', 'future slot'):
        if rejected in windows_text:
            errors.append('Chunk D rejected Character placeholder present: '+rejected)

    # Chunk E Character Progression invariants. The page must remain definition/state driven:
    # the current authoritative Skills, associated Knowledge, and only the actual Survivor Attributes.
    chunk_e_required = {
        'chunk_e_marker': 'REBIRTH UI REDESIGN - CHUNK E: CHARACTER PROGRESSION',
        'character_progression_tab': 'btnSurvivorTabProgression',
        'character_progression_panel': 'survivorProgressionPanel',
        'character_progression_skills_content': 'survivorProgressionSkillsContent',
        'character_progression_knowledge_content': 'survivorProgressionKnowledgeContent',
        'character_progression_selected_skill': 'survivorProgressionSelectedName',
        'character_progression_explorer': 'btnSurvivorProgressionExploreSelected',
        'character_progression_practical': 'xuiRebirthProgressionPractical',
        'character_progression_skill_knowledge': 'xuiRebirthProgressionSkillKnowledge',
    }
    for key, literal in chunk_e_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk E invariant missing: {key}")
    skill_row_count=len(re.findall(r'name="survivorProgressionSkillRow\d+"', windows_text))
    knowledge_row_count=len(re.findall(r'name="survivorProgressionKnowledgeRow\d+"', windows_text))
    skill_button_count=len(re.findall(r'name="btnSurvivorProgressionSkill\d+"', windows_text))
    knowledge_button_count=len(re.findall(r'name="btnSurvivorProgressionKnowledge\d+"', windows_text))
    progression_path=root/'Config'/'_Survivor'/'progression.xml'
    authoritative_skill_count=0
    if progression_path.exists():
        try:
            authoritative_skill_count=len(ET.parse(progression_path).getroot().findall('./skills/skill'))
        except Exception as ex:
            errors.append(f'Unable to resolve authoritative Skill count from progression.xml: {ex}')
    if authoritative_skill_count <= 0:
        errors.append('Authoritative Skill catalogue is empty/unavailable')
    if skill_row_count != authoritative_skill_count or skill_button_count != authoritative_skill_count:
        errors.append(f'Progression UI expected {authoritative_skill_count} authoritative Skill rows/buttons; found rows={skill_row_count} buttons={skill_button_count}')
    if knowledge_row_count != 24 or knowledge_button_count != 24:
        errors.append(f'Chunk E expected 24 associated-Knowledge rows/buttons; found rows={knowledge_row_count} buttons={knowledge_button_count}')
    # Hidden legacy tabs may remain as fallback wiring, but they must not be visible alongside the unified Progression tab.
    for legacy in ('btnSurvivorTabAttributes','btnSurvivorTabSkills','btnSurvivorTabKnowledge'):
        m=re.search(rf'<labeledbutton name="{legacy}"[^>]*>', windows_text)
        if m is None or 'visible="false"' not in m.group(0) or 'pos="-5000,-5000"' not in m.group(0):
            errors.append(f'Chunk E legacy Character tab is not hidden fallback-only: {legacy}')
    # These concept-art/system inventions are explicitly not authorized for the live Progression page.
    progression_start=windows_text.find('name="survivorProgressionPanel"')
    progression_end=windows_text.find('name="survivorMainContent"', progression_start)
    progression_scope=windows_text[progression_start:progression_end if progression_end > progression_start else len(windows_text)]
    for rejected in ('Intelligence','Perception','Recent Gains','Recently Discovered','Favorites','Sort By'):
        if re.search(re.escape(rejected), progression_scope, re.I):
            errors.append('Chunk E rejected Progression concept present: '+rejected)

    # Chunk F Character Conditions invariants. This full-width page is a read-only projection
    # over real Survivor/Metabolism/native buff state. Unsupported concept panels must not be authored.
    chunk_f_required = {
        'chunk_f_marker': 'REBIRTH UI REDESIGN - CHUNK F: CHARACTER CONDITIONS',
        'condition_panel': 'name="survivorConditionPanel" pos="10,-112" width="1744" height="760"',
        'condition_list_content': 'survivorConditionListContent',
        'condition_selected_name': 'survivorConditionSelectedName',
        'condition_selected_status': 'survivorConditionSelectedStatus',
        'condition_selected_effects': 'survivorConditionSelectedEffects',
        'condition_selected_guidance': 'survivorConditionSelectedGuidance',
        'condition_selected_source': 'survivorConditionSelectedSource',
        'condition_system_state': 'survivorConditionMetabolismSummary',
        'condition_history': 'survivorConditionHistoryContent',
    }
    for key, literal in chunk_f_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk F invariant missing: {key}")
    condition_panel_count=len(re.findall(r'name="survivorConditionPanel"', windows_text))
    condition_row_count=len(re.findall(r'name="survivorConditionRow\d+"', windows_text))
    condition_button_count=len(re.findall(r'name="btnSurvivorCondition\d+"', windows_text))
    condition_history_count=len(re.findall(r'name="survivorConditionHistoryRow\d+"', windows_text))
    if condition_panel_count != 1:
        errors.append(f'Chunk F expected exactly one Character Conditions panel; found {condition_panel_count}')
    if condition_row_count != 32 or condition_button_count != 32:
        errors.append(f'Chunk F expected 32 condition rows/buttons; found rows={condition_row_count} buttons={condition_button_count}')
    if condition_history_count != 10:
        errors.append(f'Chunk F expected 10 bounded visible condition-history rows; found {condition_history_count}')
    for legacy in ('survivorConditionMoodValue','survivorConditionDietValue','survivorConditionHealthValue','survivorConditionText','survivorConditionSupportText'):
        if legacy in windows_text:
            errors.append(f'Chunk F legacy Conditions presentation remains active: {legacy}')
    condition_start=windows_text.find('name="survivorConditionPanel"')
    condition_end=windows_text.find('name="survivorMainContent"', condition_start)
    condition_scope=windows_text[condition_start:condition_end if condition_end > condition_start else len(windows_text)]
    for rejected in ('ATTRIBUTE IMPACT','RESISTANCES','TREATMENT','VIEW FULL LOG'):
        if re.search(re.escape(rejected), condition_scope, re.I):
            errors.append('Chunk F unsupported Conditions concept present: '+rejected)

    # Chunk G Character Statistics invariants. Statistics must be backed by the new
    # persisted/server-authoritative tracking contract rather than concept-art placeholders.
    chunk_g_required = {
        'chunk_g_marker': 'REBIRTH UI REDESIGN - CHUNK G: CHARACTER STATISTICS',
        'statistics_tab': 'btnSurvivorTabStatistics',
        'statistics_panel': 'name="survivorStatisticsPanel" pos="10,-112" width="1744" height="760"',
        'statistics_controller': 'controller="RebirthSurvivorStatisticsPanel, RebirthUtils"',
        'statistics_activity': 'xuiRebirthStatisticsActivitySummary',
        'statistics_trend': 'xuiRebirthStatisticsSurvivalTrend',
        'statistics_categories': 'xuiRebirthStatisticsTopCategories',
        'statistics_combat': 'xuiRebirthStatisticsCombatBreakdown',
        'statistics_bests': 'xuiRebirthStatisticsPersonalBests',
        'statistics_milestones': 'xuiRebirthStatisticsMilestones',
    }
    for key, literal in chunk_g_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk G invariant missing: {key}")
    stats_start=windows_text.find('name="survivorStatisticsPanel"')
    stats_end=windows_text.find('name="survivorMainContent"', stats_start)
    stats_scope=windows_text[stats_start:stats_end if stats_end > stats_start else len(windows_text)]
    counts = {
        'KPI values': (len(re.findall(r'name="survivorStatisticsKpiValue\d+"', stats_scope)), 8),
        'activity fills': (len(re.findall(r'name="survivorStatisticsActivityFill\d+"', stats_scope)), 5),
        'activity values': (len(re.findall(r'name="survivorStatisticsActivityValue\d+"', stats_scope)), 5),
        'trend days': (len(re.findall(r'name="survivorStatisticsTrendDay\d+"', stats_scope)), 7),
        'trend fills': (len(re.findall(r'name="survivorStatisticsTrendFill\d+_\d+"', stats_scope)), 35),
        'category values': (len(re.findall(r'name="survivorStatisticsCategoryValue\d+"', stats_scope)), 8),
        'weapon rows': (len(re.findall(r'name="survivorStatisticsWeaponRow\d+"', stats_scope)), 16),
        'milestone rows': (len(re.findall(r'name="survivorStatisticsMilestoneRow\d+"', stats_scope)), 16),
    }
    for label,(actual,expected) in counts.items():
        if actual != expected:
            errors.append(f'Chunk G expected {expected} {label}; found {actual}')
    for rejected in ('PLAY STYLE','TIME RANGE','HOW IT WORKS','VIEW ALL'):
        if re.search(re.escape(rejected), stats_scope, re.I):
            errors.append('Chunk G unsupported Statistics concept present: '+rejected)
    statistics_cfg=root/'Config/_Survivor/statistics.xml'
    if not statistics_cfg.exists():
        errors.append('Chunk G statistics definition file missing: Config/_Survivor/statistics.xml')
    else:
        t=parse(statistics_cfg,errors)
        if t is not None:
            milestones=t.getroot().findall('milestone')
            ids=[m.get('id','') for m in milestones]
            if len(milestones) != 8: errors.append(f'Chunk G expected 8 display-only milestones; found {len(milestones)}')
            if len(set(ids)) != len(ids): errors.append('Chunk G milestone IDs are not unique')

    # Chunk H Crafting invariants. The approved Expected Outcome is explicitly a read-only
    # projection over the audited deterministic native craft path; no probability/quality roll is introduced.
    chunk_h_required = {
        'chunk_h_marker': 'REBIRTH UI REDESIGN - CHUNK H: STATION + NON-STATION CRAFTING',
        'eight_recipe_rows': 'name="recipes" depth="2" rows="8" cols="1"',
        'outcome_panel': 'name="rebirthExpectedOutcome" controller="RebirthCraftOutcomePanel, RebirthUtils"',
        'craft_info_controller': 'controller">RebirthCraftingInfoWindow, RebirthUtils</setattribute>',
        'knowledge_binding': 'text="{rebirthknowledge}"',
        'knowledge_button': 'name="btnRebirthCraftKnowledge"',
        'craft_count': 'controller="RecipeCraftCount"',
        'actions': 'controller="ItemActionList"',
        'six_requirement_grid': 'rows="3" cols="2" pos="0,-28" width="626" height="165"',
        'station_queue': 'name="rebirthCraftingQueueStation" width="330" height="124" panel="Right"',
    }
    for key, literal in chunk_h_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk H invariant missing: {key}")
    if xui_text.count('name="rebirthCraftingQueueStation"') != 7:
        errors.append(f'Chunk H expected 7 station queue group registrations; found {xui_text.count("name=\"rebirthCraftingQueueStation\"")}')
    # PC074 supersedes the old non-station Chunk-H group composition with a Rebirth-owned
    # Personal Crafting root. Station queue ownership remains unchanged. During the PC074
    # implementation sequence, the old non-station queue definition may remain as dormant
    # compatibility source, but it must no longer be registered in the `crafting` group.
    pc074_personal_root = (
        'RebirthPersonalCrafting, RebirthUtils' in xui_text
        and 'name="rebirthPersonalCraftingRoot"' in xui_text
        and 'name="open_backpack_on_open">false</setattribute>' in xui_text
    )
    if pc074_personal_root:
        if xui_text.count('name="rebirthCraftingQueueNonStation"') != 0:
            errors.append('PC074 custom Personal Crafting must not register the legacy non-station queue group window')
        if '<remove xpath="/xui/window_group[@name=\'crafting\']/window"/>' not in xui_text:
            errors.append('PC074 custom Personal Crafting root does not remove the native/legacy crafting child windows')
    else:
        if xui_text.count('name="rebirthCraftingQueueNonStation"') != 1:
            errors.append(f'Chunk H expected 1 non-station queue group registration; found {xui_text.count("name=\"rebirthCraftingQueueNonStation\"")}')
        if 'REBIRTH UI REDESIGN - CHUNK H: CRAFTING GROUP QUEUE OWNERSHIP' not in xui_text:
            errors.append('Chunk H crafting queue ownership marker missing from xui.xml')

    outcome_source=root/'Scripts/Crafting/UI/RebirthCraftOutcomeService.cs'
    presentation_source=root/'Scripts/Crafting/UI/XUiC_RebirthCraftingPresentation.cs'
    if not outcome_source.exists():
        errors.append('Chunk H outcome presentation source missing')
    else:
        ot=outcome_source.read_text(encoding='utf-8',errors='replace')
        if 'result.SuccessChance = "100%"' not in ot: errors.append('Chunk H deterministic Success Chance contract missing')
        if 'recipe.craftExpGain * batches' in ot: errors.append('Chunk H incorrectly multiplies base Crafting XP by batch count')
        if re.search(r'\b(Random|UnityEngine\.Random|System\.Random)\b', ot): errors.append('Chunk H outcome projection contains randomization')
    if not presentation_source.exists():
        errors.append('Chunk H crafting presentation controller source missing')
    else:
        pt=presentation_source.read_text(encoding='utf-8',errors='replace')
        presentation_contracts = ('XUiC_RebirthCraftingInfoWindow : XUiC_CraftingInfoWindow',
                                  'XUiC_RebirthCraftOutcomePanel : XUiController')
        for literal in presentation_contracts:
            if literal not in pt: errors.append('Chunk H presentation contract missing: '+literal)
        if pc074_personal_root:
            # PC086 / PC074 Chunk L deliberately retires the old non-station RecipeStack
            # presentation subclass. The new Personal Crafting queue has its own native-derived
            # XUiC_RebirthCraftingQueueEntry implementation.
            if 'XUiC_RebirthRecipeStack : XUiC_RecipeStack' in pt:
                errors.append('PC086 cleanup failed: obsolete PC072 RebirthRecipeStack remains')
        elif 'XUiC_RebirthRecipeStack : XUiC_RecipeStack' not in pt:
            errors.append('Chunk H presentation contract missing: XUiC_RebirthRecipeStack : XUiC_RecipeStack')

    templates_text=(cfg/'templates.xml').read_text(encoding='utf-8',errors='replace') if (cfg/'templates.xml').exists() else ''
    chunk_h_template_required = {
        'ingredient_card': '<rebirth_crafting_ingredient_card>',
        'ingredient_controller': 'controller="RebirthIngredientEntry, RebirthUtils"',
    }
    for key, literal in chunk_h_template_required.items():
        if literal not in templates_text:
            errors.append(f"Chunk H template invariant missing: {key}")
    if pc074_personal_root:
        if '<rebirth_crafting_queue_card>' in templates_text or 'controller="RebirthRecipeStack, RebirthUtils"' in templates_text:
            errors.append('PC086 cleanup failed: obsolete PC072 non-station queue template remains')
    else:
        for key,literal in {'queue_card':'<rebirth_crafting_queue_card>', 'queue_controller':'controller="RebirthRecipeStack, RebirthUtils"'}.items():
            if literal not in templates_text: errors.append(f"Chunk H template invariant missing: {key}")

    # Chunk I Players invariants. Native PlayersList/PlayersListEntry remain the authority
    # for population and existing row actions; REBIRTH adds only list selection and a
    # source-backed read-only Player Details projection in this chunk.
    chunk_i_required = {
        'chunk_i_marker': 'REBIRTH UI REDESIGN - CHUNK I: PLAYERS LIST + PLAYER DETAILS',
        'players_width': "window[@name='players']\" name=\"width\">1800",
        'players_list_controller': 'name="controller">RebirthPlayersList, RebirthUtils</setattribute>',
        'players_details_panel': 'name="rebirthPlayerDetailsPanel"',
        'players_details_controller': 'controller="RebirthPlayerDetails, RebirthUtils"',
        'players_zombie_kills': 'name="rebirthPlayerDetailsZombieKillsValue"',
        'players_player_kills': 'name="rebirthPlayerDetailsPlayerKillsValue"',
        'players_deaths': 'name="rebirthPlayerDetailsDeathsValue"',
        'players_party': 'name="rebirthPlayerDetailsPartyValue"',
        'players_allies': 'name="rebirthPlayerDetailsAlliesValue"',
        'players_distance': 'name="rebirthPlayerDetailsDistanceValue"',
    }
    for key, literal in chunk_i_required.items():
        if literal not in windows_text:
            errors.append(f"Chunk I invariant missing: {key}")

    if windows_text.count('name="rebirthPlayerDetailsPanel"') != 1:
        errors.append(f'Chunk I expected one Player Details pane; found {windows_text.count("name=\\\"rebirthPlayerDetailsPanel\\\"")}')

    players_template_required = {
        'players_header': '<rebirth_players_header',
        'players_entry': '<rebirth_players_entry>',
        'players_entry_controller': 'controller="RebirthPlayersListEntry, RebirthUtils"',
        'players_selection': 'name="rebirthSelection"',
        'players_zombie_kills_column': 'name="zombieKillsText"',
        'players_deaths_column': 'name="deathsText"',
        'players_party_action': 'name="iconPartyIcon"',
        'players_allies_action': 'name="iconAllyIcon"',
        'players_text_voice': 'name="iconChat"',
        'players_voice_action': 'name="iconVoice"',
        'players_map_action': 'name="iconShowOnMap"',
        'players_report_action': 'name="btnReportPlayer"',
    }
    for key, literal in players_template_required.items():
        if literal not in templates_text:
            errors.append(f"Chunk I template invariant missing: {key}")

    # Concept-only communication/friend-management actions remain rejected. Chunk J now
    # owns the explicitly approved REBIRTH Group Name/Color implementation.
    players_start=windows_text.find("tab_key='xuiPlayers'")
    players_scope=windows_text[players_start:] if players_start >= 0 else windows_text
    for rejected in ('MESSAGE', 'ADD FRIEND', 'MUTE'):
        if re.search(re.escape(rejected), players_scope, re.I):
            errors.append('Chunk I unsupported Players concept present: '+rejected)

    players_source=root/'Scripts/UI/Players/XUiC_RebirthPlayers.cs'
    if not players_source.exists():
        errors.append('Chunk I Players presentation source missing')
    else:
        pst=players_source.read_text(encoding='utf-8',errors='replace')
        for literal in ('XUiC_RebirthPlayersList : XUiC_PlayersList',
                        'XUiC_RebirthPlayersListEntry : XUiC_PlayersListEntry',
                        'XUiC_RebirthPlayerDetails : XUiController'):
            if literal not in pst:
                errors.append('Chunk I Players source contract missing: '+literal)
        for required in ('KilledZombies', 'KilledPlayers', '.Died', 'gameStage', 'pingToServer',
                         'IsInPartyOfLocalPlayer', 'IsFriendOfLocalPlayer', 'ValueDisplayFormatters.Distance'):
            if required not in pst:
                errors.append('Chunk I Players source-backed projection missing: '+required)
        # A read-only details pane must not synthesize unsupported remote-equipment/buff data.
        for rejected in ('remoteEquipment', 'remoteBuff', 'EquippedItemsSnapshot', 'ActiveBuffsSnapshot'):
            if rejected in pst:
                errors.append('Chunk I unverified remote Players data path present: '+rejected)


    # Chunk J Group Name / Group Color invariants. Native Party remains membership/leader
    # authority; persistent REBIRTH metadata must use durable player identity rather than
    # PlayerDisplayName or the restart-local native PartyID as its save key.
    chunk_j_required = {
        'group_identity_panel': 'name="rebirthGroupIdentityPanel"',
        'group_name_input': 'name="rebirthGroupIdentityNameInput"',
        'group_save_button': 'name="btnRebirthGroupIdentitySave"',
        'group_row_name': 'name="rebirthGroupName"',
    }
    for key,literal in chunk_j_required.items():
        target = templates_text if key == 'group_row_name' else windows_text
        if literal not in target: errors.append(f"Chunk J invariant missing: {key}")
    if sum(windows_text.count(f'name="btnRebirthGroupColor{i}"') for i in range(8)) != 8:
        errors.append('Chunk J expected exactly 8 Group Color buttons')
    group_service=root/'Scripts/UI/Players/RebirthPartyIdentityService.cs'
    group_network=root/'Scripts/UI/Players/RebirthPartyIdentityNetwork.cs'
    if not group_service.exists(): errors.append('Chunk J party identity service source missing')
    else:
        gst=group_service.read_text(encoding='utf-8',errors='replace')
        for required in ('RebirthStablePlayerIdentity.TryResolveServerEntity', 'RuntimePartyGroups', 'LeaderStableKey', 'Members', 'SaveIfDirty', 'ProcessServerRequest', 'player.Party.Leader == player'):
            if required not in gst: errors.append('Chunk J server-authority/persistence contract missing: '+required)
        if re.search(r'new XAttribute\(\"(?:party|party_id|partyId)\"', gst, re.I): errors.append('Chunk J persistence appears to serialize native PartyID')
        if 'PlayerDisplayName' in gst: errors.append('Chunk J persistence must not key group identity by player display name')
    if not group_network.exists(): errors.append('Chunk J party identity network source missing')
    else:
        gnt=group_network.read_text(encoding='utf-8',errors='replace')
        for required in ('NetPackageDirection.ToServer','ValidEntityIdForSender(playerEntityId)','NetPackageDirection.ToClient','RebirthPartyIdentityClientState.Receive'):
            if required not in gnt: errors.append('Chunk J network authority contract missing: '+required)
    if 'RebirthPartyIdentityService.Install' not in (root/'Scripts/Survivor/RebirthSurvivorInstaller.cs').read_text(encoding='utf-8',errors='replace'):
        errors.append('Chunk J installer wiring missing')

    # Chunk K final source/static hardening gates. These checks are intentionally broader
    # than the per-chunk layout assertions above: they validate the integrated project as a
    # whole without pretending to replace live 7DTD rendering, controller/gamepad, or MP tests.
    all_config_xml = sorted((root/'Config').rglob('*.xml'))
    parsed_config_count = 0
    for config_xml in all_config_xml:
        if parse(config_xml, errors) is not None:
            parsed_config_count += 1

    # Localization must not contain duplicate nonblank keys, and every REBIRTH key referenced
    # directly by active XUi key attributes must exist. (C# helpers with explicit English
    # fallback strings are allowed; the key attributes have no such fallback.)
    localization_path = root/'Config/Localization.csv'
    localization_keys = set()
    localization_duplicates = []
    if not localization_path.exists():
        errors.append('Chunk K Localization.csv missing')
    else:
        try:
            with localization_path.open('r', encoding='utf-8-sig', errors='replace', newline='') as f:
                for row in csv.reader(f):
                    if not row: continue
                    key=(row[0] or '').strip()
                    if not key or key.lower() == 'key': continue
                    if key in localization_keys: localization_duplicates.append(key)
                    localization_keys.add(key)
        except Exception as e:
            errors.append('Chunk K Localization.csv parse failed: '+str(e))
    if localization_duplicates:
        errors.append('Chunk K duplicate nonblank localization keys: '+', '.join(sorted(set(localization_duplicates))[:20]))

    xui_key_attrs=('text_key','tooltip_key','title_key','label_key')
    missing_xui_keys=[]
    for config_xml in (cfg/'windows.xml', cfg/'controls.xml', cfg/'templates.xml', cfg/'xui.xml'):
        if not config_xml.exists(): continue
        tree=parse(config_xml, errors)
        if tree is None: continue
        for elem in tree.getroot().iter():
            for attr in xui_key_attrs:
                key=(elem.attrib.get(attr) or '').strip()
                if key.startswith('xuiRebirth') and key not in localization_keys:
                    missing_xui_keys.append(f'{key} ({config_xml.name}:{elem.attrib.get("name", elem.tag)})')
    if missing_xui_keys:
        errors.append('Chunk K missing active XUi localization keys: '+', '.join(sorted(set(missing_xui_keys))))

    chunk_k_loc_required = (
        'xuiRebirthQuickStackCategoriesTooltip','xuiRebirthQuickStackItemCategoryTitle',
        'xuiRebirthQuickStackItemCategoryTooltip','xuiRebirthQuickStackCategoryUnassigned',
        'xuiRebirthQuickStackCategoryAssignedDesc','xuiRebirthQuickStackCategoryNoneDesc',
        'xuiRebirthProgressionExplorerWeakness','xuiRebirthProgressionExplorerStartingSkill',
        'xuiRebirthProgressionExplorerStartingKnowledge')
    for key in chunk_k_loc_required:
        if key not in localization_keys: errors.append('Chunk K hardening localization key missing: '+key)

    # Every RebirthUtils controller alias referenced by active XUi must resolve to a source
    # class either by exact alias or the conventional XUiC_ prefix.
    rebirth_controller_refs=set(re.findall(r'controller="([A-Za-z_][A-Za-z0-9_]*),\s*RebirthUtils"', scope))
    source_class_names=set()
    for source in (root/'Scripts').rglob('*.cs'):
        text=source.read_text(encoding='utf-8',errors='replace')
        source_class_names.update(re.findall(r'\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b', text))
    missing_controllers=[]
    for alias in sorted(rebirth_controller_refs):
        if alias not in source_class_names and ('XUiC_'+alias) not in source_class_names:
            missing_controllers.append(alias)
    if missing_controllers:
        errors.append('Chunk K unresolved RebirthUtils XUi controllers: '+', '.join(missing_controllers))

    # Custom REBIRTH atlas references must resolve to either a PNG basename or an explicit
    # settings.xml sprite entry in that same atlas. Stock UIAtlas/ItemIconAtlas are deliberately
    # excluded because native runtime sprites are not copied into this mod merely for validation.
    custom_atlases=('RebirthSurvivorIcons','RebirthUiIcons','RebirthTraderJobs','RebirthHud','RebirthMetabolism')
    atlas_sprites={}
    for atlas in custom_atlases:
        atlas_dir=root/'UIAtlases'/atlas
        names=set(p.stem for p in atlas_dir.glob('*.png')) if atlas_dir.exists() else set()
        settings=atlas_dir/'settings.xml'
        if settings.exists():
            st=parse(settings, errors)
            if st is not None:
                names.update((e.attrib.get('name') or '').strip() for e in st.getroot().iter('sprite') if (e.attrib.get('name') or '').strip())
        atlas_sprites[atlas]=names
    missing_atlas_refs=[]
    custom_atlas_ref_count=0
    for config_xml in (cfg/'windows.xml', cfg/'controls.xml', cfg/'templates.xml', cfg/'xui.xml'):
        if not config_xml.exists(): continue
        tree=parse(config_xml, errors)
        if tree is None: continue
        for elem in tree.getroot().iter():
            atlas=(elem.attrib.get('atlas') or '').strip()
            sprite=(elem.attrib.get('sprite') or '').strip()
            if atlas in atlas_sprites and sprite and not sprite.startswith('{'):
                custom_atlas_ref_count += 1
                if sprite not in atlas_sprites[atlas]:
                    missing_atlas_refs.append(f'{atlas}:{sprite}')
    if missing_atlas_refs:
        errors.append('Chunk K unresolved custom atlas sprite refs: '+', '.join(sorted(set(missing_atlas_refs))))

    # World teardown must clear client caches even when returning to menu without a full process
    # shutdown, avoiding stale Statistics/Group Identity data between worlds.
    stats_service=root/'Scripts/Survivor/Statistics/RebirthStatisticsService.cs'
    party_service=root/'Scripts/UI/Players/RebirthPartyIdentityService.cs'
    if stats_service.exists():
        sst=stats_service.read_text(encoding='utf-8',errors='replace')
        m=re.search(r'OnWorldShuttingDown\([^)]*\)\s*\{(.*?)\n\s*\}', sst, re.S)
        if m is None or 'RebirthStatisticsClientState.Reset();' not in m.group(1):
            errors.append('Chunk K Statistics client cache is not reset on world shutdown')
        if 'private const float TickSeconds = 1f;' not in sst:
            errors.append('Chunk K Statistics one-second server sampling throttle missing')
    if party_service.exists():
        pst=party_service.read_text(encoding='utf-8',errors='replace')
        m=re.search(r'OnWorldShuttingDown\([^)]*\)\s*\{(.*?)\n\s*\}', pst, re.S)
        if m is None or 'RebirthPartyIdentityClientState.Reset();' not in m.group(1):
            errors.append('Chunk K Group Identity client cache is not reset on world shutdown')
        if 'ModEvents.GameUpdate.RegisterHandler' in pst:
            errors.append('Chunk K Group Identity unexpectedly introduced a per-frame GameUpdate scan')

    # Rebirth Character remains strictly mode/committed-character gated, preserving Base Game UI.
    launcher=root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacterLauncher.cs'
    if not launcher.exists(): errors.append('Chunk K Character mode-gating launcher missing')
    else:
        lt=launcher.read_text(encoding='utf-8',errors='replace')
        for required in ('state.RebirthModeEnabled','state.HasCharacter','RebirthSurvivorOwnerCreationState.Ready'):
            if required not in lt: errors.append('Chunk K Character mode gate missing: '+required)

    print('chunk_k_config_xml_parsed:', parsed_config_count, '/', len(all_config_xml))
    print('chunk_k_localization_keys:', len(localization_keys), 'duplicates:', len(localization_duplicates))
    print('chunk_k_rebirth_controller_refs:', len(rebirth_controller_refs), 'missing:', len(missing_controllers))
    print('chunk_k_custom_atlas_refs:', custom_atlas_ref_count, 'missing:', len(missing_atlas_refs))

    chunk_c_template_required = {
        'waypoint_row_width': "/templates/waypoint_entry/rect\" name=\"width\">424",
        'quest_row_width': "/templates/quest_entry/rect\" name=\"width\">560",
        'quest_objective_width': "/templates/quest_objective_entry/rect\" name=\"width\">360",
        'challenge_group_width': "/templates/challenge_list_entry/rect\" name=\"width\">800",
        'challenge_selected_red': "/templates/challenge_entry/rect\" name=\"selected_color\">228,18,21,255",
    }
    for key, literal in chunk_c_template_required.items():
        if literal not in templates_text:
            errors.append(f"Chunk C template invariant missing: {key}")

    print('REBIRTH UI REDESIGN STATIC VALIDATION')
    print('project_root:', root)
    print('shared_foundation_templates:', len(FOUNDATION))
    for family,names in TARGETS.items():
        hits=[n for n in names if n in scope]
        print(f'{family}: project_mentions={len(hits)} {hits}')

    if args.base_xui_dir:
        b=Path(args.base_xui_dir)
        base_text='\n'.join((b/f).read_text(encoding='utf-8',errors='replace') for f in ('xui.xml','windows.xml','controls.xml','templates.xml') if (b/f).exists())
        print('base_xui_dir:', b)
        for family,names in TARGETS.items():
            hits=[n for n in names if n in base_text]
            print(f'{family}: native_mentions={len(hits)} {hits}')

    # Chunk K scrollbar + Rebirth-mode amendment invariants.
    amendment_files = {
        'ingame_windows': root / 'Config' / 'XUi_InGame' / 'windows.xml',
        'ingame_templates': root / 'Config' / 'XUi_InGame' / 'templates.xml',
        'ingame_xui': root / 'Config' / 'XUi_InGame' / 'xui.xml',
        'menu_windows': root / 'Config' / 'XUi_Menu' / 'windows.xml',
        'menu_xui': root / 'Config' / 'XUi_Menu' / 'xui.xml',
    }
    amendment_text = {k: p.read_text(encoding='utf-8', errors='ignore') for k,p in amendment_files.items() if p.exists()}
    iw = amendment_text.get('ingame_windows','')
    mw = amendment_text.get('menu_windows','')
    if iw.count("character_progression('Rebirth')") < 2:
        errors.append('Scrollbar/mode amendment missing Rebirth structural gates in XUi_InGame/windows.xml')
    # XUi_Menu V3.2 rejects <if> as a patch operation. The spawn overlay is therefore
    # registered hidden and its controller/first-entry service enforces Rebirth mode at runtime.
    spawn_gate_runtime = (root / 'Scripts' / 'Survivor' / 'UI' / 'XUiC_RebirthSpawnSelectionSurvivorGate.cs').read_text(encoding='utf-8', errors='ignore')
    if '<if cond=' in mw or 'visible="false"' not in mw or 'RebirthSurvivorMode.IsEnabledForCurrentWorld()' not in spawn_gate_runtime:
        errors.append('Scrollbar/mode amendment missing V3.2-safe runtime Rebirth gate around spawnselection overlay')
    forbidden_content_pagers = [
        'btnChooseProfilePrev','btnChooseProfileNext','btnSurvivorTraitPrev','btnSurvivorTraitNext',
        'btnSpawnProfilePrev','btnSpawnProfileNext'
    ]
    joined = iw + '\n' + mw
    for token in forbidden_content_pagers:
        if token in joined:
            errors.append('Scrollbar amendment still exposes content Prev/Next control: ' + token)
    required_scrollbars = [
        'chooseProfileNativeScrollHost','survivorTraitNativeScrollHost','survivorStatisticsWeaponScroll',
        'survivorStatisticsMilestoneScroll','rebirthCreativePagerScroll','rebirthMapWaypointPagerScroll',
        'rebirthQuestPagerScroll','rebirthSharedQuestPagerScroll','rebirthCraftingPagerScroll',
        'rebirthPlayersPagerScroll','rebirthChallengeGroupScroll','spawnProfileNativeScrollHost',
        'spawnProfileProgressionNativeScrollHost','spawnProfileTraitNativeScrollHost',
        'spawnProfileWeaknessNativeScrollHost','spawnProfileStartingItemNativeScrollHost'
    ]
    for token in required_scrollbars:
        if token not in joined:
            errors.append('Scrollbar amendment missing required stock-scroll surface: ' + token)
    for src in [
        root / 'Scripts' / 'UI' / 'RebirthNativeScrollbarUtil.cs',
        root / 'Scripts' / 'UI' / 'XUiC_RebirthPagerScrollbar.cs'
    ]:
        if not src.exists(): errors.append('Scrollbar amendment source missing: ' + str(src.relative_to(root)))
    party = root / 'Scripts' / 'UI' / 'Players' / 'RebirthPartyIdentityService.cs'
    if party.exists() and 'RebirthModeActive' not in party.read_text(encoding='utf-8', errors='ignore'):
        errors.append('Group Identity is not explicitly dormant outside Rebirth progression mode')



    for w in warnings: print('WARNING:',w)
    for e in errors: print('ERROR:',e)
    print('RESULT:', 'FAIL' if errors else 'PASS')
    return 1 if errors else 0

if __name__=='__main__': raise SystemExit(main())
