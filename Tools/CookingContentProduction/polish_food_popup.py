"""Install the compact food tooltip in every shared popup without changing equipment layouts."""
from pathlib import Path
import re
import xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[2]

def panel():
    p=E.Element('rect',name='foodPopup',width='440',height='250',visible='false')
    def label(name,x,y,w,h,size,color='235,235,240,255',text=''):
        return E.SubElement(p,'label',name=name,pos=f'{x},{-y}',width=str(w),height=str(h),font_size=str(size),color=color,depth='5',justify='left',overflow='shrinkcontent',text=text)
    E.SubElement(p,'sprite',name='foodPopupIcon',pos='18,-15',width='86',height='78',atlas='ItemIconAtlas',sprite='',depth='5')
    label('foodPopupName',120,22,302,34,26)
    label('foodPopupQuality',120,60,302,26,19,'240,112,112,255')
    E.SubElement(p,'sprite',name='foodPopupDivider',pos='18,-106',width='404',height='1',sprite='menu_empty',color='100,80,126,255',depth='4')
    for i in range(4):
        row=E.SubElement(p,'rect',name=f'foodPopupRow{i}',pos=f'18,{-116-i*34}',width='404',height='34')
        E.SubElement(row,'sprite',name='rowFill',width='404',height='34',sprite='menu_empty',color='24,24,30,255' if i%2==0 else '18,18,23,255',depth='3')
        E.SubElement(row,'sprite',name=f'foodPopupIcon{i}',pos='7,-7',width='20',height='20',sprite='',depth='5')
        for suffix,x,w,size in [('Title',38,162,19),('Value',202,202,20)]:
            E.SubElement(row,'label',name=f'foodPopup{suffix}{i}',pos=f'{x},-4',width=str(w),height='28',font_size=str(size),justify='left',overflow='shrinkcontent',depth='5')
    label('foodPopupFooter',18,226,404,24,16,'158,151,168,255','Per serving • Changes from base stats')
    return p

def install(match):
    popup=E.fromstring(match.group())
    for old in list(popup):
        if old.get('name')=='foodPopup':popup.remove(old)
    popup.append(panel())
    return E.tostring(popup,encoding='unicode')

if __name__=='__main__':
    path=ROOT/'Config/XUi_InGame/windows.xml'
    text=path.read_text(encoding='utf-8')
    text,count=re.subn(r'<panel\b[^>]*controller="RebirthCharacterStatsPopup, RebirthUtils"[^>]*>.*?</panel>',install,text,flags=re.S)
    assert count>0
    path.write_text(text,encoding='utf-8')
    print(f'Updated {count} shared food popups')
