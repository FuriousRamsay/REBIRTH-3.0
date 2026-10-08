from pathlib import Path
import sys
root = Path(sys.argv[1] if len(sys.argv) > 1 else '.')
p = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs'
text = p.read_text(encoding='utf-8-sig')
checks = [
    ('CaptureAndHide(xui.QuestTracker' not in text, 'XUiM_Quest is not passed to XUiController HUD helper'),
    ('"windowQuestTracker"' in text, 'visible quest tracker controller remains suppressed by controller id'),
    ('questModel resolved=' in text, 'quest model diagnostic remains available'),
    ('CaptureAndHide(xui.BuffPopoutList' in text, 'typed buff controller suppression retained'),
]
passed = 0
for ok, label in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + label)
    passed += int(ok)
print(f'PC094 quest-model compile recovery validation: {passed} PASS / {len(checks)-passed} FAIL')
raise SystemExit(0 if passed == len(checks) else 1)
