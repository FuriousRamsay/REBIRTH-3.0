#!/usr/bin/env python3
"""Static acceptance vectors for Survivor Background Balance Revision 3 and Diet catalogue startup readiness."""
from __future__ import annotations
import argparse
from pathlib import Path
import xml.etree.ElementTree as ET


def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); args=ap.parse_args()
    root=args.root.resolve(); checks=[]; errors=[]
    def check(name, ok, detail=''):
        checks.append((name,bool(ok),detail))
        if not ok: errors.append(f'{name}: {detail}')

    progression=ET.parse(root/'Config/_Survivor/progression.xml').getroot()
    creation=progression.find('./creation')
    base=int(creation.get('base_points','0')) if creation is not None else 0
    cap=int(creation.get('max_negative_trait_refund','0')) if creation is not None else 0
    backgrounds=ET.parse(root/'Config/_Survivor/backgrounds.xml').getroot().findall('./background')
    check('background.count',len(backgrounds)==28,f'count={len(backgrounds)}')
    seen=set()
    examples={}
    for b in backgrounds:
        bid=b.get('id',''); seen.add(bid)
        vals=[int(float(s.get('value','0'))) for s in b.findall('./starting_skills/skill')]
        pos=sum(v for v in vals if v>0); neg=sum(v for v in vals if v<0)
        trait_points=base+int(b.get('creation_point_modifier','0'))
        if bid=='background.clean_slate':
            check('clean_slate.no_skills',not vals,f'values={vals}')
            check('clean_slate.trait_budget',trait_points==12,f'points={trait_points}')
        else:
            check(f'{bid}.positive_cap',0 < pos <= 75,f'positive={pos}')
            check(f'{bid}.weakness_band',-20 <= neg <= -10,f'negative={neg}')
            check(f'{bid}.trait_budget',5 <= trait_points <= 10,f'points={trait_points}')
        if bid in {'background.butcher','background.bartender','background.engineer','background.hunter'}:
            examples[bid]=(pos,neg,trait_points)

    expected={
        'background.butcher':(55,-15,7),
        'background.bartender':(40,-15,8),
        'background.engineer':(50,-20,6),
        'background.hunter':(75,-20,5),
    }
    check('background.user_examples',examples==expected,f'actual={examples}')

    installer=(root/'Scripts/Survivor/RebirthSurvivorInstaller.cs').read_text(encoding='utf-8',errors='replace')
    catalogue=(root/'Scripts/Survivor/UI/RebirthDietFoodCatalogue.cs').read_text(encoding='utf-8',errors='replace')
    creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8',errors='replace')
    localization=(root/'Config/Localization.csv').read_text(encoding='utf-8',errors='replace')
    i_preload=installer.find('RebirthDietFoodCatalogue.PreloadAuthored')
    i_registry=installer.find('RebirthSurvivorDefinitionRegistry.Install')
    check('diet.preload_before_registry',i_preload>=0 and i_registry>=0 and i_preload < i_registry,f'preload={i_preload} registry={i_registry}')
    check('diet.authored_source','food_content.xml' in catalogue and 'PreloadAuthored' in catalogue,'required authored food data is preloaded synchronously')
    food_root=ET.parse(root/'Config/_Survivor/food_content.xml').getroot()
    authored_ids=[]
    for app in food_root.findall('./append'):
        xp=app.get('xpath','')
        if "/items/item[@name='" in xp:
            authored_ids.append(xp.split("/items/item[@name='",1)[1].split("'",1)[0])
        elif xp=='/items':
            authored_ids.extend(x.get('name','') for x in app.findall('./item') if x.get('name',''))
    check('diet.authored_food_count',len(set(authored_ids))==98,f'count={len(set(authored_ids))}')
    check('diet.rebirth_owned_foods_preloaded','string.Equals(xpath, "/items"' in catalogue and 'AddAuthoredEntry(parsed, nestedItemId, item)' in catalogue,'nested REBIRTH-owned item definitions are included before ItemClass exists')
    check('diet.native_registry_optional','ItemClass.list' in catalogue and 'authoredReady' in catalogue,'native items only enrich the preloaded authored catalogue')
    check('diet.no_reopen_instruction','reopen the diet step' not in (creator+'\n'+localization).lower(),'no user retry/reopen instruction remains')

    authoring=(root/'Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs').read_text(encoding='utf-8',errors='replace')
    check('balance.runtime_authoring_guard','positiveSkillTotal' in authoring and 'negativeSkillTotal' in authoring and 'background.clean_slate' in authoring,'revision-3 background bounds enforced')

    creation=(root/'Scripts/Survivor/Creation/RebirthSurvivorCreationValidator.cs').read_text(encoding='utf-8',errors='replace')
    check('budget.includes_diet','diet.Points' in creation,'final validator includes Diet points')
    check('budget.negative_refund_policy','RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund' in creation and cap == -1,f'negative refund policy cap={cap}')

    print('REBIRTH Survivor Background Balance Revision 3 vectors: '+('PASS' if not errors else 'FAIL'))
    print(f'passed={sum(1 for _,ok,_ in checks if ok)}/{len(checks)}')
    for e in errors: print('ERROR:',e)
    raise SystemExit(0 if not errors else 1)

if __name__=='__main__': main()
