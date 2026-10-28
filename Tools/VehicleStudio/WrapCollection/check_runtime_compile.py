"""Compile only owned runtime files against actual installed game assemblies.

Outputs a disposable isolated check library, never RebirthUtils.dll.
"""
import subprocess
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GAME = Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die')
MANAGED = GAME / '7DaysToDie_Data/Managed'
OUT = Path(__file__).resolve().parent / '_CompileCheck'
OUT.mkdir(exist_ok=True)
(OUT / 'CollaboratorStubs.cs').write_text('public static class RebirthCamperRadial { public static bool IsCamper(EntityVehicle e)=>true; }\ninternal static class RebirthCamperPickupPaint { internal static void Apply(EntityVehicle e) {} }\n', encoding='utf-8')
names = ['RebirthCamperPattern', 'RebirthCamperPatternAppearance', 'RebirthCamperWrapAppearance', 'RebirthCamperWrapCatalog']
refs = ['mscorlib', 'System', 'System.Core', 'netstandard', 'Assembly-CSharp', 'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.AssetBundleModule', 'LogLibrary']
lines = ['/nologo', '/target:library', '/nostdlib+', '/langversion:latest', '/out:"' + str(OUT / 'WrapRuntimeCheck.dll') + '"']
lines += ['/reference:"' + str(MANAGED / (name + '.dll')) + '"' for name in refs]
lines += ['"' + str(ROOT / 'Scripts/Vehicles/Camper' / (name + '.cs')) + '"' for name in names]
lines += ['"' + str(OUT / 'CollaboratorStubs.cs') + '"']
rsp = OUT / 'compile.rsp'
rsp.write_text('\n'.join(lines), encoding='utf-8')
result = subprocess.run(['dotnet', 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll', '@' + str(rsp)], capture_output=True, text=True)
(OUT / 'result.txt').write_text(result.stdout + result.stderr, encoding='utf-8')
receipt={'exitCode':result.returncode,'scope':'Four owned runtime sources compiled against actual installed native assemblies; collaborator stubs cover only unrelated camper classification/pickup-paint hooks. Not a full production DLL build or game run.',
         'sources':{str(Path('Scripts/Vehicles/Camper')/(name+'.cs')):hashlib.sha256((ROOT/'Scripts/Vehicles/Camper'/(name+'.cs')).read_bytes()).hexdigest() for name in names},
         'nativeAssembly':str(MANAGED/'Assembly-CSharp.dll'),'nativeAssemblySHA256':hashlib.sha256((MANAGED/'Assembly-CSharp.dll').read_bytes()).hexdigest(),
         'output':result.stdout+result.stderr}
(ROOT/'_Documentation/CamperWrapArt/NATIVE_COMPILE_RECEIPT_20261027.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print(result.stdout + result.stderr)
raise SystemExit(result.returncode)
