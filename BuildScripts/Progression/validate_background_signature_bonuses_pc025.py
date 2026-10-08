#!/usr/bin/env python3
from pathlib import Path
import sys,re,csv,xml.etree.ElementTree as ET
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve(); passed=failed=0

def ck(name,cond):
 global passed,failed
 if cond: passed+=1; print('PASS',name)
 else: failed+=1; print('FAIL',name)
def txt(rel): return (root/rel).read_text(encoding='utf-8',errors='replace')
def balanced(rel):
 s=txt(rel)
 s=re.sub(r'/\*.*?\*/','',s,flags=re.S); s=re.sub(r'//.*','',s)
 s=re.sub(r'@?"(?:""|\\.|[^"\\])*"','""',s); s=re.sub(r"'(?:\\.|[^'\\])'","''",s)
 return s.count('{')==s.count('}') and s.count('(')==s.count(')') and s.count('[')==s.count(']')

prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot(); skills={x.get('id'):x for x in prog.find('skills')}; know={x.get('id'):x for x in prog.find('knowledge')}
bg=ET.parse(root/'Config/_Survivor/backgrounds.xml').getroot(); salesperson=bg.find("./background[@id='background.salesperson']")
bon=ET.parse(root/'Config/_Survivor/background_bonuses.xml').getroot(); more=bon.find("./bonus[@id='background_bonus.more_options']")
traders=ET.parse(root/'Config/_Survivor/traders.xml').getroot(); buffs=ET.parse(root/'Config/buffs.xml').getroot()
loc=txt('Config/Localization.csv'); svc=txt('Scripts/Survivor/Progression/RebirthSkillWaveAService.cs'); patches=txt('Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs')
cfg=txt('Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs'); sources=txt('Config/_Survivor/skill_sources.xml'); ids=txt('Scripts/Survivor/Domain/RebirthSurvivorIds.cs')
mig=txt('Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs'); policy=txt('Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs'); models=txt('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs')
install=txt('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs'); release=txt('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')
netmodels=txt('Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs'); hud=txt('Scripts/Survivor/HudTracking/XUiC_RebirthHudTrackingManager.cs')
xui_ingame=txt('Config/XUi_InGame/windows.xml')

# Skill / relationships
s=skills.get('skill.trading'); ck('Trading skill exists',s is not None); ck('Trading is normal with signed runtime bounds',s is not None and s.get('min')=='-50' and s.get('max')=='100' and s.get('advanced')!='true'); ck('Trading LBD source is committed commerce',s is not None and 'committed NPC trader purchases and sales' in (s.get('lbd_source') or ''))
b=skills.get('skill.bartering'); ck('legacy Bartering retained',b is not None); ck('legacy Bartering marked compatibility',b is not None and 'compatibility' in ((b.get('lbd_source') or '')+(b.get('feasibility') or '')).lower()); ck('legacy Bartering no longer transaction LBD',b is not None and 'no longer receives new trader-transaction LBD' in (b.get('feasibility') or ''))
ck('47+ current Skills authored',len(skills)>=47); ck('Trading stable ID constant','SkillTrading = "skill.trading"' in ids); ck('trader familiarity links Trading and legacy',know['knowledge.trader.familiarity'].get('associated_skills')=='skill.bartering,skill.trading'); ck('appraisal links Trading and legacy',know['procedure.bartering.appraisal'].get('associated_skills')=='skill.bartering,skill.trading')
ck('Trading localized','xuiRebirthSkillTrading,Trading' in loc and 'xuiRebirthSkillSourceTrading,' in loc); ck('Trading stock localized','xuiRebirthTradingSpecialStock,Trading Stock' in loc and 'xuiRebirthTradingSpecialStockDesc,' in loc)

