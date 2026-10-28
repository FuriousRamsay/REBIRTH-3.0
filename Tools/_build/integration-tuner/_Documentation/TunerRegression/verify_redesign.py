from pathlib import Path
import csv,io,re,json,hashlib,xml.etree.ElementTree as E
from PIL import Image
root=Path(__file__).resolve().parents[2]
window=E.parse(root/'Config/_Vehicles/vehicle_tuning_windows.xml').getroot();groups=E.parse(root/'Config/_Vehicles/vehicle_tuning_xui.xml').getroot()
edit=window.find(".//window[@name='rebirthVehicleTuningWindow']");preview=window.find(".//window[@name='rebirthVehicleTuningPreviewWindow']")
assert groups.find(".//window_group[@name='rebirthVehicleTuning']").get('actionset')=='true'
assert groups.find(".//window_group[@name='rebirthVehicleTuningPreview']").get('actionset')=='false'
assert preview.get('cursor_area')=='false'
for node in preview:
 if node.tag in ['sprite','label']:assert all(node.get(k)=='false' for k in ['on_press','on_hover','on_scroll'])
for i in range(7):
 nav=edit.find("rect[@name='category%dFrame']"%i);assert nav is not None
 label=nav.find('label');glyph=[n for n in nav.findall('sprite') if n.get('atlas')=='RebirthUiIcons'][0]
 assert label.get('font_size')=='19' and label.get('pivot')=='left' and label.get('vjustify')=='middle'
 assert glyph.get('pivot')=='center' and glyph.get('pos').split(',')[1]==label.get('pos').split(',')[1]=='-22'
for key in ['xuiRbTunerReset','xuiRbTunerExport','xuiRbTunerDrive']:
 label=edit.find("label[@text_key='%s']"%key);assert label.get('font_size')=='20' and label.get('pos').endswith(',-442') and label.get('vjustify')=='middle'
for i in range(4):
 slider=edit.find(".//scrollbar[@name='row%dSlider']"%i);assert slider.find("sprite[@name='scrollbarthumb']").get('on_press')=='true'
 assert edit.find(".//sprite[@name='row%dOriginalMarker']"%i) is not None
rows=list(csv.reader((root/'Config/Localization.csv').open(encoding='utf-8-sig',newline='')));assert rows[0]==['Key','english'];keys={}
for r in rows:
 if r and r[0].startswith(('xuiRbTune','xuiRbTuner','xuiRbVehicleTune')):
  assert len(r)==2 and r[0] not in keys,r;keys[r[0]]=r[1];assert r[1] and '\ufffd' not in r[1],r
source='\n'.join(p.read_text(encoding='utf-8-sig') for p in (root/'Scripts/Vehicles/Tuning').glob('*.cs'))
refs=set(re.findall(r'Localization.Get\("([^"]+)"\s*\)',source)+[n.get('text_key') for n in window.iter() if n.get('text_key')]);assert refs<=keys.keys(),refs-keys.keys();assert all('xuiRbTunerCategory'+str(i) in keys for i in range(7))
icons=set(re.findall(r'"(rb_tuner_[a-z_]+)"',source))|{n.get('sprite') for n in window.iter() if n.get('sprite','').startswith('rb_tuner_')}
for name in icons:
 p=root/'UIAtlases/RebirthUiIcons'/(name+'.png');im=Image.open(p);assert im.mode=='RGBA' and im.size==(256,256)
 alpha=im.getchannel('A');assert alpha.getextrema()==(0,255);assert all(alpha.getpixel(xy)==0 for xy in [(0,0),(255,0),(0,255),(255,255)])
 E.parse(root/'Resources/UI/VehicleTuningIcons'/(name+'.svg'))
ui=(root/'Scripts/Vehicles/Tuning/RebirthVehicleTuning.Ui.cs').read_text(encoding='utf-8-sig');assert 'Open(window,true,false)' in ui and 'Open(window,false,false)' in ui
assert 'group.hasActionSetFor!=XUiWindowGroup.EHasActionSetFor.None' in ui
assert ui.index('args[0],"close"')<ui.index('if(!GamePrefs.GetBool')
controller=(root/'Scripts/Vehicles/Tuning/XUiC_RebirthVehicleTuning.cs').read_text(encoding='utf-8-sig');assert '.OnSubmitHandler+=' not in controller and 'UIInput.onSubmit' in controller and 'VkPropagatesEvents=false' in controller
assert 'view.ScrollBar.Set(value,false)' in controller and 'view.openFrame=Time.frameCount-2' in controller
assert 'nativeBag.Resize' not in source and 'bag.Resize' not in source
print('PASS static redesign:2native modes,7centered categories,4native row sliders,activeCSV refs,%dicons,Escape/close/source-only storage guards'%len(icons))
