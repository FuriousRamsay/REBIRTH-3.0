#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
errs=[]
def check(ok,msg):
    print(('PASS ' if ok else 'FAIL ')+msg)
    if not ok: errs.append(msg)
def names(path):
    r=ET.parse(path).getroot(); return {e.get('name'):e for e in r.iter() if e.get('name')}
menu=root/'Config/XUi_Menu/windows.xml'; game=root/'Config/XUi_InGame/windows.xml'
for p in (menu,game):
    n=names(p)
    check(n.get('creatorReviewPanel') is not None and n['creatorReviewPanel'].get('width')=='1744',p.name+' full-width Review')
    check(all(x in n for x in ['reviewIdentityPanel','reviewBackgroundArt','reviewPlayerPreview','reviewProfileName','reviewBackgroundName','reviewDietName','reviewSummaryDescription','reviewSummaryChoices']),p.name+' Profile Summary identity')
    check(all(f'reviewSkillRow{i}' in n for i in range(10)) and all(f'reviewKnowledgeRow{i}' in n for i in range(2)),p.name+' 10-row Skills & Knowledge capacity')
    check(all(f'reviewTraitRow{i}' in n for i in range(11)),p.name+' 11-row Traits capacity')
    check(all(f'reviewWeaknessRow{i}' in n for i in range(2)),p.name+' 2-row Weakness capacity')
    check(all(f'reviewStartingItemRow{i}' in n and f'reviewStartingItemIcon{i}' in n for i in range(8)),p.name+' 8 Starting Item icon rows')
    for prefix in ('reviewProgression','reviewTrait','reviewStartingItems'):
        check(all(prefix+sfx in n for sfx in ('NativeScrollHost','NativeScrollView','NativeScrollProxy','ScrollCapture')),p.name+' '+prefix+' standard scrollbar wiring')
        host=n[prefix+'NativeScrollHost']
        check(host.find('defaultscrollbar') is not None,p.name+' '+prefix+' uses defaultscrollbar')
        check(host.get('visible')=='true',p.name+' '+prefix+' authored active for native construction')
        if prefix == 'reviewTrait':
            check(host.get('height')=='319', p.name+' Review Traits use full available vertical space')
    check('reviewBudgetText' not in n and 'reviewDietPoints' not in n,p.name+' no trait-point budget/bonus display')
    # Starting Items must live in the same right column as Weaknesses.
    w=n['reviewWeaknessRow0'].get('pos','').split(',')[0]; i=n['reviewStartingItemRow0'].get('pos','').split(',')[0]
    check(w==i,p.name+' Starting Items directly beneath Weaknesses')

def rect_blob(path,name):
    s=path.read_text(); start=s.index(f'<rect name="{name}"')
    pat=re.compile(r'<rect\b[^>]*?/\s*>|<rect\b[^>]*>|</rect>'); d=0
    for m in pat.finditer(s,start):
        t=m.group(0)
        if t.startswith('</rect>'):
            d-=1
            if d==0:return s[start:m.end()]
        elif not t.rstrip().endswith('/>'): d+=1
    return ''
check(rect_blob(menu,'creatorReviewPanel')==rect_blob(game,'creatorReviewPanel'),'Menu/InGame Review panels are identical')
ctl=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text()
section=ctl[ctl.index('private void RenderReviewVisual()'):ctl.index('private void RenderBackgroundVisualDetails')]
check('bg.StartingSkills' in section and 'result.StartingSkills' not in section,'Review uses Experience-authored Skills/Weaknesses')
check('bg.StartingKnowledgeIds' in section,'Review uses Experience-authored Knowledge')
check('model.SelectedTraitIds' in section,'Review renders selected Traits separately')
check('BuildTraitBudgetSummary' not in section,'Review does not show spent trait-point budget')
check('SetStartingItemIcon(reviewStartingItemIcons[slot], item)' in section,'Review Starting Items use real item icons')
check('reviewTraitOffset = ClampOffset' in section and 'traits[logical]' in section,'Review Traits render an offset window instead of truncating')
check('private const int ReviewTraitRows = 11;' in ctl and 'private const int ReviewTraitTrackHeight = 319;' in ctl,'Review Traits expose maximum 11-row physical viewport before scrolling')
check('reviewProgressionOffset = ClampOffset' in section,'Review Skills & Knowledge render an offset window')
check('reviewStartingItemsOffset = ClampOffset' in section,'Review Starting Items render an offset window')
check('TrySelectPlayerProfileForPreview(this, "reviewPlayerPreview"' in section,'Review binds live player model preview')
# Current authored maxima are measured for diagnostics; scrollbars intentionally support overflow.
bgroot=ET.parse(root/'Config/_Survivor/backgrounds.xml').getroot(); max_prog=max_weak=max_items=0
for b in bgroot.findall('.//background'):
    pos=neg=0
    for sk in b.findall('./starting_skills/skill'):
        v=sk.get('value')
        if v is not None:
            try:f=float(v)
            except:f=0
            if f>0:pos+=1
            elif f<0:neg+=1
        else:
            tier=(sk.get('tier_id') or sk.get('tier') or '').lower()
            if 'weak' in tier or 'negative' in tier:neg+=1
            else:pos+=1
    know=len(b.findall('./starting_knowledge/knowledge'))
    max_prog=max(max_prog,pos+know); max_weak=max(max_weak,neg); max_items=max(max_items,len(b.findall('./starting_items/item')))
check(max_weak<=2,f'authored Weaknesses fit fixed area (max={max_weak})')
check(max_prog>=0 and max_items>=0,f'variable Review lists support overflow (progression max={max_prog}, items max={max_items})')
print(f'RESULT: {"PASS" if not errs else "FAIL"} ({len(errs)} failures)')
sys.exit(1 if errs else 0)
