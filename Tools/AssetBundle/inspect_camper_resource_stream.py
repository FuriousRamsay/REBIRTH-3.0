import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
import inspect
from UnityPy.streams import EndianBinaryReader
p=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(p))
print('env files',[(k,type(v).__name__) for k,v in env.files.items()])
print('env container',len(env.container))
for k,v in env.files.items():
    print(k,'attrs',[x for x in dir(v) if x in ('reader','data','save','get_raw_data','read','files','objects')])
    if hasattr(v,'files'):
        for name,child in v.files.items():
            print('nested',name,type(child).__name__,'attrs',[x for x in dir(child) if x in ('reader','data','save','get_raw_data','read','files','objects')])
            if name.endswith('.resS'):
                print('resS attrs',[x for x in dir(child) if not x.startswith('_')][:70])
                print('reader len',child.Length if hasattr(child,'Length') else None)
                print('reader base',EndianBinaryReader, 'ctor',inspect.signature(EndianBinaryReader))
                print('bundle save source',inspect.getsource(type(v).save)[:5000])
                source=inspect.getsource(type(v).save_fs)
                for ln in source.splitlines():
                    if 'self.files' in ln or 'file.save' in ln or '.bytes' in ln:print('save_fs line',ln)
