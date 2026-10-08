#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET, re
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]
def ck(cond,msg):
    ok=bool(cond); checks.append((ok,msg)); print(('PASS' if ok else 'FAIL')+' | '+msg)
menu_path=root/'Config/XUi_Menu/windows.xml'
manager_path=root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs'
menu=menu_path.read_text(encoding='utf-8',errors='ignore')
manager=manager_path.read_text(encoding='utf-8',errors='ignore') if manager_path.exists() else ''
root_xml=ET.parse(menu_path).getroot()
def named(name):
    for e in root_xml.iter():
        if e.attrib.get('name')==name: return e
    return None
# PC037/PC038 profile-summary contract.
for n in ['selectedProfileTitle','selectedProfileSubtitle','selectedProfileSignatureHeading','selectedProfileSignatureName','selectedProfileSignatureDescription','selectedProfileSignatureIcon','selectedPlayerPreview']:
    ck(named(n) is not None, 'profile summary control exists: '+n)
ck(named('selectedProfileTitle') is not None and named('selectedProfileTitle').attrib.get('pos','').startswith('18,'),'profile name restored to left edge')
ck(named('selectedProfileTitle') is not None and int(named('selectedProfileTitle').attrib.get('width','0'))>=540,'profile name keeps wide left column')
ck(named('selectedProfileSubtitle') is not None and int(named('selectedProfileSubtitle').attrib.get('width','0'))>=540,'Background/Diet keeps wide left column')
ck(named('selectedProfileSignatureDescription') is not None and named('selectedProfileSignatureDescription').attrib.get('overflow')=='shrinkcontent','Signature Bonus description preserves localization-safe shrink behavior')
ck(named('selectedProfileSignatureDescription') is not None and int(named('selectedProfileSignatureDescription').attrib.get('height','0'))>=50,'Signature Bonus description retains multiline capacity')
art=named('selectedProfileArt'); parent=None
for e in root_xml.iter():
    if art is not None and art in list(e): parent=e; break
ck(parent is not None and parent.attrib.get('visible')=='false','duplicate selected Background art is hidden')
ck(named('selectedProfileDescription') is not None and named('selectedProfileDescription').attrib.get('visible')=='false','old Background prose description remains hidden')
ck(named('selectedProfileIdentityMeta') is not None and named('selectedProfileIdentityMeta').attrib.get('visible')=='false','old Player Profile / Created metadata remains hidden')
icon=named('selectedProfileSignatureIcon')
ck(icon is not None and icon.attrib.get('atlas')=='RebirthSurvivorIcons','Signature Bonus icon uses RebirthSurvivorIcons atlas')
# Controller/XUi sync.
if manager:
    ck('lblSelectedSignatureHeading = Label("selectedProfileSignatureHeading")' in manager,'controller binds Signature Bonus heading')
    ck('selectedSignatureBonusIcon = Sprite("selectedProfileSignatureIcon")' in manager,'controller binds Signature Bonus icon')
    ck('RebirthSurvivorUiText.SignatureBonusName(selectedProfile.BackgroundId)' in manager,'controller populates authoritative Signature Bonus name')
    ck('RebirthSurvivorUiText.SignatureBonusDescription(selectedProfile.BackgroundId)' in manager,'controller populates authoritative Signature Bonus description')
    ck('RebirthSurvivorUiText.SignatureBonusIcon(selectedProfile.BackgroundId)' in manager,'controller populates authoritative Signature Bonus icon')
# PC038 z-order lost by PC056 must also be restored.
def depth(name):
    m=re.search(r'<window name="'+re.escape(name)+r'"[^>]*\bdepth="(\d+)"',menu)
    return int(m.group(1)) if m else None
pd=depth('rebirthSurvivorProfilesWindow'); cd=depth('rebirthSurvivorCreatorWindow')
ck(pd==500,'Survivor Profiles z-order restored to PC038 depth 500')
ck(cd==520,'Survivor Creator z-order restored to PC038 depth 520')
ck(pd is not None and cd is not None and cd>pd,'Creator remains above Profiles')
# PC056 Trader option must survive the surgical recovery.
ck('name="jobsToNextTierOption"' in menu,'PC056 Jobs to Next Tier row preserved')
ck('name="cbxJobsToNextTier"' in menu,'PC056 Jobs to Next Tier combobox preserved')
ck('name="btnJobsToNextTierRandomizerLock"' in menu,'PC056 Jobs to Next Tier randomizer lock preserved')
# XML parse.
ck(True,'XUi_Menu/windows.xml parses')
passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC070_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed}')
if failed:
    for ok,msg in checks:
        if not ok: print('FAILED_CHECK | '+msg)
raise SystemExit(1 if failed else 0)
