"""Update only cooking reference description records, retaining the rest of the CSV verbatim."""
from pathlib import Path
import csv, io, json, sys
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/CookingContentProduction'))
from build_xml import compact_cooking_reference
from content_model import CATALOGUE, ASSETS
replacements = {}
for task in ASSETS.values():
    if task['category'] not in ('book', 'magazine'): continue
    table = CATALOGUE['books' if task['category'] == 'book' else 'magazines']
    record = next(r for r in table if r['id'] == task['key'])
    replacements[task['icon'] + 'Desc'] = compact_cooking_reference(record)
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
text = raw.decode('utf-8-sig')
lines = text.splitlines(keepends=True)
reader = csv.reader(io.StringIO(text))
previous = 0
output, changes = [], []
for row in reader:
    original = ''.join(lines[previous:reader.line_num]); previous = reader.line_num
    if len(row) == 2 and row[0] in replacements and row[1] != replacements[row[0]]:
        updated = replacements[row[0]]
        changes.append({'key': row[0], 'before': row[1], 'after': updated})
        buf = io.StringIO(newline='')
        csv.writer(buf, lineterminator='\r\n').writerow((row[0], updated))
        output.append(buf.getvalue())
    else: output.append(original)
if changes:
    path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + ''.join(output).encode('utf-8'))
    (ROOT / '_Documentation/Description_Color_Audit/shortened_cooking_descriptions.json').write_text(
        json.dumps(changes, indent=2), encoding='utf-8')
print(json.dumps({'changed': len(changes)}))
