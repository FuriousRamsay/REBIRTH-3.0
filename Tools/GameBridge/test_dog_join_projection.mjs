import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/NPC/Dog/RebirthDogLifecycleService.cs'),'utf8');
const runtime=await readFile(join(root,'Scripts/NPC/Dog/RebirthDogSystems.cs'),'utf8');
const start=source.indexOf('    public static bool TryResolveOwnerId('),end=source.indexOf('    public static bool CanAcquire(',start);
const ps=source.indexOf('    public static void SynchronizeClientOwnerProjection'),pe=source.indexOf('    public static void SynchronizeLegacyOwnerProjection',ps);
const rs=runtime.indexOf('    private static EntityPlayer ResolveOwner('),re=runtime.indexOf('    /// <summary>',rs);
if(start<0||end<=start||ps<0||pe<=ps||rs<0||re<=rs)throw new Error('Production method missing');
const fixture=(await readFile(join(here,'test_dog_join_projection_fixture.cs'),'utf8')).replace('// IDENTITY',source.slice(start,end)).replace('__PRODUCTION_HELPER__',source.slice(ps,pe)).replace('// OWNER_RESOLVER',runtime.slice(rs,re));
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture);
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
