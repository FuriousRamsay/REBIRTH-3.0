"""Author stress traits, native passive penalties and the paired HUD cards."""
import csv,io
import xml.etree.ElementTree as E
from content_model import ROOT
def read(p):return E.parse(ROOT/p,E.XMLParser(target=E.TreeBuilder(insert_comments=True)))
def save(t,p):
    E.indent(t,space='  ');(ROOT/p).write_text(E.tostring(t.getroot(),encoding='unicode')+'\n',encoding='utf-8')

p='Config/_Survivor/traits.xml';t=read(p);root=t.getroot()
for node in root.findall('trait'):
    if node.get('id') in ('trait.steady_nerves','trait.anxious'):
        c=node.find('conflicts')
        if c is not None:node.remove(c)
        if node.get('id')=='trait.steady_nerves':E.SubElement(E.SubElement(node,'conflicts'),'trait',id='trait.easily_rattled')
        node.find('authoring').set('effect_summary','25% less danger stress.' if node.get('id')=='trait.steady_nerves' else 'Unseen hostile activity builds stress; recovery delay is 90 seconds.')
        node.find('authoring').set('implementation_surface','RebirthStressService')
for id,title,points in [('easily_rattled','EasilyRattled',2),('fear_dark','FearDark',2)]:
    existing=root.find(f"trait[@id='trait.{id}']")
    if existing is not None:
        existing.set("icon_key","rb_trait_"+id)
        continue
    n=E.SubElement(root,'trait',id='trait.'+id,name_key='rbTrait'+title,description_key='rbTrait'+title+'Desc',category='Psychological',polarity='negative',points=str(points),availability='universal',icon_key='rb_trait_'+id,modifier_id='modifier.trait.'+id)
    if id=='easily_rattled':E.SubElement(E.SubElement(n,'conflicts'),'trait',id='trait.steady_nerves')
    E.SubElement(n,'authoring',effect_summary='Server-authoritative stress trait.',implementation_surface='RebirthStressService')
save(t,p)
p='Config/_Survivor/condition_profiles.xml';t=read(p);root=t.getroot()
for misplaced in list(root.findall('modifier_profile')):root.remove(misplaced)
root=root.find('modifier_profiles')
for id in ('steady_nerves','anxious','easily_rattled','fear_dark'):
    n=root.find(f"modifier_profile[@id='modifier.trait.{id}']")
    if n is None:n=E.SubElement(root,'modifier_profile',id='modifier.trait.'+id,owner_trait_id='trait.'+id)
    for c in list(n):n.remove(c)
    n.set('effect_summary','Stress-specific behaviour; does not add a second injury Mood modifier.')
    n.set('implementation_surface','RebirthStressService');n.set('implementation_state','IMPLEMENTED')
    target,op,value={'steady_nerves':('stress.gain','multiply','.75'),'easily_rattled':('stress.gain','multiply','1.25'),'anxious':('stress.anxiety','add','1'),'fear_dark':('stress.darkness','add','1')}[id]
    E.SubElement(n,'component',target=target,op=op,value=value,phase='runtime')
save(t,p)
p='Config/_Survivor/support_profiles.xml';t=read(p)
n=t.getroot().find("support_profile[@id='support.calming_tea']")
n.set('effect_summary','Lavender and Currant Infusion gives +3 Mood target for up to ten minutes, proportional to consumed volume. Nonstacking.')
n.set('cooldown_seconds','0')
for child in list(n):n.remove(child)
E.SubElement(n,'supported_traits')
E.SubElement(E.SubElement(n,'item_bindings'),'item',id='rebirthCookingFoodN38')
E.SubElement(E.SubElement(n,'effects'),'effect',target='mood.target',op='add',value='3',scope='universal',state='positive')
save(t,p)
p='Config/_Metabolism/buffs.xml';t=read(p);root=t.getroot()
for id,penalty in [('Shaken',.05),('High',.10)]:
    name='buffRebirthStress'+id
    if root.find(f".//buff[@name='{name}']") is not None:continue
    b=E.SubElement(E.SubElement(root,'append',xpath='/buffs'),'buff',name=name,hidden='true',name_key='rbStress'+id,icon='ui_game_symbol_alert')
    E.SubElement(b,'stack_type',value='replace');E.SubElement(b,'duration',value='0');g=E.SubElement(b,'effect_group')
    E.SubElement(g,'passive_effect',name='WeaponHandling',operation='perc_add',value=str(-penalty*2))
    E.SubElement(g,'passive_effect',name='ReloadSpeedMultiplier',operation='perc_add',value=str(1/(1+penalty)-1))
    E.SubElement(g,'passive_effect',name='StaminaLoss',operation='perc_add',value=str(penalty),tags='primary,secondary')
