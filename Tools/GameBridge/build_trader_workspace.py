"""Two-column trading workspace using the approved Crafting shell and backpack presenter."""
from pathlib import Path
from copy import deepcopy
from lxml import etree as E
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT.parent.parent/'Data/Config/XUi_InGame'
OUT=ROOT/'Config/XUi_InGame'
native=E.parse(str(BASE/'windows.xml'));current=E.parse(str(OUT/'windows.xml'))
dark='17,17,21,240';purple='196,158,255,255';red='228,18,21,220'
def rect(p,**a):return E.SubElement(p,'rect',a)
def sprite(p,**a):return E.SubElement(p,'sprite',dict(sprite='menu_empty',type='sliced',**a))
def frame(p,width,height):
    sprite(p,name='panelBackground',depth='0',width=str(width),height=str(height),color=dark)
    sprite(p,name='panelFrame',depth='1',width=str(width),height=str(height),color='64,64,72,255',fillcenter='false');p[-1].set('sprite','menu_empty2px')
def flatten(p):
    for s in p.iter('sprite'):
        if s.get('name') in ['background','backgroundMain','fillerBackground'] or s.get('color') in ['[darkGrey]','[mediumGrey]','[lightGrey]','[darkestGrey]']:s.set('color','0,0,0,0')
    for h in list(p.iter('headerbg')):h.getparent().remove(h)
def rule(p,width):sprite(p,name='headerRule',depth='9',pos='0,-40',width=str(width),height='2',color=red)
doc=E.Element('configs');a=E.SubElement(doc,'append',xpath='/windows')
root=E.SubElement(a,'window',name='rebirthTraderRoot',anchor='Center',pos='-936,505',width='1872',height='935',cursor_area='true',controller='RebirthTraderSurface, RebirthUtils')
sprite(root,name='traderOuterFrame',depth='30',width='1872',height='935',color=red,fillcenter='false');root[-1].set('sprite','menu_empty2px')
root.append(deepcopy(current.xpath("//window[@name='rebirthPersonalCraftingRoot']/rect[@name='rebirthCraftingTopZone']")[0]))
body=rect(root,name='traderBody',pos='8,-70',width='1856',height='813')
w=deepcopy(native.find("window[@name='windowTrader']"));w.tag='rect';w.attrib.pop('panel',None);w.set('pos','0,0');w.set('width','760');w.set('height','813');body.append(w);flatten(w);frame(w,760,813)
h=w.find("rect[@name='header']");rule(h,760)
h.find('player_name').set('color',purple);h.find('player_name').set('width','410')
h.find("label[@text='{restocklabel}: {timeleft}']").set('pos','450,-8')
h.find("label[@name='availableMoney']").set('pos','710,-6');h.find("sprite[@name='coinIcon']").set('pos','748,-5')
c=w.find("rect[@name='content']");c.set('height','770')
for name in ['categorySelector','searchControls']:
    r=c.find(f"rect[@name='{name}']");r.set('width','754')
    for s in r.findall('sprite'):s.set('width','754')
search=c.find("rect[@name='searchControls']");search.find("rect[@pos='334,0']").set('pos','220,0');search.find("rect[@pos='220,0']/textfield").set('width','365');search.find("rect[@pos='516,0']").set('pos','634,0')
table=c.find("rect[@name='tableHeader']");table.set('width','754')
cols=table.findall('rect');cols[0].set('width','530');cols[1].set('pos','530,0');cols[2].set('pos','650,0')
for l in table.iter('label'):l.set('font_size','22');l.set('color',purple)
g=c.find("grid[@name='items']");g.set('rows','11');g.set('width','754');g.set('height','616');g.set('cell_height','56');g.set('pos','3,-139');g.find('trader_item').tag='rebirth_trader_item'
right=rect(body,name='traderRight',pos='770,0',width='1086',height='813');frame(right,1086,813)
info=deepcopy(native.find("window[@name='itemInfoPanel']"));info.tag='rect';info.set('name','traderItemInfo');info.attrib.pop('panel',None);info.set('width','1086');info.set('height','443');right.append(info);flatten(info)
h=info.find("rect[@name='header']");rule(h,1086);h.find("label[@name='windowName']").set('width','770');h.find("label[@name='windowName']").set('color',purple)
for l in h.findall('label'):
    if l.get('pivot')=='topright':l.set('pos','1045,-8')
