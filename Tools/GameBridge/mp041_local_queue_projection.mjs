// AUTHORED_NOT_RUN. Actual host-local native-card projection, with native UI/codec doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
let common=(await readFile(join(here,'mp042_cancellation_projection_fixture.cs'),'utf8')).split('// ACTUAL_PROJECTION')[0];
common=common.replace('internal int entityId=1;','internal int entityId=1,Died;');
common=common.replace('class World {','class World {internal bool Remote;internal bool IsRemote()=>Remote;');
const source=await readFile(join(root,'Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs'),'utf8');
const begin=source.indexOf('    internal bool ProjectCanonicalQueue('),end=source.indexOf('    private bool ReturnIngredients(',begin);
if(begin<0||end<=begin)throw Error('Actual local projection boundary changed');
let fixture=await readFile(join(here,'mp041_local_queue_projection_fixture.cs'),'utf8');fixture=fixture.replace('// ACTUAL_QUEUE_PROJECTION',source.slice(begin,end));
const admission=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRemoteAdmission.cs'),'utf8');
const finish=admission.indexOf('    private static bool FinishAdmissionProjection('),finishEnd=admission.indexOf('    private static void TickReturn(',finish);
if(finish<0||finishEnd<=finish)throw Error('Actual admission projection completion boundary changed');fixture=fixture.replace('// ACTUAL_FINISH',admission.slice(finish,finishEnd));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-local-cooking-projection-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,common+fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