# Salesperson / bonus
starts={x.get('id'):float(x.get('value')) for x in salesperson.find('starting_skills')}; theory={x.get('id'):float(x.get('value')) for x in salesperson.find('starting_skill_knowledge')}
ck('Salesperson starts Trading +40',starts.get('skill.trading')==40 and 'skill.bartering' not in starts); ck('Salesperson weaknesses preserved',starts.get('skill.athletics')==-10 and starts.get('skill.construction')==-5); ck('Salesperson positive starting Skill budget preserved',sum(v for v in starts.values() if v>0)==40); ck('Salesperson Trading knowledge +45',theory.get('skill.trading')==45 and 'skill.bartering' not in theory); ck('Salesperson Appraisal Knowledge preserved',salesperson.find("./starting_knowledge/knowledge[@id='procedure.bartering.appraisal']") is not None)
t=more.find("./tuning[@key='extra_reward_choices']") if more is not None else None; ck('Salesperson More Options locked +1',t is not None and t.get('value')=='1' and t.get('locked')=='true'); ck('More Options copy says option not claim','additional reward option' in loc and 'does not increase how many rewards are claimed' in loc)

# Runtime tunables and native passive ownership
for token in ['BarterNegative = -0.12f','BarterPositive = 0.16f','BarterLegacyPositiveCap = 0.04f','TradingPositive = 0.12f','TradingRewardOptionThreshold = 50f','BarterRepeatSeconds = 30f','BarterLoopSeconds = 300f','TradingVarietyWindowSeconds = 120f','TradingMinimumValue = 50f','TradingMinimumUnitValue = 5f','TradingValueSpoofMultiplier = 20f']:
 ck('runtime tuning '+token,token in cfg)
for attr in ['barter_legacy_positive_cap="0.04"','trading_positive="0.12"','trading_reward_option_threshold="50"','barter_repeat_seconds="30"','barter_loop_seconds="300"','trading_variety_window_seconds="120"','trading_minimum_value="50"','trading_minimum_unit_value="5"','trading_value_spoof_multiplier="20"']:
 ck('skill_sources tuning '+attr,attr in sources)
wave=next((a for a in buffs.findall('append') if a.get('xpath')=='/buffs' and a.find("./buff[@name='RebirthSurvivorSkillWaveAPassives']") is not None),None); wb=wave.find("./buff[@name='RebirthSurvivorSkillWaveAPassives']") if wave is not None else None; effects=wb.findall('.//passive_effect') if wb is not None else []
ck('Trading price uses native BarteringBuying',any(x.get('name')=='BarteringBuying' and x.get('value')=='@$rbSurvivorSkillBarterBuying' for x in effects)); ck('Trading price uses native BarteringSelling',any(x.get('name')=='BarteringSelling' and x.get('value')=='@$rbSurvivorSkillBarterSelling' for x in effects)); ck('Trading stock uses native SecretStash',any(x.get('name')=='SecretStash' and x.get('operation')=='base_set' and x.get('value')=='@$rbSurvivorSkillTradingStashLevel' for x in effects)); qopt=[x for x in effects if x.get('name')=='QuestRewardOptionCount']; ck('exactly two Rebirth reward option contributions',len(qopt)==2); ck('Trading midpoint reward option passive',any(x.get('value')=='@$rbSurvivorTradingRewardOptionBonus' for x in qopt)); ck('Salesperson reward option passive',any(x.get('value')=='@$rbSurvivorSalespersonRewardOptionBonus' for x in qopt)); ck('does not change reward choice count',not any(x.get('name')=='QuestRewardChoiceCount' for x in effects))

