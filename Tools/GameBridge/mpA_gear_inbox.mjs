import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// Prepared only. Running this compiles an isolated fixture; no game/native engine.
const here=dirname(fileURLToPath(import.meta.url)), root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Survivor/Support/RebirthGearOfferInbox.cs'),'utf8');
const fixture=await readFile(join(here,'mpA_gear_inbox_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-inbox-')), run=promisify(execFile);
try {
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');
 await writeFile(cs,source+'\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally { await rm(temp,{recursive:true,force:true}); }
