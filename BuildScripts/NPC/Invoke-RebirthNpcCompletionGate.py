#!/usr/bin/env python3
from __future__ import annotations
import argparse, json, subprocess, sys
from pathlib import Path

def main():
    ap=argparse.ArgumentParser(description='Refuses a COMPLETE manifest unless the assigned ACIP package passes every static conformance predicate.')
    ap.add_argument('--project-root',required=True); ap.add_argument('--package',required=True); ap.add_argument('--manifest-out',required=True); ap.add_argument('--label',default='COMPLETE')
    a=ap.parse_args(); root=Path(a.project_root).resolve(); report=root/'obj'/f'{a.package}_conformance.json'; report.parent.mkdir(parents=True,exist_ok=True)
    if a.package.upper()=='ACIP-12':
        audit=root/'BuildScripts/NPC/Invoke-RebirthNpcAcip12ClosureAudit.py'
        csv_report=root/'obj'/'ACIP-12_evidence.csv'
        rc=subprocess.call([sys.executable,str(audit),'--project-root',str(root),'--json-out',str(report),'--csv-out',str(csv_report)])
    else:
        audit=root/'BuildScripts/NPC/Invoke-RebirthNpcConformanceAudit.py'
        rc=subprocess.call([sys.executable,str(audit),'--project-root',str(root),'--package',a.package,'--json-out',str(report)])
    data=json.loads(report.read_text(encoding='utf-8')) if report.exists() else {'result':'FAIL','tests':[]}
    if rc!=0 or data.get('result')!='PASS':
        out=Path(a.manifest_out)
        if out.exists(): out.unlink()
        print(f'REFUSED: {a.package} cannot receive a completion manifest; conformance result={data.get("result","FAIL")}.',file=sys.stderr); return 3
    out=Path(a.manifest_out); out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(f'package={a.package}\nlabel={a.label}\nconformance={data.get("result","FAIL")}\nreport={report}\n',encoding='utf-8')
    print(f'WROTE: {out}'); return 0
if __name__=='__main__': sys.exit(main())
