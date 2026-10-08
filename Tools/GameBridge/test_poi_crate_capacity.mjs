import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const source=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePoiStorage.cs'),'utf8');
const native=await readFile(join(root,'Tools/StorageGear/inspect/out-installed-25661859/ItemStack.cs'),'utf8');
const fixture=(await readFile(join(here,'test_poi_crate_capacity_fixture.cs'),'utf8'))
 .replace('// SOURCE',['    private static bool TryCount(','    public static bool CanAccept(','    private static Dictionary<string, long> CombinedCounts(','    private static void AddCounts(','    private static bool SameCounts('].map(sig=>method(source,sig)).join('\n'))
 .replace('// NATIVE',['public bool CanStackWith(','public bool CanStack(','public bool CanStackPartly('].map(sig=>method(native,sig)).join('\n'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}