# Price composition / stock / reward options
ck('legacy Bartering positive cap used','BarterLegacyPositiveCap' in svc and 'legacyBarter' in svc); ck('positive legacy still Appraisal-gated','legacyBarter>0f && !RebirthKnowledgeService.HasKnowledge(player,"procedure.bartering.appraisal")' in svc); ck('Trading price curve used','trading/100f' in svc and 'TradingPositive' in svc); ck('combined positive commerce cap used','Mathf.Clamp(legacyBarter + tradingPrice, RebirthProgressionRuntimeConfig.BarterNegative, RebirthProgressionRuntimeConfig.BarterPositive)' in svc); ck('Trading stock level follows 0-100 Skill','SetCVar(player, TradingStashCVar, Mathf.Clamp(trading,0f,100f))' in svc); ck('Trading threshold grants exactly one option','trading + 0.0001f >= RebirthProgressionRuntimeConfig.TradingRewardOptionThreshold ? 1f : 0f' in svc); ck('Salesperson grants independent option','SetCVar(player, SalespersonRewardOptionCVar, salesperson ? 1f : 0f)' in svc); ck('client and server both identify Salesperson background',svc.count('background.salesperson')>=2)

# Transaction authority and anti-abuse
ck('buy patch observes completed NPC purchase','ItemActionEntryPurchase' in patches and 'spent > 0' in patches and 'ReportClientBarter(__state.Player, true' in patches); ck('sell patch observes completed NPC sale','ItemActionEntrySell' in patches and 'received > 0' in patches and 'ReportClientBarter(__state.Player, false' in patches); ck('vending/player-owned purchase excluded','e.isOwner || e.isVending' in patches); ck('player-owned/rentable trader excluded','ReadBoolMember(traderInfo, "PlayerOwned") || ReadBoolMember(traderInfo, "Rentable")' in patches)
net=txt('Scripts/Survivor/Progression/RebirthSkillWaveANetPackages.cs'); ck('transaction request is ToServer','NetPackageDirection.ToServer' in net); ck('transaction sender is validated','ValidEntityIdForSender(playerId)' in net or 'ValidEntityIdForSender(playerEntityId)' in net); ck('server requires eligible Rebirth character','RebirthSkillAwardService.TryGetEligible(player, out identity, out record)' in svc); ck('server requires nearby NPC trader','HasNearbyTrader(player, 10f)' in svc); ck('server validates item EconomicValue','ItemClass.GetForId(itemType)' in svc and 'item.EconomicValue' in svc); ck('meaningful total value floor','TradingMinimumValue' in svc and 'reportedValue' in svc); ck('meaningful unit value floor','TradingMinimumUnitValue' in svc and 'reportedValue/(float)Math.Max(1,count)' in svc); ck('spoof ceiling uses authoritative item economics','baseEconomic*Math.Max(1,count)*Mathf.Max(1f,RebirthProgressionRuntimeConfig.TradingValueSpoofMultiplier)' in svc); ck('global cooldown preserved','BarterGlobalSeconds' in svc); ck('same-item guard 30s authored',bool(re.search(r'BarterRepeatSeconds\s*=\s*30f',cfg))); ck('buy/sell loop guard 300s authored',bool(re.search(r'BarterLoopSeconds\s*=\s*300f',cfg))); ck('variety window authored',bool(re.search(r'TradingVarietyWindowSeconds\s*=\s*120f',cfg))); ck('same-item repetition diminishes','h.RepeatChain' in svc and 'Mathf.Max(0.25f,1f/(1f+h.RepeatChain))' in svc); ck('committed transaction awards Trading','RebirthSkillAwardService.TryAward(player, RebirthSurvivorIds.SkillTrading' in svc); ck('committed transaction no longer awards legacy Bartering','TryAward(player, RebirthSurvivorIds.SkillBartering' not in svc and 'TryAward(player, "skill.bartering"' not in svc)

# Native tier_items special stock
appends=traders.findall("./append")
expected_ids={'1','2','6','7','8'}; seen=set(); exact_levels=['1,24','25,49','50,74','75,100']; counts=['2','3','4','5']; tier_groups=[]
for a in appends:
 xp=a.get('xpath') or ''
 m=re.search(r"trader_info\[@id='(\d+)'\]",xp)
 if not m: continue
 tid=m.group(1); rows=a.findall('tier_items')
 if tid in expected_ids:
  seen.add(tid); ck('trader '+tid+' has four Trading Stock tiers',len(rows)==4); ck('trader '+tid+' tier levels exact',[x.get('level') for x in rows]==exact_levels); ck('trader '+tid+' stock counts increase',[x.get('count') for x in rows]==counts)
  for row in rows:
   tier_groups.extend(i.get('group') or i.get('name') or '' for i in row.findall('item'))
