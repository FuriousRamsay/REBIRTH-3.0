#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET,re,sys
ROOT=Path(__file__).resolve().parents[2]
fail=[]; checks=0
def ck(name,cond):
 global checks;checks+=1
 if not cond: fail.append(name)
# XML
xmls=list(ROOT.rglob('*.xml'))
for p in xmls:
 try: ET.parse(p)
 except Exception as e: fail.append('XML '+str(p.relative_to(ROOT))+': '+str(e))
ck('all XML parse',not any(x.startswith('XML ') for x in fail))
adv=ET.parse(ROOT/'Config/_Survivor/advanced_disciplines.xml').getroot();prog=ET.parse(ROOT/'Config/_Survivor/progression.xml').getroot();lit=ET.parse(ROOT/'Config/_Survivor/literature.xml').getroot()
knowledge={e.get('id') for e in prog.find('knowledge').findall('knowledge')}
required=['knowledge.animal_handling.basic_dog_training','knowledge.animal_handling.working_dog_training','knowledge.animal_handling.animal_behavior','knowledge.beastmaster.predator_handling','knowledge.beastmaster.pack_bonding','knowledge.beastmaster.apex_predator_handling','knowledge.animal_handling.infected_behavior','knowledge.black_magic.mind_control_i','knowledge.black_magic.mind_control_ii','knowledge.black_magic.mind_control_iii','knowledge.black_magic.undead_conditioning','knowledge.black_magic.binding_ritual','knowledge.black_magic.feral_binding','knowledge.black_magic.radiated_binding','knowledge.black_magic.charged_binding','knowledge.black_magic.infernal_binding','knowledge.black_magic.lesser_summoning','knowledge.black_magic.greater_summoning','knowledge.black_magic.army_of_the_dead','knowledge.rage.basic','knowledge.rage.controlled','knowledge.rage.offensive','knowledge.rage.blood','knowledge.animal_handling.panther_handling','knowledge.black_magic.panther_binding']
for k in required: ck('knowledge '+k,k in knowledge)
paths=adv.find('explorer_paths');ck('explorer paths exists',paths is not None);ck('explorer path breadth',paths is not None and len(paths.findall('path'))>=20)
z=adv.find('./black_magic_target_classification/zombie_animals');ck('infected animals Beastmaster false',z is not None and z.get('beastmaster_tame')=='false');ck('infected animal handling knowledge',z is not None and z.get('binding_knowledge')=='knowledge.animal_handling.infected_behavior')
# Literature crossrefs and distribution
adv_lit=[e for e in lit.findall('item') if (e.get('knowledge') or '') in required and e.get('id','').startswith(('rebirthGuide','rebirthManual'))]
ck('advanced literature count',len(adv_lit)>=17)
items=(ROOT/'Config/_Survivor/items.xml').read_text();loot=(ROOT/'Config/_Survivor/loot.xml').read_text();traders=(ROOT/'Config/_Survivor/traders.xml').read_text();dist=(ROOT/'Config/_Survivor/literature_distribution.xml').read_text()
for e in adv_lit:
 iid=e.get('id');ck(iid+' item',iid in items);ck(iid+' loot',iid in loot);ck(iid+' trader',iid in traders);ck(iid+' dist',iid in dist)
# source invariants
sp=(ROOT/'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthSpecialPantherService.cs').read_text();rage=(ROOT/'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthRageService.cs').read_text();black=(ROOT/'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBlackMagicService.cs').read_text();graph=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionGraphRegistry.cs').read_text();overlay=(ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerOverlay.cs').read_text();comp=(ROOT/'Scripts/Companions/RebirthCompanionService.cs').read_text();net=(ROOT/'Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs').read_text();codec=(ROOT/'Scripts/Survivor/Network/RebirthSurvivorNetworkCodec.cs').read_text()
ck('infected binding server knowledge gate','KnowledgeInfectedAnimalBehavior' in sp and 'Beastmaster still cannot tame infected animals' in sp)
ck('rage study plus skill','v<80f' in rage and 'KnowledgeRageBlood' in rage and 'Advanced Rage techniques are learned from Chunk-J literature' in rage)
ck('black magic advanced study separation','ShouldAutoGrantMilestone' in black)
ck('graph explorer path loader','AddAdvancedDisciplineExplorerPaths();' in graph)
ck('live trial lock state','CompletedTrialIds' in overlay and 'Initiation trial not completed' in overlay)
ck('network protocol 7','public const int Version = 7;' in net)
ck('trial state serialized','CompletedTrialIds' in codec and 'AccomplishmentIds' in codec)
ck('panther companion commands','DeployHorrorPanther = 26' in comp and 'DeployRagePanther = 27' in comp)
ck('command atlas serialized','IconAtlas = b.ReadString()' in comp and 'c.IconAtlas ?? "UIAtlas"' in comp)
# icon refs
for e in adv_lit:
 iid=e.get('id');m=re.search(r'<item name="'+re.escape(iid)+r'">(.*?)</item>',items,re.S);ck(iid+' block',m is not None)
 if m:
  im=re.search(r'CustomIcon" value="([^"]+)',m.group(1));ck(iid+' icon ref',im is not None and (ROOT/'UIAtlases/ItemIconAtlas'/((im.group(1) if im else '')+'.png')).exists())
print(f'PC012 Chunk J static validation: {checks-len(fail)}/{checks} PASS; XML={len(xmls)}; failures={len(fail)}')
for x in fail: print('FAIL:',x)
sys.exit(1 if fail else 0)
