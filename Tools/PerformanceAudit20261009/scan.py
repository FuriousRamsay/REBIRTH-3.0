import pathlib,re,csv,hashlib
root=pathlib.Path('Scripts'); rows=[]; hot=[]
risks={'scene_scan':r'FindObjectsOfType|FindObjectOfType|GetComponentsInChildren', 'reflection':r'GetMethods\(|GetFields\(|GetProperties\(', 'disk_write':r'File\.Write|\.Save\(|Flush\(true\)', 'linq_materialize':r'\.ToArray\(|\.ToList\(|\.OrderBy', 'ui_lookup':r'GetChildById\(|GetChildByType'}
for p in sorted(root.rglob('*.cs')):
 s=p.read_text(encoding='utf-8-sig'); counts={k:len(re.findall(v,s)) for k,v in risks.items()}
 rows.append({'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'lines':len(s.splitlines()),**counts})
 for m in re.finditer(r'(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|sealed)\s+)*[^\n;{}=]+\b(?:Update|Tick|Pump|OnGUI|LateUpdate|OnUpdateEntity)\s*\([^;{}]*\)\s*\{',s):
  start=m.end(); depth=1; end=start
  while depth and end<len(s):
   depth+=(s[end]=='{')-(s[end]=='}');end+=1
  body=s[start:end]; tags=[k for k,v in risks.items() if re.search(v,body)]
  if tags:hot.append((str(p),s[:m.start()].count('\n')+1,','.join(tags),body[:10000]))
with open('Tools/PerformanceAudit20261009/SOURCE_SCAN.csv','w',newline='',encoding='utf-8') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
with open('Tools/PerformanceAudit20261009/HOT_METHODS.txt','w',encoding='utf-8') as f:
 for p,line,tags,body in hot:f.write(f'\n{p}:{line} [{tags}]\n{body}\n')
print(f'{len(rows)} source files scanned; {len(hot)} update methods flagged for manual review')
for p,line,tags,body in hot:print(f'{p}:{line} [{tags}]')
