from lxml import etree as E
from copy import deepcopy
r=E.parse('Config/XUi_InGame/windows.xml');t=E.parse('Config/XUi_InGame/templates.xml');x=E.parse('Config/XUi_InGame/xui.xml')
latest=r.getroot().xpath('./conditional')[-1]
assert latest.find('if').get('cond')=="character_progression('Rebirth')"
assert t.getroot().xpath('./conditional')[-1].find('if').get('cond')=="character_progression('Rebirth')"
assert x.getroot().xpath('./conditional')[-1].find('if').get('cond')=="character_progression('Rebirth')"
# Materialize prior native/mod window operations, then require the new writes to resolve.
work=E.parse('../../Data/Config/XUi_InGame/windows.xml')
for op in r.getroot().iter():
 if not isinstance(op.tag,str) or op.tag not in ['append','setattribute','remove']:continue
 xp=op.get('xpath')
 if not xp:continue
 targets=work.xpath(xp)
 if latest in op.iterancestors() and op.tag!='remove':assert targets,('Unmatched new patch',xp)
 for target in targets:
  if op.tag=='append':
   for e in op:target.append(deepcopy(e))
  elif op.tag=='setattribute':target.set(op.get('name'),op.text or '')
  elif op.tag=='remove':target.getparent().remove(target)
creative=work.xpath('/windows/window[@name="windowCreative2"]')[0]
grid=creative.xpath('.//grid[@name="queue"]')[0]
assert grid.get('cols')=='14' and grid.get('rows')=='9' and grid.get('controller')=='Creative2StackGrid'
assert 101+14*75==1151 and 1151<1154 and 1154+20<1180
for name in ['simplepickup','devblocks','favorites','hideshapes','searchInput','pager','categories']:
 assert creative.xpath('.//*[@name="'+name+'"]'),name
assert creative.xpath('.//*[@name="devblocks" and @visible="{allow_dev}"]')
responses=work.xpath('/windows/window[@name="windowResponses"]')[0]
assert responses.get('controller')=='RebirthDialogResponseList, RebirthUtils'
assert len(responses.xpath('./rect[@name="items"]/rebirth_conversation_entry'))==24
assert not responses.xpath('.//rebirth_response_entry')
original=t.xpath('//rebirth_response_entry')[0];new=t.xpath('//rebirth_conversation_entry')[0]
for name in ['jobCard','cancelCard']:
 a=original.xpath('.//rect[@name="'+name+'"]');b=new.xpath('.//rect[@name="'+name+'"]')
 assert [E.tostring(z) for z in a]==[E.tostring(z) for z in b]
npc=work.xpath('/windows/window[@name="rebirthNpcDialogueWindow"]')[0]
for name in ['speaker','disposition','response','feedback','btnClose']+['topic'+str(i) for i in range(1,7)]+['topicLabel'+str(i) for i in range(1,7)]:assert len(npc.xpath('.//*[@name="'+name+'"]'))==1
assert 308+796==1104<1120 and 70+532==602<620
print('Rebirth-only scope, patch targets, 126-slot catalogue, category controls, 24 responses, NPC actions and untouched trader cards passed.')
