import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePoiStorage.cs'),'utf8');
const a=source.indexOf('    private static TEFeatureStorage Loot(EntityPlayerLocal p, Vector3i pos)'),b=source.indexOf('    private static bool IsPlacementSite(',a);if(a<0||b<a)throw new Error('Production method missing');
function method(signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const fixture=await readFile(join(here,'test_poi_crate_identity_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// PRODUCTION_CLASS',source.slice(a,b)+'\n'+source.match(/    public bool Contains\([^\n]+/)[0]+'\n'+method('    public string CustodyFailure(')+'\n'+method('    public bool Recover(')));
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll',cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}


