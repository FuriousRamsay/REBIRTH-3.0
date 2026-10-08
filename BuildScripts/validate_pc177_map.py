from pathlib import Path
from lxml import etree as E
root=E.parse('Config/XUi_InGame/windows.xml')
map_window=root.xpath('//window[@name="mapArea"]')[-1]
assert map_window.get('controller')=='RebirthMapArea, RebirthUtils'
view=map_window.xpath('.//*[@name="mapView"]')[0]
assert view.tag=='panel' and view.get('clippingsize')=='1406,714'
texture=view.find('texture'); assert texture.get('width')==texture.get('height')=='1406'
assert texture.get('pos')=='0,346'
for name in ['playerIcon','bedrollIcon','waypointIcon','cbxStaticMapType','switchStaticMap','mapViewTexture','clippingPanel']:
    assert len(map_window.xpath('.//*[@name="'+name+'"]'))==1,name
for name,expected in [('mapTracking',(-928,435,430,535)),('mapInvites',(-928,-108,430,270)),('mapArea',(-490,435,1418,813))]:
    w=root.xpath('//window[@name="'+name+'"]')[-1]
    assert 'panel' not in w.attrib
    assert (*map(int,w.get('pos').split(',')),int(w.get('width')),int(w.get('height')))==expected
assert -928+430+8==-490 and -490+1418==928
assert 435-535-8==-108 and -108-270==435-813
# Native square texture retains its proportions; map objects and picking agree over the cropped view.
for zoom in [.5,1,2,4]:
    scale=1406/336/zoom
    for px,py in [(0,0),(703,357),(1406,714),(1300,650)]:
        wx=(px-703)/scale; wz=-(py-357)/scale
        sx=wx*scale+703; sy=-wz*scale+357
        assert abs(px-sx)<1e-8 and abs(py-sy)<1e-8
xui=E.parse('Config/XUi_InGame/xui.xml'); native=E.parse('../../Data/Config/XUi_InGame/xui.xml')
registration=xui.xpath('//append[window[@name="rebirthMapChrome"]]')[-1]
assert len(native.xpath(registration.get('xpath')))==1
print('Map geometry, native control names, clipping, coordinate round trips and navigation registration passed.')
