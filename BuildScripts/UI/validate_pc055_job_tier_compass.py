from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT / 'Config' / 'XUi_InGame' / 'windows.xml'
CONTROLLER = ROOT / 'Scripts' / 'UI' / 'XUiC_RebirthCompassWindow.cs'
ICON = ROOT / 'UIAtlases' / 'RebirthHud' / 'rb_hud_job_tier.png'

checks = []
def check(name, condition):
    checks.append((name, bool(condition)))

xml = WINDOWS.read_text(encoding='utf-8')
cs = CONTROLLER.read_text(encoding='utf-8')
ET.parse(WINDOWS)
check('windows.xml parses', True)

check('level circle remains', 'name="rebirthLevelRingProgress"' in xml and 'name="rebirthLevelValue"' in xml)
check('horizontal XP text removed', 'name="rebirthXpText"' not in xml)
check('horizontal XP background removed', 'name="rebirthXpBackground"' not in xml)
check('horizontal XP fill removed', 'name="rebirthXpFill"' not in xml)
check('job tier icon added', 'name="rebirthJobTierIcon"' in xml and 'sprite="rb_hud_job_tier"' in xml)
check('job tier icon is 18x18 like compass weather icons', 'name="rebirthJobTierIcon"' in xml and 'width="18" height="18"' in xml)
check('job tier text binding added', 'name="rebirthJobTierText"' in xml and 'text="{rebirthjobtier}"' in xml)
check('job tier label fits old XP region', 'pos="110,-21" width="94" height="18"' in xml)

check('XP horizontal sprite reference removed from controller', 'xpBarFillView' not in cs)
check('XP text binding removed from controller', 'case "rebirthxptext"' not in cs)
check('job tier binding implemented', 'case "rebirthjobtier"' in cs and 'GetJobTierText(player)' in cs)
check('uses configured REBIRTH jobs-to-tier value', 'RebirthVariables.customJobsToNextTier' in cs)
check('safe fallback to native quest tier setting', 'Quest.QuestsPerTier' in cs)
check('uses weighted quest faction points', 'journal.GetQuestFactionPoints(factionId)' in cs)
check('preserves five-faction shared 2.6 meaning', 'factionId <= 5' in cs and 'Math.Max(questPoints' in cs)
check('uses cumulative tier weighting', 'remaining -= tier * jobsToNextTier' in cs)
check('uses 2.6 jobs-left conversion', 'int jobsLeft = pointsLeft / Math.Max(1, questTier);' in cs)
check('preserves 2.6 partial-point edge case', 'pointsLeft > 0 && pointsLeft < questTier' in cs)
check('max tier shows MAX', '" MAX"' in cs and 'Quest.MaxQuestTier' in cs)
check('level ring still gets native XP percentage', 'ApplyXpFill(levelRingProgressView, xpFill, true);' in cs and 'GetLevelProgressPercentage()' in cs)

with Image.open(ICON) as im:
    check('job tier icon is RGBA', im.mode == 'RGBA')
    check('job tier icon source is 32x32', im.size == (32, 32))
    alpha = im.getchannel('A')
    extrema = alpha.getextrema()
    check('job tier icon has transparency', extrema[0] == 0 and extrema[1] > 0)

# Replicate the source's intended 2.6 math for deterministic regression cases.
def text(points, jobs=10, max_tier=6):
    points=max(0,points)
    tier=1
    remaining=points
    for t in range(1,100):
        remaining -= t*jobs
        if remaining < 0:
            tier=min(t,max_tier)
            break
        if t >= max_tier:
            tier=max_tier
            break
    if tier >= max_tier:
        return f'TIER {tier} MAX'
    tier_points=sum(t*jobs for t in range(1,tier+1))
    points_left=max(0,tier_points-points)
    jobs_left=points_left//max(1,tier)
    if 0 < points_left < tier:
        jobs_left=1
    completed=max(0,min(jobs,jobs-jobs_left))
    return f'TIER {tier} {completed}/{jobs}'

check('scenario 0 points => TIER 1 0/10', text(0) == 'TIER 1 0/10')
check('scenario 9 points => TIER 1 9/10', text(9) == 'TIER 1 9/10')
check('scenario 10 points => TIER 2 0/10', text(10) == 'TIER 2 0/10')
check('scenario 29 points => TIER 2 9/10', text(29) == 'TIER 2 9/10')
check('scenario 30 points => TIER 3 0/10', text(30) == 'TIER 3 0/10')
check('scenario 150 points => max tier', text(150) == 'TIER 6 MAX')

failed=[name for name,ok in checks if not ok]
for name,ok in checks:
    print(('PASS' if ok else 'FAIL') + ' | ' + name)
print(f'\nSUMMARY: {len(checks)-len(failed)} / {len(checks)} checks passed.')
if failed:
    sys.exit(1)
