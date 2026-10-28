// AUTHORED_NOT_RUN. Actual hook compiled against explicit native/Harmony doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const full=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineBundle.cs'),'utf8');
const start=full.indexOf('    private static bool RestoreOriginalDomains(');if(start<0)throw Error('Actual rollback method missing');
let open=full.indexOf('{',start),depth=1,end=open+1;
for(;end<full.length&&depth;end++){if(full[end]==='{')depth++;else if(full[end]==='}')depth--;}
if(depth)throw Error('Unbalanced rollback');const source=full.slice(start,end);
let fixture=await readFile(join(root,'Tools/GameBridge/mp049_rollback_fixture.cs'),'utf8');
if(fixture.split('// ACTUAL_ROLLBACK').length!==2)throw Error('Unique fixture marker required');fixture=fixture.replace('// ACTUAL_ROLLBACK',source);
const out=await mkdtemp(join(tmpdir(),'rebirth-mp049-rollback-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