h.find("sprite[@name='costIcon']").set('pos','1050,-10')
ci=info.find("rect[@name='contentInfo']");ci.set('width','1086');ci.set('height','397');ci.find("rect[@name='preview']").set('pos','16,-20')
for d in ci.findall("rect[@name='description']"):
    d.set('pos','180,-43' if d.get('visible')=='{showstatanddescription}' else '180,0');d.set('width','890')
    for l in d.iter('label'):l.set('width','870')
    for r in d.iter('rect'):
        if r.get('width')=='453':r.set('width','890')
sc=ci.find("rect[@name='searchControls']");sc.set('pos','180,0');sc.set('width','890');ci.find("grid[@name='parts']").set('pos','180,-271')
for r in ci.findall('rect'):
    if r.get('visible')=='{iscomparing}':r.set('pos','180,-271');r.set('width','890')
    if r.get('visible') in ['{showtraderoptions}','{shownormaloptions}']:
        r.set('pos','16,-350');r.set('width','1054');actions=r.find('grid');actions.set('pos','220,0');actions.set('rows','1');actions.set('cols','4');actions.set('width','830');actions.set('cell_width','205');actions.set('cell_height','40');actions[0].set('width','200')
empty=rect(right,name='traderEmpty',width='1086',height='443',controller='InfoWindow')
E.SubElement(empty,'label',name='emptyTitle',pos='14,-8',width='1000',height='30',font_size='26',color=purple,text_key='rebirthTraderSelectedTitle');rule(empty,1086)
E.SubElement(empty,'label',pos='28,-100',width='1000',height='100',font_size='28',text_key='rebirthTraderSelectHelp',justify='center')
service=deepcopy(native.find("window[@name='serviceInfoPanel']"));service.tag='rect';service.attrib.pop('panel',None);right.append(service);flatten(service)
bag=deepcopy(current.xpath("//rect[@name='rebirthCraftingInventoryRegion']")[0]);right.append(bag);bag.set('pos','0,-453');bag.set('width','1086');bag.set('height','360')
bag.find("sprite[@name='rebirthCraftingInventoryHeaderRule']").set('width','1086');bag.find("sprite[@name='rebirthCraftingInventoryRegionBg']").set('width','1086');bag.find("sprite[@name='rebirthCraftingInventoryRegionBg']").set('height','360')
for b in bag.findall('button'):
    x,y=map(int,b.get('pos').split(','));b.set('pos',f'{x+356},{y}')
bag.find("sprite[@name='rebirthCraftingInventoryLockActive']").set('pos','963,-36')
scroll=bag.find("rect[@name='rebirthCraftingInventoryScroll']");scroll.set('width','1062');scroll.set('height','304');scroll.find('.//item_stack').set('controller','RebirthTraderInventorySlot, RebirthUtils')
E.SubElement(bag,'label',name='traderWallet',pos='490,-9',width='260',height='28',font_size='22',color='201,198,105,255',justify='right')
# The item presentation follows the crafting reference, with native transaction controllers only.
info.set('controller','RebirthTraderDetails, RebirthUtils')
preview=deepcopy(ci.find("rect[@name='preview']"))
options=[deepcopy(r) for r in ci.findall('rect') if r.get('visible') in ['{showtraderoptions}','{shownormaloptions}']]
parts=deepcopy(ci.find("grid[@name='parts']"))
for child in list(info):info.remove(child)
E.SubElement(info,'label',name='selectedHeading',pos='14,-8',width='600',height='30',font_size='26',color=purple,text_key='rebirthTraderSelectedTitle')
rule(info,1086)
preview.set('pos','16,-60');info.append(preview)
E.SubElement(info,'label',name='selectedName',pos='180,-58',width='410',height='64',font_size='29',color='245,245,247,255',text='{itemname}')
E.SubElement(info,'label',name='selectedDescription',pos='180,-126',width='410',height='170',font_size='23',color='214,214,220,255',text='{itemdescription}',parse_actions='true',overflow='clampcontent')
for i in range(1,8):
    row=rect(info,name=f'traderStat{i}',pos=f'620,{-62-(i-1)*33}',width='446',height='32')
    E.SubElement(row,'label',name=f'traderStatTitle{i}',width='300',height='30',font_size='24',text='{itemstattitle'+str(i)+'}',color='230,230,234,255')
    E.SubElement(row,'label',pos='305,0',width='141',height='30',font_size='24',text='{itemstat'+str(i)+'}',color='201,198,105,255',justify='right')