ck('all five main traders receive Trading Stock',seen==expected_ids); ck('high Trading stock includes advanced discoveries',any('rebirthLiteratureTraderDiscoveryAdvanced' in x for x in tier_groups)); ck('special stock authoring has distinct trader specialization',len(set(x for x in tier_groups if x))>=15)
ck('Rebirth-only Trading Stock label patch exists','RebirthTradingSpecialStockLabelPatch' in patches and 'xuiRebirthTradingSpecialStock' in patches); ck('Base Game/native Secret Stash label fallback','xuiSecretStash' in patches); ck('stock label patch installed','typeof(RebirthTradingSpecialStockLabelPatch)' in install)
# Quest reward UI capacity: base window remains native; Rebirth conditional patch expands only the active Rebirth turn-in window to seven entries.
ck('Rebirth reward window expands to seven rows','windowQuestTurnInRewards' in xui_ingame and 'name="rows">7</setattribute>' in xui_ingame)
ck('Rebirth reward window adds exactly two entries',xui_ingame.count('<quest_turnin_entry pos="0,-242"/>')==1 and xui_ingame.count('<quest_turnin_entry pos="0,-292"/>')==1)
ck('Rebirth reward window height expanded','windowQuestTurnInRewards' in xui_ingame and 'name="height">554</setattribute>' in xui_ingame and 'name="pos">0,-494</setattribute>' in xui_ingame)
ck('reward UI patch remains inside Rebirth conditional',xui_ingame.rfind('<if cond="character_progression(\'Rebirth\')">') < xui_ingame.find('PC025 / Chunk L: Trading/Salesperson') < xui_ingame.rfind('</if>'))

# Persistence migration and complete current catalogue
m_schema=re.search(r'CurrentSchemaVersion\s*=\s*(\d+)',models); ck('world-character schema is 14+',m_schema is not None and int(m_schema.group(1))>=14); ck('13 to 14 migration registered','Register(13, Migrate13To14)' in mig); ck('migration seeds Trading from positive Bartering','SkillTrading,ob' in mig and 'SkillTrading,pb' in mig and 'Math.Max(0f,ob)' in mig and 'Math.Max(0f,pb)' in mig); ck('migration seeds Trading Skill Knowledge','SkillTrading,ok' in mig and 'SkillTrading,pk' in mig); ck('legacy Bartering is preserved by migration','legacyBarteringPreserved=true' in mig); ck('schema 13 Drink Preparation repair exists','drinkPreparationSchema13Backfill=true' in mig and 'background.bartender' in mig and 'bartender?25f:0f' in mig and 'bartender?30f:0f' in mig); ck('migration updates definition hash/version','definitionHash' in mig and 'definitionVersion' in mig and 'RebirthSurvivorDefinitionRegistry.SemanticHash' in mig)
# Extract allowlist literals only from CurrentIds declaration
m=re.search(r'CurrentIds\s*=\s*new string\[\]\s*\{(.*?)\};',policy,re.S); current=set(re.findall(r'"(skill\.[^"]+)"',m.group(1))) if m else set(); ck('current Skill allowlist has 47+',len(current)>=47); ck('current Skill allowlist exactly matches progression',current==set(skills)); ck('allowlist contains Rage Drink Preparation Trading',{'skill.rage','skill.drink_preparation','skill.trading'}.issubset(current)); ck('network skill capacity covers catalogue','MaxSkills = 64' in netmodels and 64>=len(skills)); ck('release acceptance expects 47+ Skills',('GetCurrentSkillIds().Length' in release) or ('expected=47' in release and 'expectedSkillKnowledgeAreas=47' in release) or ('expected=48' in release and 'expectedSkillKnowledgeAreas=48' in release)); ck('HUD tracked capacity covers expanded catalogue','TrackedRows = 272' in hud); ck('HUD available capacity covers expanded catalogue','AvailableRows = 208' in hud)

