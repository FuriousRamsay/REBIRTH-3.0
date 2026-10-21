"""Author turret cards beside the storage-maintenance selected-item/backpack stack."""
from pathlib import Path
import argparse
import copy
import xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,help='Stage window XML without replacing installed configuration')
parser.add_argument('--xui-output',type=Path,help='Stage window-group XML without replacing installed configuration')
args=parser.parse_args()
if bool(args.output)!=bool(args.xui_output):parser.error('both staging outputs are required together')
path=ROOT/'Config/_Vehicles/camper_turret_windows.xml'
tree=E.parse(path); root=tree.getroot(); window=root.find('./append/window'); body=window.find("./rect[@name='turretBody']")
body.set('height','813')
header=window.find("./rect[@name='turretHeader']")
if header is None:header=E.SubElement(window,'rect',name='turretHeader',pos='8,-8',width='1856',height='52')
for name in ('brand','title','subtitle','turretClose','headerRule'):
    source=body.find(f"./*[@name='{name}']")
    if source is not None:body.remove(source);header.append(source)
subtitle=header.find("./*[@name='subtitle']")
if subtitle is not None:subtitle.set('visible','false')
for name in ('brand','turretClose'):
    element=header.find(f"./*[@name='{name}']")
    if element is not None:element.set('visible','false')
for name in ('title',):
    label=header.find(f"./*[@name='{name}']")
    if label is not None:label.set('font_size','36');label.set('justify','left')
def node(parent,tag,name,x,y,w,h,**attrs):
    return E.SubElement(parent,tag,dict(name=name,pos=f'{x},{-y}',width=str(w),height=str(h),**{k:str(v) for k,v in attrs.items()}))
def label(p,name,x,y,w,h,text='',size=20,**attrs):
    return node(p,'label',name,x,y,w,h,text=text,font_size=size,depth=7,color='240,240,244,255',overflow='shrinkcontent',**attrs)
def fill(p,name,w,h,color='17,17,21,250'):
    return node(p,'sprite',name,0,0,w,h,sprite='menu_empty2px',type='sliced',color=color,depth=2)
def action(p,name,x,y,w,h,icon,text_id,text_key=None):
    a=node(p,'rect',name,x,y,w,h,depth=4,on_press='true',on_hover='true',sound_play_on_hover='[default_hover]',controller='RebirthCookingAction, RebirthUtils')
    fill(a,'actionFill',w,h,'17,17,21,255')
    node(a,'sprite','actionFrame',0,0,w,h,sprite='menu_empty2px',type='sliced',color='96,96,104,255',fillcenter='false',depth=5)
    node(a,'sprite','actionIcon',8,6,20,20,sprite=icon,depth=6,foregroundlayer='true')
    l=label(a,text_id,33,5,w-38,h-9,size=18,justify='center',on_press='false')
    if text_key:l.set('text_key',text_key)
    return a
for child in list(body):
    if child.get('name','').startswith('turretCard'):body.remove(child)
