#!/usr/bin/env python3
from __future__ import annotations
import argparse, sys
from pathlib import Path
import xml.etree.ElementTree as ET

PRIMARY={"plant","meat","fish","egg","dairy","honey","animalfat","animalproduct"}
ANIMAL={"meat","fish","egg","dairy","honey","animalfat","animalproduct"}

def eval_rule(rule,tags,mods):
    r=(rule or '').strip().lower(); s={str(x).strip().lower() for x in tags if str(x).strip()}
    if r=='unrestricted': return 'Compatible',0.0
    if r not in {'vegetarian','pescatarian','vegan','carnivore'}: return 'UnknownRule',0.0
    if not (s&PRIMARY): return 'UnknownComposition',0.0
    if r=='pescatarian': return ('OffDiet',mods['vegetarian']) if s&{'meat','animalfat'} else ('Compatible',0.0)
    if r=='vegetarian': return ('OffDiet',mods['vegetarian']) if s&{'meat','fish','animalfat'} else ('Compatible',0.0)
    if r=='vegan': return ('OffDiet',mods['vegan']) if s&ANIMAL else ('Compatible',0.0)
    animal=bool(s&ANIMAL)
    return ('OffDiet',mods['carnivore']) if ('plant' in s or not animal) else ('Compatible',0.0)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); args=ap.parse_args(); root=args.root.resolve()
    cond=ET.parse(root/'Config/_Survivor/condition_runtime.xml').getroot().find('./diet')
    mods={'vegetarian':float(cond.get('vegetarian_violation')),'vegan':float(cond.get('vegan_violation')),'carnivore':float(cond.get('carnivore_violation'))}
    V=[
      ('unrestricted-empty','unrestricted',[],('Compatible',0.0)),('unrestricted-plant','unrestricted',['Plant'],('Compatible',0.0)),
      ('pescatarian-plant','pescatarian',['Plant'],('Compatible',0.0)),('pescatarian-fish','pescatarian',['Fish'],('Compatible',0.0)),
      ('pescatarian-egg','pescatarian',['Egg'],('Compatible',0.0)),('pescatarian-honey','pescatarian',['Honey'],('Compatible',0.0)),
      ('pescatarian-dairy','pescatarian',['Dairy'],('Compatible',0.0)),('pescatarian-meat','pescatarian',['Meat'],('OffDiet',mods['vegetarian'])),
      ('pescatarian-fat','pescatarian',['AnimalFat'],('OffDiet',mods['vegetarian'])),
      ('vegetarian-plant','vegetarian',['Plant'],('Compatible',0.0)),('vegetarian-egg','vegetarian',['Egg'],('Compatible',0.0)),
      ('vegetarian-honey','vegetarian',['Honey'],('Compatible',0.0)),('vegetarian-dairy','vegetarian',['Dairy'],('Compatible',0.0)),
      ('vegetarian-meat','vegetarian',['Meat'],('OffDiet',mods['vegetarian'])),('vegetarian-fish','vegetarian',['Fish'],('OffDiet',mods['vegetarian'])),
      ('vegetarian-fat','vegetarian',['AnimalFat'],('OffDiet',mods['vegetarian'])),('vegan-plant','vegan',['Plant'],('Compatible',0.0)),
      ('vegan-sweet-plant','vegan',['Plant','Sweet'],('Compatible',0.0)),('vegan-egg','vegan',['Egg'],('OffDiet',mods['vegan'])),
      ('vegan-honey','vegan',['Honey'],('OffDiet',mods['vegan'])),('carnivore-meat','carnivore',['Meat'],('Compatible',0.0)),
      ('carnivore-fish','carnivore',['Fish'],('Compatible',0.0)),('carnivore-egg','carnivore',['Egg'],('Compatible',0.0)),
      ('carnivore-honey','carnivore',['Honey'],('Compatible',0.0)),('carnivore-mixed','carnivore',['Meat','Plant'],('OffDiet',mods['carnivore'])),
      ('carnivore-plant','carnivore',['Plant'],('OffDiet',mods['carnivore'])),('vegetarian-empty','vegetarian',[],('UnknownComposition',0.0)),
      ('vegan-sweet-only','vegan',['Sweet'],('UnknownComposition',0.0)),('unknown-rule','flexitarian',['Fish'],('UnknownRule',0.0)),
      ('normalization',' CARNIVORE ',[' meat ','Sweet'],('Compatible',0.0)),('unknown-extra','carnivore',['Meat','FuturePresentationTag'],('Compatible',0.0)),
    ]
    failures=[]; passed=0
    for name,rule,tags,expected in V:
        got=eval_rule(rule,tags,mods)
        if got==expected: passed+=1
        else: failures.append(f'{name}: expected={expected} got={got}')
    diets=ET.parse(root/'Config/_Survivor/diets.xml').getroot().findall('./diet')
    dp={d.get('id'):int(d.get('points','0')) for d in diets}; expected_points={'diet.unrestricted':0,'diet.vegetarian':2,'diet.carnivore':3,'diet.vegan':4}
    point_pass=0
    for k,v in expected_points.items():
        if dp.get(k)==v: point_pass+=1
        else: failures.append(f'{k}: expected points={v} got={dp.get(k)}')
    total=len(V)+len(expected_points); ok=(passed+point_pass)==total
    print(f'REBIRTH Survivor Chunk 4 Diet vectors: {"PASS" if ok else "FAIL"}')
    print(f'passed={passed+point_pass}/{total} compatibility={passed}/{len(V)} definition_points={point_pass}/{len(expected_points)}')
    for f in failures: print('FAIL:',f)
    raise SystemExit(0 if ok else 1)
if __name__=='__main__': main()
