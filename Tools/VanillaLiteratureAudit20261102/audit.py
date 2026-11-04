from pathlib import Path
from lxml import etree as E
import argparse,copy,json,hashlib,re
ROOT=Path(__file__).resolve().parents[2]
DEFAULT=Path('E:/_Haven/shared/CodexPurgeRemote20261102/Saves/Navezgane/CodexPurgeRemote20261102/ConfigsDump')
BUNDLES=[f'questRewardT{i}BookBundle' for i in range(1,6)]
PATCH=ROOT/'Config/_Rebirth/literature_bundle_items.xml'
def apply_set(doc,op):
    for target in doc.xpath(op.get('xpath')):
        if op.tag=='set':target.getparent().set(target.attrname,op.text or '')
        elif op.tag=='setattribute':target.set(op.get('name'),op.text or '')
        else:raise AssertionError(op.tag)
def generate():
    pool=E.parse(str(ROOT/'Config/_Rebirth/progression_loot.xml')).xpath('//*[@name="rebirthLearningReward"]/item/@name')
    assert len(pool)==len(set(pool)) and len(pool)>=7
    doc=E.Element('configs')
    doc.append(E.Comment(' REBIRTH-only: native air-drop/quest book bundles bypass loot groups through Action0.Random_item. Reuse the canonical learning reward pool; retain native opening, unique selection and tier quantities 3..7, including previously owned bundles. Generated/checked by Tools/VanillaLiteratureAudit20261102/audit.py. '))
    xp='/items/item['+' or '.join("@name='"+x+"'" for x in BUNDLES)+"]/property[@class='Action0']/property[@name='Random_item']/@value"
    E.SubElement(doc,'set',xpath=xp).text=','.join(pool)
    # Embedded magazine outputs in other native bundles bypass loot groups too.
    progression=E.parse(str(ROOT/'Config/_Rebirth/progression_loot.xml'))
    mapping={}
    for op in progression.xpath('/configs/setattribute[@name="group"]'):
        names=re.findall(r"@name='([^']+SkillMagazine)'",op.get('xpath',''))
        if len(names)!=1:continue
        group=progression.xpath('//*[@name=$name]',name=(op.text or '').strip())[0]
        choices=group.xpath('item/@name')
        mapping[names[0]]=next((x for x in choices if x.startswith('rebirthFieldNotes')),choices[0])
    native=E.parse(str(ROOT.parents[1]/'Data/Config/items.xml'))
    for item in native.xpath('/items/item'):
        if item.get('name') in BUNDLES:continue
        for action in item.xpath('property[starts-with(@class,"Action")]'):
            for prop in action.xpath('property[@name="Create_item" or @name="Random_item"]'):
                old=prop.get('value','').split(',');new=[mapping.get(x.strip(),x.strip()) for x in old]
                if old==new:continue
                xp="/items/item[@name='"+item.get('name')+"']/property[@class='"+action.get('class')+"']/property[@name='"+prop.get('name')+"']/@value"
                E.SubElement(doc,'set',xpath=xp).text=','.join(new)
    PATCH.write_bytes(E.tostring(doc,pretty_print=True,encoding='utf-8',xml_declaration=True))
