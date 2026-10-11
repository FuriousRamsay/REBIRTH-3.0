from pathlib import Path
import json,xml.etree.ElementTree as E
p=Path('Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.MillingLayout.cs');s=p.read_text().replace('        if (c.ViewComponent.Size.x != width || c.ViewComponent.Size.y != height)','        if (c is XUiC_RebirthReadableText readable) readable.SetBounds(width,height);\n        else if (c.ViewComponent.Size.x != width || c.ViewComponent.Size.y != height)');p.write_text(s)
# Parse every directly changed XML and check authored editor cells retain unique storage indices.
for p in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/station_templates.xml','Config/_Cooking/workspace_windows.xml']:E.parse(p)
r=E.parse('Config/XUi_InGame/windows.xml').getroot().find('.//*[@name="editorBagScroll"]')
cells=[n for n in r.iter() if n.get('name','').startswith('editorCell')]
assert len(cells)==169 and len({n.get('name') for n in cells})==169
assert all(n.get('width')=='69' for n in cells)
print('PASS: changed XML parses; Modify retains 169 uniquely indexed cells, 69px on 71px pitch (11 columns).')
scenario={'name':'ui_feedback_20261009b','stopOnFailure':True,'notes':'NOT EXECUTED. Use disposable test save. Before each observation open: Modify with a backpack, then workbench/chemistry/forge/mortar. Verify Modify 11 cells per row and scroll to last occupied cell; non-cooking details, two-column requirements, central backpack, tools above queue; mortar no herbs and correct skill labels. Separately prepare sell stash at trader, note item counts and quote, press SELL once, verify exactly quoted currency and removed sold copies, retained unsold remainder; repeat empty stash and reconnect. Screenshot steps require visual review; successful screenshot capture alone does not verify layout.','steps':[{'action':{'command':'uitree','args':{}}},{'screenshot':'ui_feedback_20261009b'},{'expectNoErrors':True}]}
p=Path('Tools/GameBridge/tests/ui_feedback_20261009b.json');p.write_text(json.dumps(scenario,indent=2))