modArea=rect(info,name='traderModificationArea',pos='180,-300',width='320',height='48',visible='{rebirthtraderhasmods}')
parts.set('pos','0,0');parts.set('cell_width','48');parts.set('cell_height','48');parts[0].set('width','46');parts[0].set('height','46');modArea.append(parts)
for name in ['statButton','descriptionButton']:E.SubElement(info,'button',name=name,visible='false',width='1',height='1')
sprite(info,name='transactionDivider',pos='16,-347',width='1054',height='2',color='80,80,90,255')
E.SubElement(info,'label',name='quantityHeading',pos='22,-360',width='185',height='24',font_size='20',text_key='rebirthTraderQuantity',color=purple)
E.SubElement(info,'label',name='transactionTotalHeading',pos='244,-360',width='310',height='24',font_size='20',text='{rebirthtradertotallabel}',color=purple)
E.SubElement(info,'sprite',name='transactionCoin',pos='244,-395',width='28',height='28',sprite='ui_game_symbol_coin',color='201,198,105,255')
E.SubElement(info,'label',name='transactionPrice',pos='282,-392',width='315',height='36',font_size='32',text='{itemcost}{markup}',color='232,224,147,255')
for r in options:
    r.set('pos','16,-389');actions=r.find('grid');actions.set('pos','670,0');actions.set('cols','1');actions.set('rows','3');actions.set('width','384');actions.set('cell_width','384');actions.set('cell_height','42')
    actions[0].tag='rebirth_trader_action';actions[0].attrib.clear();info.append(r)
    counter=r.find("rect[@name='counterControl']")
    if counter is not None:
        counter.set('controller','RebirthTraderQuantity, RebirthUtils');counter.set('pos','2,0');counter.set('width','206')
        minimum=deepcopy(counter.find("button[@name='countMax']"));minimum.set('name','countMin');minimum.set('pos','14,-20');minimum.set('flip','Horizontally');minimum.set('tooltip_key','rebirthTraderMinimum');counter.insert(0,minimum)
        counter.find("button[@name='countDown']").set('pos','44,-20')
        counter.find("textfield").set('pos','65,-6');counter.find('textfield').set('width','65');counter.find('textfield').set('character_limit','6')
        counter.find("button[@name='countUp']").set('pos','150,-20')
        counter.find("button[@name='countMax']").set('pos','180,-20');counter.find("button[@name='countMax']").set('tooltip_key','rebirthTraderMaximum')
# Crafting-only gamepad targets must not survive copying the shared visual components.
ids={n.get('name') for n in root.iter() if n.get('name')}
for n in root.iter():
    for key,value in list(n.attrib.items()):
        if key.startswith('nav_') and value not in ids:n.attrib.pop(key)
templates=E.Element('configs');ta=E.SubElement(templates,'append',xpath='/templates');t=deepcopy(E.parse(str(BASE/'templates.xml')).find('trader_item'));t.tag='rebirth_trader_item';ta.append(t)
r=t.find('rect');r.set('width','754');r.set('height','54');r.find("sprite[@name='backgroundMain']").set('width','754');r.find("sprite[@name='backgroundMain']").set('height','54');r.find("sprite[@name='backgroundMain']").set('color','30,30,36,255');r.find("sprite[@name='background']").set('height','53')
r.set('controller','RebirthTraderStockEntry, RebirthUtils')
action=E.SubElement(ta,'rebirth_trader_action')
ar=deepcopy(current.xpath("//rect[@name='characterBagAction0']")[0]);action.append(ar);ar.attrib.pop('pos',None);ar.attrib.pop('name',None);ar.set('width','368');ar.set('height','42');ar.set('controller','RebirthTraderActionEntry, RebirthUtils')
for child in ar:
    if child.tag=='sprite' and child.get('name') in ['background','rebirthActionOpaqueFill']:child.set('width','368');child.set('height','42')
    if child.get('name')=='name':child.set('width','296');child.set('font_size','22')
    if child.get('name')=='keyboardButton':child.set('pos','328,-10');child.set('font_size','21')
    if child.get('name')=='gamepadIcon':child.set('pos','354,-10')
r.find("sprite[@name='Icon']").set('size','48,48');r.find("sprite[@name='Icon']").set('pos','31,-27');r.find("label[@name='Name']").set('width','457');r.find("label[@name='Name']").set('pos','62,-10');r.find("label[@name='Name']").set('font_size','25')
for n in r:
    if n.get('name') in ['durabilityBackground','durabilityBar']:n.set('pos','566,-44')
    if n.get('name')=='durabilityValue':n.set('pos','530,-10')
    if n.get('name')=='price':n.set('pos','650,-10')
for name,xml in [('trader_workspace.xml',doc),('trader_templates.xml',templates)]:
    E.indent(xml,space='  ');(OUT/name).write_bytes(E.tostring(xml,encoding='UTF-8',xml_declaration=True,pretty_print=True))
