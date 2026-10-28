"""Independently inspect binary FBX structure, takes, bone models and curve keys."""
import csv, json, struct, sys, zlib, re, math
from pathlib import Path

def inspect(path):
    with open(path,'rb') as check:
        if check.read(5)==b'; FBX': return inspect_ascii(path)
    with open(path, 'rb') as f:
        assert f.read(23) == b'Kaydara FBX Binary  \x00\x1a\x00'
        version, = struct.unpack('<I', f.read(4))
        wide = version >= 7500
        header = '<QQQB' if wide else '<IIIB'
        size = struct.calcsize(header)
        counts = {}
        takes = []
        ids = {k:set() for k in ('Model','AnimationStack','AnimationLayer','AnimationCurveNode','AnimationCurve')}
        connections = []
        key_count = 0
        def prop():
            kind = f.read(1)
            fixed = {b'Y':2,b'C':1,b'I':4,b'F':4,b'D':8,b'L':8}
            if kind in fixed:
                raw=f.read(fixed[kind])
                return struct.unpack('<q',raw)[0] if kind==b'L' else None
            if kind in (b'S',b'R'):
                n, = struct.unpack('<I',f.read(4)); data=f.read(n)
                return data.decode('utf-8',errors='replace') if kind==b'S' else None
            if kind in (b'f',b'd',b'l',b'i',b'b',b'c'):
                n,enc,length=struct.unpack('<III',f.read(12)); data=f.read(length)
                stride={b'f':4,b'd':8,b'l':8,b'i':4,b'b':1,b'c':1}[kind]
                raw=zlib.decompress(data) if enc else data
                assert len(raw)==n*stride
                return n
            raise ValueError(kind)
        def node():
            nonlocal key_count
            end,num,proplen,namelen=struct.unpack(header,f.read(size))
            if end==0: return False
            name=f.read(namelen).decode()
            start=f.tell(); values=[prop() for _ in range(num)]
            assert f.tell()==start+proplen
            counts[name]=counts.get(name,0)+1
            if name in ids: ids[name].add(values[0])
            if name=='C' and len(values)>=3: connections.append((values[1],values[2]))
            if name=='AnimationStack': takes.append(values[1].split('\x00')[0])
            if name=='KeyTime' and values: key_count+=values[0]
            while f.tell()<end:
                if not node(): break
            assert f.tell()==end,(name,f.tell(),end)
            return True
        while node(): pass
    with open(str(path)+'.clips.tsv',newline='',encoding='utf-8-sig') as f:
        clips=list(csv.DictReader(f,delimiter='\t'))
    assert len(takes)==len(clips),(len(takes),len(clips))
    assert set(takes)=={c['take'] for c in clips}
    assert counts.get('AnimationCurve',0)>0 and key_count>0
    edges={}
    for a,b in connections: edges.setdefault(a,set()).add(b)
    for curve in ids['AnimationCurve']: assert edges.get(curve,set()) & ids['AnimationCurveNode']
    for curve in ids['AnimationCurveNode']:
        assert edges.get(curve,set()) & ids['Model'], ('unbound curve node',curve)
        assert edges.get(curve,set()) & ids['AnimationLayer']
    for layer in ids['AnimationLayer']: assert edges.get(layer,set()) & ids['AnimationStack']
    return dict(file=str(path),fbx_version=version,takes=len(takes),models=counts.get('Model',0),curves=counts.get('AnimationCurve',0),keys=key_count,bone_connections='PASS',bytes=Path(path).stat().st_size)

def inspect_ascii(path):
    ids={k:set() for k in ('Model','AnimationStack','AnimationLayer','AnimationCurveNode','AnimationCurve')}
    edges={}; takes=[]; index=[]; keys=0; pending=None
    obj=re.compile(r'^(Model|AnimationStack|AnimationLayer|AnimationCurveNode|AnimationCurve): (\d+), "([^"\r\n]*)"')
    edge=re.compile(r'^C: "(?:OO|OP)",(\d+),(\d+)')
    array=re.compile(r'^(KeyTime|KeyValueFloat|KeyAttrFlags|KeyAttrDataFloat|KeyAttrRefCount): \*(\d+) \{\s*(?:a: (.*))?$')
    def check_array(kind,count,data):
        values=data.strip().split(',')
        assert len(values)==int(count),(kind,len(values),count)
        assert all(math.isfinite(float(v)) for v in values)
        if kind=='KeyTime':
            times=list(map(int,values)); assert times==sorted(times)
            return len(times)
        return 0
    with open(path,encoding='utf-8') as f:
        for line in f:
            line=line.strip()
            if pending and line.startswith('a:'):
                keys+=check_array(*pending,line[2:].strip()); pending=None; continue
            if line.startswith('Take: "'): index.append(line.split('"')[1])
            match=obj.match(line)
            if match:
                kind,num,name=match.groups(); ids[kind].add(int(num))
                if kind=='AnimationStack': takes.append(name.removeprefix('AnimStack::'))
                continue
            match=edge.match(line)
            if match: a,b=map(int,match.groups()); edges.setdefault(a,set()).add(b); continue
            match=array.match(line)
            if match:
                kind,count,data=match.groups()
                if data is None: pending=(kind,count)
                else: keys+=check_array(kind,count,data)
    with open(str(path)+'.clips.tsv',newline='',encoding='utf-8-sig') as f: clips=list(csv.DictReader(f,delimiter='\t'))
    assert len(takes)==len(clips) and set(takes)=={c['take'] for c in clips}
    assert len(index)==len(takes) and set(index)==set(takes)
    assert keys>0 and pending is None
    for curve in ids['AnimationCurve']: assert edges.get(curve,set()) & ids['AnimationCurveNode']
    for curve in ids['AnimationCurveNode']:
        assert edges.get(curve,set()) & ids['Model']
        assert edges.get(curve,set()) & ids['AnimationLayer']
    for layer in ids['AnimationLayer']: assert edges.get(layer,set()) & ids['AnimationStack']
    return dict(file=str(path),fbx_version=7300,encoding='ASCII',takes=len(takes),models=len(ids['Model']),curves=len(ids['AnimationCurve']),keys=keys,bone_connections='PASS',bytes=Path(path).stat().st_size)

if __name__=='__main__':
    result=[inspect(Path(p)) for p in sys.argv[1:]]
    print(json.dumps(result,indent=2))
