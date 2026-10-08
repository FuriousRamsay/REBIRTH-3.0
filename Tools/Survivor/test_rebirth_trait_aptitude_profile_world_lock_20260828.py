#!/usr/bin/env python3
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
errors=[]
def need(rel, token, desc=None):
    text=(ROOT/rel).read_text(encoding='utf-8-sig' if rel.endswith('Localization.csv') else 'utf-8')
    if token not in text:
        errors.append(f"{rel}: missing {desc or token}")
    return text

def forbid(rel, token, desc=None):
    text=(ROOT/rel).read_text(encoding='utf-8-sig' if rel.endswith('Localization.csv') else 'utf-8')
    if token in text:
        errors.append(f"{rel}: forbidden {desc or token}")
    return text

# XML parse sanity.
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml','Config/_Survivor/progression.xml']:
    try: ET.parse(ROOT/rel)
    except Exception as e: errors.append(f'{rel}: XML parse failed: {e}')

validator=need('Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs','ValidateForCommit','final exact-balance validator')
for token in [
    'RebirthSkillAptitudeTraitFactory.IsAptitude(t)',
    'RebirthSkillAptitudeTraitFactory.TryParse(t.Id',
    'AddDirect(skills,aptitudeSkill,aptitudeBonus,errors,t.Id)',
    'requireZeroPointBalance && points>0',
    'RebirthSurvivorCreationErrorCode.UnspentCreationPoints']:
    if token not in validator: errors.append('validator: missing '+token)
# Aptitudes must be resolved before the authored-modifier lookup.
if validator.find('RebirthSkillAptitudeTraitFactory.IsAptitude(t)') > validator.find('TryGetModifier(t.ModifierId'):
    errors.append('validator: aptitude resolution occurs after authored modifier lookup')

result=need('Scripts/Survivor/Creation/RebirthSurvivorCreationResult.cs','UnspentCreationPoints')
factory=need('Scripts/Survivor/Definitions/RebirthSkillAptitudeTraitFactory.cs','public const string DynamicModifierId = "dynamic.skill_aptitude";')
for token in ['public const int MaxTier = 2;','case 1: return 1;','case 2: return 3;','return Math.Max(1, Math.Min(MaxTier, tier)) * 5;']:
    if token not in factory: errors.append('aptitude factory: missing '+token)

# Creation-only runtime contract is explicit.
recon=need('Scripts/Survivor/Definitions/RebirthTraitRuntimeReconciliation.cs','return RebirthTraitImplementationState.CreationOnly;')
condition=need('Scripts/Survivor/Condition/RebirthConditionTraitModifierService.cs','RebirthSkillAptitudeTraitFactory.IsAptitude(trait)')
gameplay=need('Scripts/Survivor/Progression/RebirthTraitGameplayModifierService.cs','Generated Skill Aptitudes are fully resolved into Origin.StartingSkills during creation.')

# The central selected-trait list must advertise real scrollable content height, not remain at the 118px authored stub.
creator=need('Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs','traitSelectedText.Size = new Vector2i(500, selectedTraitContentHeight);')
if 'traitSelectedNativeScrollProxy.ViewComponent.Size = new Vector2i(1, selectedTraitContentHeight)' not in creator:
    errors.append('creator: selected Trait scrollbar proxy is not sized to full content')
for rel in ['Config/XUi_Menu/windows.xml','Config/XUi_InGame/windows.xml']:
    x=(ROOT/rel).read_text(encoding='utf-8')
    if 'name="traitSelectedText"' not in x or 'name="traitSelectedNativeScrollProxy"' not in x or '<defaultscrollbar/>' not in x:
        errors.append(rel+': selected trait native scrollbar/proxy contract missing')

# Exact-zero balance is authoritative for reusable profiles and actual world commit.
store=need('Scripts/Survivor/Persistence/RebirthSurvivorProfileStore.cs','ValidateForCommit(selection, false)')
for token in ['ValidateForCommit(profile.ToSelection(currentHash), false)','RebirthSurvivorCreationErrorCode.UnspentCreationPoints','RebirthSurvivorProfileCompatibilityKind.NeedsReview']:
    if token not in store: errors.append('profile store: missing '+token)
for rel in ['Scripts/Survivor/Creation/RebirthSurvivorCreationService.cs','Scripts/Survivor/Network/RebirthSurvivorCreationTransactions.cs']:
    need(rel,'ValidateForCommit','server-authoritative exact-zero validation')