save(t,p)
p=ROOT/'Config/XUi_InGame/windows.xml';s=p.read_text(encoding='utf-8')
if 'name="rebirthStressCards"' not in s:
    cards='''<conditional><if cond="character_progression('Rebirth')"><append xpath="/windows/window[@name='HUDLeftStatBars']">
    <rect name="rebirthStressCards" controller="RebirthStressHud, RebirthUtils" pos="14,76" width="164" height="26">
      <sprite name="stressBackground" sprite="menu_empty" width="118" height="26" color="18,18,18,205" depth="1" />
      <sprite name="stressIcon" atlas="RebirthSurvivorIcons" sprite="rb_condition_stress" pos="14,-13" pivot="center" width="20" height="20" depth="5" />
      <label name="stressValue" pos="30,-13" width="80" height="22" pivot="left" font_size="16" justify="right" color="255,255,255,255" text="0" depth="6" />
      <sprite name="stressLocationBackground" pos="122,0" sprite="menu_empty" width="42" height="26" color="18,18,18,205" depth="1" />
      <sprite name="stressPlaceIcon" sprite="ui_game_symbol_map" pos="143,-13" pivot="center" width="20" height="20" depth="5" />
      </rect></append></if></conditional>'''
    s=s.replace('</configs>',cards+'\n</configs>');p.write_text(s,encoding='utf-8')
s=p.read_text(encoding='utf-8').replace('name="stressIcon" sprite="ui_game_symbol_alert"','name="stressIcon" atlas="RebirthSurvivorIcons" sprite="rb_trait_anxious"')
p.write_text(s,encoding='utf-8')
names={'rbStressCamp':'Established Camp','rbStressTrader':'Trader Compound','rbStressDanger':'In Danger','rbStressExploring':'Exploring','rbStressShaken':'Shaken','rbStressHigh':'Highly Stressed',
'rbTraitEasilyRattled':'Easily Rattled','rbTraitEasilyRattledDesc':'Danger generates 25% more stress. Recovery is normal. Conflicts with Steady Nerves.',
'rbTraitFearDark':'Fear of the Dark','rbTraitFearDarkDesc':'Nighttime exploration and deeply dark places build stress. Your camp or a trader compound reduces nighttime anxiety; carried lights do not remove it.',
'xuiRebirthTraitSteadyNervesDesc':'Danger generates 25% less stress. Recovery is normal. Conflicts with Easily Rattled.',
'xuiRebirthTraitAnxiousDesc':'Nearby unseen hostile activity builds stress even before combat. Recovery begins after 90 seconds rather than 30.',
'rebirthCookingFoodN37Desc':'Calming infusion. A full serving eases 10 stress over 30 seconds and reduces incoming stress by 30% for five minutes, including during danger. Partial servings grant proportional benefit; relief is limited to once per five minutes.',
'rebirthCookingFoodN38Desc':'Comforting infusion. Raises your Mood target by 3 for ten minutes per full serving. Partial servings grant proportional duration, capped at ten minutes; strength does not stack.',
'xuiRebirthSupportCalmingTea':'Comforting Infusion'}
p=ROOT/'Config/Localization.csv';rows=list(csv.reader(io.StringIO(p.read_text(encoding='utf-8-sig'))));seen=set()
for r in rows:
    if r and r[0] in names:r[1]=names[r[0]];seen.add(r[0])
rows.extend([k,v] for k,v in names.items() if k not in seen)
out=io.StringIO();csv.writer(out,lineterminator='\n').writerows(rows);p.write_text(out.getvalue(),encoding='utf-8')
print('Stress traits, effects, localization and paired HUD authored.')
