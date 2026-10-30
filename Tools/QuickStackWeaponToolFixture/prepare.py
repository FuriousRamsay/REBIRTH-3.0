from pathlib import Path
from lxml import etree as E
import json,hashlib,difflib
R=Path(__file__).resolve().parents[2];D=R/'_Documentation/QuickStackModifierIdentity_20261029';T=Path(__file__).parent;O=T/'out';O.mkdir(exist_ok=True)
G=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die');P=G/'Mods/zzz_REBIRTH__3_0';source=P/'Scripts/QuickStack/QuickStackCore.cs'
baseline=json.loads((D/'baseline-hash.json').read_text(encoding='utf-8-sig'))['Hash'];assert hashlib.sha256(source.read_bytes()).hexdigest().upper()==baseline
old=(D/'QuickStackCore.cs.baseline').read_text(encoding='utf-8-sig');new=(R/'Scripts/QuickStack/QuickStackCore.cs').read_text(encoding='utf-8-sig')
def member(s,key):
 start=s.index(key);a=s.index('{',start);i=a+1;depth=1
 while depth:
  if s[i]=='{':depth+=1
  if s[i]=='}':depth-=1
  i+=1
 return s[start:i]
key='private static QuickStackCategory Classify('
assert old.replace(member(old,key),'CLASSIFY')==new.replace(member(new,key),'CLASSIFY'),'Unrelated delta'
# Authored source survey: native inherited definitions + all reachable REBIRTH
# item declarations (both conditional branches recorded, not claimed as live patch replay).
native=G/'Data/Config/items.xml';nodes={i.get('name'):i for i in E.parse(str(native)).xpath('/items/item')};native_names=set(nodes);files=[native];conditions=[];mods={}
def scan(path):
 if path in files:return
 files.append(path);tree=E.parse(str(path))
 for inc in tree.xpath('//include'):scan(path.parent/inc.get('filename'))
 for item in tree.xpath('//append/item|//prepend/item|//insertAfter/item|//insertBefore/item'):
  mods[item.get('name')]=item
 for c in tree.xpath('//conditional'):conditions.append(dict(path=str(path),conditions=c.xpath('.//if/@cond')))
scan(P/'Config/items.xml');nodes.update(mods)
def props(name,seen=()):
 if name in seen or name not in nodes:return {}
 i=nodes[name];own={p.get('name'):p.get('value','') for p in i.xpath('./property[@name]')};base=props(own.get('Extends',''),seen+(name,));base.update(own)
 actions=base.get('_action','')
 a=i.xpath('./property[@class="Action0"]/property[@name="Class"]/@value')
 if a:actions=a[-1]
 base['_action']=actions;return base
records=[];expected={}
for name in sorted(nodes):
 p=props(name);groups=p.get('Group','').split(',');tags=p.get('Tags','')
 record=dict(name=name,groups=groups,tags=tags,action=p.get('_action',''),className=p.get('Class',''),resource=p.get('IsResource','').lower()=='true',block=False,skills=p.get('ActionSkillGroup','')+'/'+p.get('CraftingSkillGroup',''))
 records.append(record)
 # Independent authored reading-family oracle: native inheritance of schematicMaster,
 # or explicit resolved Books group (including mod literature).
 chain=[];cur=name
 while cur in nodes and cur not in chain:
  chain.append(cur);parents=nodes[cur].xpath('./property[@name="Extends"]/@value');cur=parents[-1] if parents else ''
 if 'schematicMaster' in chain or 'Books' in groups:expected[name]='BooksAndSchematics'
 elif name.startswith('meleeTool') and not name.endswith('Parts'):expected[name]='Tools'
 elif name.startswith('gun') and record['action']=='Ranged':expected[name]='RangedWeapons'
# Explicit demonstrated/control identities; these override no broad unrelated routing rules.
expected.update({'gunMGT1AK47':'RangedWeapons','meleeToolPickT1IronPickaxe':'Tools','meleeToolPickT2SteelPickaxe':'Tools'})
expected['questRewardT1SkillMagazineBundle']='BooksAndSchematics'
# Native modifiers are separate authored definitions, not ordinary items.xml nodes.
modifier=G/'Data/Config/item_modifiers.xml';files.append(modifier)
modifier_nodes={i.get('name'):i for i in E.parse(str(modifier)).xpath('/item_modifiers/item_modifier')}
def modprops(n,seen=()):
 if n in seen or n not in modifier_nodes:return {}
 own={p.get('name'):p.get('value','') for p in modifier_nodes[n].xpath('./property[@name]')};p=modprops(own.get('Extends',''),seen+(n,));p.update(own);return p
for n in sorted(modifier_nodes):
 p=modprops(n)
 records.append(dict(name=n,groups=p.get('Group','').split(','),tags=p.get('Tags',''),action='',className='ItemClassModifier',resource=False,block=False,skills=''));expected[n]='ItemModifiers'
(O/'records.json').write_text(json.dumps(records))
(O/'expected.json').write_text(json.dumps(expected))
(D/'authored-evidence.json').write_text(json.dumps(dict(boundary='Native inheritance + reachable mod authored declarations; includes BOTH conditional branches; NOT native XML patch execution or other-mod load-order simulation',count=len(records),knowledge=len([x for x in expected.values() if x=='BooksAndSchematics']),conditions=conditions,inputs=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in files],required={n:next(r for r in records if r['name']==n) for n in ['gunMGT1AK47','meleeToolPickT1IronPickaxe','meleeToolPickT2SteelPickaxe','modGunDrumMagazineExtender']}),indent=2)+'\n')
en=old[old.index('public enum QuickStackCategory'):old.index('public static class QuickStackRuntimePolicy')]
classes=[]
original=(D/'OriginalQuickStackCore.baseline').read_text(encoding='utf-8-sig')
for label,s in [('Original',original),('Baseline',old),('Proposed',new)]:
 body=member(s,key).replace('private static QuickStackCategory Classify','public static QuickStackCategory Classify')+member(s,'private static bool ContainsAny(')+member(s,'private static bool HasExactGroup(')
 classes.append('public static class '+label+'{'+body+'}')
(O/'Actual.cs').write_text('using System;using System.Collections.Generic;using System.Text.Json;'+en+'\n'.join(classes)+(T/'Fixture.cs.txt').read_text())
(D/'runtime.diff').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='production/QuickStackCore',tofile='proposal/QuickStackCore')))
print(f'PASS production baseline guard; classifier-only delta; {len(records)} authored records, {len(expected)} independent expected family assertions')

