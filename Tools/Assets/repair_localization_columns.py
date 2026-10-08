"""Repair unquoted comma-containing English in the project's two-column CSV.

Keep every valid record verbatim; ambiguous malformed records stop the edit.
"""
from pathlib import Path
import csv, io, json

ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Config/Localization.csv'
raw = path.read_bytes()
text = raw.decode('utf-8-sig')
lines = text.splitlines(keepends=True)
reader = csv.reader(io.StringIO(text))
assert next(reader) == ['Key', 'english'], 'Only the established English-only schema is supported'
result = [lines[0]]
previous = reader.line_num
repairs = []
for fields in reader:
    record = ''.join(lines[previous:reader.line_num])
    previous = reader.line_num
    if len(fields) <= 2:
        result.append(record)
        continue
    key, english = record.rstrip('\r\n').split(',', 1)
    assert key == fields[0] and not english.startswith('"') and '\n' not in english, key
    buf = io.StringIO(newline='')
    csv.writer(buf, lineterminator='\r\n').writerow((key, english))
    result.append(buf.getvalue())
    repairs.append({'key': key, 'previousColumns': len(fields), 'english': english})
updated = ''.join(result)
assert all(len(row) <= 2 for row in csv.reader(io.StringIO(updated)))
if repairs:
    path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8'))
    (ROOT / '_Documentation/Description_Color_Audit/localization_column_repairs.json').write_text(
        json.dumps(repairs, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps({'repairedRecords': len(repairs), 'schema': 'Key,english'}))
