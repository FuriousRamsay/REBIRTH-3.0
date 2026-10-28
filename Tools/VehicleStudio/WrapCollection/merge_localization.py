"""Append approved localization deltas without rewriting any existing CSV bytes.

Read-only by default. The lead supplies --target and explicitly uses --apply.
"""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path


def main():
    p=argparse.ArgumentParser();p.add_argument('--target',required=True,type=Path);p.add_argument('--additions',required=True,type=Path);p.add_argument('--apply',action='store_true');a=p.parse_args()
    original=a.target.read_bytes();old=list(csv.reader(io.StringIO(original.decode('utf-8-sig'))));existing={}
    for row in old[1:]:
        if row:existing.setdefault(row[0],[]).append(row[1:])
    with a.additions.open(encoding='utf-8-sig',newline='') as f:new=list(csv.reader(f))
    if new[0][0]!='Key':raise ValueError('Expected localization header.')
    seen=set();append=[]
    for row in new[1:]:
        if len(row)!=2 or row[0] in seen:raise ValueError('Malformed/duplicate delta key '+str(row))
        seen.add(row[0])
        if row[0] in existing:
            if existing[row[0]]!=[row[1:]]:raise ValueError('Existing localization differs: '+row[0])
        else:append.append(row)
    out=io.StringIO(newline='');csv.writer(out).writerows(append)
    result=original+(b'\r\n' if original and not original.endswith(b'\n') else b'')+out.getvalue().encode('utf-8') if append else original
    if a.apply and result!=original:
        if a.target.read_bytes()!=original:raise RuntimeError('Target changed during review; nothing written.')
        temporary=a.target.with_name(a.target.name+'.wrap-update.tmp');temporary.write_bytes(result);temporary.replace(a.target)
    print(json.dumps({'applied':a.apply,'addedKeys':len(append),'beforeSHA256':hashlib.sha256(original).hexdigest(),'afterSHA256':hashlib.sha256(result).hexdigest()},indent=2))


if __name__=='__main__':main()
