#!/usr/bin/env python3
"""REBIRTH-3.0-HUD-TRACKER-001 source/static contract validator.

Chunk A freezes prerequisites only. Later chunks may extend this script, but must not
weaken these source contracts without an explicit project-change amendment.
"""
from pathlib import Path
import argparse, re, sys
import xml.etree.ElementTree as ET


def need(ok, msg, errors):
    if not ok: errors.append(msg)


def read(path):
    return path.read_text(encoding='utf-8', errors='replace') if path.exists() else ''


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--project-root', default='.')
    ap.add_argument('--require-final', action='store_true', help='Require the completed A-F runtime/config source contract used by Chunk G closure.')
    args=ap.parse_args()
    root=Path(args.project_root).resolve()
    errors=[]; notes=[]

    cfg=root/'Config'/'XUi_InGame'
    windows=cfg/'windows.xml'; xui=cfg/'xui.xml'; templates=cfg/'templates.xml'; controls=cfg/'controls.xml'
    for p in (windows,xui,templates,controls):
        need(p.exists(), f'missing XUi source: {p}', errors)
        if p.exists():
            try: ET.parse(p)
            except Exception as e: errors.append(f'XML parse failed {p}: {e}')

    w=read(windows)
    char=root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCharacter.cs'
    snap=root/'Scripts/Survivor/UI/RebirthCharacterUiSnapshot.cs'
    explorer=root/'Scripts/Survivor/UI/XUiC_RebirthProgressionExplorer.cs'
    projection=root/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerUiProjection.cs'
    client=root/'Scripts/Survivor/Network/RebirthSurvivorClientState.cs'
    models=root/'Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs'
    identity=root/'Scripts/Survivor/Persistence/RebirthStablePlayerIdentity.cs'
    atomic=root/'Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs'
    profile_store=root/'Scripts/Survivor/Persistence/RebirthSurvivorProfileStore.cs'
    for p in (char,snap,explorer,projection,client,models,identity,atomic,profile_store):
        need(p.exists(), f'missing audited source: {p}', errors)

    # Current before-state player-buff target. This is intentionally still global in Chunk A;
    # Chunk E must move/supersede it under the Rebirth-only contract.
    buff_xpath="/windows/window[@name='HUDLeftStatBars']/rect[@controller='BuffPopoutList']/@pos"
    matches=re.findall(r'<set\s+xpath="'+re.escape(buff_xpath)+r'"\s*>\s*([^<]+)\s*</set>', w)
    need(len(matches)==1, f'expected exactly one current BuffPopoutList position override; found {len(matches)}', errors)
    if matches:
        need(matches[0].strip()=='90,43', f'Chunk A before-state BuffPopoutList position changed unexpectedly: {matches[0].strip()}', errors)
    need('windowTargetBar' in w, 'target-bar source unexpectedly absent', errors)

    c=read(char); s=read(snap); e=read(explorer); p=read(projection); cl=read(client); m=read(models); ident=read(identity); atom=read(atomic); ps=read(profile_store)

    progression_path=root/'Config'/'_Survivor'/'progression.xml'
    authoritative_skill_count=0
    if progression_path.exists():
        try:
            authoritative_skill_count=len(ET.parse(progression_path).getroot().findall('./skills/skill'))
        except Exception as ex:
            errors.append(f'progression.xml parse failed while resolving authoritative Skill count: {ex}')
    need(authoritative_skill_count>0, 'authoritative Skill catalogue is empty/unavailable', errors)

    # Character Progression source ownership / row contract.
    for literal in ('survivorProgressionPanel','survivorProgressionSkillsContent','survivorProgressionKnowledgeContent',
                    'btnSurvivorProgressionExploreSelected','ProgressionSkill_OnPressed','ProgressionKnowledge_OnPressed'):
        need(literal in c or literal in w, f'missing Character Progression contract: {literal}', errors)
    need(len(re.findall(r'name="survivorProgressionSkillRow\d+"',w))==authoritative_skill_count, f'expected {authoritative_skill_count} authoritative Character Progression Skill rows', errors)
    need(len(re.findall(r'name="btnSurvivorProgressionSkill\d+"',w))==authoritative_skill_count, f'expected {authoritative_skill_count} authoritative Character Progression Skill buttons', errors)
    prog_i=w.find('name="survivorProgressionPanel"')
    prog_seg=w[prog_i:prog_i+50000] if prog_i>=0 else ''
    need(re.search(r'<defaultscrollbar\s*/>',prog_seg) is not None and '<scrollview ' in prog_seg, 'standard Progression Skill scrollbar/scrollview missing', errors)

    # Existing aggregate/source values.
    for literal in ('public long SurvivorRevision','List<RebirthCharacterUiAttribute>','List<RebirthCharacterUiSkill>','List<RebirthCharacterUiKnowledge>'):
        need(literal in s, f'missing Character aggregate contract: {literal}', errors)
    for icon in ('ui_game_symbol_muscle','ui_game_symbol_agility','ui_game_symbol_fortitude_mastery','rb_ui_knowledge'):
        need(icon in s or icon in w, f'missing audited semantic icon reference: {icon}', errors)

    # Owner snapshot/revision event.
    need('GetOwnerStateSnapshot()' in cl, 'owner snapshot client accessor missing', errors)
    need('OwnerStateChanged' in cl, 'owner snapshot change event missing', errors)
    need('CharacterRevision' in cl and 'CharacterRevision' in m, 'CharacterRevision contract missing', errors)

    # Stable local identity + local atomic persistence foundation.
    need('TryFromLocalPlatform' in ident, 'stable local player identity resolver missing', errors)
    need('StorageKey' in ident and 'ComputeStorageKey' in ident, 'privacy-safe stable player StorageKey missing', errors)
    need('TryWrite' in atom and 'TryLoadFinalThenBackup' in atom, 'atomic XML persistence contract missing', errors)
    need('GameIO.GetUserGameDataDir()' in ps, 'verified user-data root usage missing', errors)

    # Explorer focus/detail extension point.
    need('projection=RebirthProgressionExplorerUiProjectionService.Build' in e, 'Explorer projection build hook missing', errors)
    need('Node_OnPressed' in e, 'Explorer graph navigation handler missing', errors)
    need('public string FocusId' in p and 'public string FocusType' in p, 'Explorer FocusId/FocusType projection contract missing', errors)

    # No fixed 5-row implementation policy may be introduced in active source. Documentation may
    # describe the superseded prototype, so search C#/XML only for named tracker constants/policies.
    active=[]
    for base in (root/'Scripts', root/'Config'):
        if not base.exists(): continue
        for f in base.rglob('*'):
            if f.suffix.lower() not in ('.cs','.xml'): continue
            txt=read(f)
            if re.search(r'(TrackedProgression|HudTracking|HUDTracking|HudTracker|HUDTracker)',txt):
                active.append((f,txt))
    for f,txt in active:
        if re.search(r'(MaxTracked|MaxPinned|Tracked.*Max|Pinned.*Max)\s*=\s*5\b',txt,re.I):
            errors.append(f'fixed five-entry HUD tracking cap found in active source: {f}')

    # Rebirth gating infrastructure must remain available for later XUi changes.
    need("character_progression('Rebirth')" in w or "character_progression('Rebirth')" in read(xui), 'Rebirth progression XUi gate infrastructure missing', errors)

    # Chunk B - typed provider + client-local preference foundation.
    hud_dir=root/'Scripts/Survivor/HudTracking'
    buff_layout_file=hud_dir/'XUiC_RebirthHudBuffLayout.cs'
    buff_layout_harness_file=hud_dir/'RebirthHudBuffLayoutVectorHarness.cs'
    chunk_e_present=buff_layout_file.exists() or buff_layout_harness_file.exists() or 'rebirthHudBuffLayoutOwner' in w
    model_file=hud_dir/'RebirthHudTrackingModels.cs'
    provider_file=hud_dir/'RebirthHudTrackingProviders.cs'
    store_file=hud_dir/'RebirthHudTrackingPreferenceStore.cs'
    service_file=hud_dir/'RebirthHudTrackingPreferenceService.cs'
    harness_file=hud_dir/'RebirthHudTrackingFoundationVectorHarness.cs'
    chunk_b_present=all(x.exists() for x in (model_file,provider_file,store_file,service_file,harness_file))
    if any(x.exists() for x in (model_file,provider_file,store_file,service_file,harness_file)):
        for x in (model_file,provider_file,store_file,service_file,harness_file):
            need(x.exists(), f'Chunk B foundation file missing: {x}', errors)
        models_b=read(model_file); providers_b=read(provider_file); store_b=read(store_file); service_b=read(service_file); harness_b=read(harness_file)
        for token in ('Skill = 0','Attribute = 1','Knowledge = 2','Summary = 3','RebirthHudTrackingUnknownEntry'):
            need(token in models_b, f'Chunk B typed-ID/passthrough contract missing: {token}', errors)
        for token in ('RebirthHudSkillTrackProvider','RebirthHudAttributeTrackProvider','RebirthHudKnowledgeTrackProvider','RebirthHudSummaryTrackProvider','KnowledgeDiscovered'):
            need(token in providers_b, f'Chunk B provider missing: {token}', errors)
        need('summary.knowledge_discovered' in providers_b, 'Chunk B source-backed summary ID missing', errors)
        need('RebirthSurvivorClientState' not in providers_b, 'providers must consume supplied owner snapshot rather than fetch/network state themselves', errors)
        for token in ('GameIO.GetSaveGameDir()','GameIO.GetUserGameDataDir()','TryFromLocalPlatform','ComputeStorageKey','RebirthAtomicXmlFile.TryWrite','TryLoadFinalThenBackup'):
            need(token in store_b, f'Chunk B persistence contract missing: {token}', errors)
        need('RebirthData", "HudTracking"' in store_b, 'Chunk B client-local HUD preference path missing', errors)
        need('CurrentSchemaVersion = 1' in store_b, 'Chunk B schema version contract missing', errors)
        need('schema > CurrentSchemaVersion' in store_b, 'Chunk B future-schema overwrite protection missing', errors)
        need('UnknownEntries' in store_b and 'UnknownEntries' in models_b, 'Chunk B unknown/stale entry preservation missing', errors)
        need('TimeSpan.FromMilliseconds(500)' in service_b, 'Chunk B debounced persistence contract missing', errors)
        need('PreferencesChanged' in service_b, 'Chunk B local preference change event missing', errors)
        need('FlushPendingIfDue' in service_b and 'ResetRuntime' in service_b, 'Chunk B lifecycle/flush surface missing', errors)
        need('TryResolveCurrentContext' in service_b, 'Chunk B per-world context re-resolution missing', errors)
        for token in ('TestOrdering','TestUnknownTypeRoundTrip','TestDuplicateRecovery','TestFutureSchemaRefusal'):
            need(token in harness_b, f'Chunk B foundation vector missing: {token}', errors)
        # Gross C# delimiter preflight for the new isolated foundation sources.
        for x in (model_file,provider_file,store_file,service_file,harness_file):
            txt=read(x)
            need(txt.count('{')==txt.count('}'), f'C# brace preflight failed: {x}', errors)
            need(txt.count('(')==txt.count(')'), f'C# parenthesis preflight failed: {x}', errors)


    # Chunk C - Character -> Progression configuration surface.
    manager_file=hud_dir/'XUiC_RebirthHudTrackingManager.cs'
    layout_file=hud_dir/'RebirthHudTrackingLayoutMetrics.cs'
    chunk_c_present=manager_file.exists() or layout_file.exists() or 'survivorHudTrackingHost' in w
    if chunk_c_present:
        need(manager_file.exists(), f'Chunk C manager controller missing: {manager_file}', errors)
        need(layout_file.exists(), f'Chunk C layout metrics missing: {layout_file}', errors)
        manager_c=read(manager_file); layout_c=read(layout_file); xui_text=read(xui)
        for token in ('survivorHudTrackingHost','survivorHudTrackingOverlay','btnSurvivorProgressionHudTracking',
                      'survivorHudTrackingTrackedContent','survivorHudTrackingAvailableContent',
                      'btnSurvivorHudTrackingFilterSkill','btnSurvivorHudTrackingFilterAttribute',
                      'btnSurvivorHudTrackingFilterKnowledge','btnSurvivorHudTrackingFilterSummary'):
            need(token in w, f'Chunk C XUi contract missing: {token}', errors)
        tracked_management_rows=len(re.findall(r'name="survivorHudTrackingTrackedRow\d+"',w))
        available_management_rows=len(re.findall(r'name="survivorHudTrackingAvailableRow\d+"',w))
        need(tracked_management_rows>=180, f'Chunk C expected at least 180 tracked management rows; got {tracked_management_rows}', errors)
        need(available_management_rows>=140, f'Chunk C expected at least 140 available management rows; got {available_management_rows}', errors)
        progression_xml=root/'Config/_Survivor/progression.xml'
        if progression_xml.exists():
            try:
                ptree=ET.parse(progression_xml)
                skill_count=len(ptree.findall('.//skill')); attribute_count=len(ptree.findall('.//attribute')); knowledge_count=len(ptree.findall('.//knowledge'))
                release1_total=skill_count+attribute_count+knowledge_count+1
                need(available_management_rows>=max(skill_count,attribute_count,knowledge_count,1), f'Chunk C available-row capacity truncates a Release 1 category: rows={available_management_rows} skills={skill_count} attributes={attribute_count} knowledge={knowledge_count}', errors)
                need(tracked_management_rows>=release1_total, f'Chunk C tracked-row capacity truncates complete Release 1 catalogue: rows={tracked_management_rows} total={release1_total}', errors)
            except Exception as ex:
                errors.append(f'Chunk C progression catalogue coverage check failed: {ex}')
        host_i=w.find('name="survivorHudTrackingHost"')
        host_seg=w[host_i:host_i+700000] if host_i>=0 else ''
        need(len(re.findall(r'<defaultscrollbar\s*/>',host_seg))>=2 and host_seg.count('<scrollview ')>=2, 'Chunk C management lists must use stock scrollbars/scrollviews', errors)
        need('btnSurvivorHudTrackingPrev' not in w and 'btnSurvivorHudTrackingNext' not in w, 'Chunk C must not add content Previous/Next paging', errors)
        for token in ('RebirthHudTrackingPreferenceService.Mutate','RebirthHudTrackingRegistry.EnumerateTrackableIds',
                      'RebirthHudTrackingRegistry.TryResolveDisplay','GetSafeCapacity','PreferencesChanged',
                      'TrackedUp_OnPressed','TrackedDown_OnPressed','TrackedRemove_OnPressed','AvailableToggle_OnPressed'):
            need(token in manager_c, f'Chunk C manager behavior missing: {token}', errors)
        need('NetPackage' not in manager_c and 'SendPackage' not in manager_c, 'Chunk C manager must not create network traffic', errors)
        need('ReportMeasuredGeometry' in layout_c and 'Screen.height' in layout_c and 'Screen.width' in layout_c, 'Chunk C measured/fallback capacity contract missing', errors)
        need(('ReferenceTrackerRowPitch' in layout_c or 'TrackerRowPitch' in layout_c) and not re.search(r'(MaxTracked|MaxPinned|SafeCapacity)\s*=\s*5\b',layout_c,re.I), 'Chunk C reintroduced fixed five-row capacity', errors)
        need('hudTrackingManager.IsManagerOpen' in c and 'hudTrackingManager.CloseManager()' in c, 'Chunk C ESC overlay-close integration missing', errors)
        need("character_progression('Rebirth')" in xui_text and 'rebirthSurvivorCharacter' in xui_text, 'Chunk C parent Character window is not Rebirth-gated in xui.xml', errors)
        for key in ('xuiRebirthHudTrackingOpen','xuiRebirthHudTrackingTitle','xuiRebirthHudTrackingSkills',
                    'xuiRebirthHudTrackingAttributes','xuiRebirthHudTrackingKnowledge','xuiRebirthHudTrackingSummary',
                    'xuiRebirthHudTrackingTrackToggle','xuiRebirthHudTrackingCapacityNote'):
            need(re.search(r'^'+re.escape(key)+r',', read(root/'Config/Localization.csv'), re.M) is not None, f'Chunk C localization missing: {key}', errors)
        # Before Chunk E the global 90,43 override must remain; E supersedes it under a Rebirth gate.
        if not chunk_e_present:
            need(len(matches)==1 and matches[0].strip()=='90,43', 'Chunk C must not modify BuffPopoutList layout before Chunk E', errors)
        need('XUiC_RebirthTrackedProgressionHud' not in manager_c, 'Chunk C must not fold gameplay HUD rendering into the manager controller', errors)
        for x in (manager_file,layout_file):
            txt=read(x)
            need(txt.count('{')==txt.count('}'), f'Chunk C C# brace preflight failed: {x}', errors)
            need(txt.count('(')==txt.count(')'), f'Chunk C C# parenthesis preflight failed: {x}', errors)


    # Chunk D - live bottom-left gameplay tracker rendering.
    hud_render_file=hud_dir/'XUiC_RebirthTrackedProgressionHud.cs'
    render_math_file=hud_dir/'RebirthHudTrackingRenderMath.cs'
    render_harness_file=hud_dir/'RebirthHudTrackingRenderVectorHarness.cs'
    chunk_d_present=hud_render_file.exists() or render_math_file.exists() or 'rebirthTrackedProgressionHud' in w
    if chunk_d_present:
        for x in (hud_render_file,render_math_file,render_harness_file):
            need(x.exists(), f'Chunk D gameplay HUD source missing: {x}', errors)
        hud_d=read(hud_render_file); math_d=read(render_math_file); harness_d=read(render_harness_file); layout_d=read(layout_file)
        need('controller="RebirthTrackedProgressionHud, RebirthUtils"' in w, 'Chunk D gameplay HUD controller XUi root missing', errors)
        need(len(re.findall(r'name="rebirthTrackedProgressionRow\d+"',w))==64, 'Chunk D expected exactly 64 authored gameplay HUD rows', errors)
        need('rebirthTrackedProgressionOverflow' in w and 'rebirthTrackedProgressionOverflowText' in w, 'Chunk D overflow indicator XUi missing', errors)
        hud_i=w.find('name="rebirthTrackedProgressionHud"')
        hud_prefix=w[max(0,hud_i-300):hud_i+100] if hud_i>=0 else ''
        need("character_progression('Rebirth')" in hud_prefix, 'Chunk D gameplay HUD root must be structurally Rebirth-gated', errors)
        for token in ('RebirthHudTrackingPreferenceService.GetCurrent','RebirthSurvivorClientState.GetOwnerStateSnapshot',
                      'OwnerStateChanged','PreferencesChanged','GetSafeCapacity','CalculateVisibleWindow',
                      'ReportRenderedTrackerState','GainPulseSeconds','CalculateSkillGain'):
            need(token in hud_d or token in math_d, f'Chunk D render behavior missing: {token}', errors)
        need('NetPackage' not in hud_d and 'SendPackage' not in hud_d, 'Chunk D live HUD must not create network traffic', errors)
        need('AuthoredRows = 64' in hud_d, 'Chunk D authored-row/source contract changed unexpectedly', errors)
        need('dataRows = Math.Max(0, capacity - 1)' in math_d, 'Chunk D overflow row reservation missing', errors)
        need('stored preferences themselves are never truncated' in math_d, 'Chunk D non-destructive overflow contract missing', errors)
        need('TrackerLayoutChanged' in layout_d and 'RenderedTrackerHeight' in layout_d and 'RenderedTrackerRows' in layout_d, 'Chunk D rendered-height handoff for Chunk E missing', errors)
        need('xuiRebirthHudTrackingOverflow,' in read(root/'Config/Localization.csv'), 'Chunk D overflow localization missing', errors)
        for token in ('overflow-reserves-row','single-row-overflow','skill rollover expected 0.12'):
            need(token in harness_d, f'Chunk D deterministic render vector missing: {token}', errors)
        # Chunk D deliberately leaves the existing buff anchor untouched; E supersedes it under a Rebirth gate.
        if not chunk_e_present:
            need(len(matches)==1 and matches[0].strip()=='90,43', 'Chunk D must not move BuffPopoutList before Chunk E', errors)
        for x in (hud_render_file,render_math_file,render_harness_file,layout_file):
            txt=read(x)
            need(txt.count('{')==txt.count('}'), f'Chunk D C# brace preflight failed: {x}', errors)
            need(txt.count('(')==txt.count(')'), f'Chunk D C# parenthesis preflight failed: {x}', errors)

    # Chunk E - compact native buff presentation + tracker-height-driven yOffset/capacity.
    if chunk_e_present:
        for x in (buff_layout_file,buff_layout_harness_file,layout_file,hud_render_file,manager_file):
            need(x.exists(), f'Chunk E source missing: {x}', errors)
        buff_e=read(buff_layout_file); buff_harness=read(buff_layout_harness_file); layout_e=read(layout_file)
        hud_e=read(hud_render_file); manager_e=read(manager_file)

        try:
            wroot=ET.parse(windows).getroot()
            top_level_buff_sets=[n for n in wroot.findall('./set') if "BuffPopoutList" in (n.attrib.get('xpath') or '')]
            rebirth_ifs=[]
            for conditional in wroot.findall('./conditional'):
                rebirth_ifs.extend([n for n in conditional.findall('./if') if n.attrib.get('cond')=="character_progression('Rebirth')"])
            rebirth_buff_sets=[]
            for node in rebirth_ifs:
                rebirth_buff_sets.extend([n for n in node.findall('./set') if "BuffPopoutList" in (n.attrib.get('xpath') or '')])
            need(len(top_level_buff_sets)==0, f'Chunk E Base Game isolation failed: {len(top_level_buff_sets)} top-level BuffPopoutList set(s) remain', errors)
            need(any((n.attrib.get('xpath') or '').endswith("/@pos") and (n.text or '').strip()=='90,43' for n in rebirth_buff_sets), 'Chunk E Rebirth-gated BuffPopoutList 90,43 base anchor missing', errors)
            need(len(rebirth_buff_sets)==1, f'Chunk E V3.2-safe BuffPopoutList XUi contract expects only the verified top-level anchor; found {len(rebirth_buff_sets)} gated setters', errors)
        except Exception as ex:
            errors.append(f'Chunk E BuffPopoutList gating audit failed: {ex}')

        for token in ('rebirthHudBuffLayoutOwner','controller="RebirthHudBuffLayout, RebirthUtils"'):
            need(token in w, f'Chunk E dynamic/native buff layout owner missing: {token}', errors)
        for obsolete in ("panel[@name='item']","sprite[@name='Icon']","label[@name='TextContent']"):
            need(obsolete not in w, f'Chunk E V3.2-invalid native BuffPopoutList child-template XPath must stay removed: {obsolete}', errors)
        for token in ('SetYOffset','ReportMeasuredHudEnvelope','CalculateBuffYOffset','TrackerLayoutChanged','GetActiveBuffRows','BindingFlags.Instance'):
            need(token in buff_e or token in layout_e, f'Chunk E dynamic buff behavior missing: {token}', errors)
        need('AddNotification(' not in buff_e and 'removeEntry(' not in buff_e and 'EntityUINotificationAdded' not in buff_e and 'EntityUINotificationRemoved' not in buff_e,
             'Chunk E layout owner must not take over native buff content ownership', errors)
        need('NetPackage' not in buff_e and 'SendPackage' not in buff_e, 'Chunk E layout must not create network traffic', errors)
        need('TrackerLayoutChanged += OnTrackerLayoutChanged' in hud_e, 'Chunk E gameplay tracker does not react to measured capacity changes', errors)
        need('TrackerLayoutChanged += TrackerLayoutChanged' in manager_e, 'Chunk E Character Progression capacity indicator does not react to live layout changes', errors)
        for token in ('zero-tracker-offset','tracker-gap-offset','1080-no-buffs-capacity','1080-four-buffs-capacity','1080-twelve-buffs-capacity'):
            need(token in buff_harness, f'Chunk E deterministic layout vector missing: {token}', errors)
        need('CompactBuffRowPitch = 28f' in layout_e and 'TrackerBuffGap = 8f' in layout_e and 'UpperSafeReserve = 260f' in layout_e,
             'Chunk E compact buff/shared safe-envelope constants missing', errors)
        need('CalculateUsableTrackerHeight' in layout_e and 'CalculateMeasuredCapacity' in layout_e, 'Chunk E measured safe-capacity math missing', errors)
        for x in (buff_layout_file,buff_layout_harness_file,layout_file,hud_render_file,manager_file):
            txt=read(x)
            need(txt.count('{')==txt.count('}'), f'Chunk E C# brace preflight failed: {x}', errors)
            need(txt.count('(')==txt.count(')'), f'Chunk E C# parenthesis preflight failed: {x}', errors)

    # Chunk F - dedicated Progression Explorer focus tracking action.
    # Tracking is deliberately separate from Node_OnPressed so graph navigation remains navigation-only.
    chunk_f_present='btnProgressionExplorerTrackFocus' in w or 'TrackFocus_OnPressed' in e
    if chunk_f_present:
        loc_f=read(root/'Config/Localization.csv')
        graph_f=read(root/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs')
        skill_award_f=read(root/'Scripts/Survivor/Progression/RebirthSkillAwardService.cs')
        need('name="progressionExplorerTrackFocus"' in w and 'name="btnProgressionExplorerTrackFocus"' in w and 'name="progressionExplorerTrackFocusLabel"' in w,
             'Chunk F Explorer focus tracking control XUi missing', errors)
        for token in ('RebirthHudTrackingRegistry.EnsureDefaults','TryResolveTrackableFocus','TrackFocus_OnPressed',
                      'RebirthHudTrackingPreferenceService.TryGetCurrent','RebirthHudTrackingPreferenceService.Mutate',
                      'RebirthHudTrackType.Skill','RebirthHudTrackType.Knowledge','RebirthProgressionExplorerMode.LiveCharacter',
                      'owner.RebirthModeEnabled','owner.HasCharacter'):
            need(token in e, f'Chunk F Explorer tracking behavior missing: {token}', errors)
        need('node.Type==RebirthProgressionGraphNodeType.Skill' in e and 'node.Type==RebirthProgressionGraphNodeType.Knowledge' in e,
             'Chunk F Explorer action must support exactly graph Skill/Knowledge focus types', errors)
        need('RebirthHudTrackType.Attribute' not in e[e.find('private bool TryResolveTrackableFocus'):e.find('private void TrackFocus_OnPressed')],
             'Chunk F Explorer focus action must not expose Attribute tracking outside Character -> Progression', errors)
        need('RebirthHudTrackType.Summary' not in e[e.find('private bool TryResolveTrackableFocus'):e.find('private void TrackFocus_OnPressed')],
             'Chunk F Explorer focus action must not expose Summary tracking outside Character -> Progression', errors)
        need('AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Skill' in graph_f and
             'AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Knowledge' in graph_f,
             'Chunk F stable-ID mapping assumption changed: graph Skill/Knowledge IDs must remain progression definition IDs', errors)
        need('xuiRebirthHudTrackingTrackFocus,' in loc_f and 'xuiRebirthHudTrackingUntrackFocus,' in loc_f,
             'Chunk F Explorer tracking localization missing', errors)
        node_start=e.find('private void Node_OnPressed')
        node_end=e.find('private void SearchResult_OnPressed',node_start)
        node_body=e[node_start:node_end] if node_start>=0 and node_end>node_start else ''
        need(node_body!='', 'Chunk F could not isolate Node_OnPressed for navigation-only audit', errors)
        need('RebirthHudTrackingPreferenceService' not in node_body and '.Add(' not in node_body and '.Remove(' not in node_body,
             'Chunk F must not overload graph-node navigation with HUD tracking mutation', errors)
        need('NetPackage' not in e and 'SendPackage' not in e, 'Chunk F Explorer tracking action must not create network traffic', errors)
        # Optional contextual active Skill remains deferred: current award authority has no client-visible active-Skill event/identity.
        # The persisted future preference flag may remain in the model/store, but gameplay HUD must not infer a current Skill from snapshot deltas.
        need('event ' not in skill_award_f or 'Skill' not in ''.join(re.findall(r'event[^;]+;',skill_award_f)),
             'Chunk F active-Skill audit changed: review newly exposed Skill award event before leaving contextual row deferred', errors)
        if chunk_d_present:
            need('ShowContextualActiveSkill' not in read(hud_render_file),
                 'Chunk F contextual active Skill is deferred; gameplay HUD must not infer/render it without a truthful runtime signal', errors)
        for x in (explorer,):
            txt=read(x)
            need(txt.count('{')==txt.count('}'), f'Chunk F C# brace preflight failed: {x}', errors)
            need(txt.count('(')==txt.count(')'), f'Chunk F C# parenthesis preflight failed: {x}', errors)

    # Chunk G final source/static closure. This does not pretend to replace a real C# compile
    # or 7DTD runtime pass; it makes the final package fail if any implemented A-F source layer
    # has silently fallen out of the integrated tree.
    if args.require_final:
        need(chunk_b_present, 'Chunk G final gate: Chunk B preference/provider foundation is missing', errors)
        need(chunk_c_present, 'Chunk G final gate: Chunk C Progression configuration is missing', errors)
        need(chunk_d_present, 'Chunk G final gate: Chunk D gameplay HUD renderer is missing', errors)
        need(chunk_e_present, 'Chunk G final gate: Chunk E dynamic buff integration is missing', errors)
        need(chunk_f_present, 'Chunk G final gate: Chunk F Explorer integration is missing', errors)
        project_change=read(root/'_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_HUD_TRACKED_PROGRESSION_DESIGN_IMPLEMENTATION.md')
        need('CHUNKS A-G SOURCE/STATIC COMPLETE' in project_change,
             'Chunk G final gate: authoritative project-change status is not marked A-G source/static complete', errors)
        need('Chunk G — final validation/package:** COMPLETE' in project_change,
             'Chunk G final gate: authoritative project-change Chunk G status is not COMPLETE', errors)

    # Existing icon family inventory: no generated asset requirement in Chunk A/B.
    icons=root/'UIAtlases'/'RebirthSurvivorIcons'
    if icons.exists():
        skill_icons=list(icons.glob('rb_skill_*.png'))
        need(len(skill_icons)>=authoritative_skill_count, f'expected at least {authoritative_skill_count} Skill icons; found {len(skill_icons)}', errors)
        need((icons/'rb_ui_knowledge.png').exists(), 'rb_ui_knowledge.png missing', errors)
    else:
        errors.append(f'RebirthSurvivorIcons atlas directory missing: {icons}')

    if errors:
        print('REBIRTH HUD TRACKER STATIC CONTRACT: FAIL')
        for x in errors: print('ERROR:',x)
        return 1
    print('REBIRTH HUD TRACKER STATIC CONTRACT: PASS')
    print('XUi XML: 4/4 parsed')
    print('BuffPopoutList layout:', 'REBIRTH-GATED COMPACT/DYNAMIC' if chunk_e_present else 'HUDLeftStatBars @ 90,43 (global before-state)')
    print(f'Character Progression: {authoritative_skill_count} authoritative Skill rows/buttons + scrollview contract present')
    print('Owner snapshot: CharacterRevision + OwnerStateChanged present')
    print('Persistence foundation: stable StorageKey + GameIO user-data root + atomic XML present')
    print('Explorer extension: FocusId/FocusType projection contract present')
    print('Release 1 icon foundation: present; no Chunk A asset generation required')
    print('Chunk B preference/provider foundation:', 'PRESENT' if chunk_b_present else 'NOT YET PRESENT')
    if chunk_b_present:
        print('Chunk B providers: Skill + Attribute + Knowledge + Summary(Knowledge Discovered)')
        print('Chunk B preferences: local world/player key + schema v1 + unknown-ID preservation + 500ms debounce')
    print('Chunk C Progression configuration:', 'PRESENT' if chunk_c_present else 'NOT YET PRESENT')
    if chunk_c_present:
        print('Chunk C manager: tracked + available stock-scroll lists, 4 typed filters, reorder/remove/clear/toggle')
        print('Chunk C capacity: resolution-aware fallback + measured-geometry handoff for D/E; no fixed five-entry cap')
    print('Chunk D gameplay HUD rendering:', 'PRESENT' if chunk_d_present else 'NOT YET PRESENT')
    if chunk_d_present:
        print('Chunk D renderer: typed rows + progress/value states + gain pulse + overflow indicator')
        print('Chunk D layout handoff: live rendered tracker height/rows exposed for Chunk E')
        print('Dynamic BuffPopoutList movement:', 'PRESENT' if chunk_e_present else 'NOT YET (reserved for E)')
    elif chunk_c_present:
        print('Gameplay HUD rendering / dynamic buff layout: NOT YET (reserved for D/E)')
    if chunk_e_present:
        print('Chunk E buff integration: native BuffPopoutList + SetYOffset tracker stacking + live safe-capacity envelope; obsolete child-template XML setters removed for V3.2')
        print('Chunk E Base Game isolation: global BuffPopoutList override removed; one verified Rebirth-only anchor + runtime layout owner')
    if chunk_f_present:
        print('Chunk F Explorer integration: dedicated live-character Track/Untrack action for focused Skill/Knowledge')
        print('Chunk F graph navigation isolation: Node_OnPressed remains navigation-only')
        print('Chunk F contextual active Skill: DEFERRED (no truthful client-visible active-Skill identity/event)')
    if args.require_final:
        print('Chunk G final source/static gate: PASS (A-F integrated source layers required)')
        print('Chunk G compile/live status: PENDING external 7DTD/Managed-assembly environment')
    elif chunk_b_present:
        print('Runtime HUD/XUi behavior changed by Chunk B: NO')
    else:
        print('Runtime behavior changed by Chunk A: NO')
    return 0

if __name__=='__main__':
    sys.exit(main())
