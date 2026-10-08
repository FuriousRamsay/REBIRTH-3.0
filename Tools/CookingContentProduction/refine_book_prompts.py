"""Clarify crop silhouettes before book-cover generation."""
import json
from assets import MAN, write

m = json.loads(MAN.read_text())
subjects = {
    'B34': 'Prominent golden chanterelle mushrooms with wavy funnel caps; not root vegetables.',
    'B35': 'Prominent black trumpet mushrooms, charcoal-colored hollow funnels with thin wavy rims, not ordinary button mushrooms.',
    'B36': 'Prominent king oyster mushrooms, thick ivory cylindrical stems and small tan caps; not ordinary button mushrooms.',
    'B37': 'Prominent cultivated enoki mushrooms, clusters of very thin ivory stems with tiny round caps.',
    'B41': 'Prominent sugar-cane stalk sections and amber cane syrup. No maple tree, sap bucket, or tree tap.',
}
for t in m['tasks']:
    if t['key'] in subjects:
        t['prompt'] += ' Subject identity: ' + subjects[t['key']]
write(m)
print('Clarified five subject-specific book prompts.')
