import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'),'utf8');
const beast=await readFile(join(root,'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBeastmasterService.cs'),'utf8');
const owner=source.indexOf('public class EntityRebirthDogCompanion');
const start=source.indexOf('    public override void InitLocalActivationCommands(',owner);
const end=source.indexOf('    public override void OnEntityActivated(',start);
const ls=beast.indexOf('    public static bool IsTamedEntityClass('), le=beast.indexOf('    public static bool HasBeastmaster(',ls);
if(owner<0||start<0||end<=start||ls<0||le<=ls)throw new Error('Production method not found');
const fixture=(await readFile(join(here,'test_tamed_client_menu_fixture.cs'),'utf8')).replace('// LOOKUP',beast.slice(ls,le));
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// MENU',source.slice(start,end)));
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