for i in range(4):
    c=node(body,'rect',f'turretCard{i}',8,i*190,910,180)
    fill(c,f'cardFill{i}',910,180,'17,17,21,255')
    node(c,'sprite',f'cardBorder{i}',0,0,910,180,sprite='menu_empty2px',type='sliced',fillcenter='false',color='97,75,126,255',depth=3)
    node(c,'sprite',f'cardAccent{i}',0,0,4,180,sprite='menu_empty',color='181,140,255,255',depth=4)
    node(c,'sprite',f'cornerBadge{i}',12,8,38,32,sprite='menu_empty3px',type='sliced',color='134,57,202,255',depth=5)
    label(c,f'cornerNumber{i}',12,8,38,32,text=str(i+1),size=29,justify='center')
    label(c,f'turretCorner{i}',60,8,290,32,size=29,on_press='true')
    label(c,f'turretRange{i}',710,8,188,28,size=22,justify='right')
    label(c,f'turretName{i}',16,38,134,22,size=17)
    node(c,'sprite',f'turretIcon{i}',16,44,128,128,atlas='ItemIconAtlas',depth=5,on_press='false')
    action(c,f'turretEnabled{i}',156,48,74,56,'ui_game_symbol_electric_power',f'turretEnabledText{i}')
    action(c,f'turretCctv{i}',156,112,74,56,'ui_game_symbol_stealth',f'turretCctvText{i}','xuiCamperTurretCctv')
    action(c,f'turretInstall{i}',826,48,72,120,'ui_game_symbol_backpack',f'turretInstallText{i}')
    for control in ('turretEnabled','turretCctv','turretInstall'):
        button=c.find(f"./rect[@name='{control}{i}']")
        icon=button.find("./sprite[@name='actionIcon']");icon.set('pos','24,-8');icon.set('width','26');icon.set('height','26')
        caption=next(n for n in button if n.tag=='label');caption.set('pos','3,-35');caption.set('width','68');caption.set('height','76' if control=='turretInstall' else '24');caption.set('font_size','18')
    g=node(c,'rect',f'turretAmmoGroup{i}',244,40,256,132)
    label(g,f'ammoHeading{i}',0,0,256,25,size=20)
    action(c,f'turretMode{i}',360,8,140,28,'ui_game_symbol_shape_ammo',f'turretModeText{i}')
    for a in range(3):
        x=a*86;suffix=f'{i}_{a}'
        cell=node(g,'sprite',f'turretAmmo{suffix}',x,30,78,78,sprite='menu_empty2px',type='sliced',color='48,42,59,255',depth=4,on_press='true')
        node(cell,'sprite','ammoFrame',0,0,78,78,sprite='menu_empty2px',type='sliced',fillcenter='false',color='133,126,145,255',depth=5,on_press='false')
        node(cell,'sprite',f'turretAmmoIcon{suffix}',5,3,68,68,atlas='ItemIconAtlas',depth=5,on_press='false')
        label(cell,f'turretAmmoCount{suffix}',22,50,52,26,size=23,justify='right',on_press='false')
        label(g,f'turretAmmoText{suffix}',x,110,78,1,size=1,visible='false')
        label(g,f'turretAmmoNext{suffix}',x,109,78,23,size=17,justify='center',on_press='true')
    label(c,f'attachmentHeading{i}',512,40,300,25,size=20,text_key='xuiCamperTurretAttachments')
    for s in range(4):
        name=f'turretSilencer{i}' if s==0 else f'turretScope{i}_{s-1}'
        text=f'turretSilencerText{i}' if s==0 else f'turretScopeText{i}_{s-1}'
        cell=action(c,name,512+s*78,70,70,98,'ui_game_symbol_mods' if s==0 else 'ui_game_symbol_sight',text)
        icon=cell.find("./sprite[@name='actionIcon']");icon.set('pos','9,-8');icon.set('width','52');icon.set('height','52')
        caption=cell.find(f"./label[@name='{text}']");caption.set('pos','2,-70');caption.set('width','66');caption.set('height','24');caption.set('font_size','17')
    label(c,f'slotOrder{i}',0,0,1,1,visible='false')
    e=node(c,'rect',f'turretEmptyGroup{i}',156,50,658,120)
    label(e,f'emptyHint{i}',0,0,640,38,size=26,text_key='xuiCamperTurretEmptyHint')
    label(e,f'sourceHint{i}',0,48,640,54,size=20,text_key='xuiCamperTurretSources')

overview=body.find("./rect[@name='overviewPanel']")
if overview is not None:body.remove(overview)
for name in ('instructions','turretStatus'):
    child=body.find(f".//*[@name='{name}']")
    if child is not None:
        child.set('width','880');child.set('font_size','16');child.set('pos','16,-760' if name=='instructions' else '16,-789');child.set('height','23' if name=='instructions' else '20')
