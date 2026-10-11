import csv,json
from pathlib import Path
source=Path('Tools/Profiler/out/play_20261010_083056.json')
data=json.loads(source.read_text(encoding='utf-8-sig'))
rows=[]
for segment in data['Segments']:
 if not segment['Label'].startswith('audit20261010_'):continue
 for rank,entry in enumerate(segment.get('Rebirth',[]),1):
  rows.append(dict(segment=segment['Label'],seconds=segment['Seconds'],rank=rank,method=entry['Key'],sampled_ms_per_second=entry['Value'],bridge='RebirthGameBridge' in entry['Key']))
p=Path('_Documentation/PerformanceAudit_20261010/LIVE_MOD_SAMPLES.csv')
with p.open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
print(f'{len(rows)} saved mod-method rows; inclusive rows overlap; each segment table truncated to40; not allocation or A/B evidence')
