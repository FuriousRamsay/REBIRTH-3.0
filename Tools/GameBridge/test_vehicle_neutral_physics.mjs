import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs'),'utf8');
const start=source.indexOf('    internal static bool HasPhysicsModifiers');
const end=source.indexOf('    public static void BeforePhysics',start);
if(start<0||end<=start)throw new Error('Production method not found');
const fixture=await readFile(join(here,'test_vehicle_neutral_physics_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// PRODUCTION_HELPER',source.slice(start,end)).replace('// PRODUCTION_SNAPSHOT',source.slice(source.indexOf('public sealed class RebirthVehiclePerformanceSnapshot'),source.indexOf('public sealed class RebirthVehicleBaseStats'))));
 await run('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe',['/nologo','/out:'+exe,cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}
