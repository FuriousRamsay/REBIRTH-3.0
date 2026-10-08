#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
errors=[]
menu=(ROOT/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
ingame=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
for p in (ROOT/'Config/XUi_Menu/windows.xml',ROOT/'Config/XUi_InGame/windows.xml'):
    ET.parse(p)

def need(data, token, where):
    if token not in data: errors.append(f'{where}: missing {token}')

# Survivor Profiles summary: Trait scrollbar stays on the RIGHT edge of the middle column,
# pulled slightly left so its chrome does not touch Weaknesses/Starting Items.
need(menu,'name="profileTraitNativeScrollHost" pos="550,-40" width="22" height="254"','menu profile summary')
need(menu,'name="profileTraitScrollCapture" pos="288,-36" width="258" height="262"','menu profile summary')
need(menu,'name="profileDetailRow10" pos="288,-40" width="258" height="29"','menu profile summary')
need(menu,'pos="588,-108" width="272" height="26" font_size="18" text_key="xuiRebirthSurvivorStartingItems"','menu profile summary')
need(menu,'name="profileStartingItemRow0" pos="576,-138" width="272" height="28"','menu profile summary')
need(menu,'name="profileStartingItemRow7" pos="576,-334" width="272" height="28"','menu profile summary')

# Creator Review mirrors the same three-column layout in both menu/in-game definitions.
for data,where in ((menu,'menu review'),(ingame,'ingame review')):
    need(data,'name="reviewTraitNativeScrollHost" pos="1090,-40" width="22" height="319"',where)
    need(data,'name="reviewTraitScrollCapture" pos="564,-40" width="520" height="319"',where)
    need(data,'name="reviewTraitRow0" pos="564,-40" width="520" height="29"',where)
    need(data,'name="reviewStartingItemsHeading" pos="1128,-108" width="542" height="26" font_size="18"',where)
    need(data,'name="reviewStartingItemRow0" pos="1128,-138" width="542" height="28"',where)
    need(data,'name="reviewStartingItemRow7" pos="1128,-334" width="542" height="28"',where)
    need(data,'name="reviewStartingItemsScrollCapture" pos="1128,-138" width="542" height="224"',where)
    need(data,'name="reviewStartingItemsNativeScrollHost" pos="1666,-138" width="22" height="224"',where)

if errors:
    print('FAIL')
    for e in errors: print(' -',e)
    raise SystemExit(1)
print('PASS: Profile/Review Trait scrollbars are right-positioned with safe clearance and Starting Items headings/spacing are aligned.')
