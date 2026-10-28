// AUTHORED_NOT_RUN. Ordinary production planner composition; no recovery cases.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
let fixture=await readFile(join(here,'mp027_viewed_completion_plan_fixture.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerRuntime.PlacedPlan.cs'),'utf8');
if(fixture.split('// ACTUAL_PLANNER').length!==2)throw Error('Planner insertion changed');fixture=fixture.replace('// ACTUAL_PLANNER',source.replace(/^using .*;\r?\n/gm,''));
const out=await mkdtemp(join(tmpdir(),'rebirth-viewed-plan-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
