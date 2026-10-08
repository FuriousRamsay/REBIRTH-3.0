from pathlib import Path
exec(Path('BuildScripts/validate_pc186_ui.py').read_text())
g=t.xpath('/templates/rebirth_players_entry//*[@name="rebirthGroupName"]')[0]
assert g.get('pivot')=='topleft' and g.get('pos')=='5,-22'
assert 22+int(g.get('height'))<=46
assert w.xpath('//texture[@name="rebirthProfileBackgroundArt"]')
# World overview projection agrees with native marker coordinates at all supported scales.
for span in (2112,4160,6208,8256,12352,16448):
 zoom=max(6.15,span*712/(336*1406)*1.1)
 width=336*zoom*1406/712
 assert width>=span
 for z in (3.1,6.15,zoom):
  width=336*z*1406/712;height=336*z
  for cx,cz in ((0,0),(790,-230)):
   for wx,wz in ((0,0),(400,-900),(-1024,2048)):
    uvx=(wx-(cx-width/2))/width;uvy=(wz-(cz-height/2))/height
    px=(wx-cx)*712/(336*z)+703;py=(wz-cz)*712/(336*z)-356
    assert abs(uvx*1406-px)<1e-8
    assert abs((uvy-1)*712-py)<1e-8
# Negative coordinates address the correct native 16x16 chunk texel.
for coordinate in range(-65,66):assert (coordinate//16)*16+(coordinate&15)==coordinate
print('PASS: group row bounds, background artwork, world-width zoom and overview/marker coordinate agreement.')
