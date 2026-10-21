import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
from run_gamebridge_batch import Transport
b=Transport(Path(__file__).parent/'out/session.json')
b.run_token=json.loads((Path(__file__).parent/'out/cooking_report_control.json').read_text())['runToken']
r=b.call(sys.argv[1],sys.argv[2],**json.loads(sys.argv[3] if len(sys.argv)>3 else '{}'))
p=Path(__file__).parent/'out/cooking_report_last.json';p.write_text(json.dumps(r,indent=2))
print(json.dumps(r))
