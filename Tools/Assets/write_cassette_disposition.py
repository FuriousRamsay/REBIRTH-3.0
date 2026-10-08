from pathlib import Path
import json
root=Path(__file__).resolve().parents[2];doc=root/'_Documentation/Icon_Restoration'
audit=json.loads((doc/'cassette_consumer_audit_20260930.json').read_text());retired={r['item_id'] for r in json.loads((doc/'retired_literature_20260930.json').read_text())}
done={r['id'] for r in json.loads((doc/'generated_cassette_manifest_20260930.json').read_text())}
lines=['# Cassette disposition — current source audit','', '| Cassette ID | Current use | Artwork / disposition |','|---|---|---|']
for r in audit:
 id=r['item_id']
 if r['subtype'] in ('primer','field_notes'):use='Theory study for '+r['skill'];status='Available; existing newer artwork retained'
 else:
  use='Legacy saved cassette only; discovery content remains print-only'
  status='Withdrawn from acquisition and creative; save ID retained'
  if id in retired:status+='; source previously retired for having no effective use'
  elif id in done:status+='; generated artwork retained for saved copies'
 lines.append('| '+id+' | '+use+' | '+status+' |')
(doc/'CASSETTE_DISPOSITION_20260930.md').write_text('\n'.join(lines)+'\n')