def audit(dump,native_final=False):
    docs={k:E.parse(str(dump/(k+'.xml'))) for k in ['items','loot','traders','quests']}
    native=E.parse(str(ROOT.parents[1]/'Data/Config/items.xml'))
    exclusions=E.parse(str(ROOT/'Config/_Survivor/vanilla_progression_exclusions.xml'))
    blocked={e.get('name') for e in native.xpath(exclusions.find('remove').get('xpath')) if e.get('name','').startswith('book') or 'SkillMagazine' in e.get('name','') or 'Schematic' in e.get('name','') or e.get('name','').startswith('schematic') or e.get('name') in ['skillBookMaster','adminPerkBook']}
    registry=E.parse(str(ROOT/'Config/_Survivor/literature.xml'))
    learning=set(registry.xpath('//item/@id'))
    pool=E.parse(str(ROOT/'Config/_Rebirth/progression_loot.xml')).xpath('//*[@name="rebirthLearningReward"]/item/@name')
    itemnodes={e.get('name'):e for e in docs['items'].getroot().findall('item')}
    assert set(pool)<=learning and set(pool)<=set(itemnodes)
    def actions(name,seen=None):
        seen=set() if seen is None else seen
        if name in seen or name not in itemnodes:return {}
        seen.add(name);node=itemnodes[name];parent=node.find("property[@name='Extends']")
        result=actions(parent.get('value'),seen) if parent is not None else {}
        for a in node.findall('property'):
            if a.get('class','').startswith('Action'):
                result.setdefault(a.get('class'),{}).update({p.get('name'):p.get('value','') for p in a.findall('property')})
        return result
    def generators(name):
        out=[]
        for a in actions(name).values():
            if a.get('Class')=='OpenBundle':
                for field in ['Random_item','Create_item']:
                    out.extend(('item',v.strip()) for v in a.get(field,'').split(',') if v.strip())
            if a.get('Class')=='OpenLootBundle' and a.get('LootList'):out.append(('loot',a['LootList']))
        return out
    def graph():
        nodes={('item',n):generators(n) for n in itemnodes}
        roots=[]
        for category,key,xp in [('loot','loot','/lootcontainers/lootgroup | /lootcontainers/lootcontainer'),('traders','trader','/traders/trader_item_groups/trader_item_group | /traders/trader_info')]:
            for index,g in enumerate(docs[category].xpath(xp)):
                name=g.get('name') or g.get('id') or str(index);node=(key,name);roots.append(node);edges=[]
                for e in g.findall('item'):
                    if e.get('name'):edges.append(('item',e.get('name')))
                    if e.get('group'):edges.append((key,e.get('group')))
                nodes[node]=edges
        for index,r in enumerate(docs['quests'].xpath('/quests/quest/reward')):
            if r.get('type') not in ['Item','LootItem']:continue
            node=('quest',str(index));roots.append(node)
            nodes[node]=[('item' if r.get('type')=='Item' else 'loot',v.strip()) for v in r.get('id','').split(',') if v.strip()]
        return nodes,roots
    def forbidden(nodes,roots):
        found=[]
        for root in roots:
            queue=[(root,[root[1]])];seen=set()
            while queue:
                n,path=queue.pop()
                if n in seen:continue
                seen.add(n)
                if n[0]=='item' and n[1] in blocked:found.append({'root':root,'item':n[1],'path':path});continue
                queue.extend((e,path+[e[1]]) for e in nodes.get(n,[]))
        return found
    nodes,roots=graph();before=forbidden(nodes,roots)
    unresolved=sorted({edge for edges in nodes.values() for edge in edges if edge[0] in ['loot','trader'] and edge not in nodes})
    assert not unresolved,unresolved[:10]
    # Delta simulation on retained native dumps: no claim that XML was game-loaded after edits.
    old_actions={n:copy.deepcopy(actions(n)) for n in itemnodes}
    patch=E.parse(str(PATCH));assert len(patch.findall('set'))>1
    if not native_final:
        for op in patch.findall('set'):apply_set(docs['items'],op)
    questpatch=E.parse(str(ROOT/'Config/_Rebirth/quests.xml')).xpath('/configs/setattribute[@xpath and contains(@xpath,"plantedGraceCorn1Schematic")]')
    assert len(questpatch)==1
    if not native_final:apply_set(docs['quests'],questpatch[0])
    nodes,roots=graph();after=forbidden(nodes,roots)
    if after:(ROOT/'_Documentation/PlayerReports_20261102/VanillaBookBundles/remaining-before-fix.json').write_text(json.dumps(after,indent=2))
    assert not after,after[:5]
    changed=[]
    for name in itemnodes:
        old=old_actions[name];new=actions(name)
        if old==new:continue
        assert old.keys()==new.keys()
        for action in old:
            assert old[action].keys()==new[action].keys()
            for field in old[action]:
                a,b=old[action][field],new[action][field]
                if a==b:continue
                assert field in ['Random_item','Create_item'],(name,field)
                if name not in BUNDLES:
                    av,bv=a.split(','),b.split(',');assert len(av)==len(bv)
                    for x,y in zip(av,bv):assert (x in blocked and y in learning) or x==y,(name,x,y)
        changed.append(name)
    bundle_report=[]
    for tier,name in enumerate(BUNDLES,1):
        old=old_actions[name]['Action0'];new=actions(name)['Action0']
        assert set(new['Random_item'].split(','))==set(pool)
        assert {k:v for k,v in old.items() if k!='Random_item'}=={k:v for k,v in new.items() if k!='Random_item'}
        assert int(new['Random_count'])==tier+2 and new['Unique_random_only']=='true'
        if not native_final:assert any(v in blocked for k,v in [('item',x) for x in old['Random_item'].split(',')])
        bundle_report.append({'item':name,'quantity':tier+2,'learningChoices':len(pool),'nativeOpeningAndCountsUnchanged':True})
    # Native BaseGame definitions are never patched: include is nested in the Rebirth-only item branch.
    config=E.parse(str(ROOT/'Config/items.xml'));includes=config.xpath('//include[@filename="_Rebirth/items.xml"]')
    assert len(includes)==1 and "character_progression('Rebirth')"==includes[0].getparent().get('cond')
    assert E.parse(str(ROOT/'Config/_Rebirth/items.xml')).xpath('/configs/include[@filename="literature_bundle_items.xml"]')
    # Newly substituted bundle contents must not reintroduce learning through junk.
    junk=[n for n in roots if n[0]=='loot' and (n[1].startswith(('groupJunk','junk')) or n[1].endswith('Junk') or n[1]=='dumpster')]
    for n in junk:
        q=[n];seen=set()
        while q:
            x=q.pop()
            if x in seen:continue
            seen.add(x);assert not(x[0]=='item' and x[1] in learning),(n,x)
            q.extend(nodes.get(x,[]))
    result={'scope':('Actual native configuration dump, no transformations applied; bundle opening is a separate acceptance' if native_final else 'Exact source delta on retained native REBIRTH dumps; not post-edit native opening or October10 binary reproduction'),'dumpHashes':{k:hashlib.sha256((dump/(k+'.xml')).read_bytes()).hexdigest() for k in docs},'blockedNativeLiteratureIds':len(blocked),'rootsAudited':len(roots),'beforeForbiddenPaths':len(before),'beforeExamples':before[:8],'beforeAirDropExamples':[x for x in before if any('airdrop' in v.lower() for v in x['path'])][:3],'afterForbiddenPaths':len(after),'changedBundleActions':len(changed),'changedItems':changed,'unresolvedGroupReferences':len(unresolved),'bundles':bundle_report,'junkRootsChecked':len(junk),'baseGameConditionalIsolation':True,'directCornChallengeReward':'rebirthTheoryFarmingPrimer','nativeVerification':('CONFIG_DUMP_PASS' if native_final else 'PENDING')}
    out=ROOT/'_Documentation/PlayerReports_20261102/VanillaBookBundles'/('native-final-result.json' if native_final else 'audit-result.json');out.write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ['beforeExamples','dumpHashes']},indent=2))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--dump',type=Path,default=DEFAULT);p.add_argument('--generate',action='store_true');p.add_argument('--native-final',action='store_true');args=p.parse_args()
    if args.generate:generate()
    audit(args.dump,args.native_final)


