from pathlib import Path
from lxml import etree
import argparse
p=argparse.ArgumentParser();p.add_argument('--root',required=True);a=p.parse_args();r=Path(a.root)
t=etree.parse(str(r/'Config/XUi_Menu/windows.xml')); e=[]
for n in ['profileRow0','profileRow3','selectedPlayerPreview','selectedProfileIdentityMeta','profileTraitScrollHost','profileDetailRow17']:
    if not t.xpath("//*[@name='%s']"%n): e.append('missing '+n)
if t.xpath("//*[@name='profileRow4']"): e.append('old fifth row remains')
pr=t.xpath("//*[@name='selectedPlayerPreview']")
if not pr or pr[0].get('controller')!='SDCSPreviewWindow': e.append('preview not native SDCS')
m=(r/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text()
for x in ['VisibleRows = 4','BindBackgroundThumbnail(rowBackground)','RenderDetailSection(6, traits, traitOffset, true)','RenderDetailSection(12, weaknesses, 0, false)']:
    if x not in m:e.append('missing '+x)
seg=m[m.find('private void RenderSelectedProfileDetails'):m.find('private sealed class ProfileDetailDisplay')]
if 'BudgetDelta' in seg or 'Trait Points' in seg:e.append('trait point currency remains')
print('Profile manager redesign audit errors=%d'%len(e))
for x in e:print('ERROR:',x)
raise SystemExit(1 if e else 0)
