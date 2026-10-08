#!/usr/bin/env python3
from __future__ import annotations
import argparse, re, sys
from pathlib import Path
import xml.etree.ElementTree as ET


def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); args=ap.parse_args(); root=args.root.resolve()
    checks=[]; failures=[]
    def check(name, ok, detail=''):
        checks.append((name,bool(ok),detail))
        if not ok: failures.append(f'{name}: {detail}')

    # Background list must use preloaded small thumbnails, one for every authored Background.
    bgroot=ET.parse(root/'Config/_Survivor/backgrounds.xml').getroot()
    keys=[b.get('background_art_key','').strip() for b in bgroot.findall('./background')]
    thumb=root/'UIAssets/Survivor/BackgroundThumbnails'
    files=sorted(p.stem for p in thumb.glob('*.jpg')) if thumb.exists() else []
    check('background.authored_count',len(keys)==28,f'count={len(keys)}')
    check('background.thumbnail_exact_coverage',set(files)==set(keys),f'thumbs={len(files)} keys={len(keys)} missing={sorted(set(keys)-set(files))} extra={sorted(set(files)-set(keys))}')
    binder=(root/'Scripts/Survivor/UI/RebirthSurvivorArtTextureBinder.cs').read_text(encoding='utf-8',errors='replace')
    creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8',errors='replace')
    check('background.preload_api','PreloadBackgroundThumbnails' in binder and 'BackgroundThumbnailCache' in binder,'cache/preload API')
    check('background.creator_preloads','RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();' in creator,'creator OnOpen preload call')
    check('background.rows_use_thumbnail','BindBackgroundThumbnail(bg)' in creator and 'backgroundRowArtBinders[i].BindBackground(bg)' not in creator,'list row binder')
    check('background.full_detail_preserved','public bool BindBackground(RebirthBackgroundDefinition background)' in binder,'full-detail binder retained')

    # Live diet keeps serialized id for compatibility but is displayed/implemented as Pescatarian.
    diets={d.get('id'):d for d in ET.parse(root/'Config/_Survivor/diets.xml').getroot().findall('./diet')}
    d=diets.get('diet.vegetarian')
    check('diet.legacy_id_present',d is not None,'diet.vegetarian')
    check('diet.pescatarian_live_rule',d is not None and d.get('composition_rule')=='pescatarian',f'rule={d.get("composition_rule") if d is not None else None}')
    check('diet.pescatarian_display',d is not None and d.get('name_key')=='xuiRebirthDietPescatarian',f'name_key={d.get("name_key") if d is not None else None}')
    check('diet.points_preserved',d is not None and d.get('points')=='2',f'points={d.get("points") if d is not None else None}')
    compat=(root/'Scripts/Survivor/Condition/RebirthDietCompatibility.cs').read_text(encoding='utf-8',errors='replace')
    check('diet.pescatarian_fish_allowed','rule == "pescatarian"' in compat and 'set.Contains("Meat") || set.Contains("AnimalFat")' in compat,'pescatarian blocks meat/fat, not Fish')

    # Food creator visibility and startup-safe authored icon aliases.
    fooddoc=ET.parse(root/'Config/_Survivor/food_content.xml').getroot()
    existing={}; nested={}
    for app in fooddoc.findall('./append'):
        xp=app.get('xpath','')
        m=re.fullmatch(r"/items/item\[@name='([^']+)'\]",xp)
        if m: existing[m.group(1)]={p.get('name'):p.get('value','') for p in app.findall('./property')}
        elif xp=='/items':
            for item in app.findall('./item'):
                nested[item.get('name','')]={p.get('name'):p.get('value','') for p in item.findall('./property')}
    check('food.rotting_flesh_hidden',existing.get('foodRottingFlesh',{}).get('RebirthDietCatalogueHidden')=='true',str(existing.get('foodRottingFlesh',{})))
    check('food.sham_sandwich_present','foodShamSandwich' in existing and existing['foodShamSandwich'].get('RebirthDietCatalogueHidden')!='true',str(existing.get('foodShamSandwich')))
    aliases={
      'foodCropGraceCorn':'foodCropSuperCorn','foodCropCurrant':'foodCropCurrants','foodCropGooseberry':'foodCropGooseberries',
      'foodCropMushroomsRadiated':'foodCropMushrooms','foodCropPumpkin':'plantedPumpkin3Harvest','foodCropRaspberry':'foodCropRaspberries',
      'foodCropStrawberry':'foodCropStrawberries','foodFrostbiteSmoothie':'scorcherStew','foodShamSandwich':'foodShamSandwich'}
    for item,icon in aliases.items(): check('food.icon_alias.'+item,existing.get(item,{}).get('RebirthDietIcon')==icon,f'actual={existing.get(item,{}).get("RebirthDietIcon")} expected={icon}')
    custom={'rebirthFoodBlueberryCrumble':'foodBlueberryPie','rebirthFoodMushroomPotPie':'foodShepardsPie','rebirthFoodMushroomSkillet':'foodVegetableStew','rebirthFoodPumpkinVegetableSoup':'foodVegetableStew'}
    for item,icon in custom.items(): check('food.custom_icon.'+item,nested.get(item,{}).get('CustomIcon')==icon,f'actual={nested.get(item,{}).get("CustomIcon")} expected={icon}')
    catalog=(root/'Scripts/Survivor/UI/RebirthDietFoodCatalogue.cs').read_text(encoding='utf-8',errors='replace')
    check('food.catalogue_authored_icon_parser','RebirthDietIcon' in catalog and 'CustomIcon' in catalog and 'HasAuthoredIcon' in catalog,'authored icon fields')
    check('food.catalogue_hidden_parser','RebirthDietCatalogueHidden' in catalog and 'CreatorVisible' in catalog,'creator visibility contract')

    # UI grouping + concise details; both creator definitions must contain the same new named controls.
    menu=(root/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8',errors='replace')
    ingame=(root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8',errors='replace')
    for name in ('dietAllowedFoodsHeading','dietRestrictedFoodsHeading','dietFoodLegend'):
        check('ui.menu.'+name,f'name="{name}"' in menu,name)
        check('ui.ingame.'+name,f'name="{name}"' in ingame,name)
    check('ui.dynamic_grouping','List<RebirthDietFoodEntry> allowed' in creator and 'List<RebirthDietFoodEntry> restricted' in creator and 'SetControllerPosition(dietFoodRows[slot]' in creator,'runtime section layout')
    uitext=(root/'Scripts/Survivor/UI/RebirthSurvivorUiText.cs').read_text(encoding='utf-8',errors='replace')
    check('ui.concise_diet_copy','Diet does not own stomach transport' not in uitext and 'OFF-DIET FOOD' in uitext,'technical paragraph removed')

    total=len(checks); passed=sum(1 for _,ok,_ in checks if ok)
    print(f'REBIRTH Diet/Background UI Revision 5: {"PASS" if not failures else "FAIL"}')
    print(f'passed={passed}/{total}')
    for f in failures: print('FAIL:',f)
    raise SystemExit(0 if not failures else 1)

if __name__=='__main__': main()
