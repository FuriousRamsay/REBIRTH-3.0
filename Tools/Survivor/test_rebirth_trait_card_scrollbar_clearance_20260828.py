#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
errors=[]
for rel in ("Config/XUi_Menu/windows.xml","Config/XUi_InGame/windows.xml"):
    path=root/rel
    text=path.read_text(encoding="utf-8")
    ET.parse(path)
    where=rel
    for prefix in ("positiveTrait","negativeTrait"):
        # scrollbar must stay in its established right-edge position
        if f'name="{prefix}NativeScrollHost"' in text:
            if f'name="{prefix}NativeScrollHost" pos="528,-66" width="30"' not in text:
                errors.append(f"{where}: {prefix} native scrollbar moved")
        # Every authored trait card starts x=6 and must end before x=528.
        for i in range(7):
            m=re.search(rf'<rect name="{prefix}Row{i}" pos="6,[^"]+" width="(\d+)"', text)
            if not m:
                errors.append(f"{where}: missing {prefix}Row{i}")
                continue
            width=int(m.group(1))
            if 6+width >= 528:
                errors.append(f"{where}: {prefix}Row{i} ends at {6+width}, overlapping scrollbar x=528")
            if width != 516:
                errors.append(f"{where}: {prefix}Row{i} width {width}, expected 516")
        if f'name="{prefix}Action0"' in text and 'pos="456,-8" width="50"' not in text:
            errors.append(f"{where}: {prefix} action column not pulled clear of scrollbar")
if errors:
    print("FAIL")
    for e in errors: print(" -",e)
    raise SystemExit(1)
print("PASS: Trait cards stop before their right-side scrollbars in Menu and InGame layouts.")