# Profile Manager is management-only. World profile choice lives in the first-entry chooser.
menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
manager=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
chooser=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthChooseSurvivor.cs').read_text(encoding='utf-8')
ingame=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
menu_xui=(ROOT/'Config/XUi_Menu/xui.xml').read_text(encoding='utf-8')
if "xpath=\"/windows/window[@name='spawnselection']\"" not in menu or 'rebirthSurvivorSpawnGate' not in menu:
    errors.append('first-entry chooser: must be integrated directly into XUi_Menu spawnselection before EntityPlayerLocal exists')
if 'btnUseProfile' in menu or 'PreferProfileForNextSelection' in manager or 'PreferProfileForNextSelection' in chooser:
    errors.append('profile selection: obsolete main-menu/preference path still exists')
for token in ['profileSelectionTimingHelp','btnCreateProfile','btnEditProfile','btnDeleteProfile']:
    if token not in menu: errors.append('profile manager: missing '+token)
for token in ['btnUseSelectedSurvivor','btnCreateNewSurvivor','RequestCommit(player, selection, profile)','EvaluateCompatibility(profile)']:
    if token not in chooser and token not in ingame: errors.append('first-entry chooser: missing '+token)
first=need('Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs','native spawnselection-integrated gate','pre-spawn lifecycle gate')
for token in ['OnPlayerSpawned','OnOwnerStateChanged','LocalPlayerUI.GetUIForPrimaryPlayer()','RegisterNativeSpawnSelectionHost','StageExistingProfile','StageNewSurvivor','OpenNativeSpawnSelection','DispatchStagedCommit']:
    if token not in first: errors.append('first-entry UI: missing '+token)
if 'creation required; waiting for native spawnselection' in first:
    errors.append('first-entry UI: selector still waits for native spawn prompt')

# World origin stores all creation choices by value; source profile fields are provenance only.
origin=need('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs','Provenance only. Runtime gameplay never re-reads the reusable local profile')
for token in ['ReadOnlyCollection<string> TraitIds','ReadOnlyDictionary<string, float> StartingSkills','ReadOnlyDictionary<string, string> CreationChoices','result.TraitIds','result.StartingSkills']:
    if token not in origin: errors.append('world origin snapshot: missing '+token)
repo=need('Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs','sourceProfileId')
for token in ['origin.TraitIds','origin.StartingSkills']:
    if token not in repo: errors.append('world repository: missing persisted '+token)
# Gameplay paths may use the save-owned origin, but must not fetch the mutable local profile store.
for folder in ['Scripts/Survivor/Condition','Scripts/Survivor/Progression','Scripts/Survivor/Support']:
    for p in (ROOT/folder).glob('*.cs'):
        if 'RebirthSurvivorProfileStore' in p.read_text(encoding='utf-8'):
            errors.append(f'{p.relative_to(ROOT)}: runtime gameplay references mutable profile store')

# Existing save locks PlayerProgression; other sandbox options remain editable. Existing saves also do not rewrite future new-world defaults.
session=need('Scripts/Options/RebirthSandboxUiSession.cs','TryGetLockedPlayerProgression')
for token in ['contextWasExistingSave','requested.PlayerProgression = locked.PlayerProgression','liveState.PlayerProgression = persistedState.PlayerProgression','saveAsLastUsed && !contextWasExistingSave','EnsureContextCurrent();']:
    if token not in session: errors.append('sandbox session: missing '+token)
if session.count('RebirthSandboxPersistence.CurrentWorldHasStarted()') < 2:
    errors.append('sandbox session: progression lock does not distinguish an established world from a pre-created New Game directory')
if 'CurrentSaveDirectoryExists()' in session:
    errors.append('sandbox session: progression incorrectly locks on directory existence')
options=need('Scripts/UI/XUiC_RebirthSandboxOptions.cs','TryGetLockedPlayerProgression')
if 'cbxPlayerProgression.ViewComponent.Enabled = !progressionLocked' not in options:
    errors.append('sandbox options: progression combobox is not disabled for existing saves')

loc=(ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig')
for key in ['xuiRebirthCreationErrorUnspentCreationPoints','xuiRebirthSurvivorSelectionTimingHelp','xuiRebirthPlayerProgressionLocked']:
    if len(re.findall(r'^'+re.escape(key)+r',',loc,re.M)) != 1: errors.append('Localization.csv: missing/duplicate '+key)

print('REBIRTH Trait Aptitude/Profile/World Lock Contract:', 'PASS' if not errors else 'FAIL')
for e in errors: print('FAIL',e)
print('errors='+str(len(errors)))
raise SystemExit(1 if errors else 0)
