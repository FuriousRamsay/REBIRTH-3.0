#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
checks = []

def check(label, cond, detail=""):
    checks.append(bool(cond))
    print(("PASS" if cond else "FAIL") + " | " + label + (f" | {detail}" if detail else ""))

cs_path = root / "Scripts/UI/XUiC_RebirthPagerScrollbar.cs"
xml_path = root / "Config/XUi_InGame/windows.xml"
cs = cs_path.read_text(encoding="utf-8")
xml = xml_path.read_text(encoding="utf-8")

check("pager bridge source exists", cs_path.is_file())
check("pager bridge still derives XUiController", "class XUiC_RebirthPagerScrollbar : XUiController" in cs)
check("CS0115 source removed: no ParseAttribute override", "override bool ParseAttribute" not in cs)
check("no custom ParseAttribute method remains", "ParseAttribute(" not in cs)
check("no CultureInfo dependency remains", "CultureInfo" not in cs)
check("child scrollview derived from root ID", 'rootId + "View"' in cs)
check("child proxy derived from root ID", 'rootId + "Proxy"' in cs)
check("viewport height derived from authored UI size", "scrollView.ViewComponent.Size.y" in cs)
check("normal pager resolution retained", '"pager"' in cs)
check("players pager resolution retained", '"playerPager"' in cs)
check("players bridge selection is deterministic", 'rootId.IndexOf("Players", StringComparison.OrdinalIgnoreCase)' in cs)
check("nearest-ancestor pager search retained", "XUiController scope = Parent" in cs and "scope = scope.Parent" in cs)
check("stock scrollbar sync retained", "RebirthNativeScrollbarUtil.TrySetValue" in cs)
check("stock scrollbar polling retained", "RebirthNativeScrollbarUtil.TryGetValue" in cs)
check("native pager SetPage retained", "pager.SetPage(requested)" in cs)
check("mouse-wheel PageDown retained", "pager.PageDown()" in cs)
check("mouse-wheel PageUp retained", "pager.PageUp()" in cs)
check("Rebirth mode gate retained", "RebirthSurvivorMode.IsEnabledForCurrentWorld()" in cs)

bridges = [
    "rebirthCreativePagerScroll",
    "rebirthMapWaypointPagerScroll",
    "rebirthQuestPagerScroll",
    "rebirthSharedQuestPagerScroll",
    "rebirthCraftingPagerScroll",
    "rebirthPlayersPagerScroll",
]
check("six pager bridge controllers remain", xml.count('controller="RebirthPagerScrollbar, RebirthUtils"') == 6,
      str(xml.count('controller="RebirthPagerScrollbar, RebirthUtils"')))
for name in bridges:
    check(f"bridge root {name}", f'name="{name}"' in xml)
    check(f"bridge scrollview {name}View", f'name="{name}View"' in xml)
    check(f"bridge proxy {name}Proxy", f'name="{name}Proxy"' in xml)
for attr in ("pager_id=", "scroll_view_id=", "proxy_id=", "viewport_height="):
    check(f"obsolete custom XML attribute removed: {attr[:-1]}", attr not in xml)
check("players native playerPager still hidden", "playerPager" in xml and 'name="hotkeys_enabled">false' in xml)

xml_files = list(root.rglob("*.xml"))
xml_errors = []
for path in xml_files:
    try:
        ET.parse(path)
    except Exception as exc:
        xml_errors.append(f"{path.relative_to(root)}: {exc}")
check("all XML parse", not xml_errors, f"XML={len(xml_files)}")
for err in xml_errors[:10]:
    print("XML_ERROR | " + err)

passed = sum(checks)
failed = len(checks) - passed
print(f"PC030_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xml_files)}")
sys.exit(0 if failed == 0 else 1)
