from pathlib import Path
import re, sys
root=Path(__file__).resolve().parents[2]
xml=(root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
checks=[]
def check(name, ok):
    checks.append((name,bool(ok)))
check('Job Tier still uses approved clipboard sprite', 'name="rebirthJobTierIcon" atlas="RebirthHud" sprite="rb_hud_job_clipboard"' in xml)
check('Job Tier clipboard now renders at 24x24', 'name="rebirthJobTierIcon" atlas="RebirthHud" sprite="rb_hud_job_clipboard" pivot="center" pos="53,-19" width="24" height="24"' in xml)
check('Collapsed 18x18 Job Tier box removed', 'name="rebirthJobTierIcon" atlas="RebirthHud" sprite="rb_hud_job_clipboard" pivot="center" pos="53,-19" width="18" height="18"' not in xml)
check('Job Tier text stays in approved position', 'name="rebirthJobTierText" pivot="center" pos="96,-21" width="60" height="18"' in xml)
check('Approved compass shell remains 728x38', 'name="rebirthCompassShell" atlas="RebirthHud" sprite="rb_hud_shell_full" pos="-204,0" width="728" height="38"' in xml)
failed=[n for n,o in checks if not o]
for n,o in checks: print(('PASS' if o else 'FAIL')+': '+n)
print(f'RESULT: {len(checks)-len(failed)}/{len(checks)} PASS')
sys.exit(1 if failed else 0)
