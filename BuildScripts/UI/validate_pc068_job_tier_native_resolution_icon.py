from pathlib import Path
from PIL import Image
root = Path(__file__).resolve().parents[2]
p = root / 'UIAtlases' / 'RebirthHud' / 'rb_hud_job_clipboard.png'
errors=[]
if not p.exists():
    errors.append(f'Missing: {p}')
else:
    im=Image.open(p).convert('RGBA')
    if im.size != (26,26):
        errors.append(f'Expected 26x26 source, got {im.size}')
    bbox=im.getchannel('A').getbbox()
    if bbox is None:
        errors.append('Icon has no visible alpha content')
if errors:
    print('FAIL')
    for e in errors: print('-',e)
    raise SystemExit(1)
print('PASS - PC068 native-resolution Job Tier icon validated.')
