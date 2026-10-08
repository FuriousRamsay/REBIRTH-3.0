from pathlib import Path
from PIL import Image
import re
root = Path(__file__).resolve().parents[2]
xml = (root/'Config'/'XUi_InGame'/'windows.xml').read_text(encoding='utf-8', errors='ignore')
icon = root/'UIAtlases'/'RebirthHud'/'rb_hud_job_clipboard.png'
errors=[]
job=re.search(r'<sprite[^>]+name="rebirthJobTierIcon"[^>]+>', xml)
sun=re.search(r'<sprite[^>]+name="rebirthCompassSun"[^>]+>', xml)
if not job: errors.append('rebirthJobTierIcon not found')
if not sun: errors.append('rebirthCompassSun not found')
if job and ('width="18"' not in job.group(0) or 'height="18"' not in job.group(0)): errors.append('Job Tier icon is not 18x18')
if sun and ('width="18"' not in sun.group(0) or 'height="18"' not in sun.group(0)): errors.append('Sun icon is not 18x18')
if not icon.exists(): errors.append('replacement clipboard icon missing')
else:
    im=Image.open(icon).convert('RGBA')
    bbox=im.getchannel('A').getbbox()
    if bbox is None: errors.append('clipboard icon is fully transparent')
    else:
        visible_h=bbox[3]-bbox[1]
        if visible_h < 220: errors.append(f'clipboard visible height still too padded: {visible_h}/256')
if errors:
    print('FAIL')
    for e in errors: print('-',e)
    raise SystemExit(1)
print('PASS - PC067 Job Tier icon is normalized and configured to match the 18x18 Sun icon size.')
