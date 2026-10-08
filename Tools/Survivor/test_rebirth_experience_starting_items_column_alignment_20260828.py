from pathlib import Path
import sys
from lxml import etree
root=Path('.')
if '--root' in sys.argv:
    i=sys.argv.index('--root'); root=Path(sys.argv[i+1])
errors=[]
for rel in [Path('Config/XUi_Menu/windows.xml'),Path('Config/XUi_InGame/windows.xml')]:
    p=root/rel
    etree.parse(str(p))
    text=p.read_text(encoding='utf-8')
    expected={0:(0,-40,256),1:(266,-40,260),2:(0,-74,256),3:(266,-74,260),4:(0,-108,256),5:(266,-108,260),6:(0,-142,256),7:(266,-142,260)}
    for row,(x,y,w) in expected.items():
        token=f'backgroundStartingItemRow{row}\" pos=\"{x},{y}\" width=\"{w}\"'
        if token not in text:
            errors.append(f'{rel}: {token} missing')
    # Parent coordinates prove that the item columns line up exactly with the panels above:
    # 334 + 0 = 334 (Knowledge), 334 + 266 = 600 (Weaknesses).
    for token in [
        'backgroundKnowledgeSummaryPanel\" pos=\"334,-220\" width=\"256\"',
        'backgroundWeaknessAndPointsPanel\" pos=\"600,-220\" width=\"260\"',
        'backgroundStartingItemsPanel\" pos=\"334,-380\" width=\"526\"',
        'backgroundStartingItemsScrollCapture\" depth=\"1\" pos=\"0,-40\" width=\"526\"',
    ]:
        if token not in text:
            errors.append(f'{rel}: alignment anchor missing: {token}')
print('Experience Starting Items column alignment errors='+str(len(errors)))
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
