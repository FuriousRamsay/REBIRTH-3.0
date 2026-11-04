from pathlib import Path
from lxml import etree as E
import re
root=Path(__file__).resolve().parents[1]
native=E.parse(str(root.parents[1]/'Data/Config/XUi_InGame/windows.xml'))
new=E.parse(str(root/'Config/XUi_InGame/standalone_bank_windows.xml'))
for name in ('windowPowerSourceStats','windowPowerSourceSlots'):
    before=native.xpath('/windows/window[@name=$n]',n=name)[0]
    after=new.xpath('//append/window[@name=$n]',n=name)[0]
    names=lambda n:set(n.xpath('.//*[@name]/@name'))
    assert names(before)<=names(after),(name,'native child contract lost')
    bindings=lambda n:set(re.findall(r'\{([a-zA-Z][a-zA-Z0-9]*)\}',E.tostring(n).decode()))
    assert bindings(before)<=bindings(after),(name,'native binding lost')
grid=new.xpath('//grid[@name="inventory"]')[0]
assert (grid.get('rows'),grid.get('cols'))==('2','3')
assert grid.find('item_stack').get('controller')=='RequiredItemStack'
assert new.xpath('//rect[@name="btnOn"]') and new.xpath('//rect[@name="btnRefuel"][@visible="{showfuel}"]')
for w,h in ((1280,720),(1920,1080),(2560,1080),(2560,1440),(3440,1440)):
    scale=min((w-32)/1323,(h-32)/883)
    for x,y,width,height in ((8,62,450,360),(8,438,450,437),(474,62,841,813)):
        assert x>=0 and y>=0 and x+width<=1323 and y+height<=883
        assert (x+width)*scale<=w-32 and (y+height)*scale<=h-32
group=E.parse(str(root/'Config/XUi_InGame/standalone_bank_xui.xml'))
assert not group.xpath('//setattribute[@name="controller"]'),'native group owner must be retained'
assert group.xpath('//window[@name="rebirthTurretBackpack"]')
assert group.xpath('//window[@name="rebirthTurretItemInfo"]')
print('PASS native control names/bindings; six required-item slots; refuel visibility; native group owner; shared backpack/inspector; panel bounds at five resolutions.')
