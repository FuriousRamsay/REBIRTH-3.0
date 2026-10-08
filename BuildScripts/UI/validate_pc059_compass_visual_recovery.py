from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
xml_path = ROOT/'Config/XUi_InGame/windows.xml'
cs_path = ROOT/'Scripts/UI/XUiC_RebirthCompassWindow.cs'
icon_path = ROOT/'UIAtlases/RebirthHud/rb_hud_job_satchel.png'
stretch_path = ROOT/'UIAtlases/RebirthHud/rb_hud_shell_stretch.png'
xml = xml_path.read_text(encoding='utf-8')
cs = cs_path.read_text(encoding='utf-8')
checks=[]
def check(cond,msg):
    checks.append((bool(cond),msg))
    print(('PASS' if cond else 'FAIL')+' | '+msg)

try:
    ET.parse(xml_path); ok=True
except Exception: ok=False
check(ok,'XUi_InGame/windows.xml parses')
check('name="rebirthLevelRingBackground"' in xml and 'sprite="rb_hud_level_ring_progress"' in xml,'XP gray track uses same thick ring artwork as blue progress')
check('<sprite depth="5" name="rebirthLevelRingProgress"' in xml,'XP progress uses regular XUiV_Sprite, not XUiV_FilledSprite')
check('filldirection="Radial360"' in xml,'XP progress is authored as Radial360')
check('name="rebirthCompassLevelSeparator"' in xml,'divider exists between player level and Job Tier')
check('atlas="RebirthHud" sprite="rb_hud_job_satchel"' in xml,'authored Job Tier satchel asset is used')
check('name="rebirthJobTierIcon"' in xml and 'width="18" height="18"' in xml,'Job Tier icon displays at 18x18')
check(icon_path.exists(),'Job Tier satchel PNG exists')
if icon_path.exists():
    with Image.open(icon_path) as im:
        check(im.size==(32,32) and im.mode=='RGBA','Job Tier satchel is a 32x32 RGBA authored asset')
        alpha=im.getchannel('A')
        check(alpha.getextrema()[0]==0 and alpha.getextrema()[1]>0,'Job Tier satchel has transparent background')
check(stretch_path.exists(),'responsive shell center asset exists')
if stretch_path.exists():
    with Image.open(stretch_path) as im:
        check(im.size==(16,38) and im.mode=='RGBA','shell stretch strip is atlas-safe 16x38 RGBA')
        check(im.getpixel((8,19))[3] > 0,'shell stretch strip contains translucent body fill')
        check(im.getpixel((8,1))[3] > im.getpixel((8,19))[3],'shell stretch strip preserves stronger top border')
check('levelSeparatorView = FindView("rebirthCompassLevelSeparator")' in cs,'runtime layout resolves internal level/tier divider')
check('private const int LevelDividerX = 39;' in cs,'level/tier divider has fixed tight anchor')
check('private const int JobIconCenterX = 53;' in cs,'satchel is positioned immediately after divider')
check('private const int JobLabelLeftX = 65;' in cs,'Tier text begins 3px after 18px satchel')
check('JobLabelLeftX + jobTextWidth + ModulePaddingRight' in cs,'progression width is computed from localized Job Tier text')
check('MeasureLabelWidth(dayLabelView' in cs,'Day text width is measured dynamically')
check('MeasureLabelWidth(jobLabelView' in cs,'Job Tier text width is measured dynamically')
check('return measured > 0 ? measured' in cs,'live rendered text width overrides fallback width')
check('if (measured <= 0)' in cs and 'EstimateTextWidth(label.text' in cs,'estimated width is fallback-only when printedSize unavailable')
check('ViewComponent.Position = new Vector2i(headerOffsetX, HeaderTopY);' in cs,'complete variable-width header remains re-centered')
check('ViewComponent.Size =' not in cs and 'ViewComponent.Size=' not in cs,'native 250px compass root is never resized')
check('UIBasicSprite.FillDirection.Radial360' in cs,'runtime XP ring explicitly enforces Radial360')
check('view.Type = UIBasicSprite.Type.Filled;' in cs,'runtime XP ring uses native UISprite filled mode')
check('ValueDisplayFormatters.RomanNumber(Mathf.Clamp(tier, 1, 6))' in cs,'Job Tier remains Roman I-VI')
check('[aa7fc9]{1}[-]/{2}' in cs,'only Job Tier numerator remains purple')
check('rb_hud_shell_stretch' in xml and 'rebirthCompassShellCenter' in xml,'responsive translucent shell remains wired')
failed=[m for ok,m in checks if not ok]
print(f'\nSUMMARY: {len(checks)-len(failed)} / {len(checks)} checks passed.')
raise SystemExit(1 if failed else 0)
