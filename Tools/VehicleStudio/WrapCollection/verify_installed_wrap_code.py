"""Read-only guard against deploying copied source with a stale runtime DLL.

Check emitted .NET user strings, not just a successful/incremental build exit.
This is a deployment check, not proof of in-game texture quality.
"""
import argparse,hashlib,json
from pathlib import Path

def verify(root,design=None):
 dll=root/'RebirthUtils.dll';data=dll.read_bytes()
 required=['UndeadChillAfterHours','rebirth_undead_chill_after_hours.unity3d','frontsharp','_WrapSharpPanels']
 if design=='CaptStress':required+=['CaptStress','rebirth_captstress.unity3d','_GenmaNativeBody12V2','_WrapLayoutVariant']
 if design=='GenmaSaotome':required+=['GenmaSaotome','rebirth_genma_saotome.unity3d','_GenmaNativeBody12V2','_WrapLayoutVariant']
 found={s:s.encode('utf-16le') in data for s in required}
 result={'dllSHA256':hashlib.sha256(data).hexdigest(),'requiredRuntimeStrings':found,'status':'PASS' if all(found.values()) else 'FAIL_STALE_DLL','gameplay':'Not verified by this deployment guard'}
 return result

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',required=True,type=Path);p.add_argument('--design');p.add_argument('--receipt',type=Path);a=p.parse_args();r=verify(a.root,a.design)
 if a.receipt:a.receipt.write_text(json.dumps(r,indent=2)+'\n')
 print(json.dumps(r,indent=2));raise SystemExit(0 if r['status']=='PASS' else 1)