camera=body.find("./rect[@name='turretCameraPanel']")
camera.set('pos','16,0')
for old in list(camera):
    if old.get('name','').startswith('cctvInfo'):camera.remove(old)
for level in ('1','2','4','8','Auto'):
    old=camera.find(f"./*[@name='turretZoom{level}']")
    if old is not None:old.set('visible','false')
for name in ('turretCameraHelp','turretZoomStatus'):
    old=camera.find(f"./*[@name='{name}']")
    if old is not None:old.set('visible','false')
for index,(key,icon) in enumerate((('Aim','ui_game_symbol_sight'),('Fire','ui_game_symbol_shape_ammo'),('Zoom','ui_game_symbol_sight'),('Return','ui_game_symbol_arrow_left'))):
    card=node(camera,'rect','cctvInfo'+key,8,62+index*164,216,148,depth=46)
    fill(card,'cctvInfoFill',216,148,'31,26,43,255')
    node(card,'sprite','cctvInfoFrame',0,0,216,148,sprite='menu_empty3px',type='sliced',fillcenter='false',color='105,75,143,255',depth=47)
    node(card,'sprite','cctvInfoIcon',12,12,28,28,sprite=icon,atlas='UIAtlas',color='213,192,242,255',depth=48)
    label(card,'cctvInfoTitle'+key,48,12,156,30,size=23,text_key='xuiCamperCctv'+key+'Title').set('depth','49')
    if key=='Zoom':
        label(card,'cctvZoomLeftBracket',126,12,14,30,text='[',size=23).set('depth','50')
        value=label(card,'cctvSelectedZoom',140,12,58,30,size=23,justify='center');value.set('depth','50');value.set('color','255,211,78,255')
        label(card,'cctvZoomRightBracket',200,12,14,30,text=']',size=23).set('depth','50')
    label(card,'cctvInfoBody'+key,12,52,192,82,size=21,text_key='xuiCamperCctv'+key+'Help').set('depth','49')

native=E.parse(ROOT/'Config/_Vehicles/camper_modules_windows.xml')
append=root.find('append')
for old,new in [('sellBackpackPanel','camperTurretBackpack'),('camperSelectedPanel','camperTurretItemInfo')]:
    existing=append.find(f"./window[@name='{new}']")
    if existing is not None:append.remove(existing)
    existing=body.find(f"./rect[@name='{new}']")
    if existing is not None:body.remove(existing)
    source=next(n for n in native.iter('rect') if n.get('name')==old)
    target=copy.deepcopy(source);target.set('name',new);body.append(target)
    if old=='sellBackpackPanel':
        target.find(".//rect[@name='listContent']").set('controller','RebirthCamperTurretBackpack, RebirthUtils')
        for slot in target.iter('item_stack'):slot.set('controller','RebirthCamperTurretBackpackSlot, RebirthUtils')
        controls=target.find("./rect[@name='sectionControls']")
        controls.remove(controls.find("./button[@name='sectionFilter']"))
        target.find("./label[@name='sectionBackpackCash']").set('width','160')
        for j,(name,icon) in enumerate([('filterTurrets','ui_game_symbol_turret'),('filterAmmo','ui_game_symbol_ammo')]):
            node(target,'button',name,752+j*38,24,26,26,pivot='center',sprite=icon,style='press, hover',color='255,255,255,255',depth=15)
destination=args.output or path
destination.parent.mkdir(parents=True,exist_ok=True)
E.indent(root);tree.write(destination,encoding='utf-8',xml_declaration=False)

xui=ROOT/'Config/_Vehicles/camper_turret_xui.xml';xt=E.parse(xui);group=xt.find('./append/window_group');group.set('open_backpack','false')
for name in ('camperTurretBackpack','camperTurretItemInfo'):
    existing=group.find(f"./window[@name='{name}']")
    if existing is not None:group.remove(existing)
destination=args.xui_output or xui
destination.parent.mkdir(parents=True,exist_ok=True)
E.indent(xt);xt.write(destination,encoding='utf-8',xml_declaration=False)
