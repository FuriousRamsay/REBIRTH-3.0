from pathlib import Path
import csv,json
import research_legacy as research
research.BASE=Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die')
blocks,bfiles,unsupported=research.patched('blocks');loot,lfiles,more=research.patched('loot')
out=research.OUT/'out';out.mkdir(parents=True,exist_ok=True)
blocks.write(str(out/'current-blocks.xml'),encoding='utf-8');loot.write(str(out/'current-loot.xml'),encoding='utf-8')
(research.OUT/'current-research-files.json').write_text(json.dumps({'files':bfiles+lfiles,'unsupported':unsupported+more},indent=2)+'\n')
keys=set()
for path in (research.BASE/'Data/Config/Localization.csv',research.BASE/'Mods/zzz_REBIRTH__3_0/Config/Localization.csv'):
 with path.open(encoding='utf-8-sig',newline='') as f:keys.update(row[0] for row in csv.reader(f) if row)
(out/'localization-keys.txt').write_text('\n'.join(sorted(keys)),encoding='utf-8')
print('Prepared current patched XML and actual localization-key presence; conditional omissions explicitly recorded')
