"""Queue subject corrections identified during visual review."""
import json
from assets import MAN, write

m = json.loads(MAN.read_text())
subjects = {
    'M33': 'The cover illustration must prominently show golden chanterelle mushrooms with wavy funnel-shaped caps, whole and sliced into even pieces on a cutting board. No carrots, potatoes, or turnips.',
    'M34': 'The cover illustration must show BLACK TRUMPET mushrooms: small dark charcoal hollow funnel shapes with thin wavy rims, some chopped, on a cutting board. Not ordinary button mushrooms or broad oyster mushrooms.',
    'M35': 'The cover illustration must show KING OYSTER mushrooms: thick ivory cylindrical stems with small tan caps, whole and cut into uniform thick rounds. Not button mushrooms.',
    'M40': 'The cover illustration must show SUGAR CANE stalk sections, a worn pressing tool, and a small measuring jug of amber cane syrup. This is cane sugar, absolutely no maple tree, tree tap, or sap bucket.',
}
for t in m['tasks']:
    if t['key'] in subjects:
        t['prompt'] += ' IMPORTANT SUBJECT CORRECTION: ' + subjects[t['key']]
        t['status'] = 'pending'
write(m)
print('Queued four subject corrections; prior source paths will be preserved.')
