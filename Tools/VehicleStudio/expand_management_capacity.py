from pathlib import Path
import re, copy, xml.etree.ElementTree as E
root=Path.cwd()
base=root/'Scripts/Vehicles/Camper'
for name in ['RebirthCamperCraftingStation.cs','RebirthCamperStorageCapacity.cs']:
 p=base/name;s=p.read_text();s=s.replace('ModuleSlots = 10','ModuleSlots = 20').replace('ModuleRows = 2','ModuleRows = 4').replace('Exactly ten container','Exactly twenty container');p.write_text(s)
for name in ['RebirthCamperRemoteStationClient.cs','RebirthCamperRemoteTransport.cs','XUiC_RebirthCamperStationPicker.cs','XUiC_RebirthCamperModules.cs']:
 p=base/name;s=p.read_text();s=re.sub(r'(?<=\[)10(?=\])|(?<=[<>=] )10\b|(?<=<)10\b','RebirthCamperCraftingStation.ModuleSlots',s)
 # Visual spacing is independent of installation capacity.
 if name=='XUiC_RebirthCamperStationPicker.cs':
  s=s.replace('new Vector2i(10, -10 - installed * 40)','new Vector2i(10 + (installed % 2) * 230, -10 - (installed / 2) * 36)')
 s=s.replace('ten labelled','twenty labelled').replace('ten module','twenty module').replace('ten-position','twenty-position');p.write_text(s)
p=root/'Config/Localization.csv';s=p.read_text();s=s.replace('these ten positions','these twenty positions').replace('Installed stations: {0} / 10','Installed stations: {0} / 20').replace('Installed containers: {0} / 10','Installed containers: {0} / 20');p.write_text(s)
p=root/'Config/_Vehicles/camper_modules_windows.xml';tree=E.parse(p);left=next(n for n in tree.iter('rect') if n.get('name')=='camperInstallationPanel');template=copy.deepcopy(next(n for n in left if n.get('name')=='camperModule0'))
for n in list(left):
 if n.get('name','').startswith('camperModule') and n.tag=='rect' or n.tag=='label' and n.get('text','').isdigit():left.remove(n)
for i in range(20):
 n=copy.deepcopy(template);n.set('name',f'camperModule{i}');n.set('pos',f'{24+i%5*176},{-140-i//5*142}');n.set('height','112');bg=n.find("sprite[@name='background']");bg.set('height','112');bg.set('color','30,32,39,255');icon=n.find("sprite[@name='itemIcon']");icon.set('pos','85,-56');icon.set('width','92');icon.set('height','92');left.append(n)
 E.SubElement(left,'label',pos=f'{24+i%5*176},{-254-i//5*142}',width='170',height='24',font_size='18',text=str(i+1),justify='center',color='181,140,255,255',depth='6')
for n in left:
 if n.get('name')=='camperInstallationSummary':n.set('pos','24,-728');n.set('height','66');n.set('font_size','22')
 if n.tag=='sprite' and n.get('depth')=='0':n.set('color','17,17,21,255')
E.indent(tree,space='  ');tree.write(p,encoding='unicode')
p=root/'Config/_Vehicles/camper_station_picker_windows.xml';tree=E.parse(p);content=next(n for n in tree.iter('panel') if n.get('name')=='content');template=copy.deepcopy(next(n for n in content if n.get('name')=='camperStationChoice0'))
for n in list(content):
 if n.get('name','').startswith('camperStationChoice'):content.remove(n)
for i in range(20):
 n=copy.deepcopy(template);n.set('name',f'camperStationChoice{i}');n.set('pos',f'{10+i%2*230},{-10-i//2*36}');n.set('width','220');n.set('height','32');button=n.find('button');button.set('width','220');button.set('height','32');button.set('defaultcolor','30,32,39,255');button.set('hovercolor','64,48,87,255');label=n.find('label');label.set('name',f'camperStationName{i}');label.set('width','200');label.set('height','28');label.set('font_size','18');content.append(n)
E.indent(tree,space='  ');tree.write(p,encoding='unicode')

