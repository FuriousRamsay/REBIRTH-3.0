import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingSessionService.cs'),'utf8');
const a=source.indexOf('            if(!RebirthWorldCharacterService.FlushPlayer(player,"cooking-batch"))'),b=source.indexOf('        if(action=="abandonRegistration")',a);
const c=source.indexOf('        if(!RebirthWorldCharacterService.FlushPlayer(player, "cooking-preparation"))'),d=source.indexOf('\n    }\n}',c);
if(a<0||b<a||c<0||d<c)throw new Error('Persistence branch missing');
const registration=source.slice(a,b).slice(0,source.slice(a,b).lastIndexOf('        }'));
const preparation=source.slice(c,d);
const fixture=await readFile(join(here,'test_cooking_admission_save_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// REGISTRATION',registration).replace('// PREPARATION',preparation));
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


