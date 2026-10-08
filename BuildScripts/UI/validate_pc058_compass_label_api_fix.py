#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
CS = ROOT / 'Scripts' / 'UI' / 'XUiC_RebirthCompassWindow.cs'
text = CS.read_text(encoding='utf-8')
checks = []

def check(ok, label):
    checks.append((bool(ok), label))

check('labelView.Label' not in text, 'unsupported XUiV_Label.Label API is not used')
check('Transform labelTransform = labelView.UiTransform;' in text, 'label measurement resolves through public XUiView.UiTransform')
check('labelTransform.GetComponent<UILabel>()' in text, 'runtime UILabel is resolved from the UI transform')
check('UILabel label = null;' in text, 'UILabel lookup is null-safe')
check('PrintedSizeProperty.GetValue(label, null)' in text, 'rendered printedSize measurement remains intact')
check('EstimateTextWidth(label.text, labelView.FontSize)' in text, 'rendered-label fallback measurement remains intact')
check('EstimateTextWidth(labelView.Text, labelView.FontSize)' in text, 'XUi label-text fallback remains intact')
check('ValueDisplayFormatters.RomanNumber' in text, 'PC057 Roman numeral Job Tier behavior is preserved')
check('Mathf.Clamp(tier, 1, 6)' in text, 'PC057 Tier I-VI clamp is preserved')
check('MeasureLabelWidth(jobLabelView' in text and 'MeasureLabelWidth(dayLabelView' in text, 'PC057 responsive localization sizing remains preserved')
check(re.search(r'ViewComponent\.Size\s*=', text) is None, 'native 250px compass root is still not resized')

for ok, label in checks:
    print(('PASS' if ok else 'FAIL') + ' | ' + label)
passed = sum(1 for ok, _ in checks if ok)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
sys.exit(0 if passed == len(checks) else 1)