# Debug/vector acceptance and forward-compatible advanced discipline validators
vectors=txt('Scripts/Survivor/Progression/RebirthSkillWaveAVectorHarness.cs'); ck('vector covers legacy cap','BarterLegacyPositiveCap' in vectors); ck('vector covers Trading price contribution','TradingPositive' in vectors); ck('vector covers anti-loop guards','BarterRepeatSeconds' in vectors and 'TradingVarietyWindowSeconds' in vectors); ck('vector covers value floors','TradingMinimumValue' in vectors and 'TradingMinimumUnitValue' in vectors); ck('vector covers spoof ceiling','TradingValueSpoofMultiplier' in vectors); dbg=txt('Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs'); ck('existing Wave A debug remains exposed','wavea' in dbg.lower() and 'RebirthSkillWaveAService.BuildDebugReport' in dbg); pc010v=txt('BuildScripts/Progression/validate_advanced_disciplines_pc010.py'); ck('advanced PC010 accepts schema 13+','world schema 13+' in pc010v); ck('advanced PC013 accepts later approved Skills','later approved normal Skills allowed' in txt('BuildScripts/Progression/validate_advanced_disciplines_pc013.py'))

# Documentation
sourceaudit=txt('_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC025_CHUNK_L_SOURCE_AUDIT_20260902.md'); pc=txt('_Documentation/ProjectChanges/REBIRTH_3_0_PROJECT_CHANGE_BACKGROUND_SIGNATURE_BONUSES_PC025_CHUNK_L_TRADING_SPECIAL_STOCK_SALESPERSON_20260902.md')
for phrase in ['SecretStash','TierItemGroups','QuestRewardOptionCount','QuestRewardChoiceCount','ItemActionEntryPurchase','ItemActionEntrySell','schema 13','Bartering']:
 ck('source audit '+phrase,phrase in sourceaudit)
ck('project change records source/static not live complete','compile/live acceptance pending' in pc); ck('project change documents schema 13 to 14','schema 13 → 14' in pc); ck('project change documents 47-Skill repair','47-Skill' in pc); ck('project change documents Base Game isolation','Base Game' in pc and 'Secret Stash' in pc)

# Localization, XML, delimiters, Python syntax
rows=list(csv.reader((root/'Config/Localization.csv').open(encoding='utf-8-sig'))); keys=[r[0].strip() for r in rows[1:] if r and r[0].strip()]; ck('localization duplicate keys zero',len(keys)==len(set(keys)))
xmls=list(root.rglob('*.xml')); bad=[]
for p in xmls:
 try: ET.parse(p)
 except Exception: bad.append(str(p.relative_to(root)))
ck('all project XML parses',not bad)
for rel in ['Scripts/Survivor/Progression/RebirthSkillWaveAService.cs','Scripts/Survivor/Progression/RebirthSkillWaveAPatches.cs','Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs','Scripts/Survivor/Domain/RebirthSurvivorIds.cs','Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs','Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs','Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs','Scripts/Survivor/Progression/RebirthAttributeProgressionService.cs','Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs','Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs','Scripts/Survivor/Debug/RebirthSurvivorMigrationVectorHarness.cs','Scripts/Survivor/Debug/RebirthAdvancedDisciplinesAcceptance.cs']:
 ck('delimiter '+Path(rel).name,balanced(rel))
print(f'PC025_STATIC_CHECKS={passed+failed} PASSED={passed} FAILED={failed} XML={len(xmls)}')
sys.exit(1 if failed else 0)